//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RetroIO.Storage.Core;

namespace RetroCommander.Desktop;

/// <summary>
/// Filesystem-agnostic disk information window.
///
/// Renders whatever <see cref="VolumeInfo"/> it is handed — titled sections of label/value
/// rows plus an optional allocation strip — without knowing anything about any particular
/// filesystem. A driver gains a disk-info screen by implementing
/// <see cref="IVolumeInfoProvider"/>; no change is needed here.
///
/// Deliberately separate from <see cref="NdfsDiskInfoWindow"/>, which stays as the NDFS
/// screen because it does things this window cannot: editing the volume name, clearing the
/// entered flag, and reading/writing the SINTRAN initial-command buffer. This one is
/// read-only by design.
/// </summary>
public partial class DiskInfoWindow : Window
{
    private VolumeInfo? _info;

    public DiskInfoWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates the window for a mounted driver, using the driver's own volume info when it
    /// provides any and a generic description otherwise.
    /// </summary>
    /// <param name="driver">The mounted driver.</param>
    /// <param name="imagePath">Backing image path, when known.</param>
    /// <param name="isReadOnly">Whether the volume was mounted read-only.</param>
    public DiskInfoWindow(IFileSystemDriver driver, string imagePath, bool isReadOnly) : this()
    {
        _info = VolumeInfoBuilder.Build(driver, imagePath, isReadOnly);
        Render(_info);
    }

    /// <summary>
    /// Creates the window from an already-built description.
    /// </summary>
    public DiskInfoWindow(VolumeInfo info) : this()
    {
        _info = info ?? throw new ArgumentNullException(nameof(info));
        Render(_info);
    }

    /// <summary>
    /// Builds the visual tree for a volume description.
    /// </summary>
    private void Render(VolumeInfo info)
    {
        Title = $"Disk Information — {info.FileSystemType}";

        if (TitleText != null)
        {
            TitleText.Text = string.IsNullOrEmpty(info.VolumeLabel)
                ? info.FileSystemType
                : $"{info.VolumeLabel}  ({info.FileSystemType})";
        }

        if (SubtitleText != null)
        {
            var subtitle = new StringBuilder();
            subtitle.Append(info.IsReadOnly ? "Read-only" : "Read/write");

            if (!string.IsNullOrEmpty(info.ImagePath))
            {
                subtitle.Append("  •  ");
                subtitle.Append(info.ImagePath);
            }

            SubtitleText.Text = subtitle.ToString();
        }

        if (SectionsPanel == null)
        {
            return;
        }

        SectionsPanel.Children.Clear();

        for (int s = 0; s < info.Sections.Count; s++)
        {
            VolumeInfoSection section = info.Sections[s];

            var header = new TextBlock
            {
                Text = string.IsNullOrEmpty(section.Icon)
                    ? section.Title
                    : $"{section.Icon} {section.Title}"
            };
            header.Classes.Add("SectionHeader");
            SectionsPanel.Children.Add(header);

            // One grid per section: label | value | detail.
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("190,Auto,*")
            };

            for (int f = 0; f < section.Fields.Count; f++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (int f = 0; f < section.Fields.Count; f++)
            {
                VolumeInfoField field = section.Fields[f];

                var label = new TextBlock { Text = field.Label + ":" };
                label.Classes.Add("Label");
                Grid.SetRow(label, f);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var value = new TextBlock
                {
                    Text = field.Value,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };
                value.Classes.Add("Value");
                Grid.SetRow(value, f);
                Grid.SetColumn(value, 1);
                grid.Children.Add(value);

                if (!string.IsNullOrEmpty(field.Detail))
                {
                    var detail = new TextBlock
                    {
                        Text = field.Detail,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    };
                    detail.Classes.Add("Detail");
                    Grid.SetRow(detail, f);
                    Grid.SetColumn(detail, 2);
                    grid.Children.Add(detail);
                }
            }

            SectionsPanel.Children.Add(grid);
        }

        if (info.AllocationMap != null)
        {
            RenderAllocationMap(info.AllocationMap);
        }
    }

    // Allocation-map colours, matching NdfsDiskInfoWindow so the two screens read alike.
    // These are data-encoding colours (not chrome), so they are intentionally fixed rather
    // than theme-derived - the legend below names every one of them.
    private const uint COLOR_USED = 0xFF3B82F6;      // blue   - allocated
    private const uint COLOR_FREE = 0xFF2A2E35;      // grey   - free
    private const uint COLOR_BEYOND = 0xFFB45309;    // amber  - beyond usable capacity
    private const uint COLOR_RESERVED = 0xFF8B5CF6;  // violet - filesystem metadata

