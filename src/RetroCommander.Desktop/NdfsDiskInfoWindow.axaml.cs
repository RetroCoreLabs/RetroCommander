//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RetroCommander.Core.Metadata;
using RetroCommander.Core.Services;
using RetroIO.Storage.Ndfs.Elements;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop;

/// <summary>
/// Post-mount NDFS disk information window. Displays boot, bitmap, extended-info and user data
/// from the LIVE mounted filesystem, and offers two write actions that operate on that same live
/// mount: rename the directory (volume) and clear the "entered" interlock. Both require a
/// read-write mount; on a read-only mount the driver's write guards make them no-ops and the
/// window reports that read-write is required.
/// </summary>
public partial class NdfsDiskInfoWindow : Window
{
    private readonly INdfsFileSystem? _ndfs;
    private readonly FileSystemMetadataService? _metadataService;
    private readonly string _imagePath = string.Empty;
    private readonly bool _isReadOnly;

    // Parameterless ctor for the XAML designer only.
    public NdfsDiskInfoWindow()
    {
        InitializeComponent();
    }

    /// <param name="ndfs">The live mounted NDFS filesystem (same mount the panel holds open).</param>
    /// <param name="metadataService">Builds metadata from the filesystem; called directly whenever the view refreshes.</param>
    /// <param name="imagePath">Full path to the physical image backing the mount.</param>
    /// <param name="isReadOnly">True if the mount is read-only (write actions will be blocked).</param>
    public NdfsDiskInfoWindow(
        INdfsFileSystem ndfs,
        FileSystemMetadataService metadataService,
        string imagePath,
        bool isReadOnly)
        : this()
    {
        _ndfs = ndfs;
        _metadataService = metadataService;
        _imagePath = imagePath ?? string.Empty;
        _isReadOnly = isReadOnly;

        Populate(BuildMetadata());
        LoadInitialCommands();
    }

    // Shared read-only tooltip/notice text for the initial-command editor.
    private const string INITCMD_READ_ONLY_TIP =
        "Disabled: this volume is mounted read-only. Re-mount it read-write "
        + "(right-click the image → \"Open Read/Write\") to edit the initial commands.";

    /// <summary>
    /// Builds a fresh metadata snapshot by calling the service directly. The window owns everything
    /// it needs (the filesystem, the service, the path) and asks for the data itself.
    /// </summary>
    private NdfsMetadata BuildMetadata()
    {
        if (_ndfs == null || _metadataService == null)
            return new NdfsMetadata();

        NdfsMetadata m = _metadataService.BuildNdfsMetadata(_ndfs);
        m.ImagePath = _imagePath;

        try
        {
            if (!string.IsNullOrEmpty(_imagePath) && File.Exists(_imagePath))
            {
                long len = new FileInfo(_imagePath).Length;
                m.FileSizeBytes = len;
                m.FileSizeFormatted = $"{len:N0} bytes ({len / (1024.0 * 1024.0):F2} MB)";
            }
        }
        catch
        {
            // File size is cosmetic — never fail the info window over it.
        }

        return m;
    }

