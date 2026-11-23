# Plan

**Next:** PETSCII art in a Commodore directory listing (`retrocommander-cli dirart`), once RetroIO
puts its raw directory-name functions behind a general interface with metadata.

## Outstanding

- `retrocommander-cli dirart`: write raw PETSCII directory names, insert separator (DEL) lines,
  write the raw disk name and id, and preview the listing. Waits on RetroIO: today these exist only
  on the Commodore driver class, and RetroCommander names no format. Asked for 2026-11-23.
- B4 in `BUGS.md`: making a blank disk takes the whole disk as one block of memory (RetroIO).

## Not planned

- Dynamic and differencing VHD, VHDX, and other emulators' own disk formats.
- The naming rules in `.editorconfig` are warnings and suggestions that `dotnet format` does not
  rewrite. Renames are a separate decision.
- About 50 made-up drive-letter strings in tests stay. `eng/check-paths.ps1` guards the real ones.
- `VolumeProviderFactory` takes a cache manager and keeps it, but no code path asks it for a file yet.
  Hooking the cache into remote mounts is new work, not a leftover.
