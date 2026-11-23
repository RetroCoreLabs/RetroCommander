//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Operations;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Cli
{
    /// <summary>
    /// Runs one retrocommander-cli command and writes its output.
    /// </summary>
    /// <remarks>
    /// Every format-specific choice comes from the RetroIO drivers, read through RetroCommander.Core
    /// (the same code the window uses): the formats list, the machines and disks <c>new</c> can make,
    /// their options and the checks on them. No format is named in this program; a test fails if
    /// one ever is.
    /// </remarks>
    internal sealed partial class CliApp
    {
        private const string Help =
            "retrocommander-cli - RetroCommander's disk functions as commands\n" +
            "\n" +
            "  formats                          every disk format, with machines and what it can do\n" +
            "  new --list                       the blank disks that can be made, with their options\n" +
            "  new FILE --disk DISK [--machine MACHINE] [--set NAME=VALUE]... [--unformatted]\n" +
            "                                   make a blank disk (DISK is an id or a name from new --list)\n" +
            "  ls DISK[:PATH] [-l]              list a folder on a disk\n" +
            "  info DISK                        what the disk is: format, size, free space, details\n" +
            "  copy-out DISK:FILE [FOLDER]      copy a file off a disk into a folder (default: here)\n" +
            "  copy-in FILE... DISK[:PATH]      copy host files onto a disk\n" +
            "  conversions DISK                 the conversions the disk offers for copy-in/copy-out\n" +
            "  copy-in/copy-out ... --convert ID [--set NAME=VALUE]...  convert the bytes on the way\n" +
            "  copy DISK:FILE DISK[:PATH]       copy a file from one disk to another (or another folder)\n" +
            "  move DISK:FILE DISK[:PATH]       the same, then delete the original\n" +
            "  mkdir DISK:PATH                  make a folder\n" +
            "  rm DISK:FILE...                  delete files or folders\n" +
            "  rename DISK:FILE NEWNAME         rename a file or folder\n" +
            "  props DISK:FILE                  every property the disk keeps for a file\n" +
            "  view DISK:FILE [--text|--hex]    show a file: as text, or as hex when it is not text\n" +
            "  dirart list DISK                 directory names, the disk name and id as raw bytes\n" +
            "  dirart add DISK POSITION NAME    put a separator line (directory art) at POSITION\n" +
            "  dirart name DISK INDEX NAME      write the raw bytes of one entry's name\n" +
            "  dirart header DISK NAME ID       write the raw disk name and id\n" +
            "                                   NAME is hex bytes (\"B0 C0 A0\") or text:WORDS\n" +
            "  convert IMAGE TARGET [--chs C,H,S]\n" +
            "                                   a fixed VHD to a raw image, or a raw image to a fixed VHD\n" +
            "  assoc list | add EXT... | remove EXT... [--exe PATH]\n" +
            "                                   link disk image file types to RetroCommander in Windows\n" +
            "  script FILE                      run commands from a file, one per line (# for notes)\n" +
            "  (no command)                     an interactive prompt; exit or an empty input ends it\n" +
            "  A file copied off a disk takes NAME.EXT.xat with it when the disk's driver declares file\n" +
            "  properties; copy-in reads it back. Only what the driver declares is kept.\n" +
            "\n" +
            "  Any command: --ro (open read-only), --as LAYOUT (open an empty disk as that layout),\n" +
            "  --partition N (a partition of a hard disk). DISK is an image file or a URN.\n" +
            "  Exit code: 0 done, 1 the command failed, 2 the command line was wrong.\n";

        private readonly TextWriter _out;
        private readonly TextWriter _err;
        private readonly TextReader _in;

        /// <summary>
        /// Initializes a new instance of the <see cref="CliApp"/> class.
        /// </summary>
        /// <param name="output">
        /// Where the command's results go.
        /// </param>
        /// <param name="error">
        /// Where error messages go.
        /// </param>
        /// <param name="input">
        /// Where the interactive prompt reads its commands; null for none (the prompt ends at once).
        /// </param>
        public CliApp(TextWriter output, TextWriter error, TextReader? input = null)
        {
            _out = output;
            _err = error;
            _in = input ?? TextReader.Null;
        }

        /// <summary>
        /// Runs one command.
        /// </summary>
        /// <param name="args">
        /// The command and its arguments.
        /// </param>
        /// <returns>
        /// 0 when done, 1 when the command failed, 2 when the command line was wrong.
        /// </returns>
        public async Task<int> RunAsync(IReadOnlyList<string> args)
        {
            try
            {
                GlobalOptions o = GlobalOptions.Parse(args);
                if (o.Flags.Contains("--help") || o.Flags.Contains("-h") || (o.Words.Count > 0 && o.Words[0] == "help"))
                {
                    _out.Write(Help);
                    return 0;
                }
                if (o.Words.Count == 0)
                {
                    return await PromptAsync();
                }

                switch (o.Words[0])
                {
                    case "formats":
                        Formats();
                        return 0;
                    case "new":
                        New(o);
                        return 0;
                    case "ls":
                        await LsAsync(o);
                        return 0;
                    case "info":
                        await InfoAsync(o);
                        return 0;
                    case "copy-out":
                        await CopyOutAsync(o);
                        return 0;
                    case "copy-in":
                        await CopyInAsync(o);
                        return 0;
                    case "conversions":
                        await ConversionsAsync(o);
                        return 0;
                    case "copy":
                        await CopyOrMoveAsync(o, move: false);
                        return 0;
                    case "move":
                        await CopyOrMoveAsync(o, move: true);
                        return 0;
                    case "mkdir":
                        await MkdirAsync(o);
                        return 0;
                    case "rm":
                        await RmAsync(o);
                        return 0;
                    case "rename":
                        await RenameAsync(o);
                        return 0;
                    case "props":
                        await PropsAsync(o);
                        return 0;
                    case "view":
                        await ViewAsync(o);
                        return 0;
                    case "dirart":
                        await DirArtAsync(o);
                        return 0;
                    case "convert":
                        ConvertImage(o);
                        return 0;
                    case "assoc":
                        Assoc(o);
                        return 0;
                    case "script":
                        return await ScriptAsync(o);
                    default:
                        throw new CliException("unknown command '" + o.Words[0] + "'; run with --help", usage: true);
                }
            }
            catch (CliException ex)
            {
                _err.WriteLine("error: " + ex.Message);
                return ex.IsUsage ? 2 : 1;
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException
                || ex is InvalidOperationException || ex is NotSupportedException || ex is InvalidDataException)
            {
                // The drivers' own messages ("Disk full", "Directory full", a bad name) go out as they are.
                _err.WriteLine("error: " + ex.Message);
                return 1;
            }
        }

        // ---------------------------------------------------------------- formats

        private void Formats()
        {
            // One block per format, not a table: some driver descriptions are long, and a table
            // as wide as the longest one does not fit a terminal.
            SupportedFormat[] formats = SupportedFormats.List();
            for (int i = 0; i < formats.Length; i++)
            {
                SupportedFormat f = formats[i];
                _out.WriteLine(f.Description);
                if (f.Machines.Length > 0 && f.Machines != "-")
                {
                    _out.WriteLine("  machines:   " + f.Machines);
                }
                _out.WriteLine("  extensions: " + f.Extensions);
                _out.WriteLine("  can:        read" + (f.CanWrite ? ", write" : string.Empty) + (f.CanCreate ? ", new blank disk" : string.Empty));
            }
        }

        // ---------------------------------------------------------------- new

        private void New(GlobalOptions o)
        {
            if (o.Flags.Contains("--list"))
            {
                ListBlankDisks(o.Value("--machine"));
                return;
            }

            if (o.Words.Count != 2)
            {
                throw new CliException("new needs one file name: new FILE --disk DISK", usage: true);
            }
            string? disk = o.Value("--disk");
            if (disk == null)
            {
                throw new CliException("new needs --disk (an id or a name from: new --list)", usage: true);
            }

            BlankImageChoice choice = Catalogue.Resolve(o.Value("--machine"), disk);
            Dictionary<string, string> values = OptionValues(choice.Layout.Options, "the disk " + choice.Layout.Id, o);
            string path = o.Words[1];
            if (Path.GetExtension(path).Length == 0)
            {
                path += choice.Layout.Extension;
            }
            if (File.Exists(path))
            {
                throw new CliException("the file already exists: " + path);
            }

            bool unformatted = o.Flags.Contains("--unformatted");
            BlankImageCatalogue.Create(choice, values, path, unformatted);
            _out.WriteLine("created " + path + ": " + choice.Layout.Machine + ", " + choice.Layout.Name
                + (unformatted ? " (unformatted, all zeros)" : string.Empty));
        }

        // The --set NAME=VALUE pairs, checked by each option's own rule; an unknown name is an error.
        private static Dictionary<string, string> OptionValues(IReadOnlyList<BlankImageOption> options, string owner, GlobalOptions o)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (o.Values.TryGetValue("--set", out List<string>? sets))
            {
                for (int i = 0; i < sets.Count; i++)
                {
                    int eq = sets[i].IndexOf('=');
                    if (eq <= 0)
                    {
                        throw new CliException("--set needs NAME=VALUE, got '" + sets[i] + "'", usage: true);
                    }
                    values[sets[i].Substring(0, eq)] = sets[i].Substring(eq + 1);
                }
            }

            foreach (KeyValuePair<string, string> pair in values)
            {
                BlankImageOption? option = null;
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].Id == pair.Key)
                    {
                        option = options[i];
                    }
                }
                if (option == null)
                {
                    throw new CliException(owner + " has no option '" + pair.Key + "'", usage: true);
                }
                string? problem = option.Check(pair.Value);
                if (problem != null)
                {
                    throw new CliException(problem, usage: true);
                }
            }
            return values;
        }

        private void ListBlankDisks(string? machine)
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            string? current = null;
            for (int i = 0; i < all.Length; i++)
            {
                BlankImageLayout l = all[i].Layout;
                if (machine != null && !string.Equals(l.Machine, machine, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (current != l.Machine)
                {
                    current = l.Machine;
                    _out.WriteLine();
                    _out.WriteLine(l.Machine);
                }
                _out.WriteLine("  " + l.Id + "  " + l.Name + "  (" + l.SizeBytes.ToString("N0") + " bytes, " + l.Extension + ")");
                WriteOptions(l.Options);
            }
            if (current == null)
            {
                throw new CliException("no machine '" + machine + "'", usage: true);
            }
        }

        // ---------------------------------------------------------------- ls / info

        private async Task LsAsync(GlobalOptions o)
        {
            DiskTarget target = OneDisk(o, "ls DISK[:PATH]");
            IFileSystemPanel panel = await target.OpenAsync(o, write: false);
            try
            {
                if (target.Path != "/")
                {
                    await panel.NavigateAsync(target.Path);
                }
                await panel.RefreshAsync();

                // With a character ROM named and a disk whose driver can draw its names, the top
                // folder is listed as the machine draws it, from the raw directory.
                if (target.Path == "/"
                    && panel is DriverAdapterFileSystemPanel drawn
                    && drawn.Driver is IRawDirectoryNames raw
                    && GlyphDrawing.Open(o, raw.RawLayout) is GlyphDrawing drawing)
                {
                    WriteDrawnListing(raw, drawing);
                    return;
                }

                bool longForm = o.Flags.Contains("-l");
                string[][] rows = new string[panel.Entries.Count][];
                for (int i = 0; i < panel.Entries.Count; i++)
                {
                    IFileSystemEntry e = panel.Entries[i];
                    string name = e.IsDirectory ? e.Name + "/" : e.Name;
                    rows[i] = longForm
                        ? new[] { e.IsDirectory ? "<dir>" : e.Size.ToString("N0"), e.ModifiedDate?.ToString("yyyy-MM-dd HH:mm") ?? "-", name }
                        : new[] { name };
                }
                WriteTable(rows);
            }
            finally
            {
                target.Close(panel);
            }
        }

        private async Task InfoAsync(GlobalOptions o)
        {
            DiskTarget target = OneDisk(o, "info DISK");
            IFileSystemPanel panel = await target.OpenAsync(o, write: false);
            try
            {
                if (panel is not DriverAdapterFileSystemPanel adapter || adapter.Driver == null)
                {
                    throw new CliException("'" + panel.FileSystemType + "' volumes are not disk images; info has nothing to show");
                }

                // The same builder the Disk Info window draws from, so both show the same facts.
                VolumeInfo info = VolumeInfoBuilder.Build(adapter.Driver, target.IsUrn ? string.Empty : target.Disk, panel.IsReadOnly);
                for (int s = 0; s < info.Sections.Count; s++)
                {
                    VolumeInfoSection section = info.Sections[s];
                    _out.WriteLine(section.Title);
                    string[][] rows = new string[section.Fields.Count][];
                    for (int f = 0; f < section.Fields.Count; f++)
                    {
                        VolumeInfoField field = section.Fields[f];
                        rows[f] = new[] { "  " + field.Label, field.Detail.Length > 0 ? field.Value + "  (" + field.Detail + ")" : field.Value };
                    }
                    WriteTable(rows);
                    _out.WriteLine();
                }
            }
            finally
            {
                target.Close(panel);
            }
        }

        // ---------------------------------------------------------------- copy-out / copy-in

        private async Task CopyOutAsync(GlobalOptions o)
        {
            if (o.Words.Count < 2 || o.Words.Count > 3)
            {
                throw new CliException("copy-out DISK:FILE [FOLDER]", usage: true);
            }
            DiskTarget source = DiskTarget.Parse(o.Words[1]);
            if (source.Path == "/")
            {
                throw new CliException("name the file to copy: DISK:FILE", usage: true);
            }
            string folder = o.Words.Count == 3 ? o.Words[2] : ".";
            if (!Directory.Exists(folder))
            {
                throw new CliException("no such folder: " + folder);
            }

            IFileSystemPanel panel = await source.OpenAsync(o, write: false);
            try
            {
                IFileSystemEntry entry = await FindEntryAsync(panel, source.Path);
                if (entry.IsDirectory)
                {
                    throw new CliException(source.Path + " is a folder; copy-out copies files");
                }
                NativeFileSystemPanel host = new NativeFileSystemPanel("host", Path.GetFullPath(folder));
                await CopyAsync(entry, host, entry.FullPath, Path.GetFullPath(folder), Conversion(panel, o, toHost: true));
            }
            finally
            {
                source.Close(panel);
            }
        }

        private async Task CopyInAsync(GlobalOptions o)
        {
            if (o.Words.Count < 3)
            {
                throw new CliException("copy-in FILE... DISK[:PATH]", usage: true);
            }
            DiskTarget target = DiskTarget.Parse(o.Words[o.Words.Count - 1]);
            for (int i = 1; i < o.Words.Count - 1; i++)
            {
                if (!File.Exists(o.Words[i]))
                {
                    throw new CliException("no such file: " + o.Words[i]);
                }
            }

            IFileSystemPanel panel = await target.OpenAsync(o, write: true);
            try
            {
                if (target.Path != "/")
                {
                    await panel.NavigateAsync(target.Path);
                }
                ContentConversion? convert = Conversion(panel, o, toHost: false);
                for (int i = 1; i < o.Words.Count - 1; i++)
                {
                    NativeFileSystemEntry file = new NativeFileSystemEntry(new FileInfo(Path.GetFullPath(o.Words[i])));
                    await CopyAsync(file, panel, o.Words[i], panel.CurrentPath, convert);
                }
            }
            finally
            {
                // Closing the panel closes the driver, which saves the disk.
                target.Close(panel);
            }
        }

        // One file through the copy code the window uses (SmartFileOperations): it maps the file's
        // properties, and between a disk and a host folder writes or reads NAME.EXT.xat when the disk's
        // driver declares properties (XatSidecarRules).
        private async Task CopyAsync(IFileSystemEntry entry, IFileSystemPanel into, string from, string where, ContentConversion? convert)
        {
            CopyResult result = await new SmartFileOperations().CopyAsync(entry, into, new CopyOptions { Convert = convert });
            if (result.Skipped)
            {
                _out.WriteLine("skipped " + from + ": " + result.SkipReason);
                return;
            }
            if (!result.Success)
            {
                throw new CliException(from + ": " + result.ErrorMessage);
            }
            // TargetPath is where the file really landed: a driver may rename it or put it in a
            // folder of its own choosing.
            string landed = result.TargetPath ?? where.TrimEnd('/', '\\') + "/" + entry.Name;
            // The bytes written: a host file is measured, since a conversion changes the size;
            // on a disk the driver's own count may include its padding, so the input size is given.
            long written = File.Exists(landed) ? new FileInfo(landed).Length : entry.Size;
            _out.WriteLine("copied " + from + " -> " + landed + " (" + written.ToString("N0") + " bytes)");
        }
        // ---------------------------------------------------------------- conversions

        private async Task ConversionsAsync(GlobalOptions o)
        {
            DiskTarget target = OneDisk(o, "conversions DISK");
            IFileSystemPanel panel = await target.OpenAsync(o, write: false);
            try
            {
                ITransferConversionProvider? provider = TransferConversions.For(panel);
                if (provider == null || provider.Conversions.Count == 0)
                {
                    _out.WriteLine("this disk offers no conversions; files are copied as they are");
                    return;
                }
                for (int i = 0; i < provider.Conversions.Count; i++)
                {
                    TransferConversion c = provider.Conversions[i];
                    string ways = c.Directions == TransferDirections.Both ? "copy-in and copy-out"
                        : c.Directions == TransferDirections.ToHost ? "copy-out only" : "copy-in only";
                    _out.WriteLine(c.Id + "  " + c.Name + "  (" + ways + (c.DefaultForText ? ", the usual one for text" : string.Empty) + ")");
                    _out.WriteLine("    " + c.Description);
                    WriteOptions(c.Options);
                }
            }
            finally
            {
                target.Close(panel);
            }
        }

        // --convert ID [--set NAME=VALUE]... as the disk's driver offers it, or null when not given.
        private static ContentConversion? Conversion(IFileSystemPanel panel, GlobalOptions o, bool toHost)
        {
            string? id = o.Value("--convert");
            if (id == null)
            {
                return null;
            }
            ITransferConversionProvider? provider = TransferConversions.For(panel);
            TransferConversion? conversion = provider == null ? null : TransferConversions.Find(provider, id);
            if (provider == null || conversion == null)
            {
                throw new CliException("this disk has no conversion '" + id + "'; see: conversions DISK", usage: true);
            }
            Dictionary<string, string> values = OptionValues(conversion.Options, "the conversion " + conversion.Id, o);
            try
            {
                return TransferConversions.Make(provider, conversion, toHost, values);
            }
            catch (ArgumentException ex)
            {
                throw new CliException(ex.Message, usage: true);
            }
        }

        private void WriteOptions(IReadOnlyList<BlankImageOption> options)
        {
            for (int k = 0; k < options.Count; k++)
            {
                BlankImageOption opt = options[k];
                string kind = opt.Kind == BlankImageOptionKind.Choice
                    ? "one of: " + string.Join(" | ", opt.Choices)
                    : opt.Kind.ToString().ToLowerInvariant();
                _out.WriteLine("      --set " + opt.Id + "=...  " + opt.Label + " [" + kind
                    + (opt.Default.Length > 0 ? ", default " + opt.Default : string.Empty) + "]");
            }
        }
        // ---------------------------------------------------------------- helpers

        private static DiskTarget OneDisk(GlobalOptions o, string usage)
        {
            if (o.Words.Count != 2)
            {
                throw new CliException(usage, usage: true);
            }
            return DiskTarget.Parse(o.Words[1]);
        }

        // Finds a file by its path inside the disk: opens its folder, then matches the name the
        // way the disk lists it (case is ignored, as the panes do).
        private static async Task<IFileSystemEntry> FindEntryAsync(IFileSystemPanel panel, string path)
        {
            string p = path.Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            string folder = slash > 0 ? p.Substring(0, slash) : "/";
            string name = slash >= 0 ? p.Substring(slash + 1) : p;
            if (folder != "/")
            {
                await panel.NavigateAsync(folder);
            }
            await panel.RefreshAsync();
            for (int i = 0; i < panel.Entries.Count; i++)
            {
                if (string.Equals(panel.Entries[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return panel.Entries[i];
                }
            }
            throw new CliException("no file '" + path + "' on the disk");
        }

        // A disk file name made safe for the host: characters Windows refuses become '_'.
        private static string HostName(string name)
        {
            char[] bad = Path.GetInvalidFileNameChars();
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(bad, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }

        // Columns padded to the widest cell; the last column is not padded.
        private void WriteTable(string[][] rows)
        {
            if (rows.Length == 0)
            {
                return;
            }
            int columns = rows[0].Length;
            int[] widths = new int[columns];
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    widths[c] = Math.Max(widths[c], rows[r][c].Length);
                }
            }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int r = 0; r < rows.Length; r++)
            {
                sb.Clear();
                for (int c = 0; c < columns; c++)
                {
                    sb.Append(c == columns - 1 ? rows[r][c] : rows[r][c].PadRight(widths[c] + 2));
                }
                _out.WriteLine(sb.ToString().TrimEnd());
            }
        }
    }
}
