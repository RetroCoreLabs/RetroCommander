//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using RetroCommander.Desktop.Themes;

namespace RetroCommander.Desktop
{
    /// <summary>
    /// Preferences dialog: theme picker (Appearance) and the configurable file-viewer font.
    /// Theme changes apply live; font changes are saved and take effect next time a file is
    /// opened in the viewer. All changes persist via <see cref="UiSettings"/>.
    /// </summary>
    public partial class SettingsDialog : Window
    {
        // Guard so populating the combos at open time does not fire redundant change handlers.
        private bool _populating;

        // Curated monospace choices for the file viewer. Tag is the actual font-family stack;
        // the leading family is preferred, with fallbacks so it always resolves to a mono font.
        private static readonly (string Label, string Family)[] s_viewerFonts =
        {
            ("Consolas (default)", UiSettings.DefaultViewerFontFamily),
            ("Cascadia Mono", "Cascadia Mono, Consolas, monospace"),
            ("Cascadia Code", "Cascadia Code, Consolas, monospace"),
            ("Courier New", "Courier New, monospace"),
            ("Lucida Console", "Lucida Console, Consolas, monospace"),
        };

        // Selectable viewer font sizes (points).
        private static readonly double[] s_viewerSizes = { 9, 10, 11, 12, 13, 14, 16, 18, 20 };

        /// <summary>
        /// Creates the dialog and fills its controls from the saved theme, viewer font, mount and file transfer settings.
        /// </summary>
        public SettingsDialog()
        {
            InitializeComponent();
            PopulateThemes();
            PopulateViewerFont();
            PopulateMounts();
            PopulateFileTransfers();
        }

        // Reflect the persisted auto-unmount + automount-mode preferences into the controls.
        private void PopulateMounts()
        {
            _populating = true;
            try
            {
                AutoUnmountCheck.IsChecked = UiSettings.Instance.AutoUnmountIdleDisks;

                // Automount mode: 0 = Auto, 1 = ReadOnly, 2 = ReadWrite (matches the ComboBox order).
                var settings = RetroCommander.Core.Configuration.RetroCommanderSettings.Load();
                MountModeCombo.SelectedIndex = settings.GetDefaultMountMode() switch
                {
                    RetroCommander.Core.Providers.MountMode.ReadOnly => 1,
                    RetroCommander.Core.Providers.MountMode.ReadWrite => 2,
                    _ => 0
                };
            }
            finally
            {
                _populating = false;
            }
        }

        // Persist the auto-unmount preference; takes effect on the next panel navigation.
        private void OnAutoUnmountChanged(object? sender, RoutedEventArgs e)
        {
            if (_populating) return;
            UiSettings.Instance.AutoUnmountIdleDisks = AutoUnmountCheck.IsChecked ?? false;
            UiSettings.Instance.Save();
        }

        // Persist the automount mode. Applied to the volume factory the next time the app starts
        // (or the next time a factory is constructed) — existing mounts are unaffected.
        private void OnMountModeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_populating) return;