    private void Populate(NdfsMetadata m)
    {
        // Volume
        DirNameText.Text = m.DirectoryName;
        DiskTypeText.Text = m.DiskType;
        ImagePathText.Text = string.IsNullOrEmpty(m.ImagePath) ? "N/A" : m.ImagePath;
        FileSizeText.Text = string.IsNullOrEmpty(m.FileSizeFormatted) ? "N/A" : m.FileSizeFormatted;

        // Boot
        BootFormatText.Text = m.BootFormat;
        BootControllerText.Text = string.IsNullOrEmpty(m.BootControllerType) ? "N/A" : m.BootControllerType;
        BootableText.Text = m.IsBootable ? "Yes ✓" : "No";

        // Allocation
        UsedBlocksText.Text = $"{m.BitmapUsedPages:N0} pages";
        FreeBlocksText.Text = $"{m.BitmapFreePages:N0} pages";
        BitmapCoversText.Text = $"{m.BitmapTotalPages:N0} device pages";
        CeilingText.Text = $"{m.AllocCeilingPages:N0} pages (declared capacity)";
        BitFileSizeText.Text = $"{m.BitFileSizePages:N0} pages";

        // Extended info
        ChecksumText.Text = m.ChecksumStatus;
        // System number 0 = no owning system recorded (kernel treats 0 as "no owner").
        CpuSystemNumberText.Text = m.CpuSystemNumber.HasValue
            ? (m.CpuSystemNumber.Value == 0 ? "0 (none recorded)" : m.CpuSystemNumber.Value.ToString())
            : "N/A";
        // Honours the kernel rule that system number 0 means "no owner recorded" — never claim
        // the volume was "entered by system 0".
        EnteredText.Text = m.EnteredDescription;
        FlagWordText.Text = $"0x{m.FlagWord:X4}";

        // Visual allocation map
        RenderAllocationMap(m);

        // Users
        UsersList.ItemsSource = m.Users;
        NoUsersText.IsVisible = m.Users.Count == 0;

        // Read-only notice + action availability. Every write action (rename, clear-entered) is
        // greyed out on a read-only mount — the driver's write guards would refuse them anyway, so
        // disabling them up front is honest rather than letting the user click into a failure.
        const string READ_ONLY_TIP = "Disabled: this volume is mounted read-only. Re-mount it "
                                     + "read-write (right-click the image → \"Open Read/Write\") to enable this.";

        ReadOnlyNotice.IsVisible = _isReadOnly;

        RenameTextBox.Text = m.DirectoryName;
        RenameButton.IsEnabled = !_isReadOnly;
        RenameTextBox.IsEnabled = !_isReadOnly;
        ToolTip.SetTip(RenameButton, _isReadOnly ? READ_ONLY_TIP : "Rename the NDFS directory (volume).");

        // Clear-entered row only makes sense when the volume is actually entered.
        ClearEnteredRow.IsVisible = m.IsEntered;
        if (m.IsEntered)
        {
            bool hasOwner = m.EnteredBySystemNumber.HasValue && m.EnteredBySystemNumber.Value != 0;
            EnteredByText.Text = hasOwner
                ? $"🔒 Left entered by system {m.EnteredBySystemNumber!.Value}."
                : "🔒 Left entered, but no owning system was recorded (system number 0).";

            ClearEnteredButton.IsEnabled = !_isReadOnly;
            ToolTip.SetTip(ClearEnteredButton, _isReadOnly
                ? READ_ONLY_TIP
                : "Clear the 'directory entered' interlock (flag-word bit 15) and rewrite the checksum.");
        }
    }

    // Allocation-map pixel colours (BGRA, premultiplied). These are deliberate data-visualisation
    // colours, not chrome — the surrounding container still inherits the live theme.
    private const uint COLOR_USED = 0xFF3A7BD5;      // blue   — page allocated
    private const uint COLOR_FREE = 0xFFFFFFFF;      // white  — page free
    private const uint COLOR_BEYOND = 0xFF7A7A7A;    // grey   — above the capacity ceiling: free but
                                                     //          never allocatable (see BitFile.AllocCeiling)

