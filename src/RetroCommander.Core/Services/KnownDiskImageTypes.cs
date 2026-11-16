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
        // Built once, on first use, from the same driver set the app mounts with.
        // Lazy<T> gives a thread-safe one-time build with the result fully visible to every thread.
        private static readonly Lazy<Catalogue> s_catalogue = new Lazy<Catalogue>(Catalogue.Build);

        /// <summary>
        /// Gets every known extension, lower case with the leading dot, sorted.
        /// </summary>
        public static IReadOnlyList<string> Extensions => s_catalogue.Value.Extensions;

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
            return ext.Length > 0 && s_catalogue.Value.Descriptions.ContainsKey(ext);
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
            return s_catalogue.Value.Descriptions.TryGetValue(extension, out string? text) ? text : string.Empty;
        }

        /// <summary>
        /// Gets file-picker patterns for every known extension, for example "*.d64".
        /// </summary>
        /// <returns>
        /// One pattern per extension
        /// </returns>
        public static string[] GetFilePickerPatterns()
        {
            string[] extensions = s_catalogue.Value.Extensions;
            string[] patterns = new string[extensions.Length];
            for (int i = 0; i < extensions.Length; i++)
            {
                patterns[i] = "*" + extensions[i];
            }

            return patterns;
        }

        // The extension list and, per extension, the descriptions of the formats that use it.
        private sealed class Catalogue
        {
            public readonly string[] Extensions;
            public readonly Dictionary<string, string> Descriptions;

            private Catalogue(string[] extensions, Dictionary<string, string> descriptions)
            {
                Extensions = extensions;
                Descriptions = descriptions;
            }

            public static Catalogue Build()
            {
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

                return new Catalogue(factory.GetKnownFileExtensions(), descriptions);
            }
        }
    }
}
