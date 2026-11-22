//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// One kind of blank disk that can be created, with the driver that makes it.
    /// </summary>
    public sealed class BlankImageChoice
    {
        /// <summary>
        /// Gets the driver provider that describes and creates this disk.
        /// </summary>
        public required IBlankImageProvider Provider { get; init; }

        /// <summary>
        /// Gets the provider's short format name, for example <c>CPM</c>.
        /// </summary>
        public required string FileSystemType { get; init; }

        /// <summary>
        /// Gets the layout: machine, name, size, extension and options.
        /// </summary>
        public required BlankImageLayout Layout { get; init; }
    }

    /// <summary>
    /// Every blank disk the registered RetroIO drivers can create, for File, New Image.
    /// </summary>
    /// <remarks>
    /// The list is read from the drivers (<see cref="IBlankImageProvider"/>), never kept by hand:
    /// a format RetroIO learns to create shows up in the window with no change here. RetroIO's own
    /// rule test requires every format that can write to describe its blank disks.
    /// </remarks>
    public static class BlankImageCatalogue
    {
        /// <summary>
        /// Lists every blank disk layout, sorted by machine and then by size.
        /// </summary>
        /// <returns>
        /// The choices, in a new array the caller may keep.
        /// </returns>
        public static BlankImageChoice[] List()
        {
            DriverFactory factory = DriverFactoryBuilder.CreateWithAllDrivers();
            string[] types = factory.GetRegisteredTypes();
            List<BlankImageChoice> choices = new List<BlankImageChoice>();
            for (int t = 0; t < types.Length; t++)
            {
                if (factory.GetProvider(types[t]) is not IBlankImageProvider blank)
                {
                    continue;
                }

                IReadOnlyList<BlankImageLayout> layouts = blank.BlankLayouts;
                for (int i = 0; i < layouts.Count; i++)
                {
                    choices.Add(new BlankImageChoice
                    {
                        Provider = blank,
                        FileSystemType = ((IFileSystemDriverProvider)blank).FileSystemType,
                        Layout = layouts[i]
                    });
                }
            }

            BlankImageChoice[] result = choices.ToArray();
            Array.Sort(result, static (a, b) =>
            {
                int byMachine = string.Compare(a.Layout.Machine, b.Layout.Machine, StringComparison.OrdinalIgnoreCase);
                return byMachine != 0 ? byMachine : a.Layout.SizeBytes.CompareTo(b.Layout.SizeBytes);
            });
            return result;
        }

        /// <summary>
        /// Lists the machines that have at least one blank disk, in the order of <see cref="List"/>.
        /// </summary>
        /// <param name="choices">
        /// The choices from <see cref="List"/>.
        /// </param>
        /// <returns>
        /// Each machine name once.
        /// </returns>
        public static string[] Machines(BlankImageChoice[] choices)
        {
            List<string> machines = new List<string>();
            for (int i = 0; i < choices.Length; i++)
            {
                string m = choices[i].Layout.Machine;
                if (machines.Count == 0 || !string.Equals(machines[machines.Count - 1], m, StringComparison.OrdinalIgnoreCase))
                {
                    machines.Add(m);
                }
            }
            return machines.ToArray();
        }

        /// <summary>
        /// Creates a blank disk and writes it to a new file.
        /// </summary>
        /// <param name="choice">
        /// The disk to create.
        /// </param>
        /// <param name="options">
        /// The option values by option id; a missing one takes the option's default.
        /// </param>
        /// <param name="path">
        /// The file to write. It must not exist yet.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when an option value is not allowed; the message names the option.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown when <paramref name="path"/> already exists or cannot be written.
        /// </exception>
        public static void Create(BlankImageChoice choice, IReadOnlyDictionary<string, string> options, string path)
        {
            byte[] image = choice.Provider.CreateBlank(choice.Layout.Id, options);
            using FileStream fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            fs.Write(image, 0, image.Length);
        }
    }
}
