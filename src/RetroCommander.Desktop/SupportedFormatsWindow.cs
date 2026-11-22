//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using RetroCommander.Core.Services;

namespace RetroCommander.Desktop
{
    /// <summary>
    /// Help, "Supported formats": every disk format the registered drivers handle, in a table
    /// that can be sorted by any column and filtered with a search box.
    /// </summary>
    /// <remarks>
    /// The rows come from <see cref="SupportedFormats.List"/>, which reads the drivers, so a new
    /// RetroIO format appears here with no change to this window. The DataGrid theme is included
    /// once for the whole app in App.axaml; without it a DataGrid draws nothing.
    /// </remarks>
    public sealed class SupportedFormatsWindow : Window
    {
        private readonly SupportedFormat[] _all;
        private readonly ObservableCollection<SupportedFormat> _shown = new ObservableCollection<SupportedFormat>();
        private readonly TextBlock _count;

        // Reads one yes/no fact from a row, for a tick column.
        private delegate bool FormatFlag(SupportedFormat format);

        /// <summary>
        /// Initializes a new instance of the <see cref="SupportedFormatsWindow"/> class and fills
        /// it from the drivers.
        /// </summary>
        public SupportedFormatsWindow()
        {
            Title = "Supported formats";
            Width = 980;
            Height = 560;
            MinWidth = 520;
            MinHeight = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _all = SupportedFormats.List();

            TextBox search = new TextBox
            {
                Name = "SearchBox",
                Watermark = "Search by name, machine or extension, for example tiki or .dsk"
            };
            search.TextChanged += (_, _) => Filter(search.Text);

            _count = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            _count.Classes.Add("fluent-text-secondary");

            DataGrid grid = new DataGrid
            {
                Name = "FormatsGrid",
                ItemsSource = _shown,
                IsReadOnly = true,
                CanUserSortColumns = true,
                CanUserResizeColumns = true,
                AutoGenerateColumns = false,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            };
            grid.Columns.Add(TextColumn("Format", nameof(SupportedFormat.Description), new DataGridLength(2, DataGridLengthUnitType.Star)));
            grid.Columns.Add(TextColumn("Machines", nameof(SupportedFormat.Machines), new DataGridLength(2, DataGridLengthUnitType.Star)));
            grid.Columns.Add(TextColumn("Extensions", nameof(SupportedFormat.Extensions), new DataGridLength(1, DataGridLengthUnitType.Star)));
            grid.Columns.Add(TickColumn("Write", nameof(SupportedFormat.CanWrite), static f => f.CanWrite));
            grid.Columns.Add(TickColumn("New disk", nameof(SupportedFormat.CanCreate), static f => f.CanCreate));

            TextBlock note = new TextBlock
            {
                Text = "Every format listed can be read. \"New disk\" means File > New Image can create a blank one.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            note.Classes.Add("fluent-text-secondary");

            Button close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
            close.Classes.Add("dialog-primary");
            close.Click += (_, _) => Close();

            DockPanel bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(close, Dock.Right);
            bottom.Children.Add(close);
            bottom.Children.Add(_count);

            DockPanel root = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(search, Dock.Top);
            DockPanel.SetDock(note, Dock.Top);
            DockPanel.SetDock(bottom, Dock.Bottom);
            note.Margin = new Thickness(0, 6, 0, 6);
            root.Children.Add(search);
            root.Children.Add(note);
            root.Children.Add(bottom);
            root.Children.Add(grid);
            Content = root;

            Filter(null);
        }

        /// <summary>
        /// Gets the rows shown now, after the search filter; for tests.
        /// </summary>
        internal ObservableCollection<SupportedFormat> ShownFormats => _shown;

        /// <summary>
        /// Shows only the formats that match a search text.
        /// </summary>
        /// <param name="text">
        /// The search text; empty shows every format.
        /// </param>
        internal void Filter(string? text)
        {
            _shown.Clear();
            for (int i = 0; i < _all.Length; i++)
            {
                if (SupportedFormats.Matches(_all[i], text))
                {
                    _shown.Add(_all[i]);
                }
            }
            _count.Text = _shown.Count + " of " + _all.Length + " formats";
        }

        private static DataGridTextColumn TextColumn(string header, string property, DataGridLength width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(property),
                Width = width
            };
        }

        // A centred tick, or nothing, for a yes/no column. SortMemberPath keeps it sortable.
        private static DataGridTemplateColumn TickColumn(string header, string property, FormatFlag flag)
        {
            return new DataGridTemplateColumn
            {
                Header = header,
                SortMemberPath = property,
                Width = new DataGridLength(80),
                CellTemplate = new FuncDataTemplate<SupportedFormat>((f, _) => new TextBlock
                {
                    Text = f != null && flag(f) ? "✓" : string.Empty,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                })
            };
        }
    }
}
