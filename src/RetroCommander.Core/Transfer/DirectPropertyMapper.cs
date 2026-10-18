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
    /// <summary>
    /// Copies every key and value from <paramref name="source"/> to <paramref name="target"/>.
    /// </summary>
    /// <param name="source">
    /// The bag to read from.
    /// </param>
    /// <param name="target">
    /// The bag to write to. Values already there under the same keys are replaced; other keys are left alone.
    /// </param>
    public void MapProperties(IPropertyBag source, IPropertyBag target)
    {
        var allProps = source.GetAllProperties();
        foreach (var kvp in allProps)
        {
            target.SetProperty(kvp.Key, kvp.Value);
        }
    }
}
