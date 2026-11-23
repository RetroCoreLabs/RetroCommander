//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.Collections.Generic;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Operations;

/// <summary>
/// Decides from the disk drivers' own metadata whether a ".xat" file goes with a copied file, and
/// which properties it holds.
/// </summary>
/// <remarks>
/// <para><b>The rule</b></para>
/// A file copied off a disk into a host folder takes its properties with it in NAME.EXT.xat beside it,
/// and a file copied from a host folder onto a disk gets them back from that NAME.EXT.xat:
///  - only when the disk's driver declares, through <see cref="IExtendedAttributeSupport.GetCapabilities"/>,
///    that its files carry properties of their own (<see cref="FileSystemCapabilities.SupportsExtendedAttributes"/>);
///  - and only the properties it names in <see cref="FileSystemCapabilities.SupportedPropertyKeys"/>.
/// No format is named here. A driver that declares nothing gets no .xat; a driver that declares
/// properties gets them kept, whatever its format.
/// </remarks>
public static class XatSidecarRules
{
    /// <summary>
    /// Gets the properties a driver declares its files carry, or null when it declares none.
    /// </summary>
    /// <param name="driver">
    /// The driver, or null for a host folder or a volume with no driver.
    /// </param>
    /// <returns>
    /// The driver's declaration, or null.
    /// </returns>
    public static FileSystemCapabilities? Declared(IFileSystemDriver? driver)
    {
        if (driver is not IExtendedAttributeSupport support)
        {
            return null;
        }
        FileSystemCapabilities caps = support.GetCapabilities();
        return caps.SupportsExtendedAttributes && caps.SupportedPropertyKeys.Length > 0 ? caps : null;
    }

    /// <summary>
    /// Gets the declaration of the driver behind a disk entry, or null.
    /// </summary>
    /// <param name="entry">
    /// The file being copied.
    /// </param>
    /// <returns>
    /// The declaration, or null for a host file or a driver that declares nothing.
    /// </returns>
    public static FileSystemCapabilities? Declared(IFileSystemEntry entry)
    {
        return entry is DriverAdapterFileSystemEntry e ? Declared(e.Driver) : null;
    }

    /// <summary>
    /// Gets the declaration of the driver behind a panel, or null.
    /// </summary>
    /// <param name="panel">
    /// The panel the file is copied into.
    /// </param>
    /// <returns>
    /// The declaration, or null for a host folder or a driver that declares nothing.
    /// </returns>
    public static FileSystemCapabilities? Declared(IFileSystemPanel panel)
    {
        return panel is DriverAdapterFileSystemPanel p ? Declared(p.Driver) : null;
    }

    /// <summary>
    /// Gets the properties of a bag that a declaration names.
    /// </summary>
    /// <typeparam name="TValue">
    /// The value type of the bag (a property bag allows null values, a parsed .xat does not).
    /// </typeparam>
    /// <param name="properties">
    /// The properties, as read from a file or from a .xat.
    /// </param>
    /// <param name="declared">
    /// The driver's declaration.
    /// </param>
    /// <returns>
    /// Only the declared properties, with no null values.
    /// </returns>
    public static Dictionary<string, object> Pick<TValue>(IEnumerable<KeyValuePair<string, TValue>> properties, FileSystemCapabilities declared)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();
        foreach (KeyValuePair<string, TValue> kvp in properties)
        {
            if (kvp.Value != null && declared.SupportsProperty(kvp.Key))
            {
                result[kvp.Key] = kvp.Value!;
            }
        }
        return result;
    }
}
