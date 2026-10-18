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
    /// Metadata about an NDFS disk image (read-only DTO for UI display).
    /// </summary>
    public sealed class NdfsMetadata : IDiskMetadata
    {
        /// <inheritdoc/>
        public string FileSystemType => "NDFS (Nord Disk File System)";

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

        // Volume Info
        /// <summary>
        /// Directory name stored in the master block.
        /// </summary>
        public string DirectoryName { get; set; } = string.Empty;

        /// <summary>
        /// Volume label shown in the UI. Filled with the same value as <see cref="DirectoryName"/>.
        /// </summary>
        public string VolumeLabel { get; set; } = string.Empty;

        /// <summary>
        /// Readable guess at the disk kind, such as a FLOMON floppy or a hard disk named from its
        /// page count.
        /// </summary>
        public string DiskType { get; set; } = string.Empty;
        /// <summary>
        /// Full path to the physical disk image backing this mount.
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;

        // Capacity
        /// <summary>
        /// <see cref="TotalCapacityPages"/> in MB, counted at 2048 bytes per page. Only set when
        /// <see cref="HasExtendedInfo"/> is true.
        /// </summary>
        public double TotalCapacityMB { get; set; }

        /// <summary>
        /// Pages available on the volume, from the master block's extended info. Only set when
        /// <see cref="HasExtendedInfo"/> is true.
        /// </summary>
        public uint TotalCapacityPages { get; set; }

        /// <summary>
        /// <see cref="UsedSpacePages"/> in MB, counted at 2048 bytes per page. Only set when
        /// <see cref="HasExtendedInfo"/> is true.
        /// </summary>
        public double UsedSpaceMB { get; set; }

        /// <summary>
        /// <see cref="TotalCapacityPages"/> minus <see cref="FreeSpacePages"/>. Only set when
        /// <see cref="HasExtendedInfo"/> is true.
        /// </summary>
        public uint UsedSpacePages { get; set; }

        /// <summary>
        /// <see cref="FreeSpacePages"/> in MB, counted at 2048 bytes per page.
        /// </summary>
        public double FreeSpaceMB { get; set; }

        /// <summary>
        /// Unreserved pages, from the master block.
        /// </summary>
        public uint FreeSpacePages { get; set; }

        /// <summary>
        /// <see cref="UsedSpacePages"/> as a percentage of <see cref="TotalCapacityPages"/>, from
        /// 0 to 100. Only set when <see cref="HasExtendedInfo"/> is true.
        /// </summary>
        public double UsedPercent { get; set; }

        /// <summary>
        /// True when the master block's extended info is valid. Without it the total and used
        /// capacity are not known.
        /// </summary>
        public bool HasExtendedInfo { get; set; }

        // System Info
        /// <summary>
        /// The system number stored in the extended-info block (word 1755B) — the system that has,
        /// or last had, the directory entered. 0 means "no owning system recorded" (the kernel's
        /// enter path treats 0 as no owner). This is the ONLY system identifier NDFS stores; the
        /// old "CPU Serial" field was a phantom that was never parsed and always read 0.
        /// </summary>
        public uint? CpuSystemNumber { get; set; }

        /// <summary>
        /// Name of the boot format the file system detected on the volume.
        /// </summary>
        public string BootFormat { get; set; } = string.Empty;
        /// <summary>
        /// Boot controller family from the raw bootstrap (Floppy/SMD-ECC/Winchester/SCSI), or "Unknown".
        /// </summary>
        public string BootControllerType { get; set; } = string.Empty;

        /// <summary>
        /// True when the file system reports the volume as bootable.
        /// </summary>
        public bool IsBootable { get; set; }

        // "Entered" interlock (extended-info flag word bit 15)
        /// <summary>
        /// True when the directory is left "entered/in use" (flag-word bit 15 set).
        /// </summary>
        public bool IsEntered { get; set; }
        /// <summary>
        /// The system number that has the directory entered (only meaningful when <see cref="IsEntered"/>).
        /// NOTE: a stored value of 0 means "no owning system recorded" — the kernel's enter path
        /// (CHDSI 37763B, 040113-040114) treats system number 0 as "no owner" and lets any system
        /// take the directory. So 0 must NOT be reported as "entered by system 0".
        /// </summary>
        public uint? EnteredBySystemNumber { get; set; }

        /// <summary>
        /// Human-readable "entered" state, honouring the kernel's system-number-0 = no-owner rule.
        /// </summary>
        public string EnteredDescription
        {
            get
            {
                if (!IsEntered)
                    return "No (cleanly released)";

                // Flag bit 15 set but no owner stamped: the volume was left entered without a
                // recorded owning system (system number 0 = no owner per CHDSI).
                if (!EnteredBySystemNumber.HasValue || EnteredBySystemNumber.Value == 0)
                    return "Yes — no owning system recorded (system number 0)";

                return $"Yes — entered by system {EnteredBySystemNumber.Value}";
            }
        }
        /// <summary>
        /// Raw extended-info flag word (word 1754B).
        /// </summary>
        public ushort FlagWord { get; set; }

        // Bitmap / allocation (authoritative live free/used from the bit file)
        /// <summary>
        /// Number of device pages the bit file covers. Zero when no bit file was read.
        /// </summary>
        public uint BitmapTotalPages { get; set; }

        /// <summary>
        /// Number of pages marked used in the bit file.
        /// </summary>
        public uint BitmapUsedPages { get; set; }

        /// <summary>
        /// Number of pages marked free in the bit file.
        /// </summary>
        public uint BitmapFreePages { get; set; }
        /// <summary>
        /// Bit file's own size in pages (contiguous span).
        /// </summary>
        public uint BitFileSizePages { get; set; }
        /// <summary>
        /// Highest allocatable page + 1 (declared capacity ceiling); may differ from device size.
        /// </summary>
        public uint AllocCeilingPages { get; set; }

        /// <summary>
        /// Raw allocation bitmap bytes (1 bit per page, LSB-first within each byte: page N is
        /// bit N%8 of byte N/8). Used to render the visual allocation map.
        /// </summary>
        public byte[] BitmapData { get; set; } = System.Array.Empty<byte>();

        // Users
        /// <summary>
        /// One entry per user found in the user file.
        /// </summary>
        public List<NdfsUserInfo> Users { get; set; } = new List<NdfsUserInfo>();

        // Technical Details
        /// <summary>
        /// Block number of the object file, from the master block's pointer, or null when the
        /// pointer is missing.
        /// </summary>
        public uint? ObjectFileBlock { get; set; }

        /// <summary>
        /// Block number of the user file, from the master block's pointer, or null when the
        /// pointer is missing.
        /// </summary>
        public uint? UserFileBlock { get; set; }

        /// <summary>
        /// Block number of the bit file, from the master block's pointer, or null when the
        /// pointer is missing.
        /// </summary>
        public uint? BitFileBlock { get; set; }

        /// <summary>
        /// Master block checksum state as display text: valid, invalid or "Unknown".
        /// </summary>
        public string ChecksumStatus { get; set; } = string.Empty;

        /// <summary>
        /// True when the master block CRC checks out.
        /// </summary>
        public bool ChecksumValid { get; set; }

        /// <summary>
        /// Converts NDFS metadata to generic display properties.
        /// </summary>
        public List<MetadataProperty> GetDetailedProperties()
        {
            var properties = new List<MetadataProperty>();

            // Volume Information section
            properties.Add(new MetadataProperty
            {
                Category = "Volume Information",
                Label = "Directory Name",
                Value = DirectoryName
            });
            properties.Add(new MetadataProperty
            {
                Category = "Volume Information",
                Label = "Volume Label",
                Value = VolumeLabel
            });
            properties.Add(new MetadataProperty
            {
                Category = "Volume Information",
                Label = "Disk Type",
                Value = DiskType
            });
            properties.Add(new MetadataProperty
            {
                Category = "Volume Information",
                Label = "Image Path",
                Value = ImagePath
            });
            properties.Add(new MetadataProperty
            {
                Category = "Volume Information",
                Label = "File Size",
                Value = FileSizeFormatted
            });

            // Capacity section
            if (HasExtendedInfo)
            {
                properties.Add(new MetadataProperty
                {
                    Category = "Capacity",
                    Label = "Total Capacity",
                    Value = $"{TotalCapacityMB:F2} MB ({TotalCapacityPages:N0} pages)"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Capacity",
                    Label = "Used Space",
                    Value = $"{UsedSpaceMB:F2} MB ({UsedSpacePages:N0} pages, {UsedPercent:F1}%)"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Capacity",
                    Label = "Free Space",
                    Value = $"{FreeSpaceMB:F2} MB ({FreeSpacePages:N0} pages)"
                });
            }
            else
            {
                properties.Add(new MetadataProperty
                {
                    Category = "Capacity",
                    Label = "Free Space",
                    Value = $"{FreeSpaceMB:F2} MB ({FreeSpacePages:N0} pages)"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Capacity",
                    Label = "Extended Info",
                    Value = "Not available",
                    Icon = "⚠️"
                });
            }

            // System Information section. The system number (word 1755B) is the ONLY system
            // identifier NDFS stores — 0 means no owning system was recorded.
            if (CpuSystemNumber.HasValue)
            {
                properties.Add(new MetadataProperty
                {
                    Category = "System Information",
                    Label = "Owning System Number",
                    Value = CpuSystemNumber.Value == 0
                        ? "0 (none recorded)"
                        : CpuSystemNumber.Value.ToString()
                });
            }

            properties.Add(new MetadataProperty
            {
                Category = "System Information",
                Label = "Boot Format",
                Value = BootFormat
            });
            properties.Add(new MetadataProperty
            {
                Category = "System Information",
                Label = "Boot Controller",
                Value = BootControllerType
            });
            properties.Add(new MetadataProperty
            {
                Category = "System Information",
                Label = "Bootable",
                Value = IsBootable ? "Yes ✓" : "No",
                Icon = IsBootable ? "✓" : null
            });
            properties.Add(new MetadataProperty
            {
                Category = "System Information",
                Label = "Entered / In Use",
                Value = EnteredDescription,
                Icon = IsEntered ? "🔒" : null
            });

            // Allocation (bitmap) section — the bit file is the authoritative free/used map.
            if (BitmapTotalPages > 0)
            {
                properties.Add(new MetadataProperty
                {
                    Category = "Allocation (Bitmap)",
                    Label = "Used Blocks",
                    Value = $"{BitmapUsedPages:N0} pages"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Allocation (Bitmap)",
                    Label = "Free Blocks",
                    Value = $"{BitmapFreePages:N0} pages"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Allocation (Bitmap)",
                    Label = "Bitmap Covers",
                    Value = $"{BitmapTotalPages:N0} device pages"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Allocation (Bitmap)",
                    Label = "Allocatable Up To",
                    Value = $"{AllocCeilingPages:N0} pages (declared capacity ceiling)"
                });
                properties.Add(new MetadataProperty
                {
                    Category = "Allocation (Bitmap)",
                    Label = "Bit File Size",
                    Value = $"{BitFileSizePages:N0} pages"
                });
            }

            // Technical Details section
            if (ObjectFileBlock.HasValue)
            {
                properties.Add(new MetadataProperty
                {
                    Category = "Technical Details",
                    Label = "Object File Block",
                    Value = ObjectFileBlock.Value.ToString()
                });
            }
            if (UserFileBlock.HasValue)
            {
                properties.Add(new MetadataProperty
                {
                    Category = "Technical Details",
                    Label = "User File Block",
                    Value = UserFileBlock.Value.ToString()
                });
            }
            if (BitFileBlock.HasValue)
            {
                properties.Add(new MetadataProperty
                {
                    Category = "Technical Details",
                    Label = "Bit File Block",
                    Value = BitFileBlock.Value.ToString()
                });
            }

            // Validation section
            properties.Add(new MetadataProperty
            {
                Category = "Validation",
                Label = "Checksum",
                Value = ChecksumStatus,
                Icon = ChecksumValid ? "✓" : "❌"
            });

            return properties;
        }
    }

    /// <summary>
    /// Read-only per-user info for the NDFS disk info panel.
    /// </summary>
    public sealed class NdfsUserInfo
    {
        /// <summary>
        /// The user's index number in the user file.
        /// </summary>
        public int UserIndex { get; set; }

        /// <summary>
        /// The user's name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Number of pages reserved for the user (the quota).
        /// </summary>
        public uint PagesReserved { get; set; }

        /// <summary>
        /// Number of pages the user's files take up.
        /// </summary>
        public uint PagesUsed { get; set; }

        /// <summary>
        /// Quota remaining = reserved − used (clamped at 0).
        /// </summary>
        public uint PagesRemaining { get; set; }

        /// <summary>
        /// Enter count stored in the user's entry.
        /// </summary>
        public byte EnterCount { get; set; }
    }
}
