//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Core;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Services;

/// <summary>
/// The copy conversions a disk offers (text with the ND parity bit cleared, CP/M text cut at its
/// end mark, PETSCII as host text, ...), read from the disk driver's own metadata.
/// </summary>
/// <remarks>
/// RetroCommander names no conversion: the list, the names, the options, the checks on the
/// options and the conversion itself all come from the driver's provider through RetroIO
/// <see cref="ITransferConversionProvider"/>. A disk whose provider offers none has none.
/// </remarks>
public static class TransferConversions
{
    /// <summary>
    /// Gets the provider of the conversions for a mounted disk, or null when it offers none.
    /// </summary>
    /// <param name="panel">
    /// The open disk.
    /// </param>
    /// <returns>
    /// The provider, or null for a host folder or a disk with no conversions.
    /// </returns>
    public static ITransferConversionProvider? For(IFileSystemPanel panel)
    {
        if (panel is not DriverAdapterFileSystemPanel disk)
        {
            return null;
        }
        // The provider that made this driver: the one whose CreateDriver gives the same class.
        // Matching by name does not work: a provider and the disk it mounts may report different
        // names (the Commodore provider is "Commodore", its mounted disk says "CBM-DOS").
        Type mounted = disk.Driver.GetType();
        DriverFactory factory = DriverFactoryBuilder.CreateWithAllDrivers();
        string[] types = factory.GetRegisteredTypes();
        for (int i = 0; i < types.Length; i++)
        {
            if (factory.GetProvider(types[i]) is ITransferConversionProvider conversions &&
                conversions is IFileSystemDriverProvider maker)
            {
                using IFileSystemDriver probe = maker.CreateDriver();
                if (probe.GetType() == mounted)
                {
                    return conversions;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds one conversion a provider offers, by its id.
    /// </summary>
    /// <param name="provider">
    /// The provider.
    /// </param>
    /// <param name="id">
    /// The conversion id, as the provider lists it.
    /// </param>
    /// <returns>
    /// The conversion, or null when the provider has none with that id.
    /// </returns>
    public static TransferConversion? Find(ITransferConversionProvider provider, string id)
    {
        IReadOnlyList<TransferConversion> all = provider.Conversions;
        for (int i = 0; i < all.Count; i++)
        {
            if (string.Equals(all[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return all[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Makes the copy step for one conversion in one direction.
    /// </summary>
    /// <param name="provider">
    /// The provider that offers the conversion.
    /// </param>
    /// <param name="conversion">
    /// The conversion.
    /// </param>
    /// <param name="toHost">
    /// True when copying off the disk, false when copying onto it.
    /// </param>
    /// <param name="options">
    /// The conversion's option values by id; a missing option takes its default.
    /// </param>
    /// <returns>
    /// The conversion, ready for <see cref="CopyOptions.Convert"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the conversion does not work in that direction.
    /// </exception>
    public static ContentConversion Make(ITransferConversionProvider provider, TransferConversion conversion, bool toHost, IReadOnlyDictionary<string, string> options)
    {
        TransferDirections needed = toHost ? TransferDirections.ToHost : TransferDirections.FromHost;
        if ((conversion.Directions & needed) == 0)
        {
            throw new ArgumentException(
                "the conversion " + conversion.Id + " does not work when copying " + (toHost ? "off" : "onto") + " the disk");
        }
        string id = conversion.Id;
        return toHost
            ? content => provider.ToHost(id, content, options)
            : content => provider.FromHost(id, content, options);
    }
}
