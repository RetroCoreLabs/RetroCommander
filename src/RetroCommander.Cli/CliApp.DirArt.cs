//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Core;

namespace RetroCommander.Cli
{
    /// <summary>
    /// The dirart command: directory names, separator lines and the disk header as raw bytes.
    /// </summary>
    /// <remarks>
    /// Everything about the format (name length, pad byte, character set, what a separator is) comes
    /// from the driver through <see cref="IRawDirectoryNames"/>; nothing here names a format. A disk
    /// whose driver does not offer it is refused with that said.
    /// </remarks>
    internal sealed partial class CliApp
    {
        private const string DirArtUsage =
            "dirart list DISK | add DISK POSITION NAME | name DISK INDEX NAME | header DISK NAME ID";

        private async Task DirArtAsync(GlobalOptions o)
        {
            if (o.Words.Count < 3)
            {
                throw new CliException(DirArtUsage, usage: true);
            }

            string action = o.Words[1];
            bool write;
            int words;
            switch (action)
            {
                case "list":
                    write = false;
                    words = 3;
                    break;
                case "add":
                case "name":
                case "header":
                    write = true;
                    words = 5;
                    break;
                default:
                    throw new CliException("unknown dirart action '" + action + "'; " + DirArtUsage, usage: true);
            }

            if (o.Words.Count != words)
            {
                throw new CliException(DirArtUsage, usage: true);
            }

            DiskTarget target = DiskTarget.Parse(o.Words[2]);
            IFileSystemPanel panel = await target.OpenAsync(o, write);
            try
            {
                if (panel is not DriverAdapterFileSystemPanel onDisk || onDisk.Driver is not IRawDirectoryNames raw)
                {
                    throw new CliException("this disk's driver does not offer raw directory names, so it has no directory art");
                }

                RawDirectoryLayout layout = raw.RawLayout;
                switch (action)
                {
                    case "list":
                        ListRaw(raw, layout);
                        if (GlyphDrawing.Open(o, layout) is GlyphDrawing drawing)
                        {
                            _out.WriteLine();
                            WriteDrawnListing(raw, drawing);
                        }

                        break;
                    case "add":
                    {
                        int position = Number(o.Words[3], "POSITION");
                        raw.InsertSeparator(position, Name(raw, o.Words[4], layout.NameLength, "NAME"));
                        _out.WriteLine("added a " + layout.SeparatorTypeName + " line at " + position);
                        break;
                    }
                    case "name":
                    {
                        int index = Number(o.Words[3], "INDEX");
                        raw.WriteRawName(index, Name(raw, o.Words[4], layout.NameLength, "NAME"));
                        _out.WriteLine("named entry " + index);
                        break;
                    }
                    default:
                        raw.WriteRawVolumeHeader(
                            Name(raw, o.Words[3], layout.VolumeNameLength, "NAME"),
                            Name(raw, o.Words[4], layout.VolumeIdLength, "ID"));
                        _out.WriteLine("wrote the disk name and id");
                        break;
                }
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new CliException(e.Message.Split('(')[0].Trim());
            }
            finally
            {
                target.Close(panel);
            }
        }

        // The disk header, then one line per entry: index, type, blocks, the name as hex, and the
        // name as ASCII (20h to 7Eh as themselves, anything else as a dot). The character set the
        // bytes are really in is named on the first line; this tool has no table for drawing it.
        private void ListRaw(IRawDirectoryNames raw, RawDirectoryLayout layout)
        {
            _out.WriteLine("Character set " + layout.CharacterSet + "; a name is " + layout.NameLength
                + " bytes, padded with " + layout.PadByte.ToString("X2", CultureInfo.InvariantCulture)
                + "h; a separator is a " + layout.SeparatorTypeName + " entry.");
            _out.WriteLine(layout.Description);
            _out.WriteLine();
            byte[] name = raw.ReadRawVolumeName();
            byte[] id = raw.ReadRawVolumeId();
            _out.WriteLine("disk name  " + Hex(name) + "  " + AsAscii(name));
            _out.WriteLine("disk id    " + Hex(id) + "  " + AsAscii(id));
            _out.WriteLine();

            RawDirectoryEntry[] entries = raw.ReadRawDirectory();
            for (int i = 0; i < entries.Length; i++)
            {
                RawDirectoryEntry e = entries[i];
                _out.WriteLine(e.Index.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "  "
                    + e.TypeName.PadRight(5) + e.Blocks.ToString(CultureInfo.InvariantCulture).PadLeft(5) + "  "
                    + Hex(e.Name) + "  " + AsAscii(e.Name));
            }
        }

