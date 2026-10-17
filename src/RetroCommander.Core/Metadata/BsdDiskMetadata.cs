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
    /// Metadata about a BSD disk image (read-only DTO for UI display).
    /// </summary>
    public sealed class BsdDiskMetadata : IDiskMetadata
    {
        public string FileSystemType => "BSD V7 / FFS";
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }

        // BSD-specific fields
        public string TypeName { get; set; } = string.Empty;
        public string PackName { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;

        // Geometry
        public int BytesPerSector { get; set; }
        public uint SectorsPerTrack { get; set; }
        public uint TracksPerCylinder { get; set; }
        public uint CylindersPerUnit { get; set; }
        public uint TotalSectors { get; set; }
        public string TotalSectorsFormatted { get; set; } = string.Empty;
        public ushort RPM { get; set; }

        // Validation
        public uint Magic { get; set; }
        public bool IsMagicValid { get; set; }
        public bool IsChecksumValid { get; set; }

        // Partitions
        public List<BsdPartitionMetadata> Partitions { get; set; } = new List<BsdPartitionMetadata>();

        /// <summary>
        /// Converts BSD metadata to generic display properties.
        /// </summary>
        public List<MetadataProperty> GetDetailedProperties()
        {
            var properties = new List<MetadataProperty>();

            // Disk Information section
            properties.Add(new MetadataProperty
            {
                Category = "Disk Information",
                Label = "Type Name",
                Value = TypeName,
                IsSectionHeader = false
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Information",
                Label = "Pack Name",
                Value = PackName
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Information",
                Label = "Drive Type",
                Value = DriveType
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
                Label = "Bytes per Sector",
                Value = $"{BytesPerSector} bytes"
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Sectors per Track",
                Value = SectorsPerTrack.ToString("N0")
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Tracks per Cylinder",
                Value = TracksPerCylinder.ToString("N0")
            });
            properties.Add(new MetadataProperty
            {
                Category = "Disk Geometry",
                Label = "Cylinders per Unit",
                Value = CylindersPerUnit.ToString("N0")
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
                Value = RPM > 0 ? $"{RPM} RPM" : "Unknown"
            });

            // Validation section
            properties.Add(new MetadataProperty
            {
                Category = "Validation",
                Label = "Magic Number",
                Value = $"0x{Magic:X8} {(IsMagicValid ? "✓ Valid" : "❌ Invalid")}",
                Icon = IsMagicValid ? "✓" : "❌"
            });
            properties.Add(new MetadataProperty
            {
                Category = "Validation",
                Label = "Checksum",
                Value = IsChecksumValid ? "Valid ✓" : "Invalid ❌",
                Icon = IsChecksumValid ? "✓" : "❌"
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
                        Value = $"{part.Name} - {part.SizeFormatted} @ {part.OffsetFormatted}"
                    });
                }
            }

            return properties;
        }
    }

    public sealed class BsdPartitionMetadata
    {
        public string Letter { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string SizeFormatted { get; set; } = string.Empty;
        public uint Offset { get; set; }
        public string OffsetFormatted { get; set; } = string.Empty;
    }
}
