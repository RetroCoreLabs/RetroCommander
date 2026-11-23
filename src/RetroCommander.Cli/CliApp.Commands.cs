//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Operations;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local.Images;

namespace RetroCommander.Cli
{
    /// <summary>
    /// Makes the service that links file types to RetroCommander in Windows, for a given program.
    /// </summary>
    /// <param name="exePath">
    /// The RetroCommander program the file types are linked to.
    /// </param>
    /// <returns>
    /// The service; the caller disposes it.
    /// </returns>
    internal delegate FileAssociationService AssociationServiceFactory(string exePath);

    /// <summary>
    /// The commands that change files on a disk, show them, convert images, link file types, and
    /// run scripts and the prompt.
    /// </summary>
    internal sealed partial class CliApp
    {
        /// <summary>
        /// Gets or sets how the association service is made; tests point it at a scratch registry key.
        /// </summary>
        internal AssociationServiceFactory? Associations { get; set; }

        // ---------------------------------------------------------------- copy / move

        // copy and move between two places on disks: two disk images, or two folders of one.
        // The work is the window's own (SmartFileOperations), so properties are mapped the same way.
        private async Task CopyOrMoveAsync(GlobalOptions o, bool move)
        {
            string verb = move ? "move" : "copy";
            if (o.Words.Count != 3)
            {
                throw new CliException(verb + " DISK:FILE DISK[:PATH]", usage: true);
            }
            DiskTarget from = DiskTarget.Parse(o.Words[1]);
            DiskTarget to = DiskTarget.Parse(o.Words[2]);
            if (from.Path == "/")
            {
                throw new CliException("name the file to " + verb + ": DISK:FILE", usage: true);
            }

            // One disk image opened once: a second open of the same file would be refused.
            bool sameDisk = SameDisk(from, to);

            // Into the folder it is already in: a copy would land on itself, and a move would then
            // delete the only copy. Measured: "move b.img:A.TXT b.img:/" emptied the disk.
            if (sameDisk && string.Equals(FolderOf(from.Path), Normalise(to.Path), StringComparison.OrdinalIgnoreCase))
            {
                throw new CliException(from.Path + " is already in " + Normalise(to.Path) + "; to change its name use rename", usage: true);
            }
            IFileSystemPanel source = await from.OpenAsync(o, write: move || sameDisk);
            IFileSystemPanel? target = null;
            try
            {
                IFileSystemEntry entry = await FindEntryAsync(source, from.Path);
                target = sameDisk ? source : await to.OpenAsync(o, write: true);
                if (sameDisk)
                {
                    // The entry was found; the same panel now goes to the target folder.
                    await target.NavigateAsync(to.Path);
                }
                else if (to.Path != "/")
                {
                    await target.NavigateAsync(to.Path);
                }

                SmartFileOperations ops = new SmartFileOperations();
                if (move)
                {
                    MoveResult moved = await ops.MoveAsync(entry, source, target, new MoveOptions());
                    if (!moved.Success)
                    {
                        throw new CliException(from.Path + ": " + moved.ErrorMessage);
                    }
                }
                else
                {
                    CopyResult copied = await ops.CopyAsync(entry, target, new CopyOptions());
                    if (!copied.Success)
                    {
                        throw new CliException(from.Path + ": " + copied.ErrorMessage);
                    }
                }
                _out.WriteLine((move ? "moved " : "copied ") + entry.FullPath + " -> " + o.Words[2]);
            }
            finally
            {
                if (target != null && !sameDisk)
                {
                    to.Close(target);
                }
                from.Close(source);
            }
        }

        // ---------------------------------------------------------------- mkdir / rm / rename

        private async Task MkdirAsync(GlobalOptions o)
        {
            DiskTarget target = OneDisk(o, "mkdir DISK:PATH");
            string p = target.Path.Replace('\\', '/').TrimEnd('/');
            if (p.Length == 0)
            {
                throw new CliException("name the folder to make: DISK:PATH", usage: true);
            }
            int slash = p.LastIndexOf('/');
            string parent = slash > 0 ? p.Substring(0, slash) : "/";
            string name = p.Substring(slash + 1);

            IFileSystemPanel panel = await target.OpenAsync(o, write: true);
            try
            {
                if (parent != "/")
                {
                    await panel.NavigateAsync(parent);
                }
                IFileSystemEntry made = await panel.CreateDirectoryAsync(name);
                _out.WriteLine("made " + made.FullPath);
            }
            finally
            {
                target.Close(panel);
            }
        }

