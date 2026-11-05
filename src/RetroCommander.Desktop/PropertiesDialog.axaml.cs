//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RetroCommander.Core.Abstractions;
using RetroIO.Storage.Ndfs.Elements;
using RetroIO.Storage.Ndfs.FileSystems;
using RetroIO.Storage.Ndfs.Security;

namespace RetroCommander.Desktop
{
    /// <summary>
    /// Shows metadata for a selected file or directory: standard fields (path, size, dates) plus
    /// every raw property in the entry's IPropertyBag — filesystem-agnostic, so it works for any
    /// panel type (NDFS, Windows, ...), not just a one-off NDFS screen. Opened via right-click
    /// "Properties" on either file panel (see MainWindow.axaml.cs OnPropertiesClick).
    ///
    /// Deliberately separate from the sidebar's ad-hoc mount-properties window
    /// (MainWindow.OnSidebarPropertiesRequested) — that shows mount/URN metadata, a different data
    /// shape (no IFileSystemEntry involved), not a file/directory's properties.
    /// </summary>
    public partial class PropertiesDialog : Window
    {
        // Designer/XAML-loader parameterless constructor only; real use goes through the
        // IFileSystemEntry overload below.
        /// <summary>
        /// Creates an empty dialog with no entry shown. Meant for the XAML designer and loader only.
        /// </summary>
        public PropertiesDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Creates the dialog for one file or directory and fills in its standard fields and every non-null property in its property bag.
        /// </summary>
        /// <param name="entry">
        /// The file or directory to show.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entry"/> is null.
        /// </exception>
        public PropertiesDialog(IFileSystemEntry entry) : this()
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            Title = $"Properties - {entry.Name}";
            NameText.Text = entry.Name;

            PopulateStandardFields(entry);
            PopulateFilesystemDetails(entry);
        }

        /// <summary>
        /// Creates the dialog for a file on a mounted NDFS volume, with the File Access section that
        /// shows and sets its access (SINTRAN <c>@SET-FILE-ACCESS</c>).
        /// </summary>
        /// <param name="entry">
        /// The file or directory to show. The access section appears for a file only.
        /// </param>
        /// <param name="ndfs">
        /// The live NDFS file system the entry belongs to.
        /// </param>
        /// <param name="isReadOnly">
        /// True if the volume is mounted read-only; the access is then shown but cannot be set.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entry"/> or <paramref name="ndfs"/> is null.
        /// </exception>
        public PropertiesDialog(IFileSystemEntry entry, INdfsFileSystem ndfs, bool isReadOnly) : this(entry)
        {
            _ndfs = ndfs ?? throw new ArgumentNullException(nameof(ndfs));
            _ndfsPath = entry.FullPath;
            _isReadOnly = isReadOnly;

            if (entry.IsDirectory)
                return;

            NdfsAccessBorder.IsVisible = true;
            FileAccessBox.IsEnabled = !isReadOnly;
            SetFileAccessButton.IsEnabled = !isReadOnly;
            if (isReadOnly)
                FileAccessStatusText.Text = "This volume is mounted read-only. Re-mount read-write to change the access.";
            ShowFileAccess();
        }

        /// <summary>
        /// Gets whether the file's access was changed while the dialog was open, so the caller
        /// knows to refresh its listing.
        /// </summary>
        public bool AccessChanged { get; private set; }

        private readonly INdfsFileSystem? _ndfs;
        private readonly string _ndfsPath = string.Empty;
        private readonly bool _isReadOnly;

