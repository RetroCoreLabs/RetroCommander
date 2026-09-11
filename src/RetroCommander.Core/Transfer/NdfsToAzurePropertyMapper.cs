//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.PropertyBags;

namespace RetroCommander.Core.Transfer;

/// <summary>
/// Maps NDFS properties to Azure Blob metadata
/// </summary>
public class NdfsToAzurePropertyMapper : StandardPropertyMapper
{
    public new void MapProperties(IPropertyBag source, IPropertyBag target)
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
