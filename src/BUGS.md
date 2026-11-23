# Open defects

Defects that are known and not yet fixed. A test that fails because of one of these stays red
and names it here; it is not skipped and not loosened.

## B1 - Some RetroIO drivers leave the disk file open after the disk is closed

**Test:** `tests/RetroCommander.Core.Tests/BlankImageMountReleasesFileTests.cs` (red).

**Seen:** 2026-11-23. After a disk is opened with a RetroIO driver's `Mount(stream, ...)` and the
driver is disposed, the image file is still open for 28 blank-disk layouts: every Dragon layout,
every PC hard disk, Kaypro 4, Kaypro II, Osborne 1 and every TIKI-100 layout. The Commodore,
PC floppy and NDFS drivers close it.

**Effect:** a disk made with File > New Image (mounted with `MountManager.MountAsLayout`) and
then unmounted stays locked until RetroCommander quits.

**Cause:** RetroIO's `IBlankImageProvider.Mount` does not say who closes the stream, and the
drivers do it differently. The fix belongs in RetroIO: state that the driver owns the stream and
closes it on Dispose, as the Commodore and NDFS drivers already do, and make the others follow.
Reported to the RetroIO session the same day.

retrocommander-cli closes the file it opened itself, which is right under either rule.
