//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Transfer;

/// <summary>
/// Registry for filesystem-specific property mappers
/// </summary>
public class PropertyMapperRegistry
{
    private readonly Dictionary<(string Source, string Target), IPropertyMapper> _mappers = new();
    private readonly IPropertyMapper _fallbackMapper = new StandardPropertyMapper();

    /// <summary>
    /// Register a property mapper for source->target filesystem pair
    /// </summary>
    public void RegisterMapper(string sourceType, string targetType, IPropertyMapper mapper)
    {
        _mappers[(sourceType, targetType)] = mapper;
    }

    /// <summary>
    /// Get mapper for source->target filesystem pair
    /// </summary>
    public IPropertyMapper GetMapper(string sourceType, string targetType)
    {
        if (_mappers.TryGetValue((sourceType, targetType), out var mapper))
        {
            return mapper;
        }

        // Fallback to standard property mapper
        return _fallbackMapper;
    }

    /// <summary>
    /// Create default registry with common mappings
    /// </summary>
    public static PropertyMapperRegistry CreateDefault()
    {
        var registry = new PropertyMapperRegistry();

        // Same filesystem type - direct copy
        registry.RegisterMapper("NDFS", "NDFS", new DirectPropertyMapper());
        registry.RegisterMapper("Azure", "Azure", new DirectPropertyMapper());
        registry.RegisterMapper("Windows", "Windows", new DirectPropertyMapper());
        registry.RegisterMapper("TNFS", "TNFS", new DirectPropertyMapper());
        registry.RegisterMapper("CBM", "CBM", new DirectPropertyMapper());

        // Cross-filesystem mappings (to be implemented)
        // NDFS<->Windows only maps standard properties (dates/size/readonly) here. NDFS-specific
        // metadata (owner, access bits, page layout, ...) is handled separately by
        // SmartFileOperations.CopyAsync as an optional .xat sidecar file on export — see
        // NdfsXatSerializer — since a property mapper can only produce an in-memory bag, not write
        // a second file, and Windows has nowhere else to hold NDFS-native metadata.
        registry.RegisterMapper("NDFS", "Windows", new StandardPropertyMapper());
        registry.RegisterMapper("NDFS", "Azure", new NdfsToAzurePropertyMapper());
        registry.RegisterMapper("Azure", "NDFS", new AzureToNdfsPropertyMapper());
        registry.RegisterMapper("Azure", "Windows", new StandardPropertyMapper());
        registry.RegisterMapper("Windows", "NDFS", new StandardPropertyMapper());
        registry.RegisterMapper("Windows", "Azure", new StandardPropertyMapper());

        return registry;
    }
}
