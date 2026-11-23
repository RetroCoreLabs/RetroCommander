# retrocommander-cli

`retrocommander-cli` does what the RetroCommander window does, as commands: make blank disks,
list them, copy files in and out, copy between disks, delete, rename, convert disk images, link
file types in Windows. You can type the commands, put them in a script, or have another program
run them.

It is built on `RetroCommander.Core`, the same code the window uses, so the two treat a disk the
same way. Every choice that depends on the disk format comes from the RetroIO drivers: which
formats exist, which blank disks can be made and their options, which conversions a disk offers,
and which file properties travel with a copied file. The tool's own code names no format, and a
test fails if it ever does.

## Running it

Build it from `src`:

```powershell
dotnet build RetroCommander.Cli -c Release
```

The program is `RetroCommander.Cli\bin\Release\net10.0\retrocommander-cli.exe`. With no command it
opens a prompt; `retrocommander-cli --help` lists every command.

## Disks and paths

A disk is named by its image file, and a place on it by a colon and a path:

| You write | It means |
|---|---|
| `game.d64` | the disk, at its top folder |
| `game.d64:README.TXT` | a file on it |
| `hd.img:/SUB/FILE.TXT` | a file in a folder |
| `C:\disks\a.d64:HELLO` | a full Windows path works; the drive letter's colon is not taken as the split |
| `tnfs://host/` | a URN, opened at its top, as in the window's Mount |

The split is the first colon whose left part is an existing file. Names are matched without
regard to upper and lower case, as in the window.

A disk driver may put a file somewhere you did not name or change its name. An ND (NDFS) disk
puts files under the SYSTEM user and writes `README.TXT` as `README:TXT`. `copy-in` always says
where the file landed.

## Options every command takes

| Option | What it does |
|---|---|
| `--ro` | Opens the disk read-only. A command that changes the disk then fails. |
| `--as LAYOUT` | Opens the disk as a given blank-disk type (an id from `new --list`) instead of detecting it. Needed for an empty disk whose bytes are the same on several machines: an empty Kaypro 4 disk and an empty TIKI-100 400 KB disk are byte for byte the same. |
| `--partition N` | One partition of a partitioned hard disk, counting from 0. |
| `--help`, `-h` | The command list. |

A command that only reads (`ls`, `info`, `copy-out`, `props`, `view`, `conversions`) opens the disk
read-only even without `--ro`, so it never holds a write lock another program might need.

## Commands

### formats

Every disk format the drivers know: the machines, the file extensions, and whether each can be
written and made blank.

### new

```text
new --list [--machine MACHINE]
new FILE --disk DISK [--machine MACHINE] [--set NAME=VALUE]... [--unformatted]
```

`new --list` prints every blank disk the drivers describe, by machine, with each disk's options:

```text
PS> retrocommander-cli new --list --machine TIKI-100

TIKI-100
  tiki100-90k  90 KB floppy - 40 tracks, 1 side, single density  (92,160 bytes, .dsk)
  tiki100-200k  200 KB floppy - 40 tracks, 1 side, 10 x 512  (204,800 bytes, .dsk)
  tiki100-400k  400 KB floppy - 40 tracks, 2 sides, 10 x 512  (409,600 bytes, .dsk)
  tiki100-800k  800 KB floppy - 80 tracks, 2 sides, 10 x 512  (819,200 bytes, .dsk)
  tiki100-hd  8 MB hard disk  (8,388,608 bytes, .dsk)
```

`--disk` takes an id from that list, or with `--machine` a name, which may be cut short as long
as only one disk of that machine starts with it. A FILE without an extension gets the disk's own.

`--set NAME=VALUE` sets an option; each option is checked by the driver's own rule, and the
driver's message is printed when a value is refused. `--unformatted` writes zeros of the disk's
real size with no file system on it, for a machine or an emulator to format. An existing file is
never overwritten.

```text
PS> retrocommander-cli new game.d64 --disk cbm-d64 --set diskName=GAMES --set diskId=G1
created game.d64: Commodore 1541, D64 - 35 tracks, 170 KB

PS> retrocommander-cli new tiki.dsk --machine TIKI-100 --disk "400 KB"
created tiki.dsk: TIKI-100, 400 KB floppy - 40 tracks, 2 sides, 10 x 512
```

### ls

```text
ls DISK[:PATH] [-l]
```

Lists a folder. With `-l`: size, date and name. Folders end in `/`. The size is the one the disk's
directory records; a Commodore directory counts 254-byte blocks, so a 19-byte file is listed as
254. `copy-out` gives the real bytes.

### info

```text
info DISK
```

What the disk is: format, label, size, geometry, what the driver can do, and the driver's own
details. It is the same list the window's Disk Info shows. `info` opens the disk read-only, so
"Access" says read-only and "Write" says No for that opening.

