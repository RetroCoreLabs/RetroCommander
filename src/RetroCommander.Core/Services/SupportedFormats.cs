//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// One disk format RetroCommander can open, as the Help "Supported formats" window shows it.
    /// </summary>
    public sealed class SupportedFormat
    {
        /// <summary>
        /// Gets the driver's own short name for the format, for example <c>CPM</c>.
        /// </summary>
        public required string FileSystemType { get; init; }

        /// <summary>
        /// Gets the driver's description of the format.
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// Gets the machines or systems the format covers, comma separated; "-" when the driver
        /// does not say.
        /// </summary>
        public required string Machines { get; init; }

        /// <summary>
        /// Gets the file extensions, space separated, for example <c>.d64 .d71 .d81</c>.
        /// </summary>
        public required string Extensions { get; init; }

        /// <summary>
        /// Gets a value indicating whether files can be written to a disk of this format.
        /// </summary>
        public required bool CanWrite { get; init; }

        /// <summary>
        /// Gets a value indicating whether a new blank disk of this format can be created.
        /// </summary>
        public required bool CanCreate { get; init; }
    }

    /// <summary>
    /// The disk formats RetroCommander supports, read from the registered RetroIO drivers.
    /// </summary>
    /// <remarks>
    /// Nothing here is a hand-kept list. Each row comes from one driver provider:
    ///  - the name, description, extensions, machines and the write flag from the provider itself.
    ///  - "can create" from whether the provider also describes blank disks it can make
    ///    (<see cref="IBlankImageProvider"/>), the same list the New Image window offers.
    /// Every format listed can be read, so there is no read flag.
    /// </remarks>
    public static class SupportedFormats
    {
        /// <summary>
        /// Builds the list, one row per registered driver, sorted by description.
        /// </summary>
        /// <returns>
        /// The formats, in a new array the caller may keep.
        /// </returns>
        public static SupportedFormat[] List()
        {
            DriverFactory factory = DriverFactoryBuilder.CreateWithAllDrivers();
            string[] types = factory.GetRegisteredTypes();
            List<SupportedFormat> rows = new List<SupportedFormat>(types.Length);
            for (int t = 0; t < types.Length; t++)
            {
                IFileSystemDriverProvider? provider = factory.GetProvider(types[t]);
                if (provider == null)
                {
                    continue;
                }

                rows.Add(new SupportedFormat
                {
                    FileSystemType = provider.FileSystemType,
                    Description = provider.Description,
                    // A driver that does not say which machines it covers shows "-", never a guess.
                    Machines = provider.Machines.Count == 0 ? "-" : Join(provider.Machines, ", "),
                    Extensions = Join(provider.FileExtensions, " "),
                    CanWrite = provider.SupportsWrite,
                    CanCreate = provider is IBlankImageProvider blank && blank.BlankLayouts.Count > 0
                });
            }

            SupportedFormat[] result = rows.ToArray();
            Array.Sort(result, static (a, b) => string.Compare(a.Description, b.Description, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>
        /// Checks whether a format matches a search text, in any of its shown columns.
        /// </summary>
        /// <param name="format">
        /// The format to test.
        /// </param>
        /// <param name="text">
        /// The search text; empty or white space matches everything.
        /// </param>
        /// <returns>
        /// True when the text is found, ignoring case.
        /// </returns>
        public static bool Matches(SupportedFormat format, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string s = text.Trim();
            return format.Description.Contains(s, StringComparison.OrdinalIgnoreCase)
                || format.FileSystemType.Contains(s, StringComparison.OrdinalIgnoreCase)
                || format.Machines.Contains(s, StringComparison.OrdinalIgnoreCase)
                || format.Extensions.Contains(s, StringComparison.OrdinalIgnoreCase);
        }

        private static string Join(IReadOnlyList<string> items, string separator)
        {
            if (items.Count == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(separator);
                }
                sb.Append(items[i]);
            }
            return sb.ToString();
        }
    }
}
