//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using RetroIO.Storage.Ndfs.Creation;

namespace RetroCommander.Desktop;

/// <summary>
/// Dialog that creates a new, empty NDFS disk image file from a size template or a custom page count.
/// </summary>
/// <remarks>
/// The dialog closes with the path of the created file as its result, or with null when the user cancels.
/// </remarks>
public partial class CreateNdfsImageDialog : Window
{
    private string? _outputPath;

    /// <summary>
    /// Creates the dialog, hooks up the input controls and fills in the first preview.
    /// </summary>
    public CreateNdfsImageDialog()
    {
        InitializeComponent();

        // Wire up events
        TemplateCombo.SelectionChanged += OnTemplateChanged;
        CustomPagesInput.ValueChanged += OnCustomPagesChanged;
        ControllerTypeCombo.SelectionChanged += OnPreviewChanged;
        BootLoaderCombo.SelectionChanged += OnPreviewChanged;
        IncludeExtendedInfoCheck.IsCheckedChanged += OnExtendedInfoCheckChanged;
        DirectoryNameInput.TextChanged += OnPreviewChanged;

        // Initial preview update
        UpdatePreview();
    }

    // Cap the auto-fit (SizeToContent="Height") to the screen working area so the window can't
    // grow off-screen when both the Custom-size and Extended-info panels are expanded.
    /// <inheritdoc/>
    /// <remarks>
    /// Limits the window height to 92 percent of the working area of the screen it is on.
    /// </remarks>
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

    private void OnTemplateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (TemplateCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            bool isCustom = tag == "Custom";
            CustomSizePanel.IsVisible = isCustom;

            // Auto-set boot loader based on template
            if (!isCustom)
            {
                BootLoaderCombo.SelectedIndex = tag switch
                {
                    "Floppy360KB" => 1,  // Floppy boot loader
                    "Floppy12MB" => 1,   // Floppy boot loader
                    "Smd75MB" => 2,      // SMD boot loader
                    "Winchester74MB" => 3, // Winchester boot loader
                    _ => 0
                };

                // Floppies don't have Extended Info
                if (tag.StartsWith("Floppy"))
                {
                    IncludeExtendedInfoCheck.IsChecked = false;
                    ExtendedInfoPanel.IsVisible = false;
                }
                else
                {
                    ExtendedInfoPanel.IsVisible = true;
                    IncludeExtendedInfoCheck.IsChecked = true;
                }
            }
            else
            {
                ExtendedInfoPanel.IsVisible = true;
            }
        }

