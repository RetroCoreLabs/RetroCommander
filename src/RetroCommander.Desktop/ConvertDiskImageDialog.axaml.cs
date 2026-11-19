//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RetroIO.Storage.Local.Images;

namespace RetroCommander.Desktop;

/// <summary>
/// Dialog that converts a PC hard-disk image between raw (<c>.img</c>, <c>.hdd</c>) and fixed VHD
/// (<c>.vhd</c>).
/// </summary>
/// <remarks>
/// <para>
/// The direction comes from what the source file holds, not from its name: a file that ends in a
/// fixed VHD footer becomes a raw image, any other file becomes a fixed VHD. For raw to VHD the disk
/// shape (cylinders, heads, sectors per track) is read from the partition table or boot sector and
/// shown filled in; the user can change it, and must fill it in when the disk does not say.
/// </para>
/// <para>
/// The conversion itself is RetroIO's <see cref="DiskImageConverter"/>: it always writes a new file,
/// never changes the source, and refuses by name when the new file exists or the source is not a
/// usable image. The dialog closes with <c>true</c> when a file was written.
/// </para>
/// </remarks>
public partial class ConvertDiskImageDialog : Window
{
    // True when the loaded source is a fixed VHD (so the conversion goes to raw).
    private bool _sourceIsVhd;

    /// <summary>
    /// Creates the dialog with no source chosen.
    /// </summary>
    public ConvertDiskImageDialog()
        : this(null)
    {
    }

    /// <summary>
    /// Creates the dialog and loads a source image.
    /// </summary>
    /// <param name="sourcePath">
    /// The disk image to convert, or <c>null</c> to let the user choose.
    /// </param>
    public ConvertDiskImageDialog(string? sourcePath)
    {
        InitializeComponent();
        SourceInput.LostFocus += OnSourceLostFocus;
        GeometryPanel.IsVisible = false;
        if (!string.IsNullOrEmpty(sourcePath))
        {
            LoadSource(sourcePath);
        }
    }

    /// <summary>
    /// Gets the file the last successful conversion wrote, or <c>null</c>.
    /// </summary>
    public string? CreatedPath { get; private set; }

    /// <summary>
    /// Loads a source image: works out the direction, suggests the new file's name and, for raw to
    /// VHD, fills in the disk shape read from the disk.
    /// </summary>
    /// <param name="path">
    /// The disk image.
    /// </param>
    internal void LoadSource(string path)
    {
        SourceInput.Text = path;
        ProblemText.Text = string.Empty;
        DirectionText.Text = string.Empty;
        GeometryPanel.IsVisible = false;

        if (!File.Exists(path))
        {
            ProblemText.Text = "The file does not exist.";
            return;
        }

        try
        {
            _sourceIsVhd = DiskImageConverter.IsFixedVhd(path);
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException)
        {
            ProblemText.Text = ex.Message;
            return;
        }

        if (_sourceIsVhd)
        {
            DirectionText.Text = "This is a fixed VHD. It becomes a raw image: the same disk bytes without the 512-byte VHD footer.";
            TargetInput.Text = Path.ChangeExtension(path, ".img");
            return;
        }

        DirectionText.Text = "This is a raw image. It becomes a fixed VHD: the same disk bytes followed by a 512-byte VHD footer.";
        TargetInput.Text = Path.ChangeExtension(path, ".vhd");
        GeometryPanel.IsVisible = true;
        if (DiskImageConverter.TryDetectGeometry(path, out DiskGeometry g))
        {
            CylindersInput.Text = g.Cylinders.ToString(CultureInfo.InvariantCulture);
            HeadsInput.Text = g.Heads.ToString(CultureInfo.InvariantCulture);
            SectorsInput.Text = g.SectorsPerTrack.ToString(CultureInfo.InvariantCulture);
            GeometryNote.Text = "Read from the disk's partition table or boot sector. Change it if you know better.";
        }
        else
        {
            CylindersInput.Text = string.Empty;
            HeadsInput.Text = string.Empty;
            SectorsInput.Text = string.Empty;
            GeometryNote.Text = "The disk has no partition table or boot sector that gives its shape. Fill it in.";
        }
    }

    /// <summary>
    /// Runs the conversion with what the dialog shows. On a problem the reason is shown in the dialog.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the new file was written.
    /// </returns>
    internal bool TryConvert()
    {
        ProblemText.Text = string.Empty;
        string source = SourceInput.Text ?? string.Empty;
        string target = TargetInput.Text ?? string.Empty;
        if (source.Length == 0 || target.Length == 0)
        {
            ProblemText.Text = "Choose the disk image to convert and the new file.";
            return false;
        }

        try
        {
            if (_sourceIsVhd)
            {
                DiskImageConverter.FixedVhdToRaw(source, target);
            }
            else
            {
                if (!TryReadGeometry(out DiskGeometry g))
                {
                    ProblemText.Text = "Fill in cylinders (1-65535), heads (1-255) and sectors per track (1-255).";
                    return false;
                }
                DiskImageConverter.RawToFixedVhd(source, target, g);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            ProblemText.Text = ex.Message;
            return false;
        }

        CreatedPath = target;
        return true;
    }

    private bool TryReadGeometry(out DiskGeometry geometry)
    {
        geometry = default;
        if (!int.TryParse(CylindersInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) || c < 1 || c > 65535) return false;
        if (!int.TryParse(HeadsInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) || h < 1 || h > 255) return false;
        if (!int.TryParse(SectorsInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) || s < 1 || s > 255) return false;
        geometry = new DiskGeometry(c, h, s);
        return true;
    }

    private void OnSourceLostFocus(object? sender, RoutedEventArgs e)
    {
        string text = SourceInput.Text ?? string.Empty;
        if (text.Length > 0)
        {
            LoadSource(text);
        }
    }

    private async void OnBrowseSourceClick(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Disk image to convert",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PC hard-disk images") { Patterns = new[] { "*.img", "*.hdd", "*.vhd" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            }
        });
        if (files.Count > 0)
        {
            string? path = files[0].TryGetLocalPath();
            if (path != null)
            {
                LoadSource(path);
            }
        }
    }

    private async void OnBrowseTargetClick(object? sender, RoutedEventArgs e)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "New disk image",
            SuggestedFileName = Path.GetFileName(TargetInput.Text ?? string.Empty),
            DefaultExtension = _sourceIsVhd ? "img" : "vhd",
            ShowOverwritePrompt = false
        });
        string? path = file?.TryGetLocalPath();
        if (path != null)
        {
            TargetInput.Text = path;
        }
    }

    private void OnConvertClick(object? sender, RoutedEventArgs e)
    {
        if (TryConvert())
        {
            Close(true);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
