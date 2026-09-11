//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RetroCommander.Core.Abstractions;
using RetroIO.Storage.Ndfs.Elements;

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
        public PropertiesDialog()
        {
            InitializeComponent();
        }

        public PropertiesDialog(IFileSystemEntry entry) : this()
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            Title = $"Properties - {entry.Name}";
            NameText.Text = entry.Name;

            PopulateStandardFields(entry);
            PopulateFilesystemDetails(entry);
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

        /// <summary>Turns "ndfs.object_name" into "Object Name" for a readable label.</summary>
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
