//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
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
    /// <summary>
    /// Gets or sets the mount ID. <see cref="MountManager"/> sets it to the ID of the panel created for the volume; a new GUID string by default.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the URN the volume was mounted from.
    /// </summary>
    public string Urn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name shown for the volume.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file system type name, taken from the panel.
    /// </summary>
    public string FileSystemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the volume is reached over a network, taken from the panel.
    /// </summary>
    public bool IsRemote { get; set; }

    /// <summary>
    /// Gets or sets whether the volume can only be read, taken from the panel.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets when the mount point was created, in UTC.
    /// </summary>
    public DateTime MountedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets an optional free-text description, taken from <see cref="MountOptions.Description"/>.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Options for mounting a volume
/// </summary>
public class MountOptions
{
    /// <summary>
    /// Gets or sets the name to show for the volume. When null, the panel's own display name is used.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets an optional free-text description stored on the mount point.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether the new panel moves to <see cref="InitialPath"/> after mounting. Defaults to true.
    /// </summary>
    public bool AutoNavigateToPath { get; set; } = true;

    /// <summary>
    /// Gets or sets the path to open after mounting. Ignored when null or empty, or when <see cref="AutoNavigateToPath"/> is false.
    /// </summary>
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

    /// <summary>
    /// Creates a mount manager.
    /// </summary>
    /// <param name="providerFactory">
    /// Factory that opens a panel for a URN.
    /// </param>
    /// <param name="panelManager">
    /// Panel manager that new panels are registered with and removed from on unmount.
    /// </param>
    /// <param name="logger">
    /// Optional logger; when null nothing is logged.
    /// </param>
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
