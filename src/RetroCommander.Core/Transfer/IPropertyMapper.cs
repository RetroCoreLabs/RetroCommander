//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;

namespace RetroCommander.Core.Transfer;

/// <summary>
/// Maps properties between different filesystem types
/// </summary>
public interface IPropertyMapper
{
    /// <summary>
    /// Map properties from source to target
    /// </summary>
    void MapProperties(IPropertyBag source, IPropertyBag target);
}
