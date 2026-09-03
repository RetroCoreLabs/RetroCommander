//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RetroIO.Storage.Local;

namespace RetroCommander.Desktop;

public partial class CreatePcImageDialog : Window
{
    private string? _outputPath;

    public CreatePcImageDialog()
    {
        InitializeComponent();

        MediaTypeCombo.SelectionChanged += OnMediaTypeChanged;
        FloppySizeCombo.SelectionChanged += OnPreviewChanged;
        HddPresetCombo.SelectionChanged += OnPreviewChanged;

        UpdatePreview();
    }

    private void OnMediaTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        bool isHardDisk = IsHardDiskSelected();
        FloppySizePanel.IsVisible = !isHardDisk;
        HddPresetPanel.IsVisible = isHardDisk;
        UpdatePreview();
    }

    private void OnPreviewChanged(object? sender, EventArgs e)
    {
        UpdatePreview();
    }

    private bool IsHardDiskSelected()
    {
        return MediaTypeCombo.SelectedItem is ComboBoxItem item && item.Tag as string == "HardDisk";
    }

    private PcFloppySize GetSelectedFloppySize()
    {
        if (FloppySizeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag switch
            {
                "Size360K" => PcFloppySize.Size360K,
                "Size720K" => PcFloppySize.Size720K,
                "Size1_2M" => PcFloppySize.Size1_2M,
                "Size1_44M" => PcFloppySize.Size1_44M,
                "Size2_88M" => PcFloppySize.Size2_88M,
                _ => PcFloppySize.Size1_44M
            };
        }
        return PcFloppySize.Size1_44M;
    }

    private PcHddPreset GetSelectedHddPreset()
    {
        if (HddPresetCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag switch
            {
                "Xt5MB" => PcHddPreset.Xt5MB,
                "Xt10MB" => PcHddPreset.Xt10MB,
                "Xt15MB" => PcHddPreset.Xt15MB,
                "Xt26MB" => PcHddPreset.Xt26MB,
                "At20MB" => PcHddPreset.At20MB,
                "At30MB" => PcHddPreset.At30MB,
                "At47MB" => PcHddPreset.At47MB,
                "At62MB" => PcHddPreset.At62MB,
                _ => PcHddPreset.Xt10MB
            };
        }
        return PcHddPreset.Xt10MB;
    }

    private void UpdatePreview()
    {
        try
        {
            if (IsHardDiskSelected())
            {
                PcHddPreset preset = GetSelectedHddPreset();
                (string geometry, long size, string fsGuess) = HddPreviewInfo(preset);
                PreviewGeometry.Text = $"Geometry: {geometry}";
                PreviewFileSize.Text = $"File Size: {size:N0} bytes ({size / 1024.0 / 1024.0:F2} MB)";
                PreviewFileSystem.Text = $"Filesystem: {fsGuess} (single MBR partition, 100% of disk)";
            }
            else
            {
                PcFloppySize size = GetSelectedFloppySize();
                (string geometry, long bytes) = FloppyPreviewInfo(size);
                PreviewGeometry.Text = $"Geometry: {geometry}";
                PreviewFileSize.Text = $"File Size: {bytes:N0} bytes ({bytes / 1024.0 / 1024.0:F2} MB)";
                PreviewFileSystem.Text = "Filesystem: FAT12";
            }
        }
        catch
        {
            // Ignore preview update errors — mirrors CreateNdfsImageDialog's convention.
        }
    }

    private static (string Geometry, long Bytes) FloppyPreviewInfo(PcFloppySize size)
    {
        return size switch
        {
            PcFloppySize.Size360K => ("40 cyl, 2 heads, 9 sect/trk", 368640L),
            PcFloppySize.Size720K => ("80 cyl, 2 heads, 9 sect/trk", 737280L),
            PcFloppySize.Size1_2M => ("80 cyl, 2 heads, 15 sect/trk", 1228800L),
            PcFloppySize.Size1_44M => ("80 cyl, 2 heads, 18 sect/trk", 1474560L),
            PcFloppySize.Size2_88M => ("80 cyl, 2 heads, 36 sect/trk", 2949120L),
            _ => ("-", 0L)
        };
    }

    private static (string Geometry, long Bytes, string FileSystemGuess) HddPreviewInfo(PcHddPreset preset)
    {
        return preset switch
        {
            PcHddPreset.Xt5MB => ("306 cyl, 2 heads, 17 sect/trk", 306L * 2 * 17 * 512, "FAT12"),
            PcHddPreset.Xt10MB => ("306 cyl, 4 heads, 17 sect/trk", 306L * 4 * 17 * 512, "FAT12"),
            PcHddPreset.Xt15MB => ("306 cyl, 6 heads, 17 sect/trk", 306L * 6 * 17 * 512, "FAT12"),
            PcHddPreset.Xt26MB => ("375 cyl, 8 heads, 17 sect/trk", 375L * 8 * 17 * 512, "FAT16"),
            PcHddPreset.At20MB => ("615 cyl, 4 heads, 17 sect/trk", 615L * 4 * 17 * 512, "FAT16"),
            PcHddPreset.At30MB => ("615 cyl, 6 heads, 17 sect/trk", 615L * 6 * 17 * 512, "FAT16"),
            PcHddPreset.At47MB => ("940 cyl, 6 heads, 17 sect/trk", 940L * 6 * 17 * 512, "FAT16"),
            PcHddPreset.At62MB => ("940 cyl, 8 heads, 17 sect/trk", 940L * 8 * 17 * 512, "FAT16"),
            _ => ("-", 0L, "-")
        };
    }

    private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save PC Disk Image",
            DefaultExtension = "img",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("PC Disk Images") { Patterns = new[] { "*.img" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            },
            SuggestedFileName = "new-disk.img"
        });

        if (file != null)
        {
            _outputPath = file.Path.LocalPath;
            OutputPathInput.Text = _outputPath;
        }
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_outputPath))
        {
            await ShowMessageBoxAsync("Error", "Please select an output file location.");
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            CreateButton.Content = "Creating...";

            bool isHardDisk = IsHardDiskSelected();
            await CreateImageFileAsync(_outputPath);

            var fileInfo = new FileInfo(_outputPath);
            var message = $"PC disk image created successfully!\n\n" +
                         $"File: {Path.GetFileName(_outputPath)}\n" +
                         $"Size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)\n\n" +
                         (isHardDisk
                             ? "Mount with fragment #0 to access the partition (e.g. file:///path/to/image.img#0)."
                             : "You can now mount this image in RetroCommander.");

            await ShowMessageBoxAsync("Success", message);

            Close(_outputPath);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Error", $"Failed to create disk image:\n\n{ex.Message}");
            CreateButton.IsEnabled = true;
            CreateButton.Content = "Create Image";
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    /// <summary>
    /// The exact image-creation call the real "Create Image" button invokes — extracted so tests
    /// can drive precisely this path (and inspect the resulting file on disk) without going
    /// through the modal success/error message boxes <see cref="OnCreateClick"/> wraps it in.
    /// </summary>
    internal Task CreateImageFileAsync(string outputPath)
    {
        string volumeLabel = VolumeLabelInput.Text?.Trim() ?? string.Empty;

        if (IsHardDiskSelected())
        {
            PcHddPreset preset = GetSelectedHddPreset();
            return Task.Run(() => PcImageFactory.CreateBlankHardDiskFile(preset, outputPath, volumeLabel));
        }

        PcFloppySize size = GetSelectedFloppySize();
        return Task.Run(() => PcImageFactory.CreateBlankFloppyFile(size, outputPath, volumeLabel));
    }

    private async Task ShowMessageBoxAsync(string title, string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Padding = new Avalonia.Thickness(20, 8)
        };

        var msgBox = new Window
        {
            Title = title,
            Width = 450,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Margin = new Avalonia.Thickness(15),
                Children =
                {
                    new ScrollViewer
                    {
                        [Grid.RowProperty] = 0,
                        Content = new TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Margin = new Avalonia.Thickness(0, 0, 0, 15)
                        }
                    },
                    okButton
                }
            }
        };

        okButton.Click += (s, e) => msgBox.Close();
        Grid.SetRow(okButton, 1);

        await msgBox.ShowDialog(this);
    }
}
