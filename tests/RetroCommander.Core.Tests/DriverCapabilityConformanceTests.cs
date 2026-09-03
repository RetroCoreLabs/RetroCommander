//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local.Drivers;
using RetroIO.Storage.Ndfs.Creation;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Gap-closing-plan item 1: a matrix test over every driver registered in
    /// <see cref="DriverFactoryBuilder.CreateWithAllDrivers"/>, asserting the driver's advertised
    /// CanWrite/Capabilities actually matches real behavior — if CanWrite==true, a real write must
    /// succeed and round-trip; if false, a real write attempt must actually throw/refuse, not
    /// silently no-op.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope, stated honestly rather than silently narrowed:</b> of the 12 registered drivers,
    /// 5 get a full mount + real read/write round-trip verification (NDFS, Commodore D64, PRG,
    /// P00, PC floppy/FAT12 — all formats simple/documented enough to synthesize a minimal valid
    /// image inline). The remaining 7 (T64, CRT, TAP, DragonDOS DSK, DragonDOS VDK, BSD V7, FFS/UFS)
    /// only get the cheaper self-consistency check (CanWrite must agree with the Capabilities.Write
    /// flag) because building a genuinely valid mountable image for those requires either deep
    /// format reverse-engineering (T64 directory records, CRT CHIP records, TAP pulse data, V7/FFS
    /// superblock+inode layout) or reusing synthetic-image builders that currently live as private
    /// methods in a different test project (RetroIO.Tests' DragonDskDriverTests/DragonVdkDriverTests)
    /// that this project doesn't reference. Extending those 7 to full round-trip coverage is a
    /// real, tracked gap — not assumed to be covered.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class DriverCapabilityConformanceTests
    {
        private sealed class ConformanceCase
        {
            public required string Name;
            public required Func<IFileSystemDriver> CreateDriver;
            public required bool ExpectedCanWrite;
            public Func<byte[]>? BuildMountableImage; // null => self-consistency-only case
            public string? RoundTripSkipReason;
        }

        private static List<ConformanceCase> BuildCases()
        {
            var cases = new List<ConformanceCase>();

            cases.Add(new ConformanceCase
            {
                Name = "NDFS",
                CreateDriver = () => new RetroIO.Storage.Ndfs.Drivers.NdfsDriver(),
                ExpectedCanWrite = true,
                BuildMountableImage = BuildMinimalNdfsImage
            });

            cases.Add(new ConformanceCase
            {
                Name = "Commodore D64",
                CreateDriver = () => new CommodoreDiskDriver(),
                ExpectedCanWrite = true,
                BuildMountableImage = () => RetroIO.Storage.Local.CbmImageFactory.CreateBlank(RetroIO.Storage.Local.CbmDiskType.D64, "PROBE", "01")
            });

            cases.Add(new ConformanceCase
            {
                Name = "PRG",
                CreateDriver = () => new ProgramFileDriver(),
                ExpectedCanWrite = false,
                BuildMountableImage = BuildMinimalPrg
            });

            cases.Add(new ConformanceCase
            {
                Name = "P00",
                CreateDriver = () => new ProgramFileDriver(),
                ExpectedCanWrite = false,
                BuildMountableImage = BuildMinimalP00
            });

            cases.Add(new ConformanceCase
            {
                Name = "PC Floppy (FAT12)",
                CreateDriver = () => new PcFloppyDriver(),
                ExpectedCanWrite = true,
                BuildMountableImage = BuildMinimalFat12Floppy
            });

            cases.Add(new ConformanceCase
            {
                Name = "T64",
                CreateDriver = () => new T64Driver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "T64 directory-record layout not reverse-engineered for this test; only self-consistency checked"
            });

            cases.Add(new ConformanceCase
            {
                Name = "CRT",
                CreateDriver = () => new CrtDriver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "CRT CHIP-record layout not reverse-engineered for this test; only self-consistency checked"
            });

            cases.Add(new ConformanceCase
            {
                Name = "TAP",
                CreateDriver = () => new TapDriver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "TAP pulse-data layout not reverse-engineered for this test; only self-consistency checked"
            });

            cases.Add(new ConformanceCase
            {
                Name = "DragonDOS DSK",
                CreateDriver = () => new DragonDskDriver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "Synthetic image builder exists but is a private method in RetroFS.Tests' DragonDskDriverTests, not reachable from this project"
            });

            cases.Add(new ConformanceCase
            {
                Name = "DragonDOS VDK",
                CreateDriver = () => new DragonVdkDriver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "Synthetic image builder exists but is a private method in RetroFS.Tests' DragonVdkDriverTests, not reachable from this project"
            });

            cases.Add(new ConformanceCase
            {
                Name = "BSD V7",
                CreateDriver = () => new V7Driver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "V7 superblock/inode layout not reverse-engineered for this test; only self-consistency checked"
            });

            cases.Add(new ConformanceCase
            {
                Name = "FFS/UFS",
                CreateDriver = () => new FfsDriver(),
                ExpectedCanWrite = false,
                RoundTripSkipReason = "FFS superblock/inode layout not reverse-engineered for this test; only self-consistency checked"
            });

            return cases;
        }

        [Test]
        public void AllRegisteredDrivers_CanWriteClaim_MatchesRealBehavior()
        {
            List<ConformanceCase> cases = BuildCases();
            List<string> failures = new List<string>();
            List<string> skipped = new List<string>();

            for (int i = 0; i < cases.Count; i++)
            {
                ConformanceCase c = cases[i];
                try
                {
                    if (c.BuildMountableImage != null)
                    {
                        // Self-consistency is checked on the SAME mounted instance as the
                        // round-trip: some drivers (e.g. CommodoreDiskDriver) only know their
                        // real CanWrite value once mounted (it's null/false-safe beforehand, but
                        // meaningless — checking it pre-mount would just always see "false"
                        // regardless of the image, which isn't the real behavior being verified).
                        VerifyRealRoundTrip(c, failures);
                    }
                    else
                    {
                        // No image available for this format — the only honest check left is
                        // that the driver's own two ways of expressing "can it write" (the
                        // CanWrite property and the Capabilities.Write flag) agree with each
                        // other pre-mount, which is safe for these 7 (all simple `=> false`
                        // properties, verified not to depend on mount state).
                        VerifySelfConsistency(c, failures);
                        skipped.Add($"{c.Name}: {c.RoundTripSkipReason}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{c.Name}: unexpected exception during conformance check - {ex}");
                }
            }

            TestContext.Out.WriteLine("Round-trip verification skipped for (self-consistency only):");
            for (int i = 0; i < skipped.Count; i++)
            {
                TestContext.Out.WriteLine($"  - {skipped[i]}");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private static void VerifySelfConsistency(ConformanceCase c, List<string> failures)
        {
            IFileSystemDriver driver = c.CreateDriver();
            bool capsWrite = driver.Capabilities.Has(DriverCapabilities.Write);

            if (driver.CanWrite != capsWrite)
            {
                failures.Add($"{c.Name}: CanWrite ({driver.CanWrite}) disagrees with Capabilities.Write flag ({capsWrite})");
            }

            if (driver.CanWrite != c.ExpectedCanWrite)
            {
                failures.Add($"{c.Name}: expected CanWrite={c.ExpectedCanWrite}, got {driver.CanWrite} — update this test's expectation if the driver intentionally changed capability");
            }
        }

        private static void VerifyRealRoundTrip(ConformanceCase c, List<string> failures)
        {
            byte[] image = c.BuildMountableImage!();
            IFileSystemDriver driver = c.CreateDriver();

            using (MemoryStream stream = new MemoryStream(image, writable: true))
            {
                driver.Mount(stream, readOnly: false);

                bool capsWrite = driver.Capabilities.Has(DriverCapabilities.Write);
                if (driver.CanWrite != capsWrite)
                {
                    failures.Add($"{c.Name}: CanWrite ({driver.CanWrite}) disagrees with Capabilities.Write flag ({capsWrite})");
                }
                if (driver.CanWrite != c.ExpectedCanWrite)
                {
                    failures.Add($"{c.Name}: expected CanWrite={c.ExpectedCanWrite}, got {driver.CanWrite} — update this test's expectation if the driver intentionally changed capability");
                }

                if (driver.CanWrite)
                {
                    byte[] probeData = { 0x01, 0x02, 0x03, 0x04, 0x05 };
                    string probePath = ResolveWritableProbePath(c.Name, driver);

                    driver.WriteFile(probePath, probeData);
                    byte[] readBack = driver.ReadFile(probePath);

                    if (!BytesEqual(readBack, probeData))
                    {
                        failures.Add($"{c.Name}: claims CanWrite=true, but written data did not read back identically");
                    }
                }
                else
                {
                    bool threw = false;
                    try
                    {
                        driver.WriteFile("PROBE", new byte[] { 0x01 });
                    }
                    catch
                    {
                        threw = true;
                    }

                    if (!threw)
                    {
                        failures.Add($"{c.Name}: claims CanWrite=false, but WriteFile did not throw — writes may silently no-op");
                    }
                }

                if (driver is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private static string ResolveWritableProbePath(string caseName, IFileSystemDriver driver)
        {
            if (caseName == "NDFS")
            {
                // NDFS root is a directory of Users — create one, then write inside it (mirrors
                // F7/Mkdir's real behavior: CreateDirectory at root level creates a User).
                driver.CreateDirectory("/PROBEUSR");
                return "/PROBEUSR/FILE.TXT";
            }

            // CBM DOS and FAT are both flat at the level these images are built at.
            return "PROBEFIL";
        }

        private static byte[] BuildMinimalNdfsImage()
        {
            // Use the real, production image-creation path (same one "New NDFS Disk" in the UI
            // calls) rather than hand-crafting bytes — this is the actual format the driver needs
            // to handle, not a guess at it.
            string tempPath = Path.Combine(Path.GetTempPath(), "conformance_" + Path.GetRandomFileName() + ".ndfs.img");
            try
            {
                NdfsImageCreator.CreateImageFromTemplate(
                    tempPath,
                    NdfsImageTemplate.Floppy360KB,
                    directoryName: "PROBE",
                    bootLoaderType: NdfsBootLoaderType.None);
                return File.ReadAllBytes(tempPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static byte[] BuildMinimalPrg()
        {
            // PRG format (per ProgramFileDriver): 2-byte LE load address + raw program bytes.
            byte[] data = new byte[2 + 8];
            data[0] = 0x01; // load address low byte
            data[1] = 0x08; // load address high byte (0x0801 = standard BASIC start)
            for (int i = 0; i < 8; i++)
            {
                data[2 + i] = (byte)(0xA0 + i);
            }
            return data;
        }

        private static byte[] BuildMinimalP00()
        {
            // P00 format (per ProgramFileDriver): "C64File" + \0 + 16-byte name + \0 + recsize
            // byte, payload (PRG-format: load address + data) starts at offset 0x1A.
            byte[] prg = BuildMinimalPrg();
            byte[] data = new byte[0x1A + prg.Length];
            byte[] magic = System.Text.Encoding.ASCII.GetBytes("C64File");
            Array.Copy(magic, data, magic.Length);
            byte[] name = System.Text.Encoding.ASCII.GetBytes("PROBE");
            Array.Copy(name, 0, data, 8, name.Length);
            Array.Copy(prg, 0, data, 0x1A, prg.Length);
            return data;
        }

        private static byte[] BuildMinimalFat12Floppy()
        {
            // Minimal valid FAT12 360KB floppy boot sector — matches the real DOS 360KB BPB layout
            // (already documented in ai-docs/implement-fat.md from investigating this same driver's
            // read path). Root directory and FAT table are left zeroed (a valid, empty filesystem);
            // PcFloppyDriver.DetectDescriptor requires the image length to exactly match one of its
            // known sizes, so this must be exactly 368640 bytes (40 tracks * 2 sides * 9 sectors * 512).
            const int imageSize = 368640;
            byte[] data = new byte[imageSize];

            const ushort bytesPerSector = 512;
            const byte sectorsPerCluster = 2;
            const ushort reservedSectors = 1;
            const byte numberOfFats = 2;
            const ushort rootEntryCount = 112;
            const ushort totalSectors = 720; // 368640 / 512
            const byte mediaDescriptor = 0xFD;
            const ushort sectorsPerFat = 2;

            data[11] = (byte)(bytesPerSector & 0xFF);
            data[12] = (byte)(bytesPerSector >> 8);
            data[13] = sectorsPerCluster;
            data[14] = (byte)(reservedSectors & 0xFF);
            data[15] = (byte)(reservedSectors >> 8);
            data[16] = numberOfFats;
            data[17] = (byte)(rootEntryCount & 0xFF);
            data[18] = (byte)(rootEntryCount >> 8);
            data[19] = (byte)(totalSectors & 0xFF);
            data[20] = (byte)(totalSectors >> 8);
            data[21] = mediaDescriptor;
            data[22] = (byte)(sectorsPerFat & 0xFF);
            data[23] = (byte)(sectorsPerFat >> 8);

            // FAT12 signature string at offset 0x36, used by PcFloppyDriver.DetectFatFilesystem.
            byte[] fsType = System.Text.Encoding.ASCII.GetBytes("FAT12   ");
            Array.Copy(fsType, 0, data, 0x36, fsType.Length);

            // Boot signature.
            data[510] = 0x55;
            data[511] = 0xAA;

            return data;
        }
    }
}
