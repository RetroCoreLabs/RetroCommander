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
    /// <summary>
    /// Copies the standard properties from one bag to another, skipping any that are missing or hold a value of the wrong type.
    /// </summary>
    /// <remarks>
    /// The keys copied, with the type each must have:
    ///  - <see cref="StandardProperties.FileName"/> as string.
    ///  - <see cref="StandardProperties.FileSize"/> as long.
    ///  - <see cref="StandardProperties.IsDirectory"/>, <see cref="StandardProperties.IsReadOnly"/> and <see cref="StandardProperties.IsHidden"/> as bool.
    ///  - <see cref="StandardProperties.CreatedDate"/>, <see cref="StandardProperties.ModifiedDate"/> and <see cref="StandardProperties.AccessedDate"/> as nullable DateTime.
    ///
    /// Virtual so that a mapper for a specific pair of filesystems can copy the standard properties
    /// through this method and then add its own.
    /// </remarks>
    /// <param name="source">
    /// The bag to read from.
    /// </param>
    /// <param name="target">
    /// The bag to write to. Values already there under the same keys are replaced.
    /// </param>
    public virtual void MapProperties(IPropertyBag source, IPropertyBag target)
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

    /// <summary>
    /// Copies one key from <paramref name="source"/> to <paramref name="target"/> if it is there and its value is of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type the value must have to be copied.
    /// </typeparam>
    /// <param name="source">
    /// The bag to read from.
    /// </param>
    /// <param name="target">
    /// The bag to write to.
    /// </param>
    /// <param name="key">
    /// The property name to copy.
    /// </param>
    protected void CopyIfPresent<T>(IPropertyBag source, IPropertyBag target, string key)
    {
        if (source.TryGetProperty<T>(key, out var value))
        {
            target.SetProperty(key, value);
        }
    }
}