    /// <summary>
    /// Renders the whole allocation bitmap as an image: ONE PIXEL PER PAGE, laid out left-to-right,
    /// top-to-bottom. Bits are LSB-first within each byte (page N = bit N%8 of byte N/8) — the
    /// verified on-disk order. The grid width is chosen for a roughly 2:1 aspect so the map fits
    /// neatly under the allocation section regardless of volume size.
    /// </summary>
    private void RenderAllocationMap(NdfsMetadata m)
    {
        byte[] data = m.BitmapData;
        uint totalPages = m.BitmapTotalPages;

        if (data == null || data.Length == 0 || totalPages == 0)
        {
            BitmapImage.IsVisible = false;
            BitmapUnavailableText.IsVisible = true;
            return;
        }

        BitmapImage.IsVisible = true;
        BitmapUnavailableText.IsVisible = false;

        // Pick a grid width giving ~2:1 (width:height); clamp so tiny volumes still look sane.
        int width = (int)Math.Ceiling(Math.Sqrt(totalPages * 2.0));
        if (width < 32) width = 32;
        int height = (int)((totalPages + (uint)width - 1) / (uint)width);
        if (height < 1) height = 1;

        // Build the pixel buffer, then blit row-by-row (RowBytes may exceed width*4).
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                long page = (long)y * width + x;
                uint color;

                if (page >= totalPages)
                {
                    color = 0x00000000; // outside the volume — transparent padding
                }
                else
                {
                    int byteIndex = (int)(page >> 3);
                    int bitIndex = (int)(page & 7);
                    bool used = byteIndex < data.Length && ((data[byteIndex] >> bitIndex) & 1) != 0;

                    if (!used && m.AllocCeilingPages > 0 && page >= m.AllocCeilingPages)
                        color = COLOR_BEYOND; // free, but above the declared capacity ceiling
                    else
                        color = used ? COLOR_USED : COLOR_FREE;
                }

                int offset = (y * width + x) * 4;
                pixels[offset + 0] = (byte)(color & 0xFF);          // B
                pixels[offset + 1] = (byte)((color >> 8) & 0xFF);   // G
                pixels[offset + 2] = (byte)((color >> 16) & 0xFF);  // R
                pixels[offset + 3] = (byte)((color >> 24) & 0xFF);  // A
            }
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var fb = bitmap.Lock())
        {
            int srcStride = width * 4;
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * srcStride, fb.Address + (y * fb.RowBytes), srcStride);
            }
        }

        BitmapImage.Source = bitmap;

        // Keep the on-screen box proportional to the pixel grid so pixels stay square.
        BitmapImage.Height = BitmapImage.Width * ((double)height / width);
    }

    private void Refresh(string status)
    {
        Populate(BuildMetadata());
        StatusText.Text = status;
    }

    private void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (_ndfs == null)
            return;

        if (_isReadOnly)
        {
            StatusText.Text = "Cannot rename: volume is mounted read-only. Re-mount read-write.";
            return;
        }

        string newName = (RenameTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newName))
        {
            StatusText.Text = "Enter a new directory name.";
            return;
        }

        try
        {
            bool ok = _ndfs.SetDirectoryName(newName);
            if (ok)
                Refresh($"✓ Renamed volume to '{newName}'.");
            else
                StatusText.Text = "Rename failed: volume is read-only.";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = $"Invalid name: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Rename failed: {ex.Message}";
        }
    }

    private void OnClearEnteredClick(object? sender, RoutedEventArgs e)
    {
        if (_ndfs == null)
            return;

        if (_isReadOnly)
        {
            StatusText.Text = "Cannot clear: volume is mounted read-only. Re-mount read-write.";
            return;
        }

        try
        {
            bool cleared = _ndfs.ClearEnteredDirectory();
            if (cleared)
                Refresh("✓ Cleared the 'entered' interlock.");
            else
                StatusText.Text = "Nothing to clear (not entered, read-only, or no extended info).";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Clear failed: {ex.Message}";
        }
    }

    // ==================== Initial-command buffer tab ====================

    /// <summary>
    /// Loads the SINTRAN initial-command buffer from the live mount into the editor (one command per
    /// line). Shows the "unavailable" panel when the pack has no locatable buffer, and disables saving
    /// on a read-only mount.
    /// </summary>
    private void LoadInitialCommands()
    {
        if (_ndfs == null)
        {
            ShowInitCmdUnavailable("Filesystem not available.");
            return;
        }

        SintranInitialCommandBuffer? buffer;
        try
        {
            buffer = _ndfs.ReadSintranInitialCommands();
        }
        catch (Exception ex)
        {
            ShowInitCmdUnavailable($"Could not read the initial-command buffer: {ex.Message}");
            return;
        }

        if (buffer == null)
        {
            ShowInitCmdUnavailable(
                "No SINTRAN initial-command buffer found. This is not a SINTRAN system pack "
                + "(no SEGFIL0), or the SINTRAN version is unrecognised.");
            return;
        }

        // Populate the editor with one command per line.
        InitCmdUnavailablePanel.IsVisible = false;
        InitCmdEditor.IsVisible = true;

        var sb = new StringBuilder();
        for (int i = 0; i < buffer.Commands.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(buffer.Commands[i]);
        }
        InitCmdEditor.Text = sb.ToString();

        // Provenance line (mirrors the CLI/glass-UI info line).
        InitCmdInfoText.Text =
            $"SINTRAN {buffer.VersionLetter} · {buffer.SegfilPath} · INIBU in segment "
            + $"{Convert.ToString(buffer.SegmentNumber, 8)}₈ · SEGFIL0 byte 0x{buffer.ByteOffset:X} · "
            + $"{buffer.TextLength}/254 text bytes used.";

        // Read-only handling: the editor is view-only and Save is disabled on a read-only mount.
        InitCmdEditor.IsReadOnly = _isReadOnly;
        InitCmdSaveButton.IsEnabled = !_isReadOnly;
        ToolTip.SetTip(InitCmdSaveButton, _isReadOnly
            ? INITCMD_READ_ONLY_TIP
            : "Write the edited commands back to SEGFIL0 in place (only the buffer pages change).");

        InitCmdStatusText.Text = _isReadOnly
            ? "Read-only mount — editing disabled. Re-mount read-write to change these."
            : string.Empty;
    }

    /// <summary>Hides the editor and shows the "no buffer" message with <paramref name="message"/>.</summary>
    private void ShowInitCmdUnavailable(string message)
    {
        InitCmdEditor.IsVisible = false;
        InitCmdUnavailablePanel.IsVisible = true;
        InitCmdUnavailableText.Text = message;
        InitCmdInfoText.Text = string.Empty;
        InitCmdSaveButton.IsEnabled = false;
        InitCmdStatusText.Text = string.Empty;
    }

    private void OnInitCmdReloadClick(object? sender, RoutedEventArgs e)
    {
        LoadInitialCommands();
        if (InitCmdEditor.IsVisible && !_isReadOnly)
            InitCmdStatusText.Text = "Reloaded from disk.";
    }

    private void OnInitCmdSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_ndfs == null)
            return;

        if (_isReadOnly)
        {
            InitCmdStatusText.Text = "Cannot save: volume is mounted read-only. Re-mount read-write.";
            return;
        }

        // Split the editor into commands: one per line, trimmed; blank lines are dropped.
        string raw = (InitCmdEditor.Text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = raw.Split('\n');
        var commands = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].Trim();
            if (t.Length > 0)
                commands.Add(t);
        }

        try
        {
            bool ok = _ndfs.WriteSintranInitialCommands(commands);
            if (!ok)
            {
                InitCmdStatusText.Text = "No initial-command buffer found — nothing written.";
                return;
            }

            // Reload from disk to confirm the round trip, then report success.
            LoadInitialCommands();
            InitCmdStatusText.Text = $"✓ Saved {commands.Count} command(s) to SEGFIL0.";
        }
        catch (ArgumentException ex)
        {
            // Invalid command (empty, embedded quote, non-printable).
            InitCmdStatusText.Text = $"Invalid command: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            // Buffer overflow (>254 bytes) or read-only device.
            InitCmdStatusText.Text = $"Cannot save: {ex.Message}";
        }
        catch (Exception ex)
        {
            InitCmdStatusText.Text = $"Save failed: {ex.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