### copy-in and copy-out

```text
copy-in FILE... DISK[:PATH] [--convert ID [--set NAME=VALUE]...]
copy-out DISK:FILE [FOLDER] [--convert ID [--set NAME=VALUE]...]
```

`copy-in` copies host files onto a disk; `copy-out` copies one file off a disk into a host folder
(the current folder when none is given). They use the window's copy code: the file's properties
go with it as described under [File properties and .xat files](#file-properties-and-xat-files).
`--convert` is described under [Copy conversions](#copy-conversions).

A file name Windows does not allow (an ND name `README:TXT`) gets the window's safe form on the
host (`README.TXT`).

### conversions

```text
conversions DISK
```

The conversions the disk's driver offers for copy-in and copy-out, with their options. A disk
that offers none says so, and its files are copied as they are.

### copy and move

```text
copy DISK:FILE DISK[:PATH]
move DISK:FILE DISK[:PATH]
```

Between two disks, or between two folders of one disk. `move` copies, then deletes the original.
Copying or moving a file into the folder it is already in is refused: the copy would land on the
file itself, and a move would then delete the only copy.

### mkdir, rm, rename

```text
mkdir DISK:PATH
rm DISK:FILE...
rename DISK:FILE NEWNAME
```

`rm` takes several files, all on one disk. A disk whose format has no folders refuses `mkdir` with
the driver's message. Names must follow the disk's own rules; the driver refuses one that does not.

### props

```text
props DISK:FILE
```

Every property the disk keeps for the file, under the names the driver uses.

### view

```text
view DISK:FILE [--text | --hex]
```

Shows a file. Plain text (printable characters, tabs and line ends) is shown as text; anything
else as hex with offsets, so a binary file never sends control codes to the terminal. `--text` and
`--hex` force one or the other. The bytes are shown as stored; to see a format's text as host text,
use `copy-out --convert`.

### convert

```text
convert IMAGE TARGET [--chs CYLINDERS,HEADS,SECTORS]
```

The window's Convert disk image. A fixed VHD becomes a raw image (the same disk bytes without the
512-byte VHD footer); a raw image becomes a fixed VHD (the bytes plus the footer). The direction
comes from what the file holds, not its name. For a raw image the disk's shape is read from its
partition table or boot sector; when it has neither, give `--chs`. A new file is always written.

### assoc

```text
assoc list [--exe PATH]
assoc add EXT... [--exe PATH]
assoc remove EXT... [--exe PATH]
```

Windows only. Links disk image file types to the RetroCommander window program, as Settings in the
window does, for this Windows user (no administrator rights needed). The program is
`RetroCommander.exe` beside this tool, or the one `--exe` names. Only types the drivers know can be
linked. Windows 10 and 11 may still ask which program to use the first time a file is opened; that
choice belongs to Windows.

## File properties and .xat files

Many disks keep more about a file than its name, size and dates: an ND file has an owner and
access bits, a Commodore file has a file type. A Windows folder cannot hold those, so:

- copying a file **off** a disk into a host folder writes `NAME.xat` beside it, holding the file's
  properties;
- copying a file **onto** a disk reads `NAME.xat` beside it, if there is one, and the target disk
  gets those properties back;
- a `.xat` file is not copied onto a disk as a file of its own; it is used for the file beside it.

Whether a `.xat` is written, and what is in it, is decided only by the disk's driver: a driver that
declares its files carry properties gets them kept, and only the ones it names. A driver that
declares nothing gets no `.xat`. The window does exactly the same.

A copy between two disks of the same kind keeps every property; between different kinds, only
what the target understands.

**Today:** the NDFS driver declares property names its files do not carry, so ND files get no
`.xat` until that is fixed in RetroIO, and the other drivers declare no properties yet
(`src/BUGS.md`, B2).

## Copy conversions

A disk driver may offer conversions for copying text: a SINTRAN text file has the ND parity bit
in bit 7, a CP/M text file ends at a Ctrl-Z (1Ah) and is padded with more of them, a Commodore
text file is PETSCII. `conversions DISK` lists what a disk offers, and `--convert ID` uses one:

```text
PS> retrocommander-cli conversions sys.img
sintran-text  SINTRAN text  (copy-in and copy-out, the usual one for text)
    Copy out clears bit 7, the ND parity bit. Copy in writes seven-bit text, with bit 7 clear or with ND even parity, and refuses a byte above 7Fh.
      --set parity=...  Bit 7 on copy in [one of: clear | even parity, default clear]
      --set hostLineEnds=...  Line ends on the host [one of: CR LF | LF, default CR LF]
```

The conversions, their names, options and rules all come from the driver. A byte with no meaning
in the target (a colour code in PETSCII text, a byte above 7Fh for SINTRAN) stops the copy with the
driver's message naming the byte and where it is; nothing is guessed. Without `--convert` the bytes
are copied as they are.

## Examples by format

The ids below are the ones `new --list` printed on 2026-11-23. Run `new --list` for what your build
has.

**Commodore 1541 (D64)**

```powershell
retrocommander-cli new game.d64 --disk cbm-d64 --set diskName=GAMES --set diskId=G1
retrocommander-cli copy-in prog.prg game.d64
retrocommander-cli conversions game.d64
retrocommander-cli copy-in notes.txt game.d64 --convert petscii-text
retrocommander-cli ls game.d64 -l
```

**TIKI-100 (CP/M)**

An empty TIKI-100 400 KB disk is byte for byte an empty Kaypro 4 disk, so name the layout with
`--as` until the disk holds files:

```powershell
retrocommander-cli new tiki.dsk --machine TIKI-100 --disk "400 KB"
retrocommander-cli copy-in readme.txt tiki.dsk --as tiki100-400k --convert cpm-text
retrocommander-cli copy-out tiki.dsk:README.TXT out --as tiki100-400k --convert cpm-text
```

**Norsk Data (NDFS)**

```powershell
retrocommander-cli new sys.img --disk ndfs-floppy-360k
retrocommander-cli copy-in readme.txt sys.img --convert sintran-text --set "parity=even parity"
retrocommander-cli ls sys.img:/SYSTEM -l
```

The file lands as `/SYSTEM/README:TXT`. Its date is shown 30 years back (2026 as 1996): the ND date
word holds years 1950 to 2013 only, and a later date is moved back in steps of 30 years until it
fits.

**PC (FAT)**

```powershell
retrocommander-cli new boot.img --disk pc-1440k
retrocommander-cli mkdir boot.img:/BIN
retrocommander-cli copy-in tool.com boot.img:/BIN
```

Files in a folder are listed with the wrong path today, so deleting or renaming them there fails
(`src/BUGS.md`, B3).

**A raw hard disk to a VHD and back**

```powershell
retrocommander-cli convert disk.img disk.vhd
retrocommander-cli convert old.img old.vhd --chs 615,4,17
retrocommander-cli convert disk.vhd disk-again.img
```

## Scripts and the prompt

`script FILE` runs commands from a file, one per line, written as at the prompt. Blank lines and
lines starting with `#` are skipped. Double quotes keep a word with spaces together. The first
command that fails stops the script; its exit code is the script's, and the line is named:

```text
# make a disk and put two files on it
new "My Games.d64" --disk cbm-d64 --set diskName=GAMES
copy-in prog.prg "My Games.d64"
copy-in notes.txt "My Games.d64" --convert petscii-text
ls "My Games.d64" -l
```

With no command the tool opens a prompt, `rc> `. A command that fails is reported and the prompt
goes on. `exit`, `quit` or the end of the input ends it.

## Exit codes and messages

| Code | Meaning |
|---|---|
| 0 | Done. |
| 1 | The command could not be done: a missing file, a full disk, a name the disk refuses. |
| 2 | The command line was wrong: an unknown command or option, a missing argument, an option value the driver refuses. |

Errors go to the error output, one line starting with `error: `. Messages from a driver are passed
on as the driver wrote them. Some real ones:

```text
error: Disk ID: must be exactly 2 characters
error: no file 'NOPE' on the disk
error: no such disk image: nothere.d64
error: this disk has no conversion 'cpm-text'; see: conversions DISK
error: Bit 7 on copy in: not one of the choices
error: this command changes the disk, and --ro was given
```

## For a program or an AI agent

- Check the exit code, not the text: 0, 1 or 2 as above.
- A successful copy prints one line: `copied SOURCE -> WHERE (N bytes)`. WHERE is where the file
  really landed, which may differ from what was asked (an ND disk adds a user folder and changes the
  name). Read it from that line before using the file again.
- Ask the tool for format facts rather than assuming them: `new --list` for disks and options,
  `conversions DISK` for conversions, `formats` for what can be written.
- Use `--as LAYOUT` for a disk made with `new` until it holds files.
- Put a series of steps in a `script` file: it stops at the first failure and names the line.
- Nothing asks a question; no command waits for input except the prompt.

## Not done yet

- **PETSCII art in a Commodore directory listing.** It waits on RetroIO: the functions that write
  raw directory names exist only on the Commodore driver class, and the tool names no format, so
  they are needed behind a general interface with their own metadata first.
- **Known defects** are in `src/BUGS.md`: B2 (no `.xat` for any format yet), B3 (files in a FAT
  folder), B4 (a test that failed once).
