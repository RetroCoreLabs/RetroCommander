//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using RetroCommander.Core.Metadata;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.BlockDevices;
using RetroIO.Storage.Local.BSD.Partitions;
using RetroIO.Storage.Local.Sun.Partitions;
using RetroIO.Storage.Ndfs.BlockDevices;
using RetroIO.Storage.Ndfs.Drivers;
using RetroIO.Storage.Ndfs.FileSystems;
using RetroIO.Storage.Ndfs.Structures;

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// Service for reading filesystem metadata without full mounting.
    /// Keeps business logic OUT of UI layer.
    /// </summary>
    public sealed class FileSystemMetadataService
    {
        private readonly DriverFactory _driverFactory;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="driverFactory">
        /// The driver factory used to detect the format of an image.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="driverFactory"/> is null.
        /// </exception>
        public FileSystemMetadataService(DriverFactory driverFactory)
        {
            _driverFactory = driverFactory ?? throw new ArgumentNullException(nameof(driverFactory));
        }

        /// <summary>
        /// Reads BSD disk metadata from an image file.
        /// </summary>
        public BsdDiskMetadata? ReadBsdMetadata(string imagePath)
        {
            try
            {
                var metadata = new BsdDiskMetadata();
                var fileInfo = new FileInfo(imagePath);
                metadata.FileSizeBytes = fileInfo.Length;
                metadata.FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)";

                // Create block device and read disk label
                var descriptor = new BlockDeviceDescriptor
                {
                    Name = "BSD Disk",
                    BlockSize = 512,
                    AddressMode = BlockAddressMode.LBA,
                    TotalBlocks = (uint)(fileInfo.Length / 512)
                };

                using var stream = File.OpenRead(imagePath);
                var device = new GenericBlockDevice(descriptor);
                device.Mount(stream, readOnly: true);

                var partMgr = new BsdPartitionManager(device);
                var diskLabel = partMgr.GetDiskLabel();

                if (diskLabel == null || !diskLabel.IsValid())
                {
                    metadata.HasErrors = true;
                    metadata.ErrorMessage = "No valid BSD disk label found";
                    metadata.Warnings.Add("⚠️ No valid BSD disk label found. This may be a raw filesystem image.");
                    return metadata;
                }

                // Populate metadata
                metadata.TypeName = string.IsNullOrWhiteSpace(diskLabel.TypeName) ? "Unknown" : diskLabel.TypeName.Trim();
                metadata.PackName = string.IsNullOrWhiteSpace(diskLabel.PackName) ? "Unknown" : diskLabel.PackName.Trim();
                metadata.DriveType = GetDriveTypeName(diskLabel.DriveType);
                metadata.BytesPerSector = diskLabel.BytesPerSector;
                metadata.SectorsPerTrack = diskLabel.SectorsPerTrack;
                metadata.TracksPerCylinder = diskLabel.TracksPerCylinder;
                metadata.CylindersPerUnit = diskLabel.CylindersPerUnit;
                metadata.TotalSectors = diskLabel.SectorsPerUnit;
                metadata.TotalSectorsFormatted = $"{diskLabel.SectorsPerUnit:N0} sectors ({diskLabel.SectorsPerUnit * 512.0 / (1024.0 * 1024.0):F2} MB)";
                metadata.RPM = diskLabel.RPM;
                metadata.Magic = diskLabel.Magic;
                metadata.IsMagicValid = diskLabel.Magic == 0x82564557;
                metadata.IsChecksumValid = diskLabel.IsValid();

                // Read partitions
                for (char letter = 'a'; letter <= 'h'; letter++)
                {
                    var partition = partMgr.GetPartition(letter);
                    if (partition != null && partition.Value.Size > 0)
                    {
                        var part = partition.Value;
                        string conventionalName = GetPartitionName(letter);
                        string filesystemType = part.FileSystemTypeName;

                        // Combine conventional name with actual filesystem type
                        string displayName = $"{conventionalName} ({filesystemType})";

                        metadata.Partitions.Add(new BsdPartitionMetadata
                        {
                            Letter = letter.ToString().ToUpper(),
                            Name = displayName,
                            SizeBytes = part.Size * 512,
                            SizeFormatted = FormatSize(part.Size * 512),
                            Offset = part.Offset,
                            OffsetFormatted = $"LBA {part.Offset}"
                        });
                    }
                }

                // Collect warnings
                if (diskLabel.Magic != 0x82564557)
                    metadata.Warnings.Add("❌ Invalid magic number - disk label may be corrupted");
                if (!diskLabel.IsValid())
                    metadata.Warnings.Add("❌ Disk label validation failed");
                if (diskLabel.SectorsPerUnit == 0)
                    metadata.Warnings.Add("⚠️ Zero sectors reported - geometry may be invalid");
                if (string.IsNullOrWhiteSpace(diskLabel.TypeName))
                    metadata.Warnings.Add("⚠️ Type name is empty");

                return metadata;
            }
            catch (Exception ex)
            {
                return new BsdDiskMetadata
                {
                    HasErrors = true,
                    ErrorMessage = $"Failed to read BSD metadata: {ex.Message}",
                    Warnings = { $"❌ Failed to read BSD disk label:\n{ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Reads disk metadata from any supported filesystem type (auto-detects).
        /// </summary>
        public IDiskMetadata? ReadMetadata(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return new GenericDiskMetadata
                    {
                        FileSystemType = "Unknown",
                        FileSizeBytes = 0,
                        FileSizeFormatted = "File not found",
                        HasErrors = true,
                        ErrorMessage = $"File not found: {imagePath}"
                    };
                }

                var fileInfo = new FileInfo(imagePath);

                // Try detecting filesystem type
                var driver = _driverFactory.Detect(imagePath);
                if (driver == null)
                {
                    // No driver detected - return generic metadata
                    return new GenericDiskMetadata
                    {
                        FileSystemType = "Unknown",
                        FileSizeBytes = fileInfo.Length,
                        FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)",
                        Warnings = { "⚠️ Filesystem type could not be detected" }
                    };
                }

                // Route to appropriate metadata extractor based on driver type
                string fsType = driver.FileSystemType;

                // NDFS
                if (fsType.StartsWith("NDFS"))
                {
                    return ReadNdfsMetadata(imagePath);
                }

                // BSD V7 and FFS - check partition table type first
                if (fsType.StartsWith("BSD-V7") || fsType.StartsWith("FFS"))
                {
                    // Detect partition table type
                    System.Collections.Generic.List<RetroIO.Storage.Local.Partitions.PartitionInfo>? partitions = null;
                    RetroIO.Storage.Local.Partitions.PartitionTableType? tableType = null;

                    try
                    {
                        partitions = RetroIO.Storage.Local.Partitions.PartitionDetectionService.DetectPartitions(imagePath);
                        if (partitions != null && partitions.Count > 0)
                        {
                            tableType = partitions[0].TableType;
                        }
                    }
                    catch
                    {
                        // Partition detection failed - might be raw filesystem
                    }

                    // Only use ReadBsdMetadata if BSD Disk Label is present
                    if (tableType == RetroIO.Storage.Local.Partitions.PartitionTableType.BsdDiskLabel)
                    {
                        return ReadBsdMetadata(imagePath);
                    }

                    // Use ReadSunMetadata if Sun Disk Label is present
                    if (tableType == RetroIO.Storage.Local.Partitions.PartitionTableType.SunLabel)
                    {
                        return ReadSunMetadata(imagePath);
                    }

                    // For no partition table, use generic metadata
                    return new GenericDiskMetadata
                    {
                        FileSystemType = fsType,
                        FileSizeBytes = fileInfo.Length,
                        FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)",
                        Warnings = new System.Collections.Generic.List<string> { "ℹ️ No partition table detected (raw filesystem)" }
                    };
                }

                // Other filesystems - use generic metadata for now
                return new GenericDiskMetadata
                {
                    FileSystemType = fsType,
                    FileSizeBytes = fileInfo.Length,
                    FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)"
                };
            }
            catch (Exception ex)
            {
                return new GenericDiskMetadata
                {
                    FileSystemType = "Unknown",
                    FileSizeBytes = 0,
                    FileSizeFormatted = "Error reading file",
                    HasErrors = true,
                    ErrorMessage = $"Failed to read metadata: {ex.Message}",
                    Warnings = { $"❌ Failed to read metadata:\n{ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Reads NDFS metadata from an image file.
        /// </summary>
        public NdfsMetadata? ReadNdfsMetadata(string imagePath)
        {
            try
            {
                var fileInfo = new FileInfo(imagePath);

                // Use driver detection to mount NDFS
                var driver = _driverFactory.Detect(imagePath);
                if (driver == null || driver is not NdfsDriver ndfsDriver)
                {
                    return new NdfsMetadata
                    {
                        FileSizeBytes = fileInfo.Length,
                        FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)",
                        HasErrors = true,
                        ErrorMessage = "Not a valid NDFS image"
                    };
                }

                using var stream = File.OpenRead(imagePath);
                ndfsDriver.Mount(stream, readOnly: true);
                var ndfs = ndfsDriver.GetFileSystem();
                if (ndfs == null)
                {
                    return new NdfsMetadata
                    {
                        FileSizeBytes = fileInfo.Length,
                        FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)",
                        HasErrors = true,
                        ErrorMessage = "Failed to mount NDFS filesystem"
                    };
                }

                var metadata = BuildNdfsMetadata(ndfs);
                metadata.FileSizeBytes = fileInfo.Length;
                metadata.FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)";

                return metadata;
            }
            catch (Exception ex)
            {
                return new NdfsMetadata
                {
                    FileSizeBytes = 0,
                    FileSizeFormatted = "Error reading file",
                    HasErrors = true,
                    ErrorMessage = $"Failed to read NDFS metadata: {ex.Message}",
                    Warnings = { $"❌ Failed to read NDFS metadata:\n{ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Reads NDFS metadata from an already-mounted filesystem.
        /// </summary>
        public NdfsMetadata BuildNdfsMetadata(INdfsFileSystem ndfs)
        {
            try
            {
                var metadata = new NdfsMetadata();
                var mb = ndfs.GetMasterBlock();

                // Volume Info
                metadata.DirectoryName = mb.DirectoryName;
                metadata.VolumeLabel = mb.DirectoryName;
                metadata.DiskType = DetectDiskType(mb);

                // Capacity
                metadata.HasExtendedInfo = mb.ExtValid;
                if (mb.ExtValid)
                {
                    metadata.TotalCapacityPages = mb.ExtPagesAvailable;
                    metadata.TotalCapacityMB = mb.ExtPagesAvailable * 2048.0 / (1024 * 1024);
                    metadata.FreeSpacePages = mb.UnreservedPages;
                    metadata.FreeSpaceMB = mb.UnreservedPages * 2048.0 / (1024 * 1024);
                    metadata.UsedSpacePages = mb.ExtPagesAvailable - mb.UnreservedPages;
                    metadata.UsedSpaceMB = metadata.UsedSpacePages * 2048.0 / (1024 * 1024);
                    metadata.UsedPercent = (metadata.UsedSpacePages / (double)mb.ExtPagesAvailable) * 100;
                }
                else
                {
                    metadata.FreeSpacePages = mb.UnreservedPages;
                    metadata.FreeSpaceMB = mb.UnreservedPages * 2048.0 / (1024 * 1024);
                }

                // System Info
                if (mb.ExtValid)
                {
                    metadata.CpuSystemNumber = mb.ExtLastSystemNumber;
                }

                metadata.BootFormat = ndfs.DetectBootFormat().ToString();
                metadata.BootControllerType = ndfs.DetectBootControllerType().ToString();
                metadata.IsBootable = ndfs.IsBootable();

                // "Entered" interlock (flag-word bit 15). Only meaningful with valid Extended Info.
                metadata.FlagWord = (ushort)mb.ExtFlagWord;
                metadata.IsEntered = mb.ExtValid && mb.IsDirectoryEntered;
                if (metadata.IsEntered)
                    metadata.EnteredBySystemNumber = mb.ExtLastSystemNumber;

                // Bitmap / allocation — the bit file is the authoritative live free/used map.
                var bitFile = ndfs.GetBitFile();
                if (bitFile != null)
                {
                    metadata.BitmapTotalPages = bitFile.TotalPages;
                    metadata.BitmapUsedPages = bitFile.CalcUsedPages();
                    metadata.BitmapFreePages = bitFile.GetFreePages();
                    metadata.BitFileSizePages = bitFile.BitBlocks;
                    metadata.AllocCeilingPages = bitFile.AllocCeiling;
                    metadata.BitmapData = bitFile.GetBitmapData();
                }

                // Users
                var users = ndfs.GetUsers();
                if (users != null)
                {
                    for (int i = 0; i < users.Count; i++)
                    {
                        var u = users[i];
                        uint remaining = u.PagesReserved > u.PagesUsed ? u.PagesReserved - u.PagesUsed : 0;
                        metadata.Users.Add(new NdfsUserInfo
                        {
                            UserIndex = u.UserIndex,
                            UserName = u.UserName,
                            PagesReserved = u.PagesReserved,
                            PagesUsed = u.PagesUsed,
                            PagesRemaining = remaining,
                            EnterCount = u.EnterCount
                        });
                    }
                }

                // Technical Details
                metadata.ObjectFileBlock = mb.ObjectFilePointer?.BlockID;
                metadata.UserFileBlock = mb.UserFilePointer?.BlockID;
                metadata.BitFileBlock = mb.BitFilePointer?.BlockID;
                metadata.ChecksumStatus = GetChecksumStatus(mb.ChecksumState);
                metadata.ChecksumValid = mb.ChecksumState == ChecksumValidationState.CrcValid;

                // Collect warnings
                if (mb.ChecksumState == ChecksumValidationState.CrcInvalid)
                    metadata.Warnings.Add("❌ Invalid checksum - disk may be corrupted");
                if (mb.HasFlomon)
                    metadata.Warnings.Add("⚠️ Floppy disk (FLOMON format) - limited features");
                if (!mb.ExtValid)
                    metadata.Warnings.Add("⚠️ Extended Info not available - capacity may be inaccurate");
                if (!metadata.IsBootable)
                    metadata.Warnings.Add("⚠️ Disk is not bootable");
                if (mb.ExtReserved1 != 0 || mb.ExtReserved2 != 0 || mb.ExtReserved3 != 0)
                    metadata.Warnings.Add("⚠️ Reserved fields non-zero (unusual)");

                return metadata;
            }
            catch (Exception ex)
            {
                return new NdfsMetadata
                {
                    HasErrors = true,
                    ErrorMessage = $"Failed to build NDFS metadata: {ex.Message}",
                    Warnings = { $"❌ Failed to read NDFS metadata:\n{ex.Message}" }
                };
            }
        }

        // Helper methods
        private string GetDriveTypeName(byte driveType)
        {
            return driveType switch
            {
                0 => "SMD (Storage Module Device)",
                1 => "MSCP (Mass Storage Control Protocol)",
                2 => "SCSI",
                3 => "IDE",
                _ => $"Unknown (0x{driveType:X2})"
            };
        }

        private string GetPartitionName(char letter)
        {
            return letter switch
            {
                'a' => "Root",
                'b' => "Swap",
                'c' => "Whole Disk",
                'd' => "Usr",
                'e' => "Var",
                'f' => "Home",
                'g' => "Tmp",
                'h' => "Other",
                _ => "Unknown"
            };
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        private string GetChecksumStatus(ChecksumValidationState state)
        {
            return state switch
            {
                ChecksumValidationState.CrcValid => "Valid ✓",
                ChecksumValidationState.CrcInvalid => "Invalid ❌",
                _ => "Unknown"
            };
        }

        private string DetectDiskType(MasterBlock mb)
        {
            if (mb.HasFlomon)
                return "Floppy Disk (FLOMON)";

            if (!mb.ExtValid)
                return "Unknown (No Extended Info)";

            string diskType = mb.ExtPagesAvailable switch
            {
                36945 => "DISC-75MB-1 (SMD)",
                36396 => "DISC-74MB-1 (Winchester)",
                61036 => "Unknown SCSI (~119 MB)",
                _ => $"Unknown ({mb.ExtPagesAvailable} pages)"
            };

            return $"Hard Disk ({diskType})";
        }

        /// <summary>
        /// Reads Sun Disk Label metadata from an image file.
        /// </summary>
        public SunDiskMetadata? ReadSunMetadata(string imagePath)
        {
            try
            {
                var metadata = new SunDiskMetadata();
                var fileInfo = new FileInfo(imagePath);
                metadata.FileSizeBytes = fileInfo.Length;
                metadata.FileSizeFormatted = $"{fileInfo.Length:N0} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)";

                // Create block device and read Sun disk label
                var descriptor = new BlockDeviceDescriptor
                {
                    Name = "Sun Disk",
                    BlockSize = 512,
                    AddressMode = BlockAddressMode.LBA,
                    TotalBlocks = (uint)(fileInfo.Length / 512)
                };

                using var stream = File.OpenRead(imagePath);
                var device = new GenericBlockDevice(descriptor);
                device.Mount(stream, readOnly: true);

                var partMgr = new SunPartitionManager(device);
                var diskLabel = partMgr.GetDiskLabel();

                if (diskLabel == null || !diskLabel.IsValid())
                {
                    metadata.HasErrors = true;
                    metadata.ErrorMessage = "No valid Sun disk label found";
                    metadata.Warnings.Add("⚠️ No valid Sun disk label found. This may be a raw filesystem image.");
                    return metadata;
                }

                // Populate metadata
                metadata.LabelText = diskLabel.LabelText;
                metadata.NumCylinders = diskLabel.NumCylinders;
                metadata.NumAltCylinders = diskLabel.NumAltCylinders;
                metadata.NumHeads = diskLabel.NumHeads;
                metadata.NumSectors = diskLabel.NumSectors;
                metadata.Interleave = diskLabel.Interleave;
                metadata.DataCylinders = diskLabel.DataCylinders;
                metadata.TotalSectors = diskLabel.GetTotalSectors();
                metadata.TotalSectorsFormatted = $"{metadata.TotalSectors:N0} sectors ({metadata.TotalSectors * 512.0 / (1024.0 * 1024.0):F2} MB)";
                metadata.Rpm = diskLabel.Rpm;
                metadata.Magic = diskLabel.Magic;
                metadata.IsMagicValid = diskLabel.Magic == 0xDABE;

                // Read partitions
                var sunPartitions = partMgr.ListPartitions();
                for (int i = 0; i < sunPartitions.Count; i++)
                {
                    var (letter, part) = sunPartitions[i];
                    uint startSector = partMgr.GetPartitionStartSector(part, diskLabel);

                    metadata.Partitions.Add(new SunPartitionMetadata
                    {
                        Letter = letter.ToString().ToUpper(),
                        Tag = part.GetTagName(),
                        SizeBytes = (long)part.NumSectors * 512,
                        SizeFormatted = FormatSize(part.NumSectors * 512),
                        StartCylinder = part.StartCylinder,
                        OffsetFormatted = $"Cyl {part.StartCylinder}, LBA {startSector}"
                    });
                }

                // Collect warnings
                if (diskLabel.Magic != 0xDABE)
                    metadata.Warnings.Add("❌ Invalid magic number - disk label may be corrupted");
                if (metadata.TotalSectors == 0)
                    metadata.Warnings.Add("⚠️ Zero sectors reported - geometry may be invalid");
                if (string.IsNullOrWhiteSpace(diskLabel.LabelText))
                    metadata.Warnings.Add("ℹ️ Label text is empty");

                return metadata;
            }
            catch (Exception ex)
            {
                return new SunDiskMetadata
                {
                    HasErrors = true,
                    ErrorMessage = $"Failed to read Sun metadata: {ex.Message}",
                    Warnings = { $"❌ Failed to read Sun disk label:\n{ex.Message}" }
                };
            }
        }
    }
}