            var settings = RetroCommander.Core.Configuration.RetroCommanderSettings.Load();
            settings.DefaultMountMode = MountModeCombo.SelectedIndex switch
            {
                1 => "ReadOnly",
                2 => "ReadWrite",
                _ => "Auto"
            };
            settings.Save();
        }

        // Reflect the persisted NDFS-metadata preferences into the checkboxes.
        private void PopulateFileTransfers()
        {
            _populating = true;
            try
            {
                PreserveNdfsMetadataCheck.IsChecked = UiSettings.Instance.PreserveNdfsMetadataOnExport;
                UseXatMetadataOnImportCheck.IsChecked = UiSettings.Instance.UseXatMetadataOnImport;
            }
            finally
            {
                _populating = false;
            }
        }

        // Persist the NDFS-metadata-export preference; takes effect on the next copy.
        private void OnPreserveNdfsMetadataChanged(object? sender, RoutedEventArgs e)
        {
            if (_populating) return;
            UiSettings.Instance.PreserveNdfsMetadataOnExport = PreserveNdfsMetadataCheck.IsChecked ?? false;
            UiSettings.Instance.Save();
        }

        // Persist the NDFS-metadata-import preference; takes effect on the next copy.
        private void OnUseXatMetadataOnImportChanged(object? sender, RoutedEventArgs e)
        {
            if (_populating) return;
            UiSettings.Instance.UseXatMetadataOnImport = UseXatMetadataOnImportCheck.IsChecked ?? false;
            UiSettings.Instance.Save();
        }

        // Fill the combo with the available themes and pre-select the active one.
        private void PopulateThemes()
        {
            _populating = true;
            try
            {
                ThemeCombo.Items.Clear();

                var themes = ThemeManager.Instance.Themes;
                int selectedIndex = 0;
                for (int i = 0; i < themes.Count; i++)
                {
                    ThemeDefinition def = themes[i];
                    // Tag carries the stable id; Content shows the friendly name.
                    ComboBoxItem item = new ComboBoxItem
                    {
                        Content = def.DisplayName,
                        Tag = def.Id
                    };
                    ThemeCombo.Items.Add(item);

                    if (def.Id == ThemeManager.Instance.Current.Id)
                        selectedIndex = i;
                }

                ThemeCombo.SelectedIndex = selectedIndex;
            }
            finally
            {
                _populating = false;
            }
        }

        // Apply the chosen theme immediately (and persist the choice).
        private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_populating) return;

            if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string themeId)
            {
                ThemeManager.Instance.Apply(themeId, persist: true);
            }
        }

        // Fill the viewer font + size combos and select the persisted choices.
        private void PopulateViewerFont()
        {
            _populating = true;
            try
            {
                UiSettings s = UiSettings.Instance;

                // Font family combo.
                ViewerFontCombo.Items.Clear();
                int fontIndex = 0;
                for (int i = 0; i < s_viewerFonts.Length; i++)
                {
                    ComboBoxItem item = new ComboBoxItem
                    {
                        Content = s_viewerFonts[i].Label,
                        Tag = s_viewerFonts[i].Family
                    };
                    ViewerFontCombo.Items.Add(item);
                    if (s_viewerFonts[i].Family == s.ViewerFontFamily)
                        fontIndex = i;
                }
                ViewerFontCombo.SelectedIndex = fontIndex;

                // Size combo.
                ViewerSizeCombo.Items.Clear();
                int sizeIndex = 0;
                for (int i = 0; i < s_viewerSizes.Length; i++)
                {
                    ComboBoxItem item = new ComboBoxItem
                    {
                        Content = s_viewerSizes[i].ToString("0"),
                        Tag = s_viewerSizes[i]
                    };
                    ViewerSizeCombo.Items.Add(item);
                    // Closest match wins so a hand-edited odd size still selects sensibly.
                    if (s_viewerSizes[i] <= s.ViewerFontSize)
                        sizeIndex = i;
                }
                ViewerSizeCombo.SelectedIndex = sizeIndex;
            }
            finally
            {
                _populating = false;
            }

            UpdatePreview();
        }

        private void OnViewerFontChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_populating) return;
            if (ViewerFontCombo.SelectedItem is ComboBoxItem item && item.Tag is string family)
            {
                UiSettings.Instance.ViewerFontFamily = family;
                UiSettings.Instance.Save();
                UpdatePreview();
            }
        }

        private void OnViewerSizeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_populating) return;
            if (ViewerSizeCombo.SelectedItem is ComboBoxItem item && item.Tag is double size)
            {
                UiSettings.Instance.ViewerFontSize = size;
                UiSettings.Instance.Save();
                UpdatePreview();
            }
        }

        // Renders the preview line in the currently selected viewer font + size.
        private void UpdatePreview()
        {
            if (ViewerPreview == null) return;
            UiSettings s = UiSettings.Instance;
            ViewerPreview.FontFamily = new FontFamily(s.ViewerFontFamily);
            ViewerPreview.FontSize = s.ViewerFontSize;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
