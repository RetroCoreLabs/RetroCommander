//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;

namespace RetroCommander.Core.PropertyBags;

/// <summary>
/// Default implementation of IPropertyBag
/// </summary>
public class PropertyBag : IPropertyBag
{
    private readonly Dictionary<string, object?> _properties = new();

    /// <summary>
    /// Returns the value stored under a key, or a default if the key is missing or its value is not of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type the value is expected to have.
    /// </typeparam>
    /// <param name="key">
    /// The property name, usually one of the constants in <see cref="StandardProperties"/> or a filesystem-specific class.
    /// </param>
    /// <param name="defaultValue">
    /// The value to return when the key is missing or holds a value of another type.
    /// </param>
    /// <returns>
    /// The stored value, or <paramref name="defaultValue"/>.
    /// </returns>
    public T? GetProperty<T>(string key, T? defaultValue = default)
    {
        if (_properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Tries to read the value stored under a key as type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type the value is expected to have.
    /// </typeparam>
    /// <param name="key">
    /// The property name.
    /// </param>
    /// <param name="value">
    /// The stored value when the call returns true; otherwise the default of <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// True if the key exists and its value is of type <typeparamref name="T"/>; otherwise false.
    /// A stored null counts as not found.
    /// </returns>
    public bool TryGetProperty<T>(string key, out T? value)
    {
        if (_properties.TryGetValue(key, out var obj))
        {
            if (obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Stores a value under a key, replacing any value already there.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value.
    /// </typeparam>
    /// <param name="key">
    /// The property name.
    /// </param>
    /// <param name="value">
    /// The value to store.
    /// </param>
    public void SetProperty<T>(string key, T value)
    {
        _properties[key] = value;
    }

    /// <summary>
    /// Checks whether a key is present, whatever its value.
    /// </summary>
    /// <param name="key">
    /// The property name.
    /// </param>
    /// <returns>
    /// True if the key is present, even when its value is null; otherwise false.
    /// </returns>
    public bool HasProperty(string key) => _properties.ContainsKey(key);

    /// <summary>
    /// Returns every key and value in the bag.
    /// </summary>
    /// <returns>
    /// A read-only view of the bag's own dictionary, so later changes to the bag show up in it.
    /// </returns>
    public IReadOnlyDictionary<string, object?> GetAllProperties()
    {
        return _properties;
    }

    /// <summary>
    /// Copies the properties of another bag into this one.
    /// </summary>
    /// <param name="other">
    /// The bag to copy from.
    /// </param>
    /// <param name="strategy">
    /// What to do when a key is in both bags. <see cref="PropertyMergeStrategy.PreferSource"/> overwrites the value here;
    /// <see cref="PropertyMergeStrategy.PreferTarget"/> and <see cref="PropertyMergeStrategy.OnlyMissing"/> both keep the value here
    /// and only add keys this bag does not have.
    /// </param>
    public void MergeFrom(IPropertyBag other, PropertyMergeStrategy strategy = PropertyMergeStrategy.PreferSource)
    {
        var otherProps = other.GetAllProperties();

        foreach (var kvp in otherProps)
        {
            switch (strategy)
            {
                case PropertyMergeStrategy.PreferSource:
                    _properties[kvp.Key] = kvp.Value;
                    break;

                case PropertyMergeStrategy.PreferTarget:
                    if (!_properties.ContainsKey(kvp.Key))
                        _properties[kvp.Key] = kvp.Value;
                    break;

                case PropertyMergeStrategy.OnlyMissing:
                    if (!_properties.ContainsKey(kvp.Key))
                        _properties[kvp.Key] = kvp.Value;
                    break;
            }
        }
    }

    /// <summary>
    /// Removes a key and its value.
    /// </summary>
    /// <param name="key">
    /// The property name.
    /// </param>
    /// <returns>
    /// True if the key was there and is now gone; false if it was not there.
    /// </returns>
    public bool RemoveProperty(string key) => _properties.Remove(key);

    /// <summary>
    /// Removes every key and value from the bag.
    /// </summary>
    public void Clear() => _properties.Clear();
}
