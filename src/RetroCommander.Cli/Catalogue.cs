//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using RetroCommander.Core.Services;

namespace RetroCommander.Cli
{
    /// <summary>
    /// Looks up the blank-disk layouts the drivers describe, by id or by machine and name.
    /// </summary>
    internal static class Catalogue
    {
        /// <summary>
        /// Finds a layout by its id.
        /// </summary>
        /// <param name="layoutId">
        /// The id, as <c>new --list</c> prints it.
        /// </param>
        /// <returns>
        /// The layout, or null when no driver describes it.
        /// </returns>
        public static BlankImageChoice? FindLayout(string layoutId)
        {
            BlankImageChoice[] all = BlankImageCatalogue.List();
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].Layout.Id, layoutId, StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a layout from what was typed after <c>--disk</c>: its id, or its name, which may
        /// be shortened to the start of the name as long as only one disk of the machine matches.
        /// </summary>
        /// <param name="machine">
        /// The machine, or null to search every machine (then only an id or a full name matches).
        /// </param>
        /// <param name="disk">
        /// The id or name typed.
        /// </param>
        /// <returns>
        /// The layout.
        /// </returns>
        /// <exception cref="CliException">
        /// Thrown when nothing or more than one disk matches; the message lists the choices.
        /// </exception>
        public static BlankImageChoice Resolve(string? machine, string disk)
        {
            BlankImageChoice? byId = FindLayout(disk);
            if (byId != null)
            {
                return byId;
            }

            BlankImageChoice[] all = BlankImageCatalogue.List();
            BlankImageChoice? found = null;
            int matches = 0;
            System.Text.StringBuilder choices = new System.Text.StringBuilder();
            for (int i = 0; i < all.Length; i++)
            {
                if (machine != null && !string.Equals(all[i].Layout.Machine, machine, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                choices.Append("\n  ").Append(all[i].Layout.Id).Append("  ").Append(all[i].Layout.Name);
                bool exact = string.Equals(all[i].Layout.Name, disk, StringComparison.OrdinalIgnoreCase);
                bool start = machine != null && all[i].Layout.Name.StartsWith(disk, StringComparison.OrdinalIgnoreCase);
                if (exact)
                {
                    return all[i];
                }
                if (start)
                {
                    found = all[i];
                    matches++;
                }
            }

            if (matches == 1)
            {
                return found!;
            }
            if (choices.Length == 0)
            {
                throw new CliException("no machine '" + machine + "'; see: new --list", usage: true);
            }
            if (machine == null)
            {
                // Every disk of every machine would be a page of text; point at the list instead.
                throw new CliException("no disk '" + disk + "'; give an id from new --list, or --machine with a name", usage: true);
            }
            throw new CliException(
                (matches == 0 ? "no disk matches '" : "more than one disk matches '") + disk + "'. Choices:" + choices,
                usage: true);
        }
    }
}
