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
using Avalonia.Platform.Storage;
using RetroIO.Storage.Local;
using RetroIO.Storage.Ndfs.Creation;

namespace RetroCommander.Desktop;

/// <summary>
/// One window for creating a disk image in ANY supported format. Replaces the old three-way split
/// (an F2 format-chooser popup in front of two separate dialogs, plus a wholly separate
/// "New Commodore Disk" menu item).
///
/// The layout follows the standard "New Project" pattern: pick a format on the left, configure it
/// on the right. Format choice is mutually exclusive and destructive (switching invalidates the
/// options below it), which is exactly why a left-hand list is used rather than tabs — tabs imply
/// parallel contexts you can fill in simultaneously, and they grow horizontally with every format
/// added. The common spine (name, output path, preview, create) is shared; only the middle region
/// swaps.
/// </summary>
public partial class NewImageDialog : Window
{
    /// <summary>Set when an image was created; the full path to it.</summary>
    public string? CreatedImagePath { get; private set; }

    /// <summary>True if the user asked for the new image to be mounted.</summary>
    public bool ShouldMount { get; private set; }

    // The format currently selected in the left-hand list.
    private string _format = "NDFS";

    // Guards the SelectionChanged/TextChanged handlers while we populate controls programmatically.
    private bool _loading = true;

    // NDFS drive templates, in the order they appear in the combo.
    private static readonly NdfsImageTemplate[] NdfsTemplates =
    {
        NdfsImageTemplate.Floppy360KB,
        NdfsImageTemplate.Floppy12MB,
        NdfsImageTemplate.Smd75MB,
        NdfsImageTemplate.Winchester74MB,
        NdfsImageTemplate.Scsi126MB,
        NdfsImageTemplate.Custom
    };

    private static readonly NdfsBootLoaderType[] NdfsBootLoaders =
    {
        NdfsBootLoaderType.None,
        NdfsBootLoaderType.Floppy,
        NdfsBootLoaderType.Smd,
        NdfsBootLoaderType.Winchester,
        NdfsBootLoaderType.Scsi
    };

    public NewImageDialog()
    {
        InitializeComponent();
        PopulateNdfs();
        PopulatePc();

        NdfsList.SelectedIndex = 0;   // default to NDFS
        _loading = false;

        SelectFormat("NDFS");
    }

    // ---------------------------------------------------------------- population

    private void PopulateNdfs()
    {
        var items = new List<ComboBoxItem>();
        for (int i = 0; i < NdfsTemplates.Length; i++)
            items.Add(new ComboBoxItem { Content = DescribeNdfsTemplate(NdfsTemplates[i]) });

        NdfsTemplateCombo.ItemsSource = items;
        NdfsTemplateCombo.SelectedIndex = 2; // SMD 75MB

        var boots = new List<ComboBoxItem>();
        for (int i = 0; i < NdfsBootLoaders.Length; i++)
            boots.Add(new ComboBoxItem { Content = NdfsBootLoaders[i].ToString() });

        NdfsBootCombo.ItemsSource = boots;
        NdfsBootCombo.SelectedIndex = 0; // None — a valid NDFS volume need not be bootable
    }

    /// <summary>
    /// Human-readable drive description. Device/capacity/spare are the REAL measured geometries —
    /// every ND drive reserves a bad-sector spare region, so the device is always larger than the
    /// usable capacity.
    /// </summary>
    private static string DescribeNdfsTemplate(NdfsImageTemplate t)
    {
        return t switch
        {
            NdfsImageTemplate.Floppy360KB => "Floppy — SINTRAN format 0 (154 pages, 308 KB)",
            NdfsImageTemplate.Floppy12MB => "Floppy — SINTRAN format 17₈ (616 pages, 1.2 MB)",
            NdfsImageTemplate.Smd75MB => "SMD 75 MB — DISC-75MB (36,945 usable of 38,400 pages)",
            NdfsImageTemplate.Winchester74MB => "Winchester 74 MB — Micropolis 1325, ST-506/MFM (36,396 of 36,864)",
            NdfsImageTemplate.Scsi126MB => "SCSI 126 MB — ND-3201 (61,036 usable of 64,656 pages)",
            NdfsImageTemplate.Custom => "Custom size...",
            _ => t.ToString()
        };
    }

