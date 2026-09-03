//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RetroCommander.Desktop.ViewModels
{
    /// <summary>
    /// One persisted favorite. Stored as plain data (not the view-model) so the on-disk shape is
    /// stable and independent of UI types. <see cref="OriginType"/> is persisted as the enum name
    /// (e.g. "Mount", "UserFolder") so re-ordering the enum never corrupts existing favorites.
    /// </summary>
    public sealed class FavoriteRecord
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The mount URN (for Mount favorites, re-mountable) or the folder path.</summary>
        public string Path { get; set; } = string.Empty;

        public string FilesystemType { get; set; } = string.Empty;

        /// <summary>Enum name of <see cref="SidebarItemType"/> the favorite points at.</summary>
        public string OriginType { get; set; } = nameof(SidebarItemType.UserFolder);
    }

    /// <summary>
    /// Persists the user's pinned favorites as a single JSON document at
    /// %APPDATA%\RetroCommander\favorites.json. Mirrors the best-effort pattern used by UiSettings:
    /// IO failures never break the app, they just mean favorites are not saved/restored this run.
    /// </summary>
    public static class FavoritesStore
    {
        /// <summary>
        /// Test seam: when set, favorites are read/written under this directory instead of
        /// %APPDATA%\RetroCommander. Lets unit tests use a temp directory without ever touching the
        /// real user's favorites file. Null in normal operation.
        /// </summary>
        internal static string? DirectoryOverride { get; set; }

        private static string GetDirectory()
        {
            if (!string.IsNullOrEmpty(DirectoryOverride))
                return DirectoryOverride!;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RetroCommander");
        }

        private static string GetPath()
        {
            return Path.Combine(GetDirectory(), "favorites.json");
        }

        /// <summary>Load the persisted favorites (empty list if none / unreadable).</summary>
        public static List<FavoriteRecord> Load()
        {
            try
            {
                string file = GetPath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    List<FavoriteRecord>? loaded = JsonSerializer.Deserialize<List<FavoriteRecord>>(json);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt/unreadable favorites file must never break startup.
            }
            return new List<FavoriteRecord>();
        }

        /// <summary>Persist the given favorites (best-effort; IO failures are ignored).</summary>
        public static void Save(IList<FavoriteRecord> favorites)
        {
            try
            {
                string file = GetPath();
                string? dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(favorites,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(file, json);
            }
            catch (Exception)
            {
                // Persisting favorites is best-effort; ignore IO/permission failures.
            }
        }
    }
}
