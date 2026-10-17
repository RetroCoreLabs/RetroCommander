//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using RetroCommander.Core.Metadata;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Drivers;
using RetroIO.Storage.Ndfs.Drivers;

namespace RetroCommander.Desktop;

public partial class GenericMountDialog : Window
{
    public string? SelectedUrn { get; private set; }
    public bool DontShowAgain { get; private set; }
    public bool MountConfirmed { get; private set; }

    private string _imagePath;
    private List<DetectionResult> _detectionResults = new();
    private List<PartitionMetadata>? _detectedPartitions;
    private FileSystemMetadataService? _metadataService;

    public GenericMountDialog() : this(string.Empty)
    {
    }

    public GenericMountDialog(string imagePath, Dictionary<string, float>? detectionConfidences = null)
    {
        InitializeComponent();
        _imagePath = imagePath;

        PopulateFileInfo();
        PopulateDetectionResults(detectionConfidences);
        UpdateUrnPreview();

        // Default to auto-detect
        FilesystemTypeCombo.SelectedIndex = 0;

        // Auto-detect partitions on startup
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(100); // Small delay to let UI settle
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await DetectBsdPartitionsAsync();
                await LoadDetailsTabAsync();
            });
        });
    }

    // The Mount tab's content is DYNAMIC: the "BSD Partition Selection" panel is revealed
    // asynchronously after detection (~100ms post-open), and changing the filesystem-type dropdown
    // shows/hides panels too. So we keep SizeToContent="Height" active for the WHOLE lifetime of the
    // dialog — the window automatically re-fits itself whenever the content changes. We only clamp
    // the height to the screen working area so a very tall Details tab scrolls instead of running
    // off-screen. (Previous versions "locked" the height after the first layout, which froze it
    // before the BSD panel appeared and clipped the bottom — that was the bug.)
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Screen? screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        if (screen != null)
        {
            double scaling = screen.Scaling <= 0 ? 1.0 : screen.Scaling;
            MaxHeight = (screen.WorkingArea.Height / scaling) * 0.92;
        }
    }

    private void PopulateFileInfo()
    {
        FilePathText.Text = _imagePath;

        if (File.Exists(_imagePath))
        {
            var fileInfo = new FileInfo(_imagePath);
            FileSizeText.Text = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)";
        }
        else
        {
            FileSizeText.Text = "File not found";
        }
    }

    private void PopulateDetectionResults(Dictionary<string, float>? detectionConfidences)
    {
        if (detectionConfidences == null || detectionConfidences.Count == 0)
        {
            DetectionStatusText.Text = "No detection results available. Using auto-detection.";
            WarningPanel.IsVisible = true;
            WarningTextBlock.Text = "⚠️ Unable to detect filesystem type. Please select manually.";
            return;
        }

        _detectionResults.Clear();
        float maxConfidence = 0;
        string maxType = "";

        foreach (var kvp in detectionConfidences.OrderByDescending(x => x.Value))
        {
            var result = new DetectionResult
            {
                FilesystemType = kvp.Key,
                Confidence = kvp.Value
            };
            _detectionResults.Add(result);

            if (kvp.Value > maxConfidence)
            {
                maxConfidence = kvp.Value;
                maxType = kvp.Key;
            }
        }

        DetectionConfidenceList.ItemsSource = _detectionResults;

        // Determine status
        if (maxConfidence >= 0.8f)
        {
            DetectionStatusText.Text = $"✓ Confident detection: {maxType} (confidence: {maxConfidence:P0})";
            WarningPanel.IsVisible = false;
            DontShowCheckBox.IsVisible = true;
        }
        else if (maxConfidence >= 0.5f)
        {
            DetectionStatusText.Text = $"⚠ Uncertain detection: {maxType} (confidence: {maxConfidence:P0}). Multiple filesystems detected.";
            WarningPanel.IsVisible = true;
            WarningTextBlock.Text = $"⚠️ Detection confidence is low ({maxConfidence:P0}). Multiple filesystems may match. Please verify the filesystem type manually.";
            DontShowCheckBox.IsVisible = true;
        }
        else
        {
            DetectionStatusText.Text = "❌ No confident detection. Please select filesystem type manually.";
            WarningPanel.IsVisible = true;
            WarningTextBlock.Text = "❌ Unable to confidently detect filesystem type. Please select the correct filesystem type from the dropdown.";
            DontShowCheckBox.IsVisible = false;
        }

        // Auto-select best match if confidence is high enough
        if (maxConfidence >= 0.8f)
        {
            SelectFilesystemTypeByTag(maxType.ToLower());
        }
    }

    private void SelectFilesystemTypeByTag(string tag)
    {
        for (int i = 0; i < FilesystemTypeCombo.ItemCount; i++)
        {
            var item = FilesystemTypeCombo.Items[i];
            if (item is ComboBoxItem comboItem && comboItem.Tag?.ToString() == tag)
            {
                FilesystemTypeCombo.SelectedIndex = i;
                return;
            }
        }
    }

    private void OnFilesystemTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePartitionVisibility();
        UpdateUrnPreview();

        // Auto-detect partitions if BSD or FFS is selected (both use BSD Disk Labels)
        string fsTag = GetSelectedFilesystemTag();
        if (fsTag == "bsd" || fsTag == "ffs")
        {
            _ = DetectBsdPartitionsAsync();
        }
    }

    private void OnPartitionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateUrnPreview();
        UpdatePartitionInfo();
    }

    private void UpdatePartitionVisibility()
    {
        // Show partition selection for both BSD (V7) and FFS/UFS (both use BSD Disk Labels)
        string fsTag = GetSelectedFilesystemTag();
        bool needsPartitionSelection = (fsTag == "bsd" || fsTag == "ffs");
        BsdPartitionPanel.IsVisible = needsPartitionSelection;
    }

    private string GetSelectedFilesystemTag()
    {
        if (FilesystemTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag;
        }
        return "auto";
    }

    private async void OnDetectPartitionsClick(object? sender, RoutedEventArgs e)
    {
        await DetectBsdPartitionsAsync();
    }

    private async System.Threading.Tasks.Task DetectBsdPartitionsAsync()
    {
        if (!File.Exists(_imagePath))
        {
            return;
        }

        try
        {
            DetectPartitionsButton.IsEnabled = false;
            DetectPartitionsButton.Content = "Detecting...";
            BsdPartitionCombo.Items.Clear();
            BsdPartitionCombo.Items.Add(new ComboBoxItem { Content = "Detecting partitions...", IsEnabled = false });
            BsdPartitionCombo.SelectedIndex = 0;

            // Use RetroFS unified PartitionDetector API
            System.Diagnostics.Debug.WriteLine($"[GenericMountDialog] Detecting partitions for: {_imagePath}");
            var partitionResult = await System.Threading.Tasks.Task.Run(() =>
                PartitionDetector.DetectFromFile(_imagePath));

            System.Diagnostics.Debug.WriteLine($"[GenericMountDialog] Partition detection result: TableType={partitionResult.TableType}, Count={partitionResult.Partitions?.Count ?? 0}");

            _detectedPartitions = partitionResult.Partitions;

            BsdPartitionCombo.Items.Clear();

            // Filter BSD and Sun partitions (ignore MBR/GPT for now)
            var bsdPartitions = _detectedPartitions?
                .Where(p => p.TableType == PartitionTableType.BsdDiskLabel || p.TableType == PartitionTableType.SunLabel)
                .ToList() ?? new List<PartitionMetadata>();

            System.Diagnostics.Debug.WriteLine($"[GenericMountDialog] BSD/Sun partitions found: {bsdPartitions.Count}");

            if (_detectedPartitions != null)
            {
                for (int i = 0; i < _detectedPartitions.Count; i++)
                {
                    var p = _detectedPartitions[i];
                    System.Diagnostics.Debug.WriteLine($"[GenericMountDialog]   Partition {i}: Id={p.Id}, TableType={p.TableType}, Offset={p.OffsetSectors}, Size={p.SizeSectors}");
                }
            }

            // Show partition panel if partitions were found
            if (bsdPartitions.Count > 0)
            {
                BsdPartitionPanel.IsVisible = true;
            }

            if (bsdPartitions.Count == 0)
            {
                // No auto-detected partitions - show manual partition selection
                // This handles Sun Disk Labels (not yet implemented) and disks without partition tables
                System.Diagnostics.Debug.WriteLine($"[GenericMountDialog] No BSD partitions auto-detected, showing manual partition selection");

                // Add raw filesystem option
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Raw filesystem (no partition table)",
                    Tag = "raw"
                });

                // Add manual partition selections (a-h) for Sun disks and others
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition a (root, try this first)",
                    Tag = "a"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition b (swap/secondary)",
                    Tag = "b"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition c (whole disk)",
                    Tag = "c"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition d (user data)",
                    Tag = "d"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition e (user data)",
                    Tag = "e"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition f (user data)",
                    Tag = "f"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition g (user data)",
                    Tag = "g"
                });
                BsdPartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Partition h (user data)",
                    Tag = "h"
                });

                BsdPartitionCombo.SelectedIndex = 1; // Default to partition 'a'
                PartitionInfoText.Text = "Note: Partitions could not be auto-detected. Manual selection available. Try partition 'a' (root) first.";
                PartitionInfoText.IsVisible = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[GenericMountDialog] Adding {bsdPartitions.Count} BSD partitions to combo");
                foreach (var partition in bsdPartitions)
                {
                    double sizeMB = (partition.SizeSectors * 512.0) / (1024.0 * 1024.0);

                    BsdPartitionCombo.Items.Add(new ComboBoxItem
                    {
                        Content = partition.DisplayText,
                        Tag = partition.Id
                    });
                }
                BsdPartitionCombo.SelectedIndex = 0;
                UpdatePartitionInfo();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GenericMountDialog] ERROR detecting partitions: {ex}");
            BsdPartitionCombo.Items.Clear();
            BsdPartitionCombo.Items.Add(new ComboBoxItem
            {
                Content = $"Error: {ex.Message}",
                IsEnabled = false
            });
        }
        finally
        {
            DetectPartitionsButton.IsEnabled = true;
            DetectPartitionsButton.Content = "Detect Partitions";
        }
    }

    private void UpdatePartitionInfo()
    {
        if (_detectedPartitions == null || _detectedPartitions.Count == 0)
        {
            PartitionInfoText.IsVisible = false;
            return;
        }

        if (BsdPartitionCombo.SelectedItem is ComboBoxItem selectedItem &&
            selectedItem.Tag is string partitionTag &&
            partitionTag != "raw")
        {
            var partition = _detectedPartitions.FirstOrDefault(p => p.Id == partitionTag);

            if (partition != null && partition.SizeSectors > 0)
            {
                double sizeMB = (partition.SizeSectors * 512.0) / (1024.0 * 1024.0);
                PartitionInfoText.Text = $"Partition {partition.Id.ToUpperInvariant()}: Starts at sector {partition.OffsetSectors}, Size: {sizeMB:F2} MB, Type Code: 0x{partition.FileSystemTypeCode:X2}";
                PartitionInfoText.IsVisible = true;
            }
            else
            {
                PartitionInfoText.IsVisible = false;
            }
        }
        else
        {
            PartitionInfoText.IsVisible = false;
        }
    }

    private void UpdateUrnPreview()
    {
        string scheme = GetSelectedFilesystemTag() switch
        {
            "bsd" => "bsd://",
            "ffs" => "ffs://",
            "ndfs" => "ndfs://",
            "d64" => "d64://",
            "d71" => "d71://",
            "d81" => "d81://",
            "file" => "file:///",
            _ => "file:///" // auto-detect uses file:///
        };

        string path = _imagePath.Replace("\\", "/");
        string partition = "";

        // Add partition fragment for BSD and FFS (both use BSD Disk Labels)
        // IMPORTANT: Also add for "file:///" (auto-detect) if partition panel is visible
        if (BsdPartitionPanel.IsVisible &&
            BsdPartitionCombo.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            tag != "raw")
        {
            partition = "#" + tag;
        }

        UrnPreviewText.Text = scheme + path + partition;
    }

    private void OnMountClick(object? sender, RoutedEventArgs e)
    {
        MountConfirmed = true;
        DontShowAgain = DontShowCheckBox.IsChecked == true;
        SelectedUrn = UrnPreviewText.Text;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        MountConfirmed = false;
        DontShowAgain = false;
        SelectedUrn = null;
        Close(false);
    }

    /// <summary>
    /// Loads detailed filesystem metadata asynchronously for the Details tab.
    /// </summary>
    private async System.Threading.Tasks.Task LoadDetailsTabAsync()
    {
        try
        {
            // Initialize metadata service if not already done
            if (_metadataService == null)
            {
                var driverFactory = new DriverFactory();
                driverFactory.Register(new V7DriverProvider());
                driverFactory.Register(new FfsDriverProvider());
                driverFactory.Register(new NdfsDriverProvider());
                driverFactory.Register(new CommodoreDiskDriverProvider());
                driverFactory.Register(new DragonVdkDriverProvider());
                driverFactory.Register(new DragonDskDriverProvider());
                driverFactory.Register(new PcFloppyDriverProvider());
                driverFactory.Register(new T64DriverProvider());

                _metadataService = new FileSystemMetadataService(driverFactory);
            }

            // Read metadata on background thread
            var metadata = await System.Threading.Tasks.Task.Run(() =>
                _metadataService.ReadMetadata(_imagePath));

            // Update UI on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (metadata == null || metadata.HasErrors)
                {
                    DetailsLoadingPanel.IsVisible = false;
                    DetailsErrorPanel.IsVisible = true;
                    DetailsErrorText.Text = metadata?.ErrorMessage ?? "Failed to read metadata";
                    return;
                }

                // Group properties by category
                var properties = metadata.GetDetailedProperties();
                var groupedProperties = new List<MetadataCategory>();

                // Manual grouping to avoid LINQ
                var categories = new Dictionary<string, List<MetadataProperty>>();
                for (int i = 0; i < properties.Count; i++)
                {
                    var prop = properties[i];
                    if (!categories.ContainsKey(prop.Category))
                    {
                        categories[prop.Category] = new List<MetadataProperty>();
                    }
                    categories[prop.Category].Add(prop);
                }

                // Convert to display format
                Dictionary<string, List<MetadataProperty>>.Enumerator enumerator = categories.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    groupedProperties.Add(new MetadataCategory
                    {
                        Category = enumerator.Current.Key,
                        Properties = enumerator.Current.Value
                    });
                }
                enumerator.Dispose();

                // Bind to UI
                MetadataList.ItemsSource = groupedProperties;
                MetadataList.IsVisible = true;
                DetailsLoadingPanel.IsVisible = false;

                // Show warnings if any
                if (metadata.Warnings.Count > 0)
                {
                    WarningsList.ItemsSource = metadata.Warnings;
                    DetailsWarningsPanel.IsVisible = true;
                }
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                DetailsLoadingPanel.IsVisible = false;
                DetailsErrorPanel.IsVisible = true;
                DetailsErrorText.Text = $"Exception: {ex.Message}";
            });
        }
    }

    private class DetectionResult
    {
        public string FilesystemType { get; set; } = "";
        public float Confidence { get; set; }
        public string ConfidenceText => $"{Confidence:P0}";
        public Avalonia.Media.IBrush ConfidenceColor => Confidence >= 0.8f ?
            Avalonia.Media.Brushes.Green :
            Confidence >= 0.5f ?
            Avalonia.Media.Brushes.Orange :
            Avalonia.Media.Brushes.Red;
    }

    /// <summary>
    /// Metadata grouped by category for display.
    /// </summary>
    private class MetadataCategory
    {
        public string Category { get; set; } = "";
        public List<MetadataProperty> Properties { get; set; } = new();
    }
}

