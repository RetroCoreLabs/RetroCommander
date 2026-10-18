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
using Avalonia.Media;
using RetroCommander.Desktop.Themes;

namespace RetroCommander.Desktop;

/// <summary>
/// Read-only viewer for a file's contents, shown either as text or as a hex dump.
/// </summary>
/// <remarks>
/// Text mode can wrap lines and clear bit 7 of every byte before showing it. Data that looks binary
/// (see <see cref="IsBinaryData"/>) opens in hex mode.
/// </remarks>
public partial class FileViewerDialog : Window
{
    private byte[] _fileData = Array.Empty<byte>();
    private string _fileName = string.Empty;
    private bool _isHexMode;
    private bool _clearBit7;

    // Maximum file size to load (10 MB)
    private const int MaxFileSize = 10 * 1024 * 1024;

    /// <summary>
    /// Creates an empty viewer with the viewer font from the user settings.
    /// </summary>
    public FileViewerDialog()
    {
        InitializeComponent();
        ApplyViewerFont();
    }

    // Applies the user-configured monospace viewer font (family + size) from UiSettings.
    private void ApplyViewerFont()
    {
        UiSettings s = UiSettings.Instance;
        ContentViewer.FontFamily = new FontFamily(s.ViewerFontFamily);
        ContentViewer.FontSize = s.ViewerFontSize;
    }

    /// <summary>
    /// Creates the viewer for one file and shows its contents, in hex mode if the data looks binary.
    /// </summary>
    /// <param name="fileName">
    /// The file name shown in the title and the header.
    /// </param>
    /// <param name="data">
    /// The file contents to show.
    /// </param>
    public FileViewerDialog(string fileName, byte[] data) : this()
    {
        _fileName = fileName;
        _fileData = data;

        Title = $"File Viewer - {fileName}";
        FileNameText.Text = fileName;
        FileSizeText.Text = FormatFileSize(data.Length);

        // Auto-detect binary data and default to hex mode
        if (IsBinaryData(data))
        {
            _isHexMode = true;
            HexModeRadio.IsChecked = true;
            TextModeRadio.IsChecked = false;
        }

        UpdateContent();
    }

    /// <summary>
    /// Creates a viewer for one file. Same as calling the constructor that takes a file name and data.
    /// </summary>
    /// <param name="fileName">
    /// The file name shown in the title and the header.
    /// </param>
    /// <param name="data">
    /// The file contents to show.
    /// </param>
    /// <returns>
    /// The new viewer, not yet shown.
    /// </returns>
    public static FileViewerDialog CreateForFile(string fileName, byte[] data)
    {
        return new FileViewerDialog(fileName, data);
    }

    private void UpdateContent()
    {
        if (_isHexMode)
        {
            UpdateHexView();
        }
        else
        {
            UpdateTextView();
        }
    }

    private void UpdateTextView()
    {
        TextOptionsPanel.IsVisible = true;
        ContentViewer.TextWrapping = WordWrapCheckBox.IsChecked == true
            ? Avalonia.Media.TextWrapping.Wrap
            : Avalonia.Media.TextWrapping.NoWrap;

        byte[] displayData = _fileData;

        if (_clearBit7)
        {
            // Create a copy with bit 7 cleared
            displayData = new byte[_fileData.Length];
            for (int i = 0; i < _fileData.Length; i++)
            {
                displayData[i] = (byte)(_fileData[i] & 0x7F);
            }
            EncodingText.Text = "ASCII (Bit 7 cleared)";
        }
        else
        {
            EncodingText.Text = DetectEncoding(displayData);
        }

        // Convert to string, handling control characters
        var sb = new StringBuilder();
        for (int i = 0; i < displayData.Length; i++)
        {
            byte b = displayData[i];

            // Handle common control characters
            if (b == 0x0D) // CR
            {
                // Skip CR if followed by LF (Windows line ending)
                if (i + 1 < displayData.Length && displayData[i + 1] == 0x0A)
                {
                    continue;
                }
                sb.Append('\n');
            }
            else if (b == 0x0A) // LF
            {
                sb.Append('\n');
            }
            else if (b == 0x09) // Tab
            {
                sb.Append('\t');
            }
            else if (b >= 0x20 && b < 0x7F) // Printable ASCII
            {
                sb.Append((char)b);
            }
            else if (b >= 0x80) // High-bit characters
            {
                // Display as-is for extended ASCII
                sb.Append((char)b);
            }
            else // Other control characters
            {
                sb.Append('\u00B7'); // Middle dot for non-printable
            }
        }

        ContentViewer.Text = sb.ToString();
        PositionText.Text = $"{_fileData.Length:N0} bytes";
    }

