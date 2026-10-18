//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using System.Text.Json;

namespace RetroCommander.Desktop.Themes
{
    /// <summary>
    /// Persisted UI preferences for RetroCommander, stored as a single JSON document at
    /// %APPDATA%\RetroCommander\ui-settings.json. This is the one place that owns the on-disk
    /// settings shape; <see cref="ThemeManager"/> and the file viewer both read/write through it.
    ///
    /// A single in-memory instance (<see cref="Instance"/>) is loaded once at startup. Mutate its
    /// properties and call <see cref="Save"/> to persist.
    /// </summary>
    public sealed class UiSettings
    {
        // Curated default monospace stack for the file viewer (text + hex dumps).
        public const string DefaultViewerFontFamily = "Consolas, Courier New, monospace";
        public const double DefaultViewerFontSize = 13.0;

        /// <summary>Selected theme id (see <see cref="ThemeManager.Themes"/>). Null = default.</summary>
        public string? Theme { get; set; }

        /// <summary>Monospace font family used by the File Viewer dialog.</summary>
        public string ViewerFontFamily { get; set; } = DefaultViewerFontFamily;

        /// <summary>Font size (points) used by the File Viewer dialog.</summary>
        public double ViewerFontSize { get; set; } = DefaultViewerFontSize;

        /// <summary>
        /// When true, a mounted disk image is automatically unmounted once neither panel is showing
        /// it (its root or any subdirectory), so the "Mounts" list cannot grow without bound.
        /// Reference-counted by panel: a mount is kept while a panel is "inside" it. Off by default
        /// because auto-unmounting is surprising if unexpected.
        /// </summary>
        public bool AutoUnmountIdleDisks { get; set; } = false;

        /// <summary>
        /// When true, copying a file OUT of a mounted NDFS disk image to another filesystem saves
        /// NDFS-specific details (owner, dates, access, page layout) in a companion ".xat" sidecar
        /// file next to the copy, so they aren't silently dropped. Copies between two NDFS images
        /// always keep this information automatically, regardless of this setting. On by default —
        /// preserving metadata is the safer default for a tool built around retro filesystems.
        /// </summary>
        public bool PreserveNdfsMetadataOnExport { get; set; } = true;

        /// <summary>
        /// When true, copying a file INTO a mounted NDFS disk image restores NDFS-specific details
        /// (owner, dates, access, page layout) from a companion ".xat" sidecar file sitting next to
        /// the source, if one exists (typically produced by a previous export via
        /// <see cref="PreserveNdfsMetadataOnExport"/>). The sidecar itself is used only as metadata
        /// for its paired file — it is not copied into the NDFS image as a separate file. On by
        /// default, matching the export default.
        /// </summary>
        public bool UseXatMetadataOnImport { get; set; } = true;

        // ------------------------------------------------------------------

        private static UiSettings? s_instance;

        /// <summary>The process-wide settings instance (loaded from disk on first access).</summary>
        public static UiSettings Instance => s_instance ??= Load();

        private static string GetSettingsPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RetroCommander");
            return Path.Combine(dir, "ui-settings.json");
        }

        private static UiSettings Load()
        {
            try
            {
                string file = GetSettingsPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    UiSettings? loaded = JsonSerializer.Deserialize<UiSettings>(json);
                    if (loaded != null)
                    {
                        // Guard against a corrupt/zero size sneaking in from a hand-edited file.
                        if (string.IsNullOrWhiteSpace(loaded.ViewerFontFamily))
                            loaded.ViewerFontFamily = DefaultViewerFontFamily;
                        if (loaded.ViewerFontSize < 6 || loaded.ViewerFontSize > 48)
                            loaded.ViewerFontSize = DefaultViewerFontSize;
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt/unreadable settings file must never break startup — fall back to defaults.
            }
            return new UiSettings();
        }

        /// <summary>Writes the current settings to disk (best-effort; IO failures are ignored).</summary>
        public void Save()
        {
            try
            {
                string file = GetSettingsPath();
                string? dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(this,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch (Exception)
            {
                // Persisting a preference is best-effort; ignore IO/permission failures.
            }
        }
    }
}
