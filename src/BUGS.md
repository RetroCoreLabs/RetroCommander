# Open defects

Defects that are known and not yet fixed. A test that fails because of one of these stays red
and names it here; it is not skipped and not loosened.

## B4 - A blank-disk test failed once in a full run; cause not known

**Test:** `tests/RetroCommander.Core.Tests/BlankImageCatalogueTests.cs`,
`EveryLayout_CreatesAFileOfItsSize_ThatOpensAsThatLayout`.

**Seen:** 2026-11-23, once, when all three test projects ran together (the CLI tests had just
been added). The failure message was not kept. It passed when run alone, and in three more full
runs. Each test run uses its own folder, so nothing obvious is shared. Not known what failed. If it
fails again, keep the whole message and add it here.

**Lead, not proved:** in eight more full runs the same day this test passed every time, but run 7
failed a CLI test with `OutOfMemoryException` inside RetroIO `NdfsImageCreator.BuildImage`, which
makes a blank ND disk as one array of its full size (up to 601 MB, `ndfs-scsi-630m`). At that
moment the machine's commit charge was 137.7 GB of a 143.7 GB limit, nearly all other sessions'
processes. This test makes every blank disk, those big ones included, so the same failure fits it,
but its own message was never captured. The CLI test made every disk size for no reason and now
uses the smallest disk of each format; this test has to make every layout, so it stays as it is.
A real fix for the memory use is in RetroIO: `IBlankImageProvider.CreateBlank` returns the whole
disk as one array, where writing it to a stream would not need the 601 MB block at all.

## B3 - FAT disks give a file in a folder the path of the top folder

**Test:** `tests/RetroCommander.Cli.Tests/CliCommandTests.cs`,
`Mkdir_CopyIntoTheFolder_ListAndDeleteThere` (red).

**Seen:** 2026-11-23, with retrocommander-cli. On a PC (FAT) disk, a file in `/SUB` is listed
with the path `/INSIDE` instead of `/SUB/INSIDE`. Deleting it then fails with
"File not found: /INSIDE" - or, if a file of that name is also in the top folder, would act on
that one. The window lists folders the same way, so its delete and rename in a folder are
affected too.

**Cause:** RetroIO `Fat12FileSystem.cs`, `Fat16FileSystem.cs` and `Fat32FileSystem.cs` set
`Path = "/" + entry.FileName` for every entry, whatever folder is listed. The fix is in RetroIO,
with a rule test over every driver that has folders. Reported to the RetroIO session the same
day.

## B2 - The NDFS driver declares property names its files do not have

**Tests:** `NdfsUiPanelTests.CopyFile_NdfsToNative_WritesMetadataSidecar` and
`CopyFile_ExportThenReimport_RestoresPortableNdfsMetadata` (red).

**Seen:** 2026-11-23. Whether a copied file takes a `.xat` with its properties, and which ones, is
now decided only by what the disk's driver declares (RetroIO `IExtendedAttributeSupport.GetCapabilities`,
see `Operations/XatSidecarRules.cs`). The NDFS driver declares names such as `ndfs:UserName` and
`ndfs:AccessBits`, but the properties its files carry are named `ndfs.user_name`,
`ndfs.access_bits` (RetroIO `NdfsPropertyBagKeys`). None match, so nothing is kept.

**Effect:** a file copied off an NDFS disk into a Windows folder gets no `.xat`, and its owner,
access bits and other ND properties are lost on the way back in.

**Cause:** the declared list in RetroIO `NdfsDriver.GetCapabilities` was typed by hand and never
checked against the real names. The fix is in RetroIO: declare the real names, and a rule test
that every declared name occurs and every property a file carries is declared. The other drivers
(Commodore, CP/M, Dragon, PC, BSD) declare no properties yet, so their files get no `.xat` either
until they do. Reported to the RetroIO session the same day.
