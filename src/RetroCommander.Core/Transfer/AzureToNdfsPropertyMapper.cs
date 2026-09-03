//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.PropertyBags;

namespace RetroCommander.Core.Transfer;

/// <summary>
/// Maps Azure Blob metadata back to NDFS properties
/// </summary>
public class AzureToNdfsPropertyMapper : StandardPropertyMapper
{
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
