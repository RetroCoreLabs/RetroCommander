//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;

namespace RetroCommander.Cli
{
    /// <summary>
    /// A command line split into its words and its options.
    /// </summary>
    /// <remarks>
    /// Options any command takes:
    ///  - <c>--ro</c>: open the disk read-only.
    ///  - <c>--as LAYOUT</c>: open the disk as a given blank-disk layout (see <c>new --list</c>),
    ///    for an empty disk whose bytes are the same on several machines.
    ///  - <c>--partition N</c>: the partition of a partitioned hard disk, from 0.
    /// Options of one command (<c>--set name=value</c>, <c>-l</c>, ...) are kept in
    /// <see cref="Values"/> and <see cref="Flags"/> for that command to read. <c>--set</c> may be
    /// given more than once; every other option once.
    /// </remarks>
    internal sealed class GlobalOptions
    {
        // Options that take a value; anything else starting with '-' is a flag.
        private static readonly string[] s_valueOptions =
        {
            "--as", "--partition", "--machine", "--disk", "--set", "--convert", "--exe", "--chs", "--charset"
        };

        /// <summary>
        /// Gets the words that are not options, in order: the command, then its arguments.
        /// </summary>
        public List<string> Words { get; } = new List<string>();

        /// <summary>
        /// Gets the value options, by name; <c>--set</c> keeps every value given.
        /// </summary>
        public Dictionary<string, List<string>> Values { get; } = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the flags given, such as <c>-l</c> or <c>--unformatted</c>.
        /// </summary>
        public HashSet<string> Flags { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets a value indicating whether the disk is to be opened read-only.
        /// </summary>
        public bool ReadOnly => Flags.Contains("--ro");

        /// <summary>
        /// Gets the layout the disk is to be opened as, or null to detect it.
        /// </summary>
        public string? Layout => Value("--as");

        /// <summary>
        /// Gets the partition number, or null for an unpartitioned disk.
        /// </summary>
        public string? Partition => Value("--partition");

        /// <summary>
        /// Splits a command line.
        /// </summary>
        /// <param name="args">
        /// The arguments as given.
        /// </param>
        /// <returns>
        /// The words and options.
        /// </returns>
        /// <exception cref="CliException">
        /// Thrown when an option that takes a value is the last word.
        /// </exception>
        public static GlobalOptions Parse(IReadOnlyList<string> args)
        {
            GlobalOptions o = new GlobalOptions();
            for (int i = 0; i < args.Count; i++)
            {
                string a = args[i];
                if (a.Length > 1 && a[0] == '-' && !IsNumber(a))
                {
                    if (Array.IndexOf(s_valueOptions, a) >= 0)
                    {
                        if (i + 1 >= args.Count)
                        {
                            throw new CliException(a + " needs a value");
                        }
                        if (!o.Values.TryGetValue(a, out List<string>? list))
                        {
                            list = new List<string>();
                            o.Values[a] = list;
                        }
                        list.Add(args[++i]);
                    }
                    else
                    {
                        o.Flags.Add(a);
                    }
                }
                else
                {
                    o.Words.Add(a);
                }
            }
            return o;
        }

        /// <summary>
        /// Gets the last value of an option, or null when it was not given.
        /// </summary>
        /// <param name="name">
        /// The option, with its dashes.
        /// </param>
        /// <returns>
        /// The value, or null.
        /// </returns>
        public string? Value(string name)
        {
            return Values.TryGetValue(name, out List<string>? list) && list.Count > 0 ? list[list.Count - 1] : null;
        }

        private static bool IsNumber(string s) => long.TryParse(s, out _);
    }

    /// <summary>
    /// A command failed for a reason the user can act on; the message is printed as it is.
    /// </summary>
    internal sealed class CliException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CliException"/> class.
        /// </summary>
        /// <param name="message">
        /// What went wrong, in words for the user.
        /// </param>
        /// <param name="usage">
        /// True when the command line itself was wrong (exit code 2), false when the command
        /// could not be done (exit code 1).
        /// </param>
        public CliException(string message, bool usage = false)
            : base(message)
        {
            IsUsage = usage;
        }

        /// <summary>
        /// Gets a value indicating whether the command line itself was wrong.
        /// </summary>
        public bool IsUsage { get; }
    }
}
