# Plan

**Next:** nothing outstanding.

## Not planned

- The naming rules in `.editorconfig` are warnings and suggestions that `dotnet format` does not
  rewrite. Renames are a separate decision.
- About 50 made-up drive-letter strings in tests stay. `eng/check-paths.ps1` guards the real ones.
- `VolumeProviderFactory` takes a cache manager and keeps it, but no code path asks it for a file yet.
  Hooking the cache into remote mounts is new work, not a leftover.
