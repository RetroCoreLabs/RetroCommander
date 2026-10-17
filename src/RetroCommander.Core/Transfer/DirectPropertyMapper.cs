//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;

namespace RetroCommander.Core.Transfer;

/// <summary>
/// Copies all properties directly (for same filesystem type)
/// </summary>
public class DirectPropertyMapper : IPropertyMapper
{
    public void MapProperties(IPropertyBag source, IPropertyBag target)
    {
        var allProps = source.GetAllProperties();
        foreach (var kvp in allProps)
        {
            target.SetProperty(kvp.Key, kvp.Value);
        }
    }
}
