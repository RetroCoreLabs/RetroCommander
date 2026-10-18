//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.PropertyBags;

namespace RetroCommander.Core.Transfer;

/// <summary>
/// Maps Azure Blob metadata back to NDFS properties
/// </summary>
public class AzureToNdfsPropertyMapper : StandardPropertyMapper
{
    /// <summary>
    /// Copies the standard properties, then reads the NDFS properties back out of the Azure metadata dictionary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each of ndfs-userid, ndfs-objecttype, ndfs-accessmode, ndfs-paritybit, ndfs-username and ndfs-effectivedate
    /// is parsed and set only if it is present and parses. When the source has no Azure metadata at all, the target gets
    /// user id 0, object type 1, access mode <c>0xFF</c> and parity bit false. If no effective date was set,
    /// the source's <see cref="StandardProperties.ModifiedDate"/> is used for it when present.
    /// </para>
    /// <para>
    /// This method hides <see cref="StandardPropertyMapper.MapProperties"/> with <c>new</c> and does not override it.
    /// A call made through an <see cref="IPropertyMapper"/> or <see cref="StandardPropertyMapper"/> reference runs the base method,
    /// so only the standard properties are copied.
    /// </para>
    /// </remarks>
    /// <param name="source">
    /// The bag holding the Azure properties.
    /// </param>
    /// <param name="target">
    /// The bag to fill with standard and NDFS properties.
    /// </param>
    public new void MapProperties(IPropertyBag source, IPropertyBag target)
    {
        // First copy standard properties
        base.MapProperties(source, target);

        // Try to restore NDFS metadata from Azure metadata
        if (source.TryGetProperty<Dictionary<string, string>>(AzureProperties.Metadata, out var metadata) && metadata != null)
        {
            // Restore NDFS properties if they exist
            if (metadata.TryGetValue("ndfs-userid", out var userIdStr) &&
                ushort.TryParse(userIdStr, out var userId))
            {
                target.SetProperty(NdfsProperties.UserId, userId);
            }

            if (metadata.TryGetValue("ndfs-objecttype", out var objTypeStr) &&
                byte.TryParse(objTypeStr, out var objType))
            {
                target.SetProperty(NdfsProperties.ObjectType, objType);
            }

            if (metadata.TryGetValue("ndfs-accessmode", out var accessModeStr) &&
                byte.TryParse(accessModeStr, out var accessMode))
            {
                target.SetProperty(NdfsProperties.AccessMode, accessMode);
            }

            if (metadata.TryGetValue("ndfs-paritybit", out var parityStr) &&
                bool.TryParse(parityStr, out var parity))
            {
                target.SetProperty(NdfsProperties.ParityBit, parity);
            }

            if (metadata.TryGetValue("ndfs-username", out var userName))
            {
                target.SetProperty(NdfsProperties.UserName, userName);
            }

            if (metadata.TryGetValue("ndfs-effectivedate", out var effectiveDateStr) &&
                DateTime.TryParse(effectiveDateStr, out var effectiveDate))
            {
                target.SetProperty(NdfsProperties.EffectiveDate, effectiveDate);
            }
        }
        else
        {
            // No NDFS metadata found - use defaults
            target.SetProperty(NdfsProperties.UserId, (ushort)0);
            target.SetProperty(NdfsProperties.ObjectType, (byte)1); // Regular file
            target.SetProperty(NdfsProperties.AccessMode, (byte)0xFF); // Read/write
            target.SetProperty(NdfsProperties.ParityBit, false);
        }

        // Map Azure modified date to NDFS effective date if not already set
        if (!target.HasProperty(NdfsProperties.EffectiveDate) &&
            source.TryGetProperty<DateTime?>(StandardProperties.ModifiedDate, out var modifiedDate))
        {
            target.SetProperty(NdfsProperties.EffectiveDate, modifiedDate);
        }
    }
}
