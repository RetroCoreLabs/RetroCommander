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

    public T? GetProperty<T>(string key, T? defaultValue = default)
    {
        if (_properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

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

    public void SetProperty<T>(string key, T value)
    {
        _properties[key] = value;
    }

    public bool HasProperty(string key) => _properties.ContainsKey(key);

    public IReadOnlyDictionary<string, object?> GetAllProperties()
    {
        return _properties;
    }

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

    public bool RemoveProperty(string key) => _properties.Remove(key);

    public void Clear() => _properties.Clear();
}
