//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.Collections.Generic;

namespace RetroCommander.Core.Metadata
{
    /// <summary>
    /// Generic disk metadata for filesystems that don't have specialized extractors yet.
    /// Provides basic file size and error reporting.
    /// </summary>
    public sealed class GenericDiskMetadata : IDiskMetadata
    {
        /// <inheritdoc/>
        public string FileSystemType { get; set; } = "Unknown";

        /// <inheritdoc/>
        public long FileSizeBytes { get; set; }

        /// <inheritdoc/>
        public string FileSizeFormatted { get; set; } = string.Empty;

        /// <inheritdoc/>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <inheritdoc/>
        public bool HasErrors { get; set; }

        /// <inheritdoc/>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets minimal detailed properties (just file size for now).
        /// </summary>
        public List<MetadataProperty> GetDetailedProperties()
        {
            var properties = new List<MetadataProperty>();

            properties.Add(new MetadataProperty
            {
                Category = "File Information",
                Label = "Filesystem Type",
                Value = FileSystemType
            });

            properties.Add(new MetadataProperty
            {
                Category = "File Information",
                Label = "File Size",
                Value = FileSizeFormatted
            });

            if (Warnings.Count > 0)
            {
                for (int i = 0; i < Warnings.Count; i++)
                {
                    properties.Add(new MetadataProperty
                    {
                        Category = "Warnings",
                        Label = $"Warning {i + 1}",
                        Value = Warnings[i],
                        Icon = "⚠️"
                    });
                }
            }

            return properties;
        }
    }
}
