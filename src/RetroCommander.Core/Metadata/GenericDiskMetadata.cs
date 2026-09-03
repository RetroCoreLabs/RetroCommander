//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
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
        public string FileSystemType { get; set; } = "Unknown";
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public bool HasErrors { get; set; }
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