        private async Task RmAsync(GlobalOptions o)
        {
            if (o.Words.Count < 2)
            {
                throw new CliException("rm DISK:FILE...", usage: true);
            }
            DiskTarget first = DiskTarget.Parse(o.Words[1]);
            IFileSystemPanel panel = await first.OpenAsync(o, write: true);
            try
            {
                SmartFileOperations ops = new SmartFileOperations();
                for (int i = 1; i < o.Words.Count; i++)
                {
                    DiskTarget t = DiskTarget.Parse(o.Words[i]);
                    if (!SameDisk(first, t))
                    {
                        throw new CliException("rm works on one disk at a time: " + o.Words[i], usage: true);
                    }
                    if (t.Path == "/")
                    {
                        throw new CliException("name the file to delete: DISK:FILE", usage: true);
                    }
                    IFileSystemEntry entry = await FindEntryAsync(panel, t.Path);
                    DeleteResult deleted = await ops.DeleteAsync(entry, panel);
                    if (!deleted.Success)
                    {
                        throw new CliException(t.Path + ": " + deleted.ErrorMessage);
                    }
                    _out.WriteLine("deleted " + entry.FullPath);
                }
            }
            finally
            {
                first.Close(panel);
            }
        }

        private async Task RenameAsync(GlobalOptions o)
        {
            if (o.Words.Count != 3)
            {
                throw new CliException("rename DISK:FILE NEWNAME", usage: true);
            }
            DiskTarget target = DiskTarget.Parse(o.Words[1]);
            if (target.Path == "/")
            {
                throw new CliException("name the file to rename: DISK:FILE", usage: true);
            }
            IFileSystemPanel panel = await target.OpenAsync(o, write: true);
            try
            {
                IFileSystemEntry entry = await FindEntryAsync(panel, target.Path);
                RenameResult renamed = await new SmartFileOperations().RenameAsync(entry, panel, o.Words[2]);
                if (!renamed.Success)
                {
                    throw new CliException(target.Path + ": " + renamed.ErrorMessage);
                }
                _out.WriteLine("renamed " + entry.FullPath + " -> " + o.Words[2]);
            }
            finally
            {
                target.Close(panel);
            }
        }

        // ---------------------------------------------------------------- props / view

        private async Task PropsAsync(GlobalOptions o)
        {
            DiskTarget target = OneDisk(o, "props DISK:FILE");
            IFileSystemPanel panel = await target.OpenAsync(o, write: false);
            try
            {
                IFileSystemEntry entry = await FindEntryAsync(panel, target.Path);
                // Every property the driver gives for the file, under the names it uses.
                IReadOnlyDictionary<string, object?> all = entry.Properties.GetAllProperties();
                List<string[]> rows = new List<string[]>();
                foreach (KeyValuePair<string, object?> kvp in all)
                {
                    rows.Add(new[] { kvp.Key, Show(kvp.Value) });
                }
                rows.Sort(static (a, b) => string.CompareOrdinal(a[0], b[0]));
                WriteTable(rows.ToArray());
            }
            finally
            {
                target.Close(panel);
            }
        }

        private async Task ViewAsync(GlobalOptions o)
        {
            DiskTarget target = OneDisk(o, "view DISK:FILE [--text|--hex]");
            IFileSystemPanel panel = await target.OpenAsync(o, write: false);
            try
            {
                IFileSystemEntry entry = await FindEntryAsync(panel, target.Path);
                if (entry.IsDirectory)
                {
                    throw new CliException(target.Path + " is a folder; use ls");
                }
                byte[] data;
                using (Stream s = await entry.OpenReadAsync())
                using (MemoryStream all = new MemoryStream())
                {
                    await s.CopyToAsync(all);
                    data = all.ToArray();
                }

                bool hex = o.Flags.Contains("--hex") || (!o.Flags.Contains("--text") && !IsPlainText(data));
                if (hex)
                {
                    WriteHex(data);
                }
                else
                {
                    // Bytes are shown as they are stored (Latin-1 keeps every byte one character);
                    // a format's own text conversion is copy-out --convert.
                    _out.Write(Encoding.Latin1.GetString(data));
                    if (data.Length > 0 && data[data.Length - 1] != '\n')
                    {
                        _out.WriteLine();
                    }
                }
            }
            finally
            {
                target.Close(panel);
            }
        }

        // Text here means printable ASCII with tabs and line ends: anything else is shown as hex,
        // so a binary file never sends control codes to the terminal.
        private static bool IsPlainText(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (!(b == 9 || b == 10 || b == 13 || (b >= 0x20 && b <= 0x7E)))
                {
                    return false;
                }
            }
            return true;
        }

