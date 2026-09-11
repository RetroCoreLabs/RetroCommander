//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Providers;

namespace RetroCommander.Core.Managers;

/// <summary>
/// Describes a mounted volume
/// </summary>
public class MountPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Urn { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileSystemType { get; set; } = string.Empty;
    public bool IsRemote { get; set; }
    public bool IsReadOnly { get; set; }
    public DateTime MountedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
}

/// <summary>
/// Options for mounting a volume
/// </summary>
public class MountOptions
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool AutoNavigateToPath { get; set; } = true;
    public string? InitialPath { get; set; }
}

/// <summary>
/// Manages filesystem mounts
/// </summary>
public class MountManager
{
    private readonly Dictionary<string, MountPoint> _mountPoints = new();
    private readonly IVolumeProviderFactory _providerFactory;
    private readonly PanelManager _panelManager;
    private readonly IRetroCommanderLogger? _logger;

    public MountManager(
        IVolumeProviderFactory providerFactory,
        PanelManager panelManager,
        IRetroCommanderLogger? logger = null)
    {
        _providerFactory = providerFactory;
        _panelManager = panelManager;
        _logger = logger;
    }

    /// <summary>
    /// All mounted volumes
    /// </summary>
    public IReadOnlyDictionary<string, MountPoint> MountPoints => _mountPoints;

    /// <summary>
    /// Mount a volume and create a panel for it
    /// </summary>
    public async Task<(MountPoint MountPoint, IFileSystemPanel Panel)?> MountAsync(
        string urn,
        MountOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogOperation("Mount", $"Mounting: {urn}", LogLevel.Info);

            options ??= new MountOptions();

            // Check if already mounted - but only return if panel is valid
            // Use case-insensitive comparison and log all existing mounts for debugging
            _logger?.LogOperation("Mount", $"Checking for existing mounts. Current mount count: {_mountPoints.Count}", LogLevel.Debug);
            foreach (var mp in _mountPoints.Values)
            {
                _logger?.LogOperation("Mount", $"  Existing mount: {mp.Urn} (ID: {mp.Id}, Type: {mp.FileSystemType})", LogLevel.Debug);
            }

            var existing = _mountPoints.Values.FirstOrDefault(mp =>
                string.Equals(mp.Urn, urn, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _logger?.LogWarning($"Volume already mounted: {urn} (found existing mount ID: {existing.Id})");
                var existingPanel = _panelManager.GetPanel(existing.Id);
                if (existingPanel != null)
                {
                    _logger?.LogInfo($"Returning existing mount: {existing.DisplayName} (ID: {existing.Id})");
                    return (existing, existingPanel);
                }
                else
                {
                    // Panel was removed but mount point still exists - remove stale mount point and continue
                    _logger?.LogWarning($"Panel for mount {existing.Id} not found, removing stale mount point and creating new mount");
                    _mountPoints.Remove(existing.Id);
                    // Continue to create new mount below
                }
            }

            // Create panel
            var panel = await _providerFactory.CreatePanelAsync(urn, cancellationToken);

            // Check if panel with same ID already exists (shouldn't happen, but safety check)
            var existingPanelWithId = _panelManager.GetPanel(panel.Id);
            if (existingPanelWithId != null && existingPanelWithId != panel)
            {
                _logger?.LogError($"Panel ID collision detected! Panel ID {panel.Id} already exists but is a different panel. This should not happen.");
                // This is a serious error - we can't have two panels with the same ID
                // Return null to indicate failure
                return null;
            }

            // Register panel with PanelManager if not already registered
            if (existingPanelWithId == null)
            {
                _panelManager.RegisterPanel(panel);
                _logger?.LogOperation("Mount", $"Registered panel with ID: {panel.Id}", LogLevel.Debug);
            }
            else
            {
                _logger?.LogOperation("Mount", $"Panel {panel.Id} already registered, skipping registration", LogLevel.Debug);
            }

            // Create mount point
            var mountPoint = new MountPoint
            {
                Id = panel.Id,
                Urn = urn,
                DisplayName = options.DisplayName ?? panel.DisplayName,
                FileSystemType = panel.FileSystemType,
                IsRemote = panel.IsRemote,
                IsReadOnly = panel.IsReadOnly,
                Description = options.Description
            };

            _mountPoints[mountPoint.Id] = mountPoint;

            // Navigate to initial path if specified
            if (options.AutoNavigateToPath && !string.IsNullOrEmpty(options.InitialPath))
            {
                await panel.NavigateAsync(options.InitialPath, cancellationToken);
            }

            _logger?.LogInfo($"Mounted: {mountPoint.DisplayName} ({mountPoint.FileSystemType})");

            return (mountPoint, panel);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to mount: {urn}", ex);
            return null;
        }
    }

    /// <summary>
    /// Unmount a volume
    /// </summary>
    public bool Unmount(string mountPointId)
    {
        if (_mountPoints.TryGetValue(mountPointId, out var mountPoint))
        {
            _logger?.LogOperation("Unmount", $"Unmounting: {mountPoint.DisplayName}", LogLevel.Info);

            // Remove from panel manager
            _panelManager.RemovePanel(mountPointId);

            // Remove mount point
            _mountPoints.Remove(mountPointId);

            _logger?.LogInfo($"Unmounted: {mountPoint.DisplayName}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get mount point by ID
    /// </summary>
    public MountPoint? GetMountPoint(string mountPointId)
    {
        return _mountPoints.TryGetValue(mountPointId, out var mp) ? mp : null;
    }

    /// <summary>
    /// Get all mount points of a specific filesystem type
    /// </summary>
    public IEnumerable<MountPoint> GetMountPointsByType(string fileSystemType)
    {
        return _mountPoints.Values.Where(mp => mp.FileSystemType == fileSystemType);
    }

    /// <summary>
    /// Get recent mounts (for UI quick access)
    /// </summary>
    public IEnumerable<MountPoint> GetRecentMounts(int count = 10)
    {
        return _mountPoints.Values
            .OrderByDescending(mp => mp.MountedAt)
            .Take(count);
    }

    /// <summary>
    /// Check if URN is already mounted
    /// </summary>
    public bool IsMounted(string urn)
    {
        return _mountPoints.Values.Any(mp => mp.Urn == urn);
    }
}