    /// <summary>
    /// Renders the allocation strip as a one-pixel-per-unit bitmap, plus a counted legend.
    /// </summary>
    private void RenderAllocationMap(VolumeAllocationMap map)
    {
        if (SectionsPanel == null || map.Units.Length == 0)
        {
            return;
        }

        var header = new TextBlock
        {
            Text = $"\U0001F9EE Allocation ({map.Units.Length:N0} {map.UnitName}s" +
                   (map.UnitSizeBytes > 0 ? $" of {map.UnitSizeBytes:N0} bytes" : string.Empty) + ")"
        };
        header.Classes.Add("SectionHeader");
        SectionsPanel.Children.Add(header);

        int free = 0;
        int used = 0;
        int beyond = 0;
        int reserved = 0;

        for (int i = 0; i < map.Units.Length; i++)
        {
            switch (map.Units[i])
            {
                case VolumeAllocationState.Free: free++; break;
                case VolumeAllocationState.Used: used++; break;
                case VolumeAllocationState.Beyond: beyond++; break;
                case VolumeAllocationState.Reserved: reserved++; break;
            }
        }

        // Aim for roughly 2:1 (width:height) so the strip stays readable, with a floor so a
        // tiny volume does not collapse into a single thin line.
        int total = map.Units.Length;
        int width = (int)Math.Ceiling(Math.Sqrt(total * 2.0));
        if (width < 32)
        {
            width = 32;
        }

        int height = (total + width - 1) / width;
        if (height < 1)
        {
            height = 1;
        }

        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int unit = y * width + x;
                uint color;

                if (unit >= total)
                {
                    color = 0x00000000;  // padding outside the volume - transparent
                }
                else
                {
                    color = map.Units[unit] switch
                    {
                        VolumeAllocationState.Used => COLOR_USED,
                        VolumeAllocationState.Beyond => COLOR_BEYOND,
                        VolumeAllocationState.Reserved => COLOR_RESERVED,
                        _ => COLOR_FREE
                    };
                }

                int offset = (y * width + x) * 4;
                pixels[offset + 0] = (byte)(color & 0xFF);           // B
                pixels[offset + 1] = (byte)((color >> 8) & 0xFF);    // G
                pixels[offset + 2] = (byte)((color >> 16) & 0xFF);   // R
                pixels[offset + 3] = (byte)((color >> 24) & 0xFF);   // A
            }
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        // RowBytes may exceed width*4, so blit row by row rather than in one copy.
        using (var fb = bitmap.Lock())
        {
            int srcStride = width * 4;
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * srcStride, fb.Address + (y * fb.RowBytes), srcStride);
            }
        }

        var image = new Image
        {
            Source = bitmap,
            Width = 560,
            Height = 560.0 * height / width,   // keep pixels square
            Stretch = Avalonia.Media.Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(20, 4, 0, 6)
        };

        // Nearest-neighbour: one unit must stay one crisp pixel, not a blurred average.
        Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        SectionsPanel.Children.Add(image);

        // Legend, with counts, so the colours are never ambiguous.
        var legend = new Grid { ColumnDefinitions = new ColumnDefinitions("190,Auto,*") };
        int row = 0;
        row = AddLegendRow(legend, row, "Used", used, COLOR_USED);
        row = AddLegendRow(legend, row, "Free", free, COLOR_FREE);

        if (reserved > 0)
        {
            row = AddLegendRow(legend, row, "Reserved (metadata)", reserved, COLOR_RESERVED);
        }

        if (beyond > 0)
        {
            AddLegendRow(legend, row, "Beyond capacity", beyond, COLOR_BEYOND);
        }

        SectionsPanel.Children.Add(legend);
    }

    /// <summary>
    /// Adds one legend row: colour swatch, name and count. Returns the next row index.
    /// </summary>
    private static int AddLegendRow(Grid grid, int row, string label, int count, uint color)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var swatchAndLabel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Avalonia.Thickness(20, 1, 0, 1)
        };

        swatchAndLabel.Children.Add(new Border
        {
            Width = 11,
            Height = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromUInt32(color))
        });

        var labelText = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Avalonia.Application.Current?.FindResource("FluentTextSecondaryBrush") as Avalonia.Media.IBrush
        };
        swatchAndLabel.Children.Add(labelText);

        Grid.SetRow(swatchAndLabel, row);
        Grid.SetColumn(swatchAndLabel, 0);
        grid.Children.Add(swatchAndLabel);

        var valueText = new TextBlock { Text = $"{count:N0}" };
        valueText.Classes.Add("Value");
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);

        return row + 1;
    }

    /// <summary>
    /// Adds one counted row to the allocation summary grid.
    /// </summary>
    private static void AddSummaryRow(Grid grid, int row, string label, int count)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var labelText = new TextBlock { Text = label + ":" };
        labelText.Classes.Add("Label");
        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, 0);
        grid.Children.Add(labelText);

        var valueText = new TextBlock { Text = $"{count:N0}" };
        valueText.Classes.Add("Value");
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, 1);
        grid.Children.Add(valueText);
    }

    /// <summary>
    /// Copies the whole report as plain text, so it can be pasted into a bug report.
    /// </summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (_info == null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{_info.FileSystemType}  {_info.VolumeLabel}".TrimEnd());
        sb.AppendLine(_info.IsReadOnly ? "Read-only" : "Read/write");

        if (!string.IsNullOrEmpty(_info.ImagePath))
        {
            sb.AppendLine(_info.ImagePath);
        }

        for (int s = 0; s < _info.Sections.Count; s++)
        {
            VolumeInfoSection section = _info.Sections[s];
            sb.AppendLine();
            sb.AppendLine($"== {section.Title} ==");

            for (int f = 0; f < section.Fields.Count; f++)
            {
                VolumeInfoField field = section.Fields[f];
                sb.Append("  ");
                sb.Append(field.Label.PadRight(22));
                sb.Append(field.Value);

                if (!string.IsNullOrEmpty(field.Detail))
                {
                    sb.Append("   [");
                    sb.Append(field.Detail);
                    sb.Append(']');
                }

                sb.AppendLine();
            }
        }

        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(sb.ToString());
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