        // The listing as the machine draws it: the disk name and id on top, then per entry its
        // blocks, its name and its type, each name drawn from the character ROM.
        private void WriteDrawnListing(IRawDirectoryNames raw, GlyphDrawing drawing)
        {
            _out.WriteLine("(drawn in the " + drawing.SetName + " set)");
            string[] name = drawing.Draw(raw.ReadRawVolumeName());
            string[] id = drawing.Draw(raw.ReadRawVolumeId());
            for (int l = 0; l < name.Length; l++)
            {
                _out.WriteLine("      " + name[l] + " " + id[l]);
            }

            RawDirectoryEntry[] entries = raw.ReadRawDirectory();
            for (int i = 0; i < entries.Length; i++)
            {
                string[] lines = drawing.Draw(entries[i].Name);
                for (int l = 0; l < lines.Length; l++)
                {
                    string left = l == 0 ? entries[i].Blocks.ToString(CultureInfo.InvariantCulture).PadLeft(5) + " " : "      ";
                    string right = l == 0 ? " " + entries[i].TypeName : string.Empty;
                    _out.WriteLine(left + lines[l] + right);
                }
            }
        }

        private static int Number(string text, string what)
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
            {
                throw new CliException(what + " must be a number from 0, not '" + text + "'", usage: true);
            }

            return n;
        }

        // A name as hex bytes ("B0 B1 C0" or "B0B1C0") or as "text:WORDS"; padded with the driver's
        // pad byte up to the length, and refused when longer. Text becomes bytes by the driver's own
        // rule for file names (IRawDirectoryNames.EncodeText), so the CLI knows no character set: on
        // a Commodore disk "text:hello" and "text:HELLO" are both 48 45 4C 4C 4F.
        internal static byte[] Name(IRawDirectoryNames raw, string text, int length, string what)
        {
            byte pad = raw.RawLayout.PadByte;
            byte[] given;
            if (text.StartsWith("text:", StringComparison.Ordinal))
            {
                try
                {
                    given = raw.EncodeText(text.Substring(5));
                }
                catch (ArgumentException e)
                {
                    throw new CliException(what + ": " + e.Message, usage: true);
                }
            }
            else
            {
                StringBuilder digits = new StringBuilder(text.Length);
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] != ' ')
                    {
                        digits.Append(text[i]);
                    }
                }

                if (digits.Length % 2 != 0)
                {
                    throw new CliException(what + ": hex needs two digits per byte: '" + text + "'", usage: true);
                }

                given = new byte[digits.Length / 2];
                for (int i = 0; i < given.Length; i++)
                {
                    if (!byte.TryParse(digits.ToString(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out given[i]))
                    {
                        throw new CliException(what + ": not hex: '" + text + "' (plain text is written text:WORDS)", usage: true);
                    }
                }
            }

            if (given.Length > length)
            {
                throw new CliException(what + " is " + given.Length + " bytes; this disk takes at most " + length, usage: true);
            }

            byte[] full = new byte[length];
            given.CopyTo(full, 0);
            for (int i = given.Length; i < length; i++)
            {
                full[i] = pad;
            }

            return full;
        }

        private static string Hex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        private static string AsAscii(byte[] bytes)
        {
            char[] c = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                c[i] = bytes[i] >= 0x20 && bytes[i] <= 0x7E ? (char)bytes[i] : '.';
            }

            return new string(c);
        }
    }
}
