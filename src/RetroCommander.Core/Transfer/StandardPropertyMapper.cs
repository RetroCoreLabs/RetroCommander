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
/// Maps only standard properties (safe for all filesystem types)
/// </summary>
public class StandardPropertyMapper : IPropertyMapper
{
    public void MapProperties(IPropertyBag source, IPropertyBag target)
    {
        // Copy standard properties that exist
        CopyIfPresent<string>(source, target, StandardProperties.FileName);
        CopyIfPresent<long>(source, target, StandardProperties.FileSize);
        CopyIfPresent<bool>(source, target, StandardProperties.IsDirectory);
        CopyIfPresent<DateTime?>(source, target, StandardProperties.CreatedDate);
        CopyIfPresent<DateTime?>(source, target, StandardProperties.ModifiedDate);
        CopyIfPresent<DateTime?>(source, target, StandardProperties.AccessedDate);
        CopyIfPresent<bool>(source, target, StandardProperties.IsReadOnly);
        CopyIfPresent<bool>(source, target, StandardProperties.IsHidden);
    }

    protected void CopyIfPresent<T>(IPropertyBag source, IPropertyBag target, string key)
    {
        if (source.TryGetProperty<T>(key, out var value))
        {
            target.SetProperty(key, value);
        }
    }
}
