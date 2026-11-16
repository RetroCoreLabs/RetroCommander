//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// The disk image file extensions RetroCommander can open, taken from the registered RetroIO
    /// drivers (each driver lists its own extensions, and its detection uses the same list).
    /// </summary>
    /// <remarks>
    /// This is the one list for the whole app: the double-click "is this a disk image" check,
    /// the Mount dialog's file filter and the Settings "File associations" section all read it,
    /// so a new driver shows up everywhere without touching the UI. An extension is only a hint;
    /// the mount itself still detects the format from the file's contents.
    /// </remarks>
    public static class KnownDiskImageTypes
    {
        private static readonly object s_lock = new object();
        private static string[]? s_extensions;
        private static Dictionary<string, string>? s_descriptions;

        /// <summary>
        /// Gets every known extension, lower case with the leading dot, sorted.
        /// </summary>
        public static IReadOnlyList<string> Extensions
        {
            get
            {
                EnsureLoaded();
                return s_extensions!;
            }
        }

        /// <summary>
        /// Checks whether a file name ends in one of the known extensions (case does not matter).
        /// </summary>
        /// <param name="fileName">
        /// File name or path
        /// </param>
        /// <returns>
        /// True when the extension is known
        /// </returns>
        public static bool IsKnown(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            if (ext.Length == 0)
            {
                return false;
            }

            EnsureLoaded();
            return s_descriptions!.ContainsKey(ext);
        }

        /// <summary>
        /// Describes which formats use an extension, for example
        /// "PC floppy disk images (FAT12/16); Berkeley Fast File System".
        /// </summary>
        /// <param name="extension">
        /// Extension with the leading dot
        /// </param>
        /// <returns>
        /// The descriptions of every driver that lists the extension, or an empty string
        /// </returns>
        public static string Describe(string extension)
        {
            EnsureLoaded();
            return s_descriptions!.TryGetValue(extension, out string? text) ? text : string.Empty;
        }

        /// <summary>
        /// Gets file-picker patterns for every known extension, for example "*.d64".
        /// </summary>
        /// <returns>
        /// One pattern per extension
        /// </returns>
        public static string[] GetFilePickerPatterns()
        {
            IReadOnlyList<string> extensions = Extensions;
            string[] patterns = new string[extensions.Count];
            for (int i = 0; i < extensions.Count; i++)
            {
                patterns[i] = "*" + extensions[i];
            }

            return patterns;
        }

        // Builds the list once from the same driver set the app mounts with.
        private static void EnsureLoaded()
        {
            if (s_extensions != null)
            {
                return;
            }

            lock (s_lock)
            {
                if (s_extensions != null)
                {
                    return;
                }

                DriverFactory factory = DriverFactoryBuilder.CreateWithAllDrivers();
                Dictionary<string, string> descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string[] types = factory.GetRegisteredTypes();
                for (int t = 0; t < types.Length; t++)
                {
                    IFileSystemDriverProvider? provider = factory.GetProvider(types[t]);
                    if (provider == null)
                    {
                        continue;
                    }

                    IReadOnlyList<string> extensions = provider.FileExtensions;
                    for (int i = 0; i < extensions.Count; i++)
                    {
                        string ext = extensions[i].ToLowerInvariant();
                        descriptions[ext] = descriptions.TryGetValue(ext, out string? existing)
                            ? existing + "; " + provider.Description
                            : provider.Description;
                    }
                }

                s_descriptions = descriptions;
                s_extensions = factory.GetKnownFileExtensions();
            }
        }
    }
}
