//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using System.Threading.Tasks;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Services;
using RetroIO.Storage.Core;

namespace RetroCommander.Cli
{
    /// <summary>
    /// A disk named on the command line, with an optional path inside it: <c>disk.d64</c>,
    /// <c>disk.d64:HELLO</c>, <c>disk.img:/USER1/FILE.TXT</c>, or a URN such as <c>tnfs://host/</c>.
    /// </summary>
    /// <remarks>
    /// The split between the disk and the path is the first colon whose left part is an existing
    /// file, so a Windows drive letter (<c>C:\disks\a.d64:HELLO</c>) is not taken for one. A URN
    /// (anything with <c>://</c>) is the disk as a whole and is opened at its root.
    /// </remarks>
    internal sealed class DiskTarget : IDisposable
    {
        // The image file this target opened itself (for --as), closed in Dispose.
        private FileStream? _stream;

        private DiskTarget(string disk, string path)
        {
            Disk = disk;
            Path = path;
        }

        /// <summary>
        /// Gets the disk image file or URN.
        /// </summary>
        public string Disk { get; }

        /// <summary>
        /// Gets the path inside the disk; <c>/</c> for its root.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets a value indicating whether the disk is a URN rather than a file.
        /// </summary>
        public bool IsUrn => Disk.Contains("://", StringComparison.Ordinal);

        /// <summary>
        /// Splits a command-line argument into the disk and the path inside it.
        /// </summary>
        /// <param name="arg">
        /// The argument as typed.
        /// </param>
        /// <returns>
        /// The disk and path.
        /// </returns>
        public static DiskTarget Parse(string arg)
        {
            if (arg.Contains("://", StringComparison.Ordinal))
            {
                return new DiskTarget(arg, "/");
            }

            for (int i = 0; i < arg.Length; i++)
            {
                if (arg[i] != ':' || i == 0)
                {
                    continue;
                }
                string left = arg.Substring(0, i);
                if (File.Exists(left))
                {
                    string inside = arg.Substring(i + 1);
                    return new DiskTarget(left, inside.Length == 0 ? "/" : inside);
                }
            }

            return new DiskTarget(arg, "/");
        }

        /// <summary>
        /// Opens the disk as a panel, the way the window opens it.
        /// </summary>
        /// <param name="options">
        /// The global options: read-only, a layout to open an empty disk as, a partition.
        /// </param>
        /// <param name="write">
        /// True when the command changes the disk; a command that only reads opens it read-only,
        /// so it never takes the write lock another program may need.
        /// </param>
        /// <returns>
        /// The panel, at the disk's root.
        /// </returns>
        /// <exception cref="CliException">
        /// Thrown when the disk does not exist or no driver can open it.
        /// </exception>
        public async Task<IFileSystemPanel> OpenAsync(GlobalOptions options, bool write)
        {
            bool readOnly = options.ReadOnly || !write;
            if (write && options.ReadOnly)
            {
                throw new CliException("this command changes the disk, and --ro was given");
            }

            if (!IsUrn && !File.Exists(Disk))
            {
                throw new CliException("no such disk image: " + Disk);
            }

            if (options.Layout != null)
            {
                return OpenAsLayout(options.Layout, readOnly);
            }

            string urn = IsUrn ? Disk : "file:///" + System.IO.Path.GetFullPath(Disk).Replace('\\', '/');
            if (options.Partition != null)
            {
                urn += "#" + options.Partition;
            }

            VolumeProviderFactory factory = new VolumeProviderFactory(null, DriverFactoryBuilder.CreateWithAllDrivers());
            try
            {
                return await factory.CreatePanelAsync(urn, readOnly ? MountMode.ReadOnly : MountMode.ReadWrite);
            }
            catch (NotSupportedException ex)
            {
                throw new CliException(ex.Message);
            }
        }

        // An empty disk whose bytes are the same on several machines cannot be told apart by
        // detection; --as names the layout to open it as, through the driver that describes it.
        private IFileSystemPanel OpenAsLayout(string layout, bool readOnly)
        {
            BlankImageChoice? choice = Catalogue.FindLayout(layout);
            if (choice == null)
            {
                throw new CliException("no disk layout '" + layout + "'; see: new --list");
            }

            FileStream stream = new FileStream(
                Disk,
                FileMode.Open,
                readOnly ? FileAccess.Read : FileAccess.ReadWrite,
                readOnly ? FileShare.ReadWrite : FileShare.Read);
            _stream = stream;
            try
            {
                IFileSystemDriver driver = choice.Provider.Mount(stream, readOnly, choice.Layout.Id);
                string urn = "file:///" + System.IO.Path.GetFullPath(Disk).Replace('\\', '/');
                return new DriverAdapterFileSystemPanel("cli", driver, urn);
            }
            catch
            {
                stream.Dispose();
                _stream = null;
                throw;
            }
        }

        /// <summary>
        /// Closes the panel, then the image file if this target opened it.
        /// </summary>
        /// <param name="panel">
        /// The panel <see cref="OpenAsync"/> returned.
        /// </param>
        /// <remarks>
        /// The file is closed here as well as by the driver: whoever opens a file closes it, and
        /// some RetroIO drivers do not close the stream they were given (BUGS.md B1).
        /// </remarks>
        public void Close(IFileSystemPanel panel)
        {
            (panel as IDisposable)?.Dispose();
            Dispose();
        }

        /// <summary>
        /// Closes the image file if this target opened it.
        /// </summary>
        public void Dispose()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }
}