    private void PopulatePc()
    {
        PcMediaCombo.SelectedIndex = 0; // Floppy
        RebuildPcGeometry();
    }

    private void RebuildPcGeometry()
    {
        bool floppy = PcMediaCombo.SelectedIndex == 0;
        var items = new List<ComboBoxItem>();

        if (floppy)
        {
            items.Add(new ComboBoxItem { Content = "360 KB — 5.25\" DD (40/2/9)" });
            items.Add(new ComboBoxItem { Content = "720 KB — 3.5\" DD (80/2/9)" });
            items.Add(new ComboBoxItem { Content = "1.2 MB — 5.25\" HD (80/2/15)" });
            items.Add(new ComboBoxItem { Content = "1.44 MB — 3.5\" HD (80/2/18)" });
            items.Add(new ComboBoxItem { Content = "2.88 MB — 3.5\" ED (80/2/36)" });
        }
        else
        {
            items.Add(new ComboBoxItem { Content = "5 MB — XT type 0 (306/2/17)" });
            items.Add(new ComboBoxItem { Content = "10 MB — XT, ST-412 (306/4/17)" });
            items.Add(new ComboBoxItem { Content = "15 MB — XT type 2 (306/6/17)" });
            items.Add(new ComboBoxItem { Content = "26 MB — XT type 1 (375/8/17)" });
            items.Add(new ComboBoxItem { Content = "20 MB — AT, ST-225 (615/4/17)" });
            items.Add(new ComboBoxItem { Content = "30 MB — AT type 3 (615/6/17)" });
            items.Add(new ComboBoxItem { Content = "47 MB — AT type 5 (940/6/17)" });
            items.Add(new ComboBoxItem { Content = "62 MB — AT type 4 (940/8/17)" });
        }

        PcGeometryCombo.ItemsSource = items;
        PcGeometryCombo.SelectedIndex = floppy ? 3 : 4; // 1.44MB / AT 20MB
    }

    // ---------------------------------------------------------------- format switching

