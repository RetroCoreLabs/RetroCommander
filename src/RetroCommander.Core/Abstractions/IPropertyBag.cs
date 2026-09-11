//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Abstractions;

/// <summary>
/// Property merge strategies when combining property bags
/// </summary>
public enum PropertyMergeStrategy
{
    /// <summary>
    /// Source properties overwrite target properties
    /// </summary>
    PreferSource,

    /// <summary>
    /// Target properties take precedence
    /// </summary>
    PreferTarget,

    /// <summary>
    /// Only add properties that don't exist in target
    /// </summary>
    OnlyMissing
}

/// <summary>
/// Dictionary-based property bag for filesystem metadata
/// </summary>
public interface IPropertyBag
{
    /// <summary>
    /// Get property value with optional default
    /// </summary>
    T? GetProperty<T>(string key, T? defaultValue = default);

    /// <summary>
    /// Try to get property value
    /// </summary>
    bool TryGetProperty<T>(string key, out T? value);

    /// <summary>
    /// Set property value
    /// </summary>
    void SetProperty<T>(string key, T value);

    /// <summary>
    /// Check if property exists
    /// </summary>
    bool HasProperty(string key);

    /// <summary>
    /// Get all properties
    /// </summary>
    IReadOnlyDictionary<string, object?> GetAllProperties();

    /// <summary>
    /// Merge properties from another bag
    /// </summary>
    void MergeFrom(IPropertyBag other, PropertyMergeStrategy strategy = PropertyMergeStrategy.PreferSource);

    /// <summary>
    /// Remove a property
    /// </summary>
    bool RemoveProperty(string key);

    /// <summary>
    /// Clear all properties
    /// </summary>
    void Clear();
}
