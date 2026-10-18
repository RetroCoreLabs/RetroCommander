# RetroCommander

**A file manager for vintage disk images.** One window, two panes, and every format RetroIO can
read - D64, NDFS, DragonDOS, FAT, UNIX V7, FFS and the rest - alongside ordinary directories and
remote shares. A D64, a TNFS share and a folder on your disk are all just volumes, so copying
between them is one operation. Metadata the destination filesystem cannot hold is kept in a
sidecar rather than dropped.

This site is the reference for the code, generated from its XML documentation.

## The two projects

| Project | What it is |
|---|---|
| `RetroCommander.Core` | The library: volumes and panels, mounting by URN, file operations between filesystems, property mapping, caching and logging. |
| `RetroCommander` (`RetroCommander.Desktop`) | The Avalonia application: the two-pane window, dialogs, sidebar and themes. |

## A short example

Set up the core and open a volume as a panel. Taken from `CoreDemo` in `RetroCommander.Core`:

```csharp
var cacheManager = new FileSystemCacheManager(new CacheConfiguration());
var driverFactory = new DriverFactory();
var factory = new VolumeProviderFactory(null, driverFactory, cacheManager, logger);

// A disk image is opened by URN: the scheme names the filesystem, the rest is the path.
var panel = await factory.CreatePanelAsync("ndfs://" + imagePath.Replace("\\", "/"));
Console.WriteLine($"{panel.DisplayName} ({panel.FileSystemType}), read-only: {panel.IsReadOnly}");
```

## Where to go next

- [API reference](api/index.md) - every namespace, type and member.
- [The repository](https://github.com/RetroCoreLabs/RetroCommander) - source, releases and the
  README.