        UpdatePreview();
    }

    private void OnCustomPagesChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        // Update custom size preview
        if (CustomPagesInput.Value.HasValue)
        {
            uint pages = (uint)CustomPagesInput.Value.Value;
            double sizeMB = (pages * 2048) / 1024.0 / 1024.0;
            CustomSizePreview.Text = $"{sizeMB:F2} MB";
        }

        UpdatePreview();
    }

    private void OnPreviewChanged(object? sender, EventArgs e)
    {
        UpdatePreview();
    }

    private void OnExtendedInfoCheckChanged(object? sender, RoutedEventArgs e)
    {
        ExtendedInfoFields.IsVisible = IncludeExtendedInfoCheck.IsChecked == true;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            var template = GetSelectedTemplate();
            var options = BuildCreationOptions();

            uint ndfsPages;
            uint fileBlocks;
            string controllerName;

            // Calculate preview based on template
            if (template == NdfsImageTemplate.Custom)
            {
                if (options.CustomNdfsPages == 0) return;

                ndfsPages = options.CustomNdfsPages;
                uint spareBlocks = CalculateSpareBlocks(ndfsPages, options.ControllerType);
                fileBlocks = ndfsPages + spareBlocks;
                controllerName = options.ControllerType.ToString();
            }
            else
            {
                // Hard-coded template values (from s_ndfsTemplates)
                (ndfsPages, fileBlocks, controllerName) = template switch
                {
                    NdfsImageTemplate.Floppy360KB => (154u, 154u, "Floppy"),
                    NdfsImageTemplate.Floppy12MB => (616u, 616u, "Floppy"),
                    NdfsImageTemplate.Smd75MB => (36945u, 38400u, "SMD"),
                    // file_blocks MUST be the device size (36864), not 36360. The old 36360 was
                    // SMALLER than the 36396 pages of declared capacity, so the previewed image was
                    // 36 pages short of its own size. Corrected to the measured Micropolis 1325
                    // device size = 36864 pages = 72.0 MiB exactly, matching s_ndfsTemplates.Winchester74MB
                    // (see NdfsTemplateSpec.cs "BUG FIXED" note and NewImageDialog.axaml.cs which was
                    // already correct). Verified geometry: DISC-BOOT-SECTOR-ANATOMY.md geometry block
                    // (512/9/72/1021/1011) confirms the 1325 Winchester pack.
                    NdfsImageTemplate.Winchester74MB => (36396u, 36864u, "Winchester"),
                    _ => (0u, 0u, "Unknown")
                };
            }

            PreviewNdfsPages.Text = $"NDFS Pages: {ndfsPages:N0}";
            PreviewFileSize.Text = $"File Size: {fileBlocks * 2048:N0} bytes ({(fileBlocks * 2048 / 1024.0 / 1024.0):F2} MB)";
            PreviewControllerType.Text = $"Controller: {controllerName}";

            bool isFloppy = (template == NdfsImageTemplate.Floppy360KB || template == NdfsImageTemplate.Floppy12MB);
            PreviewHasExtInfo.Text = isFloppy ? "Extended Info: No (Floppy)" :
                                    (options.IncludeExtendedInfo ? "Extended Info: Yes" : "Extended Info: No");
        }
        catch
        {
            // Ignore preview update errors
        }
    }

    private uint CalculateSpareBlocks(uint ndfsPages, NdfsControllerType controllerType)
    {
        return controllerType switch
        {
            NdfsControllerType.Smd => (uint)(ndfsPages * 0.0394),
            NdfsControllerType.Scsi => (uint)(ndfsPages * 0.0593),
            NdfsControllerType.Winchester => 0,
            NdfsControllerType.Floppy => 0,
            _ => 0
        };
    }

    private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save NDFS Disk Image",
            DefaultExtension = "img",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("NDFS Disk Images") { Patterns = new[] { "*.img", "*.ndfs" } },
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

        if (string.IsNullOrWhiteSpace(DirectoryNameInput.Text))
        {
            await ShowMessageBoxAsync("Error", "Please enter a directory name.");
            return;
        }

        try
        {
            CreateButton.IsEnabled = false;
            CreateButton.Content = "Creating...";

            var options = BuildCreationOptions();

            // Create the image
            await Task.Run(() => NdfsImageCreator.CreateImage(_outputPath, options));

            // Show success message
            var fileInfo = new FileInfo(_outputPath);
            var message = $"NDFS disk image created successfully!\n\n" +
                         $"File: {Path.GetFileName(_outputPath)}\n" +
                         $"Size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)\n" +
                         $"Directory: {options.DirectoryName}\n\n" +
                         $"You can now mount this image in RetroCommander.";

            await ShowMessageBoxAsync("Success", message);

            // Return the path to the caller
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

    private NdfsImageTemplate GetSelectedTemplate()
    {
        if (TemplateCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag switch
            {
                "Floppy360KB" => NdfsImageTemplate.Floppy360KB,
                "Floppy12MB" => NdfsImageTemplate.Floppy12MB,
                "Smd75MB" => NdfsImageTemplate.Smd75MB,
                "Winchester74MB" => NdfsImageTemplate.Winchester74MB,
                "Custom" => NdfsImageTemplate.Custom,
                _ => NdfsImageTemplate.Floppy360KB
            };
        }
        return NdfsImageTemplate.Floppy360KB;
    }

    private NdfsBootLoaderType GetSelectedBootLoader()
    {
        if (BootLoaderCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag switch
            {
                "None" => NdfsBootLoaderType.None,
                "Floppy" => NdfsBootLoaderType.Floppy,
                "Smd" => NdfsBootLoaderType.Smd,
                "Winchester" => NdfsBootLoaderType.Winchester,
                "Scsi" => NdfsBootLoaderType.Scsi,
                _ => NdfsBootLoaderType.None
            };
        }
        return NdfsBootLoaderType.None;
    }

    private NdfsControllerType GetSelectedControllerType()
    {
        if (ControllerTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return tag switch
            {
                "Smd" => NdfsControllerType.Smd,
                "Winchester" => NdfsControllerType.Winchester,
                "Scsi" => NdfsControllerType.Scsi,
                "Floppy" => NdfsControllerType.Floppy,
                _ => NdfsControllerType.Winchester
            };
        }
        return NdfsControllerType.Winchester;
    }

    private NdfsImageCreationOptions BuildCreationOptions()
    {
        var template = GetSelectedTemplate();

        var options = new NdfsImageCreationOptions
        {
            Template = template,
            DirectoryName = DirectoryNameInput.Text?.Trim() ?? "EMPTY-DISK",
            BootLoaderType = GetSelectedBootLoader(),
            IncludeExtendedInfo = IncludeExtendedInfoCheck.IsChecked == true
        };

        // Extended Info fields (hard disks only)
        if (SystemNumberInput.Value.HasValue)
            options.SystemNumber = (ushort)SystemNumberInput.Value.Value;

        if (FlagWordInput.Value.HasValue)
            options.FlagWord = (ushort)FlagWordInput.Value.Value;

        // Custom template fields
        if (template == NdfsImageTemplate.Custom)
        {
            if (CustomPagesInput.Value.HasValue)
                options.CustomNdfsPages = (uint)CustomPagesInput.Value.Value;

            options.ControllerType = GetSelectedControllerType();
        }

        return options;
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