        private void WriteHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(80);
            for (int row = 0; row < data.Length; row += 16)
            {
                sb.Clear();
                sb.Append(row.ToString("X8", CultureInfo.InvariantCulture)).Append("  ");
                for (int i = 0; i < 16; i++)
                {
                    sb.Append(row + i < data.Length ? data[row + i].ToString("X2", CultureInfo.InvariantCulture) + " " : "   ");
                    if (i == 7)
                    {
                        sb.Append(' ');
                    }
                }
                sb.Append(' ');
                for (int i = 0; i < 16 && row + i < data.Length; i++)
                {
                    byte b = data[row + i];
                    sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
                }
                _out.WriteLine(sb.ToString());
            }
        }

        private static string Show(object? value)
        {
            return value switch
            {
                null => "-",
                byte[] bytes => Convert.ToHexString(bytes),
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        // ---------------------------------------------------------------- convert (disk images)

        // The window's Convert disk image, through the same RetroIO DiskImageConverter: the
        // direction comes from what the file holds (a fixed VHD footer or not).
        private void ConvertImage(GlobalOptions o)
        {
            if (o.Words.Count != 3)
            {
                throw new CliException("convert IMAGE TARGET [--chs C,H,S]", usage: true);
            }
            string source = o.Words[1];
            if (!File.Exists(source))
            {
                throw new CliException("no such disk image: " + source);
            }
            bool isVhd = DiskImageConverter.IsFixedVhd(source);
            string target = o.Words[2];

            if (isVhd)
            {
                DiskImageConverter.FixedVhdToRaw(source, target);
                _out.WriteLine("converted " + source + " (fixed VHD) -> " + target + " (raw image)");
                return;
            }

            DiskGeometry g;
            string? chs = o.Value("--chs");
            if (chs != null)
            {
                g = ParseChs(chs);
            }
            else if (!DiskImageConverter.TryDetectGeometry(source, out g))
            {
                throw new CliException("the disk has no partition table or boot sector that gives its shape; give --chs CYLINDERS,HEADS,SECTORS", usage: true);
            }
            DiskImageConverter.RawToFixedVhd(source, target, g);
            _out.WriteLine("converted " + source + " (raw image) -> " + target + " (fixed VHD, "
                + g.Cylinders + " cylinders, " + g.Heads + " heads, " + g.SectorsPerTrack + " sectors per track)");
        }

        private static DiskGeometry ParseChs(string chs)
        {
            string[] parts = chs.Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) && c >= 1 && c <= 65535 &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) && h >= 1 && h <= 255 &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) && s >= 1 && s <= 255)
            {
                return new DiskGeometry(c, h, s);
            }
            throw new CliException("--chs is CYLINDERS,HEADS,SECTORS: cylinders 1-65535, heads 1-255, sectors 1-255", usage: true);
        }

        // ---------------------------------------------------------------- assoc

        // The window's Settings > file types, through the same FileAssociationService. Only the
        // disk image types the drivers know can be linked.
        private void Assoc(GlobalOptions o)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new CliException("file types are linked in Windows only");
            }
            if (o.Words.Count < 2)
            {
                throw new CliException("assoc list | add EXT... | remove EXT... [--exe PATH]", usage: true);
            }

            string exe = o.Value("--exe") ?? Path.Combine(AppContext.BaseDirectory, "RetroCommander.exe");
            if (!File.Exists(exe))
            {
                throw new CliException("RetroCommander.exe is not beside this tool; give --exe PATH to the RetroCommander program");
            }
            exe = Path.GetFullPath(exe);

            using FileAssociationService service = Associations != null ? Associations(exe) : FileAssociationService.ForCurrentUser(exe);
            string action = o.Words[1];
            if (action == "list")
            {
                IReadOnlyList<string> known = KnownDiskImageTypes.Extensions;
                string[][] rows = new string[known.Count][];
                for (int i = 0; i < known.Count; i++)
                {
                    FileAssociationState state = service.GetState(known[i]);
                    string shown = state == FileAssociationState.Linked ? "linked"
                        : state == FileAssociationState.NotLinked ? "-" : "linked to another copy";
                    rows[i] = new[] { known[i], shown, KnownDiskImageTypes.Describe(known[i]) };
                }
                WriteTable(rows);
                _out.WriteLine("Files open with: " + service.ExePath);
                return;
            }
            if (action != "add" && action != "remove")
            {
                throw new CliException("assoc list | add EXT... | remove EXT...", usage: true);
            }
            if (o.Words.Count < 3)
            {
                throw new CliException("assoc " + action + " needs at least one file type, such as .d64", usage: true);
            }

            string[] exts = new string[o.Words.Count - 2];
            for (int i = 0; i < exts.Length; i++)
            {
                string e = o.Words[i + 2];
                e = e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant();
                if (!KnownDiskImageTypes.IsKnown("x" + e))
                {
                    throw new CliException(e + " is not a disk image type RetroCommander opens; see: assoc list", usage: true);
                }
                exts[i] = e;
            }
            if (action == "add")
            {
                service.Apply(exts, ReadOnlySpan<string>.Empty);
            }
            else
            {
                service.Apply(ReadOnlySpan<string>.Empty, exts);
            }
            _out.WriteLine((action == "add" ? "linked " : "unlinked ") + string.Join(" ", exts)
                + (action == "add" ? " to " + service.ExePath + " (Windows may still ask which program to use the first time)" : string.Empty));
        }

        // ---------------------------------------------------------------- script / prompt

        // One command per line, as typed at the prompt. Blank lines and lines starting with # are
        // skipped. The first command that fails stops the script, and its exit code is returned.
        private async Task<int> ScriptAsync(GlobalOptions o)
        {
            if (o.Words.Count != 2)
            {
                throw new CliException("script FILE", usage: true);
            }
            if (!File.Exists(o.Words[1]))
            {
                throw new CliException("no such script: " + o.Words[1]);
            }
            string[] lines = File.ReadAllLines(o.Words[1]);
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                List<string> words = SplitLine(line);
                if (words.Count == 0)
                {
                    continue;
                }
                int code = await RunAsync(words);
                if (code != 0)
                {
                    _err.WriteLine("script stopped at line " + (n + 1) + ": " + line);
                    return code;
                }
            }
            return 0;
        }

        // Reads commands until "exit", "quit" or the end of the input. A failing command is
        // reported and the prompt goes on.
        private async Task<int> PromptAsync()
        {
            while (true)
            {
                _out.Write("rc> ");
                _out.Flush();
                string? line = await _in.ReadLineAsync();
                if (line == null)
                {
                    _out.WriteLine();
                    return 0;
                }
                line = line.Trim();
                if (line == "exit" || line == "quit")
                {
                    return 0;
                }
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }
                List<string> words = SplitLine(line);
                if (words.Count > 0)
                {
                    await RunAsync(words);
                }
            }
        }

        /// <summary>
        /// Splits a command line into words: spaces separate them, and double quotes keep a word
        /// with spaces together ("My Disk.d64").
        /// </summary>
        /// <param name="line">
        /// The line as typed.
        /// </param>
        /// <returns>
        /// The words.
        /// </returns>
        /// <exception cref="CliException">
        /// Thrown when a quote is not closed.
        /// </exception>
        internal static List<string> SplitLine(string line)
        {
            List<string> words = new List<string>();
            StringBuilder word = new StringBuilder();
            bool quoted = false;
            bool any = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    quoted = !quoted;
                    any = true;
                }
                else if (char.IsWhiteSpace(ch) && !quoted)
                {
                    if (any)
                    {
                        words.Add(word.ToString());
                        word.Clear();
                        any = false;
                    }
                }
                else
                {
                    word.Append(ch);
                    any = true;
                }
            }
            if (quoted)
            {
                throw new CliException("a quote is not closed: " + line, usage: true);
            }
            if (any)
            {
                words.Add(word.ToString());
            }
            return words;
        }

        // "/SUB/A.TXT" -> "/SUB", "A.TXT" -> "/".
        private static string FolderOf(string path)
        {
            string p = Normalise(path);
            int slash = p.LastIndexOf('/');
            return slash <= 0 ? "/" : p.Substring(0, slash);
        }

        // Forward slashes, a leading slash, no trailing slash except for the root.
        private static string Normalise(string path)
        {
            string p = path.Replace('\\', '/').TrimEnd('/');
            if (!p.StartsWith('/'))
            {
                p = "/" + p;
            }
            return p.Length == 0 ? "/" : p;
        }

        private static bool SameDisk(DiskTarget a, DiskTarget b)
        {
            return a.IsUrn || b.IsUrn
                ? string.Equals(a.Disk, b.Disk, StringComparison.Ordinal)
                : string.Equals(Path.GetFullPath(a.Disk), Path.GetFullPath(b.Disk), StringComparison.OrdinalIgnoreCase);
        }
    }
}
