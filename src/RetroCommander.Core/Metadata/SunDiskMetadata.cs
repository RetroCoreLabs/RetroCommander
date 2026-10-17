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
    /// Metadata about a Sun Disk Label disk image (read-only DTO for UI display).
    /// </summary>
    public sealed class SunDiskMetadata : IDiskMetadata
    {
        public string FileSystemType => "FFS (Sun Disk Label)";
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }

        // Sun-specific fields
        public string LabelText { get; set; } = string.Empty;

        // Geometry
        public ushort NumCylinders { get; set; }
        public ushort NumAltCylinders { get; set; }
        public ushort NumHeads { get; set; }
        public ushort NumSectors { get; set; }
        public ushort Interleave { get; set; }
        public ushort DataCylinders { get; set; }
        public uint TotalSectors { get; set; }
        public string TotalSectorsFormatted { get; set; } = string.Empty;
        public ushort Rpm { get; set; }

        // Validation
        public ushort Magic { get; set; }
        public bool IsMagicValid { get; set; }

        // Partitions
        public List<SunPartitionMetadata> Partitions { get; set; } = new List<SunPartitionMetadata>();

        /// <summary>
        /// Converts Sun metadata to generic display properties.
        /// </summary>
        public List<MetadataProperty> GetDetailedProperties()
        {
            var properties = new List<MetadataProperty>();

            // Disk Information section
            properties.Add(new MetadataProperty
            {
                Category = "Disk Information",
                Label = "Label Text",
                Value = string.IsNullOrWhiteSpace(LabelText) ? "(none)" : LabelText
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Information",
                Label = "File Size",
                Value = FileSizeFormatted
            });

            // Geometry section
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Cylinders",
                Value = $"{NumCylinders} ({NumAltCylinders} alternate)"
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Heads per Cylinder",
                Value = NumHeads.ToString("N0")
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Sectors per Track",
                Value = NumSectors.ToString("N0")
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Data Cylinders",
                Value = DataCylinders.ToString("N0")
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Interleave",
                Value = Interleave.ToString()
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Total Sectors",
                Value = TotalSectorsFormatted
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "RPM",
                Value = Rpm > 0 ? $"{Rpm} RPM" : "Unknown"
            });

            // Validation section
            properties.Add(new MetadataProperty
            {
                Category = "Validation",
                Label = "Magic Number",
                Value = $"0x{Magic:X4} {(IsMagicValid ? "✓ Valid" : "❌ Invalid")}",
                Icon = IsMagicValid ? "✓" : "❌"
            });

            // Partitions section
            if (Partitions.Count > 0)
            {
                for (int i = 0; i < Partitions.Count; i++)
                {
                    var part = Partitions[i];
                    properties.Add(new MetadataProperty
                    {
                        Category = "Partitions",
                        Label = $"Partition {part.Letter}",
                        Value = $"{part.Tag} - {part.SizeFormatted} @ {part.OffsetFormatted}"
                    });
                }
            }

            return properties;
        }
    }

    public sealed class SunPartitionMetadata
    {
        public string Letter { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeFormatted { get; set; } = string.Empty;
        public uint StartCylinder { get; set; }
        public string OffsetFormatted { get; set; } = string.Empty;
    }
}