        /// <summary>
        /// Reads the file's access from the live file system and shows it.
        /// </summary>
        private void ShowFileAccess()
        {
            try
            {
                ushort bits = _ndfs!.GetFileAccess(_ndfsPath);
                FileAccessSintranText.Text = SintranAccessForm.FormatAccessTriple(bits);
                FileAccessBitsText.Text = $"0x{bits:X4} ({Convert.ToString(bits, 8).PadLeft(6, '0')}B)";
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is InvalidOperationException)
            {
                FileAccessSintranText.Text = "(unknown)";
                FileAccessBitsText.Text = string.Empty;
                FileAccessStatusText.Text = $"Cannot read the access: {ex.Message}";
                FileAccessBox.IsEnabled = false;
                SetFileAccessButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Applies the PUBLIC,FRIEND,OWN box to the file's access and writes it.
        /// </summary>
        private void OnSetFileAccessClick(object? sender, RoutedEventArgs e)
        {
            if (_ndfs == null || _isReadOnly)
                return;

            try
            {
                ushort before = _ndfs.GetFileAccess(_ndfsPath);
                ushort after = SintranAccessForm.ApplyAccessTriple(before, FileAccessBox.Text ?? string.Empty);
                if (after == before)
                {
                    FileAccessStatusText.Text = "No change: the access is already " + SintranAccessForm.FormatAccessTriple(before) + ".";
                    return;
                }

                _ndfs.SetFileAccess(_ndfsPath, after);
                AccessChanged = true;
                FileAccessBox.Text = string.Empty;
                ShowFileAccess();
                FileAccessStatusText.Text = $"✓ Access {SintranAccessForm.FormatAccessTriple(before)} -> {SintranAccessForm.FormatAccessTriple(after)}.";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is IOException)
            {
                FileAccessStatusText.Text = $"Not changed: {ex.Message}";
            }
        }

        private void PopulateStandardFields(IFileSystemEntry entry)
        {
            int row = 0;
            AddRow(StandardGrid, ref row, "Name:", entry.Name);
            AddRow(StandardGrid, ref row, "Location:", entry.FullPath);
            AddRow(StandardGrid, ref row, "Type:", entry.IsDirectory ? "Directory" : "File");
            if (!entry.IsDirectory)
                AddRow(StandardGrid, ref row, "Size:", $"{entry.Size:N0} bytes");
            AddRow(StandardGrid, ref row, "Created:", FormatDate(entry.CreatedDate));
            AddRow(StandardGrid, ref row, "Modified:", FormatDate(entry.ModifiedDate));
            AddRow(StandardGrid, ref row, "Accessed:", FormatDate(entry.AccessedDate));
        }

        private void PopulateFilesystemDetails(IFileSystemEntry entry)
        {
            var all = entry.Properties.GetAllProperties();

            int row = 0;
            foreach (var kvp in all)
            {
                if (kvp.Value == null)
                    continue;

                AddRow(DetailsGrid, ref row, FormatPropertyKey(kvp.Key) + ":", FormatPropertyValue(kvp.Key, kvp.Value));
            }

            // Nothing to show — hide the whole section instead of an empty "Filesystem Details"
            // header with no rows under it.
            if (row == 0)
            {
                DetailsBorder.IsVisible = false;
            }
        }

        /// <summary>
        /// NDFS ("ndfs."-prefixed) date fields are raw packed SINTRAN/ND-100 timestamps (see
        /// NdDateTime) — decode them for display here rather than showing the opaque uint32. Every
        /// other property is shown as its plain ToString(); this is a display-only decode, the
        /// underlying IPropertyBag/.xat sidecar keeps the raw value so it round-trips losslessly.
        /// </summary>
        private static string FormatPropertyValue(string key, object value)
        {
            if (IsNdfsDateKey(key) && value is uint rawDate)
            {
                var decoded = NdDateTime.FromNdDate(rawDate);
                return decoded.HasValue ? decoded.Value.ToString("yyyy-MM-dd HH:mm:ss") : "(not set)";
            }

            return value.ToString() ?? string.Empty;
        }

        private static bool IsNdfsDateKey(string key)
        {
            return key == "ndfs.date_created" ||
                   key == "ndfs.last_read_date" ||
                   key == "ndfs.last_write_date" ||
                   key == "ndfs.user_date_created" ||
                   key == "ndfs.last_entered";
        }

        /// <summary>
        /// Turns "ndfs.object_name" into "Object Name" for a readable label.
        /// </summary>
        private static string FormatPropertyKey(string key)
        {
            int dot = key.LastIndexOf('.');
            string tail = dot >= 0 ? key.Substring(dot + 1) : key;

            var parts = tail.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private static string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("yyyy-MM-dd HH:mm:ss") : "(not set)";
        }

        private static void AddRow(Grid grid, ref int row, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var labelBlock = new TextBlock
            {
                Text = label,
                Classes = { "fluent-text-secondary" },
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                Classes = { "fluent-text" },
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            row++;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