    private void OnFormatSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || sender is not ListBox list || list.SelectedItem is not ListBoxItem item)
            return;

        // Single logical selection across the three grouped lists — clear the others.
        _loading = true;
        if (!ReferenceEquals(list, NdfsList)) NdfsList.SelectedItem = null;
        if (!ReferenceEquals(list, PcList)) PcList.SelectedItem = null;
        if (!ReferenceEquals(list, CbmList)) CbmList.SelectedItem = null;
        _loading = false;

        SelectFormat(item.Tag?.ToString() ?? "NDFS");
    }

    private void SelectFormat(string format)
    {
        _format = format;

        bool ndfs = format == "NDFS";
        bool pc = format == "PC";
        bool cbm = !ndfs && !pc;

        NdfsPanel.IsVisible = ndfs;
        PcPanel.IsVisible = pc;
        CbmPanel.IsVisible = cbm;

        FormatTitle.Text = format switch
        {
            "NDFS" => "NDFS Disk (Norsk Data)",
            "PC" => "PC Disk (FAT12 / FAT16)",
            "D64" => "Commodore D64 — 1541",
            "D71" => "Commodore D71 — 1571",
            "D81" => "Commodore D81 — 1581",
            _ => format
        };

        UpdatePreviewAndValidation();
    }

    // ---------------------------------------------------------------- change handlers

    private void OnNdfsOptionChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;

        bool custom = SelectedNdfsTemplate() == NdfsImageTemplate.Custom;
        NdfsCustomPages.IsVisible = custom;

        bool blank = NdfsBlankCheck.IsChecked == true;
        NdfsBlankNote.IsVisible = blank;

        // A blank image carries no metadata at all, so every metadata field is meaningless.
        NdfsName.IsEnabled = !blank;
        NdfsBootCombo.IsEnabled = !blank;
        NdfsExtInfoCheck.IsEnabled = !blank;
        NdfsExtFields.IsEnabled = !blank && NdfsExtInfoCheck.IsChecked == true;

        UpdatePreviewAndValidation();
    }

    private void OnNdfsOptionChanged(object? sender, SelectionChangedEventArgs e) => OnNdfsOptionChanged(sender, (RoutedEventArgs)null!);
    private void OnNdfsTextChanged(object? sender, TextChangedEventArgs e) => UpdatePreviewAndValidation();

    private void OnPcOptionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, PcMediaCombo))
        {
            _loading = true;
            RebuildPcGeometry();
            _loading = false;
        }
        UpdatePreviewAndValidation();
    }

    private void OnPcTextChanged(object? sender, TextChangedEventArgs e) => UpdatePreviewAndValidation();
    private void OnCbmTextChanged(object? sender, TextChangedEventArgs e) => UpdatePreviewAndValidation();
    private void OnOutputChanged(object? sender, TextChangedEventArgs e) => UpdatePreviewAndValidation();

    private NdfsImageTemplate SelectedNdfsTemplate()
    {
        int i = NdfsTemplateCombo.SelectedIndex;
        return (i >= 0 && i < NdfsTemplates.Length) ? NdfsTemplates[i] : NdfsImageTemplate.Smd75MB;
    }

    // ---------------------------------------------------------------- preview + validation

    /// <summary>
    /// Rebuilds the preview line and the validation message. Per-format name rules differ, so they
    /// are checked inline here rather than blowing up on Create.
    /// </summary>
    private void UpdatePreviewAndValidation()
    {
        if (_loading) return;

        string preview;
        string? error = null;

        if (_format == "NDFS")
        {
            preview = PreviewNdfs(out error);
        }
        else if (_format == "PC")
        {
            preview = PreviewPc(out error);
        }
        else
        {
            preview = PreviewCbm(out error);
        }

        PreviewText.Text = preview;

        if (error == null && string.IsNullOrWhiteSpace(OutputPath.Text))
            error = "Choose an output file.";

        ValidationText.Text = error ?? string.Empty;
        ValidationText.IsVisible = error != null;
        CreateButton.IsEnabled = error == null;
    }

    private string PreviewNdfs(out string? error)
    {
        error = null;

        NdfsImageTemplate template = SelectedNdfsTemplate();
        uint capacity, device;

        if (template == NdfsImageTemplate.Custom)
        {
            if (!uint.TryParse((NdfsCustomPages.Text ?? string.Empty).Trim(), out capacity) || capacity < 8)
            {
                error = "Enter a custom page count (at least 8 pages).";
                return "—";
            }
            // Custom sizes have no real drive behind them, so the spare region is only an estimate.
            device = capacity;
        }
        else
        {
            (capacity, device) = NdfsGeometry(template);
        }

        if (NdfsBlankCheck.IsChecked != true)
        {
            string name = (NdfsName.Text ?? string.Empty).Trim();
            if (name.Length == 0)
                error = "Enter a directory name.";
            else if (name.Length > 16)
                error = "Directory name must be 16 characters or fewer.";
            else
            {
                for (int i = 0; i < name.Length; i++)
                {
                    if (name[i] < 0x20 || name[i] > 0x7E)
                    {
                        error = "Directory name must be printable ASCII.";
                        break;
                    }
                }
            }

            if (error == null && NdfsExtInfoCheck.IsChecked == true)
            {
                if (!ushort.TryParse((NdfsSystemNumber.Text ?? "0").Trim(), out _))
                    error = "System number must be a number (0-65535).";
                else if (!ushort.TryParse((NdfsFlagWord.Text ?? "0").Trim(), out _))
                    error = "Flag word must be a number (0-65535).";
            }
        }

        long bytes = (long)device * 2048;
        uint spare = device > capacity ? device - capacity : 0;

        string s = $"{device:N0} device pages × 2048 B = {bytes / (1024.0 * 1024.0):F1} MB";
        if (spare > 0)
            s += $"\n{capacity:N0} usable pages · {spare:N0} pages reserved for bad-sector remapping";
        else
            s += $"\n{capacity:N0} usable pages";

        if (NdfsBlankCheck.IsChecked == true)
            s += "\nBlank: all 0x00 — no boot sector, no directory, not mountable.";

        return s;
    }

    /// <summary>Real measured drive geometry: (usable capacity, physical device) in pages.</summary>
    private static (uint capacity, uint device) NdfsGeometry(NdfsImageTemplate t)
    {
        return t switch
        {
            NdfsImageTemplate.Floppy360KB => (154u, 154u),
            NdfsImageTemplate.Floppy12MB => (616u, 616u),
            NdfsImageTemplate.Smd75MB => (36945u, 38400u),
            NdfsImageTemplate.Winchester74MB => (36396u, 36864u),
            NdfsImageTemplate.Scsi126MB => (61036u, 64656u),
            _ => (0u, 0u)
        };
    }

    private string PreviewPc(out string? error)
    {
        error = null;

        string label = (PcLabel.Text ?? string.Empty).Trim();
        if (label.Length > 11)
            error = "FAT volume labels are limited to 11 characters.";

        bool floppy = PcMediaCombo.SelectedIndex == 0;
        int idx = PcGeometryCombo.SelectedIndex;
        if (idx < 0)
        {
            error ??= "Choose a geometry.";
            return "—";
        }

        var item = (PcGeometryCombo.ItemsSource as List<ComboBoxItem>)?[idx];
        string desc = item?.Content?.ToString() ?? "";

        return floppy
            ? $"FAT12 floppy — {desc}"
            : $"FAT16 hard disk — {desc}";
    }

    private string PreviewCbm(out string? error)
    {
        error = null;

        string name = (CbmName.Text ?? string.Empty).Trim();
        string id = (CbmId.Text ?? string.Empty).Trim();

        if (name.Length == 0)
            error = "Enter a disk name.";
        else if (name.Length > 16)
            error = "Commodore disk names are limited to 16 characters.";
        else if (id.Length != 2)
            error = "Disk ID must be exactly 2 characters.";

        return _format switch
        {
            "D64" => "D64 — 1541, 35 tracks, 174,848 bytes",
            "D71" => "D71 — 1571, 70 tracks, 349,696 bytes",
            "D81" => "D81 — 1581, 80 tracks, 819,200 bytes",
            _ => "—"
        };
    }

    // ---------------------------------------------------------------- browse / create

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        string ext = _format switch
        {
            "D64" => "d64",
            "D71" => "d71",
            "D81" => "d81",
            _ => "img"
        };

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Disk Image",
            DefaultExtension = ext,
            SuggestedFileName = $"new.{ext}",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Disk Images")
                {
                    Patterns = new[] { "*.img", "*.d64", "*.d71", "*.d81", "*.ndfs" }
                },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (file != null)
            OutputPath.Text = file.Path.LocalPath;
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        string path = (OutputPath.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(path))
            return;

        CreateButton.IsEnabled = false;
        try
        {
            // Snapshot EVERY control value HERE, on the UI thread. The actual file creation runs on
            // a background thread (images can be 100+ MB), and Avalonia controls may only be touched
            // from the UI thread — reading a TextBox/ComboBox inside the Task.Run body throws
            // "Call from invalid thread". So the worker gets plain data, never a control.
            NewImageRequest request = BuildRequest(path);

            await Task.Run(() => Execute(request));

            CreatedImagePath = path;
            ShouldMount = MountAfterCreate.IsChecked == true;
            Close(true);
        }
        catch (Exception ex)
        {
            ValidationText.Text = $"Create failed: {ex.Message}";
            ValidationText.IsVisible = true;
            CreateButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// A pure, control-free description of the image to create. Built on the UI thread; consumed on
    /// a background thread. Also the seam the headless UI tests drive.
    /// </summary>
    internal sealed class NewImageRequest
    {
        public string Format = "NDFS";
        public string OutputPath = string.Empty;

        // NDFS
        public NdfsImageTemplate NdfsTemplate;
        public uint NdfsCustomPages;
        public string NdfsDirectoryName = string.Empty;
        public NdfsBootLoaderType NdfsBoot;
        public bool NdfsIncludeExtInfo;
        public bool NdfsBlank;
        public ushort NdfsSystemNumber;
        public ushort NdfsFlagWord;

        // PC
        public bool PcIsFloppy;
        public PcFloppySize PcFloppy;
        public PcHddPreset PcHdd;
        public string PcVolumeLabel = string.Empty;

        // Commodore
        public CbmDiskType CbmType;
        public string CbmDiskName = string.Empty;
        public string CbmDiskId = string.Empty;
    }

    /// <summary>Reads the controls (UI thread only) into a plain request object.</summary>
    internal NewImageRequest BuildRequest(string path)
    {
        var r = new NewImageRequest { Format = _format, OutputPath = path };

        if (_format == "NDFS")
        {
            r.NdfsTemplate = SelectedNdfsTemplate();
            r.NdfsBlank = NdfsBlankCheck.IsChecked == true;
            r.NdfsDirectoryName = r.NdfsBlank ? "EMPTY" : (NdfsName.Text ?? "EMPTY").Trim();
            r.NdfsIncludeExtInfo = NdfsExtInfoCheck.IsChecked == true;

            if (r.NdfsTemplate == NdfsImageTemplate.Custom)
                uint.TryParse((NdfsCustomPages.Text ?? "0").Trim(), out r.NdfsCustomPages);

            int bootIdx = NdfsBootCombo.SelectedIndex;
            r.NdfsBoot = (bootIdx >= 0 && bootIdx < NdfsBootLoaders.Length)
                ? NdfsBootLoaders[bootIdx]
                : NdfsBootLoaderType.None;

            if (r.NdfsIncludeExtInfo && !r.NdfsBlank)
            {
                ushort.TryParse((NdfsSystemNumber.Text ?? "0").Trim(), out r.NdfsSystemNumber);
                ushort.TryParse((NdfsFlagWord.Text ?? "0").Trim(), out r.NdfsFlagWord);
            }
        }
        else if (_format == "PC")
        {
            r.PcVolumeLabel = (PcLabel.Text ?? string.Empty).Trim();
            r.PcIsFloppy = PcMediaCombo.SelectedIndex == 0;
            int idx = PcGeometryCombo.SelectedIndex;

            if (r.PcIsFloppy)
            {
                r.PcFloppy = idx switch
                {
                    0 => PcFloppySize.Size360K,
                    1 => PcFloppySize.Size720K,
                    2 => PcFloppySize.Size1_2M,
                    4 => PcFloppySize.Size2_88M,
                    _ => PcFloppySize.Size1_44M
                };
            }
            else
            {
                r.PcHdd = idx switch
                {
                    0 => PcHddPreset.Xt5MB,
                    1 => PcHddPreset.Xt10MB,
                    2 => PcHddPreset.Xt15MB,
                    3 => PcHddPreset.Xt26MB,
                    5 => PcHddPreset.At30MB,
                    6 => PcHddPreset.At47MB,
                    7 => PcHddPreset.At62MB,
                    _ => PcHddPreset.At20MB
                };
            }
        }
        else
        {
            r.CbmType = _format switch
            {
                "D71" => CbmDiskType.D71,
                "D81" => CbmDiskType.D81,
                _ => CbmDiskType.D64
            };
            r.CbmDiskName = (CbmName.Text ?? "BLANK").Trim();
            r.CbmDiskId = (CbmId.Text ?? "01").Trim();
        }

        return r;
    }

    /// <summary>
    /// Creates the image. PURE — touches no UI, so it is safe on a background thread and directly
    /// unit-testable.
    /// </summary>
    internal static void Execute(NewImageRequest r)
    {
        if (r.Format == "NDFS")
        {
            var options = new NdfsImageCreationOptions
            {
                Template = r.NdfsTemplate,
                DirectoryName = r.NdfsDirectoryName,
                IncludeExtendedInfo = r.NdfsIncludeExtInfo,
                CreateBlankImage = r.NdfsBlank,
                BootLoaderType = r.NdfsBoot,
                CustomNdfsPages = r.NdfsCustomPages,
                SystemNumber = r.NdfsSystemNumber,
                FlagWord = r.NdfsFlagWord
            };

            NdfsImageCreator.CreateImage(r.OutputPath, options);
        }
        else if (r.Format == "PC")
        {
            if (r.PcIsFloppy)
                PcImageFactory.CreateBlankFloppyFile(r.PcFloppy, r.OutputPath, r.PcVolumeLabel);
            else
                PcImageFactory.CreateBlankHardDiskFile(r.PcHdd, r.OutputPath, r.PcVolumeLabel);
        }
        else
        {
            CbmImageFactory.CreateBlankFile(r.CbmType, r.OutputPath, r.CbmDiskName, r.CbmDiskId);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
