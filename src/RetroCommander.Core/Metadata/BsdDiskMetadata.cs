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
        /// <inheritdoc/>
        public string FileSystemType => "BSD V7 / FFS";

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

        // BSD-specific fields
        /// <summary>
        /// Disk type name from the BSD disk label, trimmed, or "Unknown" when the label has none.
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// Pack name from the BSD disk label, trimmed, or "Unknown" when the label has none.
        /// </summary>
        public string PackName { get; set; } = string.Empty;

        /// <summary>
        /// Readable name of the drive type code in the disk label, such as "SCSI" or "IDE".
        /// </summary>
        public string DriveType { get; set; } = string.Empty;

        // Geometry
        /// <summary>
        /// Sector size in bytes, from the disk label.
        /// </summary>
        public int BytesPerSector { get; set; }

        /// <summary>
        /// Sectors per track, from the disk label.
        /// </summary>
        public uint SectorsPerTrack { get; set; }

        /// <summary>
        /// Tracks per cylinder, from the disk label.
        /// </summary>
        public uint TracksPerCylinder { get; set; }

        /// <summary>
        /// Number of cylinders on the drive, from the disk label.
        /// </summary>
        public uint CylindersPerUnit { get; set; }

        /// <summary>
        /// Total number of sectors on the drive, from the disk label.
        /// </summary>
        public uint TotalSectors { get; set; }

        /// <summary>
        /// <see cref="TotalSectors"/> as display text, with the size in MB counted at 512 bytes
        /// per sector.
        /// </summary>
        public string TotalSectorsFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Drive rotation speed in revolutions per minute, from the disk label. Zero means unknown.
        /// </summary>
        public ushort RPM { get; set; }

        // Validation
        /// <summary>
        /// Magic number read from the disk label.
        /// </summary>
        public uint Magic { get; set; }

        /// <summary>
        /// True when <see cref="Magic"/> equals the BSD disk label magic <c>0x82564557</c>.
        /// </summary>
        public bool IsMagicValid { get; set; }

        /// <summary>
        /// True when the disk label passes its own validity check. Shown as the checksum line.
        /// </summary>
        public bool IsChecksumValid { get; set; }

        // Partitions
        /// <summary>
        /// Partitions a to h from the disk label that have a non-zero size.
        /// </summary>
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

    /// <summary>
    /// One partition from a BSD disk label, for UI display.
    /// </summary>
    public sealed class BsdPartitionMetadata
    {
        /// <summary>
        /// Partition letter in upper case, A to H.
        /// </summary>
        public string Letter { get; set; } = string.Empty;

        /// <summary>
        /// Usual role of the partition letter (such as "Root" or "Swap") followed by its
        /// filesystem type in brackets.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Partition size in bytes, counted at 512 bytes per sector.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// <see cref="SizeBytes"/> as display text in B, KB or MB.
        /// </summary>
        public string SizeFormatted { get; set; } = string.Empty;

        /// <summary>
        /// First sector of the partition (LBA).
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// <see cref="Offset"/> as display text, such as "LBA 16".
        /// </summary>
        public string OffsetFormatted { get; set; } = string.Empty;
    }
}
