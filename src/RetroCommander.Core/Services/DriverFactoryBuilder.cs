//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Drivers;
using RetroIO.Storage.Ndfs.Drivers;

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// Builder for creating DriverFactory with all standard drivers registered.
    /// Centralizes driver registration to keep it OUT of UI layer.
    /// </summary>
    public static class DriverFactoryBuilder
    {
        /// <summary>
        /// Creates a DriverFactory with ALL standard RetroFS drivers registered.
        /// </summary>
        public static DriverFactory CreateWithAllDrivers()
        {
            var factory = new DriverFactory();

            // Register all standard RetroFS drivers
            factory.Register(new NdfsDriverProvider());                  // Nord Disk File System
            factory.Register(new CommodoreDiskDriverProvider());        // Commodore D64/D71/D81
            factory.Register(new T64DriverProvider());                  // Commodore T64 tape archives
            factory.Register(new P00DriverProvider());                  // Commodore PC64 .P00 program container
            factory.Register(new PrgDriverProvider());                  // Commodore PRG program file
            factory.Register(new CrtDriverProvider());                  // Commodore CRT cartridge image
            factory.Register(new TapDriverProvider());                  // Commodore TAP tape image
            factory.Register(new DragonDskDriverProvider());            // DragonDOS DSK
            factory.Register(new DragonVdkDriverProvider());            // DragonDOS VDK
            factory.Register(new PcFloppyDriverProvider());             // PC Floppy (FAT12/16/32)
            factory.Register(new V7DriverProvider());                   // UNIX V7 filesystem (BSD 2.11)
            factory.Register(new FfsDriverProvider());                  // Berkeley Fast File System (FFS/UFS) for SunOS 3.4 / 4.2BSD

            return factory;
        }
    }
}
