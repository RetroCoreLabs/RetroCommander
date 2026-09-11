//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Partitions;
using RetroIO.Storage.Ndfs.Drivers;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop;

public partial class MountDialog : Window
{
    private readonly DriverFactory _driverFactory;

    public MountDialog() : this(new DriverFactory())
    {
    }

    public MountDialog(DriverFactory driverFactory)
    {
        _driverFactory = driverFactory ?? throw new ArgumentNullException(nameof(driverFactory));

        InitializeComponent();

        FileSystemTypeCombo.SelectionChanged += OnTypeChanged;
        PathTextBox.TextChanged += OnPathChanged;
        PartitionCombo.SelectionChanged += OnPartitionChanged;

        UpdateUrnPreview();
    }

    private async void OnTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePartitionVisibility();
        UpdateUrnPreview();
        await LoadNdfsInformationIfNeeded();
    }

    private async void OnPathChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateUrnPreview();
        await LoadNdfsInformationIfNeeded();
    }

    private void OnPartitionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateUrnPreview();
    }

    private void UpdatePartitionVisibility()
    {
        // Show partition selector for filesystems that support partitions
        int selectedIndex = FileSystemTypeCombo.SelectedIndex;

        // Show for:
        // - Index 9: BSD Disk Image (can have BSD disk label with 8 partitions)
        // - Future: PC hard disk images with MBR/GPT can be added here
        bool showPartitions = selectedIndex == 9;  // BSD filesystem

        PartitionPanel.IsVisible = showPartitions;
    }

    private void UpdateUrnPreview()
    {
        // Map UI index to FileSystemScheme enum
        var scheme = FileSystemTypeCombo.SelectedIndex switch
        {
            0 => FileSystemScheme.File,
            1 => FileSystemScheme.Ftp,
            2 => FileSystemScheme.Ftps,
            3 => FileSystemScheme.Sftp,
            4 => FileSystemScheme.Tnfs,
            5 => FileSystemScheme.Ndfs,
            6 => FileSystemScheme.D64,
            7 => FileSystemScheme.D71,
            8 => FileSystemScheme.D81,
            9 => FileSystemScheme.V7,
            10 => FileSystemScheme.Zip,
            _ => FileSystemScheme.File
        };

        var type = scheme.ToSchemeString() + "://";
        // Only add extra / for file:// scheme (RFC standard), not for custom schemes
        // Custom schemes like bsd://, d64://, etc. should use cleaner format
        if (scheme == FileSystemScheme.File)
        {
            type += "/";  // file:/// format for local files (RFC standard)
        }

        var path = PathTextBox.Text ?? "";

        // Check if this is a network filesystem
        bool isNetwork = scheme.IsNetworkFilesystem();

        // Check if partition selector is visible
        bool hasPartition = PartitionPanel.IsVisible;
        string partition = "";
        if (hasPartition && PartitionCombo.SelectedIndex >= 0)
        {
            // Get partition ID from selected item's Tag (if available), otherwise use index
            var selectedItem = PartitionCombo.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is string partitionId && !string.IsNullOrEmpty(partitionId))
            {
                // Skip fragment for "raw" filesystem (no partitions)
                if (partitionId != "raw")
                {
                    partition = "#" + partitionId;
                }
            }
            else if (PartitionCombo.SelectedIndex > 0 || selectedItem?.Tag != null)
            {
                // Fallback: use index-based partition letter for BSD
                partition = "#" + ((char)('a' + PartitionCombo.SelectedIndex)).ToString();
            }
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            if (isNetwork)
            {
                // Network URN format: scheme://user:pass@host:port/path#partition
                UrnPreview.Text = type + path + partition;
            }
            else
            {
                // File URN format: scheme:///path#partition
                var cleanPath = path.Replace("\\", "/");
                UrnPreview.Text = type + cleanPath + partition;
            }
        }
        else
        {
            if (isNetwork)
            {
                UrnPreview.Text = type + "user:pass@host:port/path" + partition;
            }
            else
            {
                UrnPreview.Text = type + "<path>" + partition;
            }
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var storage = StorageProvider;
        int selectedIndex = FileSystemTypeCombo.SelectedIndex;

        // Browse button only works for local files/folders, not network protocols
        if (selectedIndex >= 1 && selectedIndex <= 4)
        {
            // Network protocols (FTP, FTPS, SFTP, TNFS) - no file browser
            return;
        }

        if (selectedIndex == 0)
        {
            // Browse for folder (Windows Folder)
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                PathTextBox.Text = folders[0].Path.LocalPath;
            }
        }
        else if (selectedIndex == 10)
        {
            // Browse for ZIP archive
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select ZIP Archive",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("ZIP Archives") { Patterns = new[] { "*.zip", "*.jar", "*.apk" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                PathTextBox.Text = files[0].Path.LocalPath;
            }
        }
        else
        {
            // Browse for disk image (NDFS, D64, D71, D81, BSD)
            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Disk Image",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Disk Images") { Patterns = new[] { "*.ndfs", "*.img", "*.d64", "*.d71", "*.d81", "*.bsd", "*.dsk" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                PathTextBox.Text = files[0].Path.LocalPath;
            }
        }
    }

    private void OnMountClick(object? sender, RoutedEventArgs e)
    {
        Close(UrnPreview.Text);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private async void OnDetectPartitionsClick(object? sender, RoutedEventArgs e)
    {
        var path = PathTextBox.Text;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            // Show error - need valid file path first
            await ShowMessageBox("Error", "Please select a valid disk image file first.");
            return;
        }

        try
        {
            DetectPartitionsButton.IsEnabled = false;
            DetectPartitionsButton.Content = "Detecting...";

            var partitions = await Task.Run(() => DetectPartitionsFromImage(path));

            // Clear existing items and add detected partitions
            PartitionCombo.Items.Clear();

            if (partitions.Count == 0)
            {
                // No partitions found - offer raw filesystem option
                PartitionCombo.Items.Add(new ComboBoxItem
                {
                    Content = "Raw filesystem (no partition table)",
                    Tag = "raw"
                });
                PartitionCombo.SelectedIndex = 0;
            }
            else
            {
                foreach (var partition in partitions)
                {
                    PartitionCombo.Items.Add(new ComboBoxItem { Content = partition.DisplayText, Tag = partition.Id });
                }
                PartitionCombo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            await ShowMessageBox("Error", $"Failed to detect partitions: {ex.Message}");
        }
        finally
        {
            DetectPartitionsButton.IsEnabled = true;
            DetectPartitionsButton.Content = "Detect Partitions";
        }
    }

    private async Task ShowMessageBox(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Width = 80
                    }
                }
            },
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var button = (dialog.Content as StackPanel)?.Children[1] as Button;
        if (button != null)
        {
            button.Click += (s, e) => dialog.Close();
        }

        await dialog.ShowDialog(this);
    }

    private List<PartitionInfo> DetectPartitionsFromImage(string imagePath)
    {
        // Use library service for all partition detection
        return PartitionDetectionService.DetectPartitions(imagePath);
    }

    private async Task LoadNdfsInformationIfNeeded()
    {
        // Only load NDFS info if NDFS filesystem type is selected (index 5)
        if (FileSystemTypeCombo.SelectedIndex != 5)
        {
            NdfsInfoPanel.IsVisible = false;
            return;
        }

        string path = PathTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            NdfsInfoPanel.IsVisible = false;
            return;
        }

        try
        {
            await Task.Run(() => LoadNdfsInformation(path));
        }
        catch (Exception ex)
        {
            // Failed to load NDFS info - hide panel
            NdfsInfoPanel.IsVisible = false;
            Console.WriteLine($"Failed to load NDFS info: {ex.Message}");
        }
    }

    private void LoadNdfsInformation(string imagePath)
    {
        // Use the centralized DriverFactory passed from MainWindow (has ALL drivers registered)
        var driver = _driverFactory.Detect(imagePath);
        if (driver == null || driver is not NdfsDriver ndfsDriver)
        {
            return;
        }

        using var stream = File.OpenRead(imagePath);
        ndfsDriver.Mount(stream, readOnly: true);
        var ndfs = ndfsDriver.GetFileSystem();
        if (ndfs == null)
        {
            return;
        }

        // Update UI on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // Master block info
                NdfsDirectoryName.Text = ndfs.GetDirectoryName();

                uint freePages = ndfs.GetPagesAvailable();
                var masterBlock = ndfs.GetMasterBlock();
                uint totalPages = masterBlock.ImageSize;
                uint usedPages = totalPages > freePages ? totalPages - freePages : 0;

                NdfsTotalPages.Text = $"{totalPages} pages";
                NdfsFreePages.Text = $"{freePages} free / {usedPages} used";

                // Boot info
                bool isBootable = ndfs.IsBootable();
                var bootFormat = ndfs.DetectBootFormat();
                NdfsBootable.Text = isBootable ? $"Yes ({bootFormat})" : "No";

                // Checksum info
                var extendedInfo = ndfs.GetExtendedMasterBlockInfo();
                bool hasValidChecksum = ndfs.HasValidExtendedInfo();
                if (extendedInfo != null && extendedInfo.ContainsKey("ChecksumValid"))
                {
                    bool checksumValid = (bool)extendedInfo["ChecksumValid"];
                    NdfsChecksumStatus.Text = checksumValid ? "✓ Valid" : "⚠ Invalid";
                }
                else
                {
                    NdfsChecksumStatus.Text = hasValidChecksum ? "✓ Valid (Hard Disk)" : "N/A (Floppy)";
                }

                // Users list
                var users = ndfs.GetUsers();
                var userViewModels = new List<NdfsUserViewModel>();
                foreach (var user in users)
                {
                    userViewModels.Add(new NdfsUserViewModel
                    {
                        Name = user.UserName,
                        Quota = $"Reserved: {user.PagesReserved} pages",
                        Used = $"Used: {user.PagesUsed} pages"
                    });
                }
                NdfsUsersList.ItemsSource = userViewModels;

                // Extended info text
                if (extendedInfo != null && hasValidChecksum)
                {
                    var extText = string.Join(", ", extendedInfo.Select(kv => $"{kv.Key}: {kv.Value}"));
                    NdfsExtendedInfoText.Text = extText;
                    NdfsExtendedInfoPanel.IsVisible = true;
                }
                else
                {
                    NdfsExtendedInfoPanel.IsVisible = false;
                }

                // Show the panel
                NdfsInfoPanel.IsVisible = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating NDFS UI: {ex.Message}");
                NdfsInfoPanel.IsVisible = false;
            }
        });
    }

}

// View model for NDFS user list
public class NdfsUserViewModel
{
    public string Name { get; set; } = "";
    public string Quota { get; set; } = "";
    public string Used { get; set; } = "";
}
