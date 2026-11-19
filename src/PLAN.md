# Plan

**Next:** the RetroCore side of the hard-disk formats (section below).

## RetroCore side of `.hdd` / fixed `.vhd` and the converter (asked 2026-11-19)

Left, in the RetroCore repository: RetroIO repacked into `Nuget/_shared/localfeed`
(`src/eng/publish-local.ps1 -Feed`), a `disk.convert <file> <new file>` command in both RetroCore
programs, a "Convert..." button beside each hard-disk drive on the settings window's Media tab (only
while the machine is off), and `disk.*` tests on a fixed `.vhd`.
Not in scope: dynamic and differencing VHD, VHDX, DOSBox-X's own formats. For swapping disks with
DOSBox, raw `.img` with the same cylinders/heads/sectors on both sides is the safe format.

## Not planned

- The naming rules in `.editorconfig` are warnings and suggestions that `dotnet format` does not
  rewrite. Renames are a separate decision.
- About 50 made-up drive-letter strings in tests stay. `eng/check-paths.ps1` guards the real ones.
- `VolumeProviderFactory` takes a cache manager and keeps it, but no code path asks it for a file yet.
  Hooking the cache into remote mounts is new work, not a leftover.
