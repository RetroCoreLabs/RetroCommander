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
        /// <inheritdoc/>
        public string FileSystemType => "FFS (Sun Disk Label)";

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

        // Sun-specific fields
        /// <summary>
        /// Text label stored in the Sun disk label. May be empty.
        /// </summary>
        public string LabelText { get; set; } = string.Empty;

        // Geometry
        /// <summary>
        /// Number of cylinders, from the disk label. Shown as the cylinder count.
        /// </summary>
        public ushort NumCylinders { get; set; }

        /// <summary>
        /// Number of alternate cylinders, from the disk label.
        /// </summary>
        public ushort NumAltCylinders { get; set; }

        /// <summary>
        /// Number of heads (tracks per cylinder), from the disk label.
        /// </summary>
        public ushort NumHeads { get; set; }

        /// <summary>
        /// Sectors per track, from the disk label.
        /// </summary>
        public ushort NumSectors { get; set; }

        /// <summary>
        /// Sector interleave, from the disk label.
        /// </summary>
        public ushort Interleave { get; set; }

        /// <summary>
        /// Number of data cylinders, from the disk label.
        /// </summary>
        public ushort DataCylinders { get; set; }

        /// <summary>
        /// Total number of sectors, as worked out by the disk label reader from its geometry.
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
        public ushort Rpm { get; set; }

        // Validation
        /// <summary>
        /// Magic number read from the disk label.
        /// </summary>
        public ushort Magic { get; set; }

        /// <summary>
        /// True when <see cref="Magic"/> equals the Sun disk label magic <c>0xDABE</c>.
        /// </summary>
        public bool IsMagicValid { get; set; }

        // Partitions
        /// <summary>
        /// Partitions listed by the Sun disk label.
        /// </summary>
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

    /// <summary>
    /// One partition from a Sun disk label, for UI display.
    /// </summary>
    public sealed class SunPartitionMetadata
    {
        /// <summary>
        /// Partition letter in upper case.
        /// </summary>
        public string Letter { get; set; } = string.Empty;

        /// <summary>
        /// Name of the partition's tag from the disk label.
        /// </summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>
        /// Partition size in bytes, counted at 512 bytes per sector.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// <see cref="SizeBytes"/> as display text in B, KB or MB.
        /// </summary>
        public string SizeFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Cylinder the partition starts on.
        /// </summary>
        public uint StartCylinder { get; set; }

        /// <summary>
        /// Start of the partition as display text, giving both the cylinder and the LBA sector,
        /// such as "Cyl 0, LBA 0".
        /// </summary>
        public string OffsetFormatted { get; set; } = string.Empty;
    }
}
