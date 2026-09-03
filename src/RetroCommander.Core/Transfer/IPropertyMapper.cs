//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
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
