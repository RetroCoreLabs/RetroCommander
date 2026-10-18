//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;        // ResourceDictionary
using Avalonia.Media;           // Color, SolidColorBrush
using Avalonia.Styling;         // ThemeVariant

namespace RetroCommander.Desktop.Themes
{
    /// <summary>
    /// Central runtime theming engine for RetroCommander.
    ///
    /// Design:
    ///   * Every visual brush used by the XAML (FluentXxxBrush keys) is supplied
    ///     PER theme through Avalonia's <see cref="IResourceDictionary.ThemeDictionaries"/>.
    ///   * Switching a theme is just setting <see cref="Application.RequestedThemeVariant"/>;
    ///     because all XAML references the brushes via {DynamicResource ...} the whole UI
    ///     re-colours live, with no restart.
    ///   * Custom (non Light/Dark) themes are modelled as a <see cref="ThemeVariant"/> that
    ///     INHERITS from a base Light or Dark variant. That inheritance is what lets the
    ///     built-in Avalonia FluentTheme (buttons, scrollbars, DataGrid, ...) keep rendering
    ///     correctly even though it only ships Light/Dark resources.
    ///   * The chosen theme id is persisted to %APPDATA%\RetroCommander\ui-settings.json and
    ///     restored on the next launch.
    /// </summary>
    public sealed class ThemeManager
    {
        // Single shared instance — the App and the Settings dialog both talk to this.
        /// <summary>
        /// Gets the single shared theme manager.
        /// </summary>
        public static ThemeManager Instance { get; } = new ThemeManager();

        private Application? _app;
        private readonly List<ThemeDefinition> _themes = new();

        /// <summary>
        /// All themes the user can choose from (in display order).
        /// </summary>
        public IReadOnlyList<ThemeDefinition> Themes => _themes;

        /// <summary>
        /// The currently applied theme (never null after <see cref="Initialize"/>).
        /// </summary>
        public ThemeDefinition Current { get; private set; } = null!;

        private ThemeManager()
        {
            BuildThemes();
        }

        /// <summary>
        /// Registers every theme's brush dictionary on the application and applies the
        /// previously persisted theme (falling back to Light). Must be called once, BEFORE
        /// the first window is shown, from <c>App.OnFrameworkInitializationCompleted</c>.
        /// </summary>
        public void Initialize(Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));

            // Build and register one brush ResourceDictionary per theme variant.
            for (int i = 0; i < _themes.Count; i++)
            {
                ThemeDefinition def = _themes[i];
                ResourceDictionary rd = BuildBrushDictionary(def);
                // The indexer accepts any IThemeVariantProvider; ResourceDictionary is one.
                app.Resources.ThemeDictionaries[def.Variant] = rd;
            }

            // Restore the saved choice; fall back to the first theme (Light) if missing/invalid.
            string? savedId = UiSettings.Instance.Theme;
            ThemeDefinition? theme = FindById(savedId);
            if (theme == null)
                theme = _themes.Count > 0 ? _themes[0] : null;

