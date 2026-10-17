//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using System.Text.Json;

namespace RetroCommander.Core.Configuration
{
    /// <summary>
    /// Persistent settings for RetroCommander application.
    /// Stored in %APPDATA%\RetroCommander\settings.json
    /// </summary>
    public class RetroCommanderSettings
    {
        /// <summary>
        /// When true, skip NDFS info dialog and mount directly.
        /// Default: false (show dialog).
        /// </summary>
        public bool SkipNdfsInfoDialog { get; set; } = false;

        /// <summary>
        /// When true, skip BSD info dialog and mount directly.
        /// Default: false (show dialog).
        /// </summary>
        public bool SkipBsdInfoDialog { get; set; } = false;

        /// <summary>
        /// How disk images are opened when auto-mounted (double-click / navigate / F9 mount).
        /// One of "Auto", "ReadOnly", "ReadWrite" — see <see cref="Providers.MountMode"/>.
        ///
        /// "Auto" (default) mounts read-write when the file allows it and silently falls back to
        /// read-only otherwise. "ReadWrite" refuses to mount rather than silently degrading (useful
        /// when you intend to modify the image — e.g. NDFS rename / clear-entered need write access).
        /// "ReadOnly" always mounts read-only, protecting the image from any accidental change.
        ///
        /// The right-click "Open Read/Write" / "Open Read Only" commands override this per-mount.
        /// </summary>
        public string DefaultMountMode { get; set; } = "Auto";

        /// <summary>
        /// Parses <see cref="DefaultMountMode"/> into the enum, falling back to
        /// <see cref="Providers.MountMode.Auto"/> for an unrecognised value.
        /// </summary>
        public Providers.MountMode GetDefaultMountMode()
        {
            if (string.Equals(DefaultMountMode, "ReadOnly", System.StringComparison.OrdinalIgnoreCase))
                return Providers.MountMode.ReadOnly;
            if (string.Equals(DefaultMountMode, "ReadWrite", System.StringComparison.OrdinalIgnoreCase))
                return Providers.MountMode.ReadWrite;
            return Providers.MountMode.Auto;
        }

        /// <summary>
        /// Logging level for application.
        /// </summary>
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// Default path for left panel on startup.
        /// </summary>
        public string? DefaultLeftPath { get; set; }

        /// <summary>
        /// Default path for right panel on startup.
        /// </summary>
        public string? DefaultRightPath { get; set; }

        /// <summary>
        /// Gets the default settings file path.
        /// </summary>
        public static string GetSettingsPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string retroCommanderPath = Path.Combine(appDataPath, "RetroCommander");

            // Ensure directory exists
            if (!Directory.Exists(retroCommanderPath))
                Directory.CreateDirectory(retroCommanderPath);

            return Path.Combine(retroCommanderPath, "settings.json");
        }

        /// <summary>
        /// Loads settings from disk, or creates default if not found.
        /// </summary>
        public static RetroCommanderSettings Load()
        {
            string path = GetSettingsPath();

            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<RetroCommanderSettings>(json);
                    return settings ?? new RetroCommanderSettings();
                }
            }
            catch
            {
                // If load fails, return defaults
            }

            return new RetroCommanderSettings();
        }

        /// <summary>
        /// Saves settings to disk.
        /// </summary>
        public void Save()
        {
            string path = GetSettingsPath();

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - settings save failures shouldn't crash app
                Console.WriteLine($"Warning: Failed to save settings: {ex.Message}");
            }
        }
    }
}
