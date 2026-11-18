# Plan

**Next:** open the PC/AT hard-disk formats `.hdd` and fixed `.vhd` (section below).

## Open RetroCore PC/AT hard-disk images: `.img`, `.hdd`, fixed `.vhd` (asked 2026-11-19)

RetroCore's PC/AT IDE hard-disk slot accepts three file types (RetroCore repository,
`Nuget/HackerCorpLabs.Emulation.Machines.PCAT/src/PcAtMachineBase.cs`, the "AT IDE hard disks
(C:/D:)" slot: `.img`, `.hdd`, `.vhd`). RetroCommander must open all three, list the MBR
partitions and read and write files in the FAT12/16/32 partitions, as it already does for `.img`.

State, read 2026-11-19 (not built, not tested):
- `.img` works: RetroIO's `RetroIO.Storage.Local/src/Drivers/PcFloppyDriver.cs` handles raw images
  with an MBR (it is an `IPartitionProvider`; partitions chosen as `path#0`..`path#3`), and the
  RetroCore test that writes files onto `msdos-6.22-ami486.img` through RetroIO passes.
- `.hdd` is in no extension list: not in `PcFloppyDriverProvider.KnownExtensions`
  (`.img .ima .vfd .flp`), not in RetroCommander's file-association list
  (`RetroCommander.Core/Services/FileAssociationService.cs`) nor its open-file mapping
  (`RetroCommander.Desktop/MainWindow.axaml.cs`, the `".img" => "file:///"` switch). In RetroCore a
  `.hdd` is the same raw sector image as `.img`.
- `.vhd` is handled nowhere in RetroIO or RetroCommander.

Fixed VHD, the format RetroCore writes (Microsoft "Virtual Hard Disk Image Format Specification"
1.0, under the Open Specification Promise; RetroCore's reader/writer is
`Nuget/HackerCorpLabs.Emulation.Machines.Base/src/Storage/FixedVhd.cs`, which cites every field):
- the file is the raw disk sectors followed by one 512-byte footer; the sector data is exactly
  like an `.img`;
- the footer starts with the ASCII cookie `conectix`; all fields are big-endian; it holds the
  disk size (original and current, 8 bytes each), the disk geometry (cylinders 2 bytes, heads 1,
  sectors per track 1), the disk type (2 = fixed, 3 = dynamic, 4 = differencing), a one's
  complement checksum of the footer bytes with the checksum field taken as zero, and a UUID.

Work, in order:
1. RetroIO (the RetroIO repository, `src/RetroIO.Storage.Local`): a block device
   for fixed VHD that recognises the file by its footer (cookie `conectix` in the last 512 bytes,
   disk type 2, checksum correct) rather than by name, and exposes only the sectors before the
   footer; writes never touch the footer; a file that is too short, has a bad checksum or is
   dynamic/differencing (types 3 and 4) is refused with a message that names the reason. Detection
   gives the VHD confidence so it wins over the raw-image drivers for the same file.
2. RetroIO: add `.hdd` (raw, same as `.img`) and `.vhd` to the PC driver's extension lists, so
   detection and RetroCommander's lists both pick them up from the one list.
3. RetroCommander: `.hdd` and `.vhd` in the open-file mapping and in the Windows file
   associations; the create-image dialog may offer fixed VHD as an output choice (optional).
4. Tests: RetroIO unit tests on a test-built MBR+FAT16 disk saved as `.img`, `.hdd` and fixed
   `.vhd` (footer written per the spec): list partitions, read, write, overwrite, delete; the VHD
   footer is byte-for-byte unchanged after writes; bad cookie / bad checksum / dynamic VHD refused
   by name; each test shown to fail when its code is broken. RetroCommander test: open each of the
   three and list the FAT partition's files. A cross-check that a VHD written by RetroCore's
   `FixedVhd` opens in RetroIO (build a tiny one in the test with the same field values, since
   RetroIO does not reference RetroCore).
5. Then repack RetroIO into RetroCore's local package folder (`Nuget/_shared/localfeed`, RetroIO's
   `src/eng/publish-local.ps1 -Feed`) so RetroCore's `disk.*` commands get `.vhd` and `.hdd` too.

Not in scope: dynamic and differencing VHD, VHDX, DOSBox-X's own formats. For swapping disks with
DOSBox, raw `.img` with the same cylinders/heads/sectors on both sides is the safe format.

## Not planned

- The naming rules in `.editorconfig` are warnings and suggestions that `dotnet format` does not
  rewrite. Renames are a separate decision.
- About 50 made-up drive-letter strings in tests stay. `eng/check-paths.ps1` guards the real ones.
- `VolumeProviderFactory` takes a cache manager and keeps it, but no code path asks it for a file yet.
  Hooking the cache into remote mounts is new work, not a leftover.
