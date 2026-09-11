# RetroCommander — the file manager

## What it is

An Avalonia desktop file manager for **vintage disk images and retro filesystems**. Mount a D64, an NDFS system
pack, a DragonDOS VDK, a FAT floppy, a UNIX V7 partition, a TNFS share or an ordinary directory — all in one
namespace — and work with them the way you would work with folders.

## Why you need it

Because the alternative is a different command-line tool per format, and none at all for several of them.

The thing it does that nothing else here does is **copy between formats**. Getting a file from an NDFS pack onto a
FAT floppy image means reading one layout and writing another, and deciding what happens to the metadata the
destination cannot hold. That decision is made properly: a `.xat` JSON sidecar preserves it rather than dropping it.

It is also where NDFS gets a face. Creating an image, inspecting users and quotas, reading the disc information —
all of that exists as library calls and as `ndfs-analyzer`, but seeing a user's quota in a window is how you notice
it is wrong.

## What you can use it for

- **Browsing and extracting** from any format RetroIO reads.
- **Copying between formats**, with metadata preserved in a sidecar when the target cannot hold it natively.
- **Creating blank images** — NDFS and PC — that are valid rather than merely zeroed.
- Inspecting an NDFS pack: disc information, users, quotas.
- Viewing and editing a file in place.
- Mounting a **remote** share — TNFS, Econet, FTP, SFTP — beside a local image.
- Reading file properties, including the filesystem-specific ones.

## Prerequisites

| # | Requirement | Why | How to check |
|---|---|---|---|
| P1 | .NET SDK as pinned in `global.json`, or the published exe | To build or run | `dotnet --info` |
| P2 | **A Windows desktop session** | It is an Avalonia GUI | Not usable over a plain SSH session |
| P3 | RetroIO packed to the local feed | Restore | `dir ..\localfeed` |
| P4 | Images to open | Anything useful | The test corpus, or your own |
| P5 | **`RETROFS_DATA_PATH` not set** | Finding the corpus | `$env:RETROFS_DATA_PATH` must be empty |
| P6 | A copy of anything you will modify | It writes in place | Copy it to a scratch directory first |
| P7 | A running TNFS or Econet server | The remote providers only | Optional |

> **Work on copies.** Editing a file, writing to an image, or repairing one changes the image you opened. The test
> corpus is not a scratch directory.

## The windows and dialogs

| Window / dialog | What it does |
|---|---|
| **Main** | Two panes, navigation sidebar, quick-access bar |
| **Mount** / **Generic Mount** | Mount an image or a provider |
| **New Image** | Create a blank image |
| **Create NDFS Image** | A blank NDFS pack, with initial users |
| **Create PC Image** | A blank FAT floppy or hard disk |
| **Disk Info** | Volume information for any mounted volume |
| **NDFS Disk Info** | The NDFS-specific view |
| **NDFS User Info** | Users, quotas and usage on an NDFS pack |
| **NDFS Info** | NDFS details for a file or volume |
| **Properties** | File properties, including filesystem-specific ones |
| **File Viewer** | Look inside a file |
| **Edit File** | Change it |
| **Settings** | Application settings |

## What it is built on

```mermaid
flowchart TB
    UI["RetroCommander windows"]
    VFS["RetroVFS + MountRegistry<br/>one namespace"]
    FACT["DriverFactory<br/>confidence-scored detection"]
    DRV["The format drivers<br/>(RetroIO.Storage.Local)"]
    NDFS["RetroIO.Storage.Ndfs<br/>users, quotas, image creation"]
    PROV["Providers: native, TNFS,<br/>Econet, FTP, SFTP"]
    CAPS["FileSystemCapabilities<br/>what the target can hold"]
    XAT["NdfsXatSerializer<br/>.xat sidecars"]

    UI --> VFS
    VFS --> FACT --> DRV
    VFS --> PROV
    UI --> NDFS
    UI --> CAPS --> XAT

    classDef app   fill:#2196F3,stroke:#1565C0,stroke-width:2px,color:#fff
    classDef core  fill:#009688,stroke:#00695C,stroke-width:2px,color:#fff
    classDef q     fill:#FFA726,stroke:#F57C00,stroke-width:2px,color:#000
    classDef sock  fill:#3F51B5,stroke:#283593,stroke-width:2px,color:#fff
    classDef ok    fill:#4CAF50,stroke:#2E7D32,stroke-width:2px,color:#fff

    class UI app
    class VFS,DRV,NDFS core
    class FACT,CAPS q
    class PROV sock
    class XAT ok
```

## Opening an unknown image

```mermaid
flowchart TB
    FILE["You pick a file"]
    HINTS["DetectionHints:<br/>name, size, first bytes - read ONCE"]
    SCORE["Every driver scores it"]
    BEST{"A clear winner?"}
    PART{"A partition table?<br/>BSD, MBR, GPT, Sun VTOC"}
    PICK["You choose a partition"]
    MOUNT["Mounted, and listed"]
    ASKU["Unrecognised - offer an explicit<br/>filesystem (urn scheme)"]

    FILE --> HINTS --> SCORE --> BEST
    BEST -->|"yes"| PART
    BEST -->|"no"| ASKU
    PART -->|"yes"| PICK --> MOUNT
    PART -->|"no"| MOUNT

    classDef media fill:#9C27B0,stroke:#7B1FA2,stroke-width:2px,color:#fff
    classDef core  fill:#009688,stroke:#00695C,stroke-width:2px,color:#fff
    classDef q     fill:#FFA726,stroke:#F57C00,stroke-width:2px,color:#000
    classDef ok    fill:#4CAF50,stroke:#2E7D32,stroke-width:2px,color:#fff
    classDef bad   fill:#F44336,stroke:#C62828,stroke-width:2px,color:#fff

    class FILE media
    class HINTS,SCORE core
    class BEST,PART q
    class PICK,MOUNT ok
    class ASKU bad
```

Several of these formats are **headerless** — PRG has no signature at all — so detection is scored by confidence
rather than first-match, and "might be" is an honest answer a driver is allowed to give.

> **First-launch caveat:** headless tests cannot catch compiled-binding errors or a missing `avares://` resource.
> Watch for those on the first window after a change.

## Related

- `RetroIO.Storage.Core` (`RetroIO/src/RetroIO.Storage.Core/README.md`) — the VFS, detection and
  capabilities.
- `RetroIO.Storage.Local` (`RetroIO/src/RetroIO.Storage.Local/README.md`) — every format driver, and the
  table of what is supported.
- `RetroIO.Storage.Ndfs` (`RetroIO/src/RetroIO.Storage.Ndfs/README.md`) — NDFS, including image creation.
- `ndfs-analyzer` (`RetroIO/tools/RetroIO.Tools.NdfsAnalyzer/README.md`) — the command-line NDFS analysis
  and repair this app's NDFS windows sit beside.
- Tests: `tests/RetroCommander.Desktop.Tests`.
