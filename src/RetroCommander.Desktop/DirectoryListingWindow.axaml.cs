//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Desktop
{
    /// <summary>
    /// Shows a disk's directory as the machine draws it: the disk name and id, then every entry
    /// with its blocks, its name drawn pixel for pixel from a character ROM, and its type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Offered for disks whose driver gives a drawing (<see cref="IRawDirectoryNames"/> with a
    /// glyph table, see <see cref="GlyphRaster.CanDraw"/>); nothing here names a format. The ROM is
    /// one the user has, chosen here once; the owner keeps it (the main window in <see cref="Themes.UiSettings.CharacterRomPath"/>).
    /// </para>
    /// <para>
    /// The colours are fixed, chosen to look like the machine's screen; they are not taken from a
    /// measured palette and do not follow the app theme, since this area shows the machine.
    /// </para>
    /// </remarks>
    public partial class DirectoryListingWindow : Window
    {
        // Each ROM pixel is drawn as Scale by Scale screen pixels, with no smoothing.
        private const int Scale = 2;

        // The screen colours, as BGRA words for the bitmap and as brushes for the text beside it.
        private const uint InkBgra = 0xFFB55E6C;
        private const uint PaperBgra = 0xFF792835;
        private static readonly IBrush s_ink = new SolidColorBrush(Color.FromRgb(0x6C, 0x5E, 0xB5));

        private readonly IRawDirectoryNames? _raw;
        private readonly RomChosen? _romChosen;
        private string? _romPath;
        private bool _loading;

        /// <summary>
        /// Called when the user chooses a character ROM file here, so the owner can keep it.
        /// </summary>
        /// <param name="path">
        /// The chosen file.
        /// </param>
        public delegate void RomChosen(string path);

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryListingWindow"/> class for the
        /// designer; it shows nothing.
        /// </summary>
        public DirectoryListingWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryListingWindow"/> class for one disk.
        /// </summary>
        /// <param name="raw">
        /// The mounted disk's driver; its layout must give a drawing.
        /// </param>
        /// <param name="title">
        /// What the title bar names the disk by.
        /// </param>
        /// <param name="romPath">
        /// The character ROM file to draw with, or null when none is chosen yet.
        /// </param>
        /// <param name="romChosen">
        /// Called when the user chooses another ROM file here; null to keep the choice to this window.
        /// </param>
        public DirectoryListingWindow(IRawDirectoryNames raw, string title, string? romPath, RomChosen? romChosen)
        {
            InitializeComponent();
            _raw = raw;
            _romPath = romPath;
            _romChosen = romChosen;
            Title = "Directory listing - " + title;

            _loading = true;
            CharacterSetPlace[] sets = raw.RawLayout.CharacterSets;
            List<string> names = new List<string>(sets.Length);
            for (int i = 0; i < sets.Length; i++)
            {
                names.Add(sets[i].Name);
            }

            SetCombo.ItemsSource = names;
            SetCombo.SelectedIndex = 0;
            _loading = false;
            Redraw();
        }

        private void Redraw()
        {
            ListingPanel.Children.Clear();
            MessageText.IsVisible = false;
            if (_raw == null)
            {
                return;
            }

            RawDirectoryLayout layout = _raw.RawLayout;
            string? path = _romPath;
            RomPathText.Text = string.IsNullOrEmpty(path) ? "(none chosen)" : path;
            if (string.IsNullOrEmpty(path))
            {
                ShowMessage("Choose a character ROM file to draw the names. " + layout.CharacterRomDescription);
                return;
            }

            GlyphRaster raster;
            try
            {
                raster = GlyphRaster.Load(path, layout, Math.Max(0, SetCombo.SelectedIndex));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentOutOfRangeException)
            {
                ShowMessage(ex.Message);
                return;
            }

            // The disk name and id on top, then the entries in listing order.
            Grid head = Row(string.Empty, null, string.Empty);
            StackPanel both = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = raster.GlyphWidth * Scale };
            both.Children.Add(Picture(raster, _raw.ReadRawVolumeName()));
            both.Children.Add(Picture(raster, _raw.ReadRawVolumeId()));
            Grid.SetColumn(both, 1);
            head.Children.Add(both);
            head.Margin = new Thickness(0, 0, 0, raster.GlyphHeight * Scale);
            ListingPanel.Children.Add(head);

            RawDirectoryEntry[] entries = _raw.ReadRawDirectory();
            for (int i = 0; i < entries.Length; i++)
            {
                ListingPanel.Children.Add(Row(
                    entries[i].Blocks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Picture(raster, entries[i].Name),
                    entries[i].TypeName));
            }
        }

        private void ShowMessage(string text)
        {
            MessageText.Text = text;
            MessageText.IsVisible = true;
        }

        private static Grid Row(string blocks, Control? picture, string type)
        {
            Grid row = new Grid { ColumnDefinitions = new ColumnDefinitions("56,Auto,Auto") };
            TextBlock left = new TextBlock
            {
                Text = blocks,
                Foreground = s_ink,
                FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };
            row.Children.Add(left);
            if (picture != null)
            {
                Grid.SetColumn(picture, 1);
                row.Children.Add(picture);
            }

            TextBlock right = new TextBlock
            {
                Text = type,
                Foreground = s_ink,
                FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            Grid.SetColumn(right, 2);
            row.Children.Add(right);
            return row;
        }

        // One run of raw bytes as a bitmap: each glyph read from the ROM, each pixel Scale by Scale.
        private static Image Picture(GlyphRaster raster, byte[] bytes)
        {
            int w = Math.Max(1, bytes.Length) * raster.GlyphWidth;
            int h = raster.GlyphHeight;
            int[] pixels = new int[w * h];
            for (int i = 0; i < bytes.Length; i++)
            {
                ReadOnlySpan<byte> glyph = raster.Glyph(bytes[i]);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < raster.GlyphWidth; x++)
                    {
                        bool on = (glyph[y] & (0x80 >> x)) != 0;
                        pixels[(y * w) + (i * raster.GlyphWidth) + x] = unchecked((int)(on ? InkBgra : PaperBgra));
                    }
                }
            }

            WriteableBitmap bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (ILockedFramebuffer frame = bitmap.Lock())
            {
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(pixels, y * w, frame.Address + (y * frame.RowBytes), w);
                }
            }

            Image image = new Image { Source = bitmap, Width = w * Scale, Height = h * Scale, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            return image;
        }

        private async void OnChooseRomClick(object? sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a character ROM file",
                AllowMultiple = false,
            });
            if (files.Count == 0)
            {
                return;
            }

            string? path = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                ShowMessage("That file has no local path.");
                return;
            }

            _romPath = path;
            _romChosen?.Invoke(path);
            Redraw();
        }

        private void OnSetChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_loading)
            {
                Redraw();
            }
        }
    }
}
