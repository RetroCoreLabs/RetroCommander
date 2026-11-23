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