            if (theme != null)
                Apply(theme.Id, persist: false);
        }

        /// <summary>
        /// Applies the theme with the given id immediately (live re-colour of the whole UI).
        /// </summary>
        /// <param name="themeId">
        /// Id of one of <see cref="Themes"/>.
        /// </param>
        /// <param name="persist">
        /// When true the choice is written to disk for next launch.
        /// </param>
        public void Apply(string themeId, bool persist = true)
        {
            if (_app == null) return;

            ThemeDefinition? theme = FindById(themeId);
            if (theme == null) return;

            Current = theme;
            // This single assignment swaps every {DynamicResource FluentXxxBrush} lookup.
            _app.RequestedThemeVariant = theme.Variant;

            if (persist)
            {
                UiSettings.Instance.Theme = theme.Id;
                UiSettings.Instance.Save();
            }
        }

        private ThemeDefinition? FindById(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _themes.Count; i++)
            {
                if (string.Equals(_themes[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return _themes[i];
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Brush dictionary construction
        // ------------------------------------------------------------------

        // The brush resource keys referenced throughout the XAML, in the same order the
        // palette exposes its colours below. Keep these two lists aligned.
        private static readonly string[] s_brushKeys =
        {
            "FluentBackgroundPrimaryBrush",
            "FluentBackgroundSecondaryBrush",
            "FluentSurfaceAltBrush",
            "FluentBorderLightBrush",
            "FluentBorderDarkBrush",
            "FluentAccentBlueBrush",
            "FluentTextPrimaryBrush",
            "FluentTextSecondaryBrush",
            "FluentSelectionHighlightBrush",
            "FluentHoverHighlightBrush",
            "FluentSidebarBackgroundBrush",
            "FluentSidebarSeparatorBrush",
            "FluentWarningBackgroundBrush",
            "FluentWarningBorderBrush",
            "FluentSuccessBackgroundBrush",
            "FluentSuccessBorderBrush",
            "FluentErrorBackgroundBrush",
            "FluentErrorBorderBrush",
            "FluentErrorTextBrush",
        };

        // Builds an immutable-ish ResourceDictionary of SolidColorBrushes for one theme.
        private static ResourceDictionary BuildBrushDictionary(ThemeDefinition def)
        {
            ResourceDictionary rd = new ResourceDictionary();
            string[] hex = def.Palette.ToArray();
            // s_brushKeys and the palette array are intentionally 1:1 in length and order.
            for (int i = 0; i < s_brushKeys.Length; i++)
            {
                rd[s_brushKeys[i]] = new SolidColorBrush(Color.Parse(hex[i]));
            }
            return rd;
        }

        // ------------------------------------------------------------------
        // Theme catalogue (Light, Dark + presets)
        // ------------------------------------------------------------------

        private void BuildThemes()
        {
            // ---- Light (the original Fluent palette) ----
            _themes.Add(new ThemeDefinition("Light", "Light", ThemeVariant.Light, new Palette
            {
                BackgroundPrimary = "#F3F3F3",
                BackgroundSecondary = "#FFFFFF",
                SurfaceAlt = "#F8F8F8",
                BorderLight = "#D0D0D0",
                BorderDark = "#3C3C3C",
                AccentBlue = "#0078D7",
                TextPrimary = "#202020",
                TextSecondary = "#666666",
                SelectionHighlight = "#CDE8FF",
                HoverHighlight = "#EAF6FF",
                SidebarBackground = "#F0F0F0",
                SidebarSeparator = "#E0E0E0",
                WarningBackground = "#FFF8DC",
                WarningBorder = "#FFD700",
                SuccessBackground = "#F0F8F0",
                SuccessBorder = "#90EE90",
                ErrorBackground = "#FDE7E9",
                ErrorBorder = "#E81123",
                ErrorText = "#C42B1C",
            }));

            // ---- Dark (neutral dark, VS-Code-ish) ----
            _themes.Add(new ThemeDefinition("Dark", "Dark", ThemeVariant.Dark, new Palette
            {
                BackgroundPrimary = "#1E1E1E",
                BackgroundSecondary = "#252526",
                SurfaceAlt = "#2D2D30",
                BorderLight = "#3F3F46",
                BorderDark = "#555555",
                AccentBlue = "#0A84FF",
                TextPrimary = "#E8E8E8",
                TextSecondary = "#A0A0A0",
                SelectionHighlight = "#094771",
                HoverHighlight = "#2A2D2E",
                SidebarBackground = "#252526",
                SidebarSeparator = "#3F3F46",
                WarningBackground = "#3A3320",
                WarningBorder = "#D7B600",
                SuccessBackground = "#1E3320",
                SuccessBorder = "#4EC94E",
                ErrorBackground = "#3A1D1D",
                ErrorBorder = "#C44C4C",
                ErrorText = "#FF99A4",
            }));

            // ---- Solarized Dark (Ethan Schoonover palette) — inherits Dark base controls ----
            _themes.Add(new ThemeDefinition("SolarizedDark", "Solarized Dark",
                new ThemeVariant("SolarizedDark", ThemeVariant.Dark), new Palette
                {
                    BackgroundPrimary = "#002B36",   // base03
                    BackgroundSecondary = "#073642", // base02
                    SurfaceAlt = "#08404F",
                    BorderLight = "#094B5A",
                    BorderDark = "#586E75",          // base01
                    AccentBlue = "#268BD2",          // blue
                    TextPrimary = "#93A1A1",         // base1
                    TextSecondary = "#657B83",       // base00
                    SelectionHighlight = "#0B4F60",
                    HoverHighlight = "#0B4A59",
                    SidebarBackground = "#073642",
                    SidebarSeparator = "#094B5A",
                    WarningBackground = "#33301A",
                    WarningBorder = "#B58900",       // yellow
                    SuccessBackground = "#14361F",
                    SuccessBorder = "#859900",       // green
                    ErrorBackground = "#3A1F1A",
                    ErrorBorder = "#DC322F",         // solarized red
                    ErrorText = "#E8857F",
                }));

            // ---- Nord (arctic palette) — inherits Dark base controls ----
            _themes.Add(new ThemeDefinition("Nord", "Nord",
                new ThemeVariant("Nord", ThemeVariant.Dark), new Palette
                {
                    BackgroundPrimary = "#2E3440",   // nord0
                    BackgroundSecondary = "#3B4252", // nord1
                    SurfaceAlt = "#434C5E",          // nord2
                    BorderLight = "#4C566A",         // nord3
                    BorderDark = "#5E6877",
                    AccentBlue = "#88C0D0",          // nord8 (frost)
                    TextPrimary = "#ECEFF4",         // nord6
                    TextSecondary = "#9AA4B5",
                    SelectionHighlight = "#434C5E",
                    HoverHighlight = "#3B4252",
                    SidebarBackground = "#3B4252",
                    SidebarSeparator = "#4C566A",
                    WarningBackground = "#4A3F2A",
                    WarningBorder = "#EBCB8B",       // aurora yellow
                    SuccessBackground = "#34402C",
                    SuccessBorder = "#A3BE8C",       // aurora green
                    ErrorBackground = "#43292C",
                    ErrorBorder = "#BF616A",         // aurora red
                    ErrorText = "#D99CA3",
                }));

            // ---- Retro Green (P1 phosphor) — inherits Dark base controls ----
            // The three CRT themes below are RetroTerm's, carried over verbatim so both apps
            // offer the same catalogue. RetroTerm's ThemeDefinition holds 29 colours against
            // this Palette's 19; the mapping is WindowBackground -> BackgroundPrimary,
            // SectionBackground -> BackgroundSecondary, PanelBackground -> SurfaceAlt,
            // FocusBorder -> AccentBlue (it is the vivid signature colour in each), and
            // ErrorPanel* -> Error*. Warning/Success backgrounds have no RetroTerm equivalent
            // and are dark tints picked to sit on this theme's own ground.
            _themes.Add(new ThemeDefinition("RetroGreen", "Retro Green",
                new ThemeVariant("RetroGreen", ThemeVariant.Dark), new Palette
                {
                    BackgroundPrimary = "#001100",
                    BackgroundSecondary = "#002200",
                    SurfaceAlt = "#001A00",
                    BorderLight = "#004400",
                    BorderDark = "#005500",
                    AccentBlue = "#00FF00",
                    TextPrimary = "#00FF00",
                    TextSecondary = "#00AA00",
                    SelectionHighlight = "#004400",
                    HoverHighlight = "#003300",
                    SidebarBackground = "#002200",
                    SidebarSeparator = "#004400",
                    WarningBackground = "#1A1A00",
                    WarningBorder = "#FFFF00",
                    SuccessBackground = "#002200",
                    SuccessBorder = "#00FF00",
                    ErrorBackground = "#220000",
                    ErrorBorder = "#FF3300",
                    ErrorText = "#FF6644",
                }));

            // ---- Amber CRT (P3 phosphor) — inherits Dark base controls ----
            _themes.Add(new ThemeDefinition("AmberCrt", "Amber CRT",
                new ThemeVariant("AmberCrt", ThemeVariant.Dark), new Palette
                {
                    BackgroundPrimary = "#1A0F00",
                    BackgroundSecondary = "#261800",
                    SurfaceAlt = "#221400",
                    BorderLight = "#553300",
                    BorderDark = "#664400",
                    AccentBlue = "#FFB000",
                    TextPrimary = "#FFB000",
                    TextSecondary = "#CC8800",
                    SelectionHighlight = "#443300",
                    HoverHighlight = "#332200",
                    SidebarBackground = "#261800",
                    SidebarSeparator = "#553300",
                    WarningBackground = "#262600",
                    WarningBorder = "#FFFF00",
                    SuccessBackground = "#261800",
                    SuccessBorder = "#FFB000",
                    ErrorBackground = "#220000",
                    ErrorBorder = "#FF3300",
                    ErrorText = "#FF6644",
                }));

            // ---- Synthwave '84 — inherits Dark base controls ----
            _themes.Add(new ThemeDefinition("Synthwave", "Synthwave '84",
                new ThemeVariant("Synthwave", ThemeVariant.Dark), new Palette
                {
                    BackgroundPrimary = "#262335",
                    BackgroundSecondary = "#2A2139",
                    SurfaceAlt = "#241B2F",
                    BorderLight = "#463465",
                    BorderDark = "#614D85",
                    AccentBlue = "#FF7EDB",
                    TextPrimary = "#FFFFFF",
                    TextSecondary = "#B893CE",
                    SelectionHighlight = "#463465",
                    HoverHighlight = "#34294F",
                    SidebarBackground = "#2A2139",
                    SidebarSeparator = "#463465",
                    WarningBackground = "#332B1F",
                    WarningBorder = "#FEDE5D",
                    SuccessBackground = "#12332A",
                    SuccessBorder = "#72F1B8",
                    ErrorBackground = "#351525",
                    ErrorBorder = "#FE4450",
                    ErrorText = "#F97E72",
                }));

            // ---- High Contrast (accessibility: black/white/bright) — inherits Dark base ----
            _themes.Add(new ThemeDefinition("HighContrast", "High Contrast",
                new ThemeVariant("HighContrast", ThemeVariant.Dark), new Palette
                {
                    BackgroundPrimary = "#000000",
                    BackgroundSecondary = "#000000",
                    SurfaceAlt = "#1A1A1A",
                    BorderLight = "#FFFFFF",
                    BorderDark = "#FFFFFF",
                    AccentBlue = "#1F6FEB",          // readable with the forced white button text
                    TextPrimary = "#FFFFFF",
                    TextSecondary = "#E0E0E0",
                    SelectionHighlight = "#1F6FEB",
                    HoverHighlight = "#333333",
                    SidebarBackground = "#000000",
                    SidebarSeparator = "#FFFFFF",
                    WarningBackground = "#000000",
                    WarningBorder = "#FFFF00",
                    SuccessBackground = "#000000",
                    SuccessBorder = "#00FF00",
                    ErrorBackground = "#000000",
                    ErrorBorder = "#FF0000",
                    ErrorText = "#FF6666",
                }));
        }

        // Persistence is delegated to RetroCommander.Desktop.Themes.UiSettings (the single owner of
        // the on-disk ui-settings.json document).
    }

    /// <summary>
    /// One selectable theme: an id, a display name, its Avalonia variant and palette.
    /// </summary>
    public sealed class ThemeDefinition
    {
        /// <summary>
        /// Gets the theme id, for example <c>Light</c> or <c>SolarizedDark</c>. This is the
        /// value saved to the settings file and passed to <see cref="ThemeManager.Apply"/>.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the name shown to the user, for example <c>Solarized Dark</c>.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the Avalonia theme variant that selects this theme's brush dictionary.
        /// </summary>
        public ThemeVariant Variant { get; }

        /// <summary>
        /// Gets the colours this theme uses for the shared brush keys.
        /// </summary>
        public Palette Palette { get; }

        /// <summary>
        /// Creates a theme definition from its parts.
        /// </summary>
        /// <param name="id">
        /// The theme id saved to the settings file.
        /// </param>
        /// <param name="displayName">
        /// The name shown to the user.
        /// </param>
        /// <param name="variant">
        /// The Avalonia theme variant for this theme.
        /// </param>
        /// <param name="palette">
        /// The colours for this theme.
        /// </param>
        public ThemeDefinition(string id, string displayName, ThemeVariant variant, Palette palette)
        {
            Id = id;
            DisplayName = displayName;
            Variant = variant;
            Palette = palette;
        }
    }

    /// <summary>
    /// A flat colour palette (hex strings). Field ORDER here defines the order returned by
    /// <see cref="ToArray"/>, which must stay aligned with ThemeManager.s_brushKeys.
    /// </summary>
    public sealed class Palette
    {
        /// <summary>
        /// Hex colour for the <c>FluentBackgroundPrimaryBrush</c> resource.
        /// </summary>
        public string BackgroundPrimary = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentBackgroundSecondaryBrush</c> resource.
        /// </summary>
        public string BackgroundSecondary = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentSurfaceAltBrush</c> resource.
        /// </summary>
        public string SurfaceAlt = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentBorderLightBrush</c> resource.
        /// </summary>
        public string BorderLight = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentBorderDarkBrush</c> resource.
        /// </summary>
        public string BorderDark = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentAccentBlueBrush</c> resource. The name is historical; in
        /// several themes this accent is not blue.
        /// </summary>
        public string AccentBlue = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentTextPrimaryBrush</c> resource.
        /// </summary>
        public string TextPrimary = "#FFFFFF";

        /// <summary>
        /// Hex colour for the <c>FluentTextSecondaryBrush</c> resource.
        /// </summary>
        public string TextSecondary = "#FFFFFF";

        /// <summary>
        /// Hex colour for the <c>FluentSelectionHighlightBrush</c> resource.
        /// </summary>
        public string SelectionHighlight = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentHoverHighlightBrush</c> resource.
        /// </summary>
        public string HoverHighlight = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentSidebarBackgroundBrush</c> resource.
        /// </summary>
        public string SidebarBackground = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentSidebarSeparatorBrush</c> resource.
        /// </summary>
        public string SidebarSeparator = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentWarningBackgroundBrush</c> resource.
        /// </summary>
        public string WarningBackground = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentWarningBorderBrush</c> resource.
        /// </summary>
        public string WarningBorder = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentSuccessBackgroundBrush</c> resource.
        /// </summary>
        public string SuccessBackground = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentSuccessBorderBrush</c> resource.
        /// </summary>
        public string SuccessBorder = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentErrorBackgroundBrush</c> resource.
        /// </summary>
        public string ErrorBackground = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentErrorBorderBrush</c> resource.
        /// </summary>
        public string ErrorBorder = "#000000";

        /// <summary>
        /// Hex colour for the <c>FluentErrorTextBrush</c> resource.
        /// </summary>
        public string ErrorText = "#FFFFFF";

        /// <summary>
        /// Returns the hex values in the canonical brush-key order.
        /// </summary>
        public string[] ToArray()
        {
            return new[]
            {
                BackgroundPrimary,
                BackgroundSecondary,
                SurfaceAlt,
                BorderLight,
                BorderDark,
                AccentBlue,
                TextPrimary,
                TextSecondary,
                SelectionHighlight,
                HoverHighlight,
                SidebarBackground,
                SidebarSeparator,
                WarningBackground,
                WarningBorder,
                SuccessBackground,
                SuccessBorder,
                ErrorBackground,
                ErrorBorder,
                ErrorText,
            };
        }
    }
}
