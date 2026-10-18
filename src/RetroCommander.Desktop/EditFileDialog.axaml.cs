//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RetroCommander.Desktop.Themes;

namespace RetroCommander.Desktop;

/// <summary>
/// Plain-text file editor used by F4 Edit. Uses Latin-1 (ISO-8859-1) rather than UTF-8/ASCII so
/// every byte 0x00-0xFF round-trips through the TextBox exactly (GetBytes(GetString(b)) == b for
/// the full byte range) — anything the user doesn't touch is preserved byte-for-byte on Save.
/// Deliberately text-only: a hex/binary editor is a much larger, riskier feature and out of scope
/// here (see F3 View's separate Hex mode for read-only binary inspection).
/// </summary>
public partial class EditFileDialog : Window
{
    private static readonly Encoding s_roundTripEncoding = Encoding.Latin1;

    public bool Saved { get; private set; }
    public byte[] EditedData { get; private set; } = Array.Empty<byte>();

    public EditFileDialog()
    {
        InitializeComponent();
        ApplyEditorFont();
    }

    private void ApplyEditorFont()
    {
        UiSettings s = UiSettings.Instance;
        ContentEditor.FontFamily = new Avalonia.Media.FontFamily(s.ViewerFontFamily);
        ContentEditor.FontSize = s.ViewerFontSize;
    }

    public EditFileDialog(string fileName, byte[] data) : this()
    {
        Title = $"Edit File - {fileName}";
        FileNameText.Text = fileName;
        FileSizeText.Text = FormatFileSize(data.Length);
        ContentEditor.Text = s_roundTripEncoding.GetString(data);
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} bytes";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        EditedData = s_roundTripEncoding.GetBytes(ContentEditor.Text ?? string.Empty);
        Saved = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Saved = false;
        Close();
    }
}