    private void UpdateHexView()
    {
        TextOptionsPanel.IsVisible = false;
        ContentViewer.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
        EncodingText.Text = "Hex dump";

        var sb = new StringBuilder();
        const int bytesPerLine = 16;
        int lineCount = (_fileData.Length + bytesPerLine - 1) / bytesPerLine;

        // Header
        sb.AppendLine("Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII");
        sb.AppendLine("--------  -----------------------  -----------------------  ----------------");

        for (int line = 0; line < lineCount; line++)
        {
            int offset = line * bytesPerLine;

            // Offset
            sb.Append($"{offset:X8}  ");

            // Hex bytes (first 8)
            for (int i = 0; i < 8; i++)
            {
                int idx = offset + i;
                if (idx < _fileData.Length)
                {
                    sb.Append($"{_fileData[idx]:X2} ");
                }
                else
                {
                    sb.Append("   ");
                }
            }

            sb.Append(' '); // Gap between groups

            // Hex bytes (second 8)
            for (int i = 8; i < 16; i++)
            {
                int idx = offset + i;
                if (idx < _fileData.Length)
                {
                    sb.Append($"{_fileData[idx]:X2} ");
                }
                else
                {
                    sb.Append("   ");
                }
            }

            sb.Append(' '); // Gap before ASCII

            // ASCII representation
            for (int i = 0; i < bytesPerLine; i++)
            {
                int idx = offset + i;
                if (idx < _fileData.Length)
                {
                    byte b = _fileData[idx];
                    if (b >= 0x20 && b < 0x7F)
                    {
                        sb.Append((char)b);
                    }
                    else
                    {
                        sb.Append('.');
                    }
                }
            }

            sb.AppendLine();
        }

        ContentViewer.Text = sb.ToString();
        PositionText.Text = $"{_fileData.Length:N0} bytes, {lineCount:N0} lines";
    }

    // Exposed for OnF4Edit (MainWindow.axaml.cs) to decide whether a file is safe to open in the
    // text editor — shares this heuristic instead of duplicating it.
    /// <summary>
    /// Guesses whether data is binary rather than text by looking at its first 4096 bytes.
    /// </summary>
    /// <param name="data">
    /// The data to check.
    /// </param>
    /// <returns>
    /// True if the checked bytes hold any zero byte, or if more than 10 percent of them are control characters
    /// other than tab, LF and CR; false otherwise, and false for empty data.
    /// </returns>
    public static bool IsBinaryData(byte[] data)
    {
        if (data.Length == 0)
        {
            return false;
        }

        int checkLength = Math.Min(data.Length, 4096);
        int controlCount = 0;
        int nullCount = 0;

        for (int i = 0; i < checkLength; i++)
        {
            byte b = data[i];
            if (b == 0x00)
            {
                nullCount++;
            }
            else if (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D)
            {
                controlCount++;
            }
        }

        // Consider binary if:
        // - Contains null bytes (common in binary files)
        // - More than 10% control characters
        double controlRatio = (double)controlCount / checkLength;
        return nullCount > 0 || controlRatio > 0.1;
    }

    private string DetectEncoding(byte[] data)
    {
        int highBitCount = 0;
        int controlCount = 0;
        int printableCount = 0;

        int checkLength = Math.Min(data.Length, 4096);
        for (int i = 0; i < checkLength; i++)
        {
            byte b = data[i];
            if (b >= 0x80)
            {
                highBitCount++;
            }
            else if (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D)
            {
                controlCount++;
            }
            else if (b >= 0x20 && b < 0x7F)
            {
                printableCount++;
            }
        }

        if (checkLength == 0)
        {
            return "Empty file";
        }

        double highBitRatio = (double)highBitCount / checkLength;
        double controlRatio = (double)controlCount / checkLength;

        if (highBitRatio > 0.3)
        {
            return "Binary / Extended ASCII";
        }
        else if (controlRatio > 0.1)
        {
            return "Binary";
        }
        else if (highBitCount > 0)
        {
            return "Extended ASCII";
        }
        else
        {
            return "ASCII";
        }
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} bytes";
        }
        else if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        else
        {
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }

    private void OnTextModeClick(object? sender, RoutedEventArgs e)
    {
        _isHexMode = false;
        UpdateContent();
    }

    private void OnHexModeClick(object? sender, RoutedEventArgs e)
    {
        _isHexMode = true;
        UpdateContent();
    }

    private void OnClearBit7Changed(object? sender, RoutedEventArgs e)
    {
        _clearBit7 = ClearBit7CheckBox.IsChecked == true;
        if (!_isHexMode)
        {
            UpdateTextView();
        }
    }

    private void OnWordWrapChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isHexMode)
        {
            bool wrap = WordWrapCheckBox.IsChecked == true;
            ContentViewer.TextWrapping = wrap
                ? Avalonia.Media.TextWrapping.Wrap
                : Avalonia.Media.TextWrapping.NoWrap;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
