# Plan

**Next:** PETSCII in Commodore listings, in the CLI and the window.

## Outstanding

- PETSCII view, decided 2026-11-23: the CLI draws each character's 8x8 pixels from a C64 character
  ROM file the user names (never shipped in the repository) with Unicode braille dots, 4 columns by
  2 lines per character; the window draws Commodore listings with the same pixels. The pixels and the
  byte-to-character order come from RetroIO as driver data, so RetroCommander names no format.
- B4 in `BUGS.md`: making a blank disk takes the whole disk as one block of memory (RetroIO).

## Not planned

- Dynamic and differencing VHD, VHDX, and other emulators' own disk formats.
- The naming rules in `.editorconfig` are warnings and suggestions that `dotnet format` does not
  rewrite. Renames are a separate decision.
- About 50 made-up drive-letter strings in tests stay. `eng/check-paths.ps1` guards the real ones.
- `VolumeProviderFactory` takes a cache manager and keeps it, but no code path asks it for a file yet.
  Hooking the cache into remote mounts is new work, not a leftover.
