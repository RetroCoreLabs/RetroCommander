//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System.Collections.Generic;

namespace RetroCommander.Core.Metadata
{
    /// <summary>
    /// Metadata about an NDFS disk image (read-only DTO for UI display).
    /// </summary>
    public sealed class NdfsMetadata : IDiskMetadata
    {
        public string FileSystemType => "NDFS (Nord Disk File System)";
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }

        // Volume Info
        public string DirectoryName { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DiskType { get; set; } = string.Empty;
        /// <summary>Full path to the physical disk image backing this mount.</summary>
        public string ImagePath { get; set; } = string.Empty;

        // Capacity
        public double TotalCapacityMB { get; set; }
        public uint TotalCapacityPages { get; set; }
        public double UsedSpaceMB { get; set; }
        public uint UsedSpacePages { get; set; }
        public double FreeSpaceMB { get; set; }
        public uint FreeSpacePages { get; set; }
        public double UsedPercent { get; set; }
        public bool HasExtendedInfo { get; set; }

        // System Info
        /// <summary>
        /// The system number stored in the extended-info block (word 1755B) — the system that has,
        /// or last had, the directory entered. 0 means "no owning system recorded" (the kernel's
        /// enter path treats 0 as no owner). This is the ONLY system identifier NDFS stores; the
        /// old "CPU Serial" field was a phantom that was never parsed and always read 0.
        /// </summary>
        public uint? CpuSystemNumber { get; set; }
        public string BootFormat { get; set; } = string.Empty;
        /// <summary>Boot controller family from the raw bootstrap (Floppy/SMD-ECC/Winchester/SCSI), or "Unknown".</summary>
        public string BootControllerType { get; set; } = string.Empty;
        public bool IsBootable { get; set; }

        // "Entered" interlock (extended-info flag word bit 15)
        /// <summary>True when the directory is left "entered/in use" (flag-word bit 15 set).</summary>
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
        /// <summary>Raw extended-info flag word (word 1754B).</summary>
        public ushort FlagWord { get; set; }

        // Bitmap / allocation (authoritative live free/used from the bit file)
        public uint BitmapTotalPages { get; set; }
        public uint BitmapUsedPages { get; set; }
        public uint BitmapFreePages { get; set; }
        /// <summary>Bit file's own size in pages (contiguous span).</summary>
        public uint BitFileSizePages { get; set; }
        /// <summary>Highest allocatable page + 1 (declared capacity ceiling); may differ from device size.</summary>
        public uint AllocCeilingPages { get; set; }

        /// <summary>
        /// Raw allocation bitmap bytes (1 bit per page, LSB-first within each byte: page N is
        /// bit N%8 of byte N/8). Used to render the visual allocation map.
        /// </summary>
        public byte[] BitmapData { get; set; } = System.Array.Empty<byte>();

        // Users
        public List<NdfsUserInfo> Users { get; set; } = new List<NdfsUserInfo>();

        // Technical Details
        public uint? ObjectFileBlock { get; set; }
        public uint? UserFileBlock { get; set; }
        public uint? BitFileBlock { get; set; }
        public string ChecksumStatus { get; set; } = string.Empty;
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
        public int UserIndex { get; set; }
        public string UserName { get; set; } = string.Empty;
        public uint PagesReserved { get; set; }
        public uint PagesUsed { get; set; }
        /// <summary>Quota remaining = reserved − used (clamped at 0).</summary>
        public uint PagesRemaining { get; set; }
        public byte EnterCount { get; set; }
    }
}
