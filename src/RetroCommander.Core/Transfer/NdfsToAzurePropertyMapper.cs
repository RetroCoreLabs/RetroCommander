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
/// Maps NDFS properties to Azure Blob metadata
/// </summary>
public class NdfsToAzurePropertyMapper : StandardPropertyMapper
{
    /// <summary>
    /// Copies the standard properties, then packs the NDFS properties into the Azure metadata dictionary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The NDFS user id, object type, access mode, parity bit, user name and effective date are written as text
    /// under the metadata names ndfs-userid, ndfs-objecttype, ndfs-accessmode, ndfs-paritybit, ndfs-username and
    /// ndfs-effectivedate. The effective date uses the round-trip "O" format. The metadata is only set when at least one
    /// of these was found. The content type is set to application/octet-stream when the target has none.
    /// </para>
    /// <para>
    /// This method overrides <see cref="StandardPropertyMapper.MapProperties"/>, so it also runs when the mapper
    /// is used through an <see cref="IPropertyMapper"/> reference, as the registry hands it out.
    /// </para>
    /// </remarks>
    /// <param name="source">
    /// The bag holding the NDFS properties.
    /// </param>
    /// <param name="target">
    /// The bag to fill with standard and Azure properties.
    /// </param>
    public override void MapProperties(IPropertyBag source, IPropertyBag target)
    {
        // First copy standard properties
        base.MapProperties(source, target);

        // Store NDFS metadata in Azure metadata dictionary
        var metadata = new Dictionary<string, string>();

        if (source.TryGetProperty<ushort>(NdfsProperties.UserId, out var userId))
            metadata["ndfs-userid"] = userId.ToString();

        if (source.TryGetProperty<byte>(NdfsProperties.ObjectType, out var objType))
            metadata["ndfs-objecttype"] = objType.ToString();

        if (source.TryGetProperty<byte>(NdfsProperties.AccessMode, out var accessMode))
            metadata["ndfs-accessmode"] = accessMode.ToString();

        if (source.TryGetProperty<bool>(NdfsProperties.ParityBit, out var parityBit))
            metadata["ndfs-paritybit"] = parityBit.ToString();

        if (source.TryGetProperty<string>(NdfsProperties.UserName, out var userName))
            metadata["ndfs-username"] = userName!;

        if (source.TryGetProperty<DateTime?>(NdfsProperties.EffectiveDate, out var effectiveDate))
            metadata["ndfs-effectivedate"] = effectiveDate?.ToString("O") ?? "";

        if (metadata.Count > 0)
        {
            target.SetProperty(AzureProperties.Metadata, metadata);
        }

        // Default content type for binary files
        if (!target.HasProperty(AzureProperties.ContentType))
        {
            target.SetProperty(AzureProperties.ContentType, "application/octet-stream");
        }
    }
}
