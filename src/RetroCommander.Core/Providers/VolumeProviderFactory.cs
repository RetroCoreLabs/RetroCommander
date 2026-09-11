//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Caching;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Managers;
using RetroIO.Storage.Core;
using RetroIO.Storage.Core.Utilities;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Providers;

/// <summary>
/// Factory for creating filesystem panels from URNs
/// </summary>
public class VolumeProviderFactory : IVolumeProviderFactory
{
    private readonly PanelManager? _panelManager;
    private readonly DriverFactory _driverFactory;
    private readonly ICacheManager? _cacheManager;
    private readonly IRetroCommanderLogger? _logger;

    public VolumeProviderFactory(
        PanelManager? panelManager,
        DriverFactory driverFactory,
        ICacheManager? cacheManager = null,
        IRetroCommanderLogger? logger = null)
    {
        _panelManager = panelManager;
        _driverFactory = driverFactory;
        _cacheManager = cacheManager;
        _logger = logger;
    }

    /// <summary>
    /// Mount mode used when no explicit mode is supplied (automount). Set from user preference.
    /// </summary>
    public MountMode DefaultMountMode { get; set; } = MountMode.Auto;

    /// <summary>
    /// Automount entry point — uses <see cref="DefaultMountMode"/>.
    /// </summary>
    public Task<IFileSystemPanel> CreatePanelAsync(string urn, CancellationToken cancellationToken = default)
        => CreatePanelAsync(urn, DefaultMountMode, cancellationToken);

    public Task<IFileSystemPanel> CreatePanelAsync(string urn, MountMode mode, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogOperation("CreatePanel", $"Creating panel for: {urn} (mountMode={mode})", LogLevel.Debug);

            var panelId = _panelManager?.GetNextPanelId() ?? Guid.NewGuid().ToString();

            // Parse URN to determine filesystem type
            if (urn.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                // Extract path (strips fragment like #a, #g, etc.)
                var path = PathCleaner.ExtractFilePathFromUri(urn);

                // Check if it's a file (disk image) or directory
                if (File.Exists(path))
                {
                    // It's a disk image file - use auto-detection. DriverFactory.Detect already
                    // tries partition-table-aware detection internally (raw filesystem at sector
                    // 0 first, then within the first mountable partition if that fails) and
                    // returns a driver instance WITHOUT pre-selecting a partition — the caller is
                    // expected to call SelectPartition/SetRawMode via IPartitionProvider before
                    // Mount, same mechanism BSD disk labels already use (see
                    // ConfigurePartitionForDriver below). PcFloppyDriver implements this for MBR
                    // partitions the same way BsdDriver already does for BSD disk labels.
                    _logger?.LogOperation("CreatePanel", $"Detected disk image file: {path}, using auto-detection", LogLevel.Debug);

                    // Use RetroFS library's detection directly
                    var detectedDriver = _driverFactory.Detect(path);
                    if (detectedDriver == null)
                    {
                        throw new NotSupportedException($"Unable to detect filesystem format for: {path}");
                    }

                    _logger?.LogOperation("CreatePanel", $"Auto-detected driver: {detectedDriver.FileSystemType}", LogLevel.Debug);

                    // Configure partition if fragment is present (e.g., #a, #g, #0) — must happen
                    // BEFORE Mount, since IPartitionProvider drivers use the selected partition to
                    // compute their filesystem's offset/size during Mount.
                    ConfigurePartitionForDriver(detectedDriver, urn);

                    // Open stream - DO NOT use 'using' because driver needs to keep stream open.
                    // MUST open read-WRITE so changes persist: File.OpenRead() returns a read-only
                    // stream, which silently turns every mount into a throwaway (writes live only in
                    // the in-memory page cache and vanish on unmount).
                    var (stream, mountReadOnly) = OpenImageStream(path, mode);

                    try
                    {
                        detectedDriver.Mount(stream, mountReadOnly);
                        _logger?.LogOperation("CreatePanel", $"Successfully mounted {path} (readOnly={mountReadOnly})", LogLevel.Debug);
                    }
                    catch (Exception ex)
                    {
                        stream.Dispose();
                        _logger?.LogError($"Failed to mount {path}: {ex.Message}", ex);
                        throw;
                    }

                    // Create panel adapter
                    var adapter = new DriverAdapterFileSystemPanel(panelId, detectedDriver, urn);
                    return Task.FromResult<IFileSystemPanel>(adapter);
                }
                else if (Directory.Exists(path))
                {
                    // Windows native filesystem directory
                    return Task.FromResult<IFileSystemPanel>(new NativeFileSystemPanel(panelId, path));
                }
                else
                {
                    throw new FileNotFoundException($"Path not found: {path}");
                }
            }
            else if (urn.Contains("://"))
            {
                // Disk image file (NDFS, D64, D71, D81, etc.)
                // Use PathCleaner to properly extract file path from URN
                var imagePath = PathCleaner.ExtractFilePathFromUri(urn);

                _logger?.LogOperation("CreatePanel", $"Mounting disk image: {imagePath}", LogLevel.Debug);

                if (!File.Exists(imagePath))
                    throw new FileNotFoundException($"Disk image not found: {imagePath}");

                var fileInfo = new FileInfo(imagePath);
                _logger?.LogOperation("CreatePanel", $"Image size: {fileInfo.Length} bytes", LogLevel.Debug);

                // Extract scheme from URN to force correct driver
                var scheme = ExtractScheme(urn);
                var driver = CreateDriverForScheme(scheme, imagePath);

                // Handle partition selection for BSD drivers
                ConfigurePartitionForDriver(driver, urn);

                _logger?.LogOperation("CreatePanel", $"Detected driver: {driver.FileSystemType}", LogLevel.Debug);

                // Open stream - DO NOT use 'using' because driver needs to keep stream open.
                // Read-WRITE so changes persist (see note above; File.OpenRead would discard writes).
                var (stream, mountReadOnly) = OpenImageStream(imagePath, mode);

                try
                {
                    driver.Mount(stream, mountReadOnly);
                    _logger?.LogOperation("CreatePanel", $"Successfully mounted {imagePath} (readOnly={mountReadOnly})", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    stream.Dispose();
                    _logger?.LogError($"Failed to mount {imagePath}: {ex.Message}", ex);
                    throw;
                }

                return Task.FromResult<IFileSystemPanel>(new DriverAdapterFileSystemPanel(panelId, driver, urn));
            }
            else
            {
                throw new NotSupportedException($"Unsupported URN format: {urn}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to create panel for URN: {urn}", ex);
            throw;
        }
    }

    /// <summary>
    /// Opens a disk-image file for mounting, honouring the requested <see cref="MountMode"/>.
    /// The stream stays open for the driver's lifetime.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="MountMode.ReadOnly"/> — always a read-only stream, even if writable.</item>
    /// <item><see cref="MountMode.ReadWrite"/> — require write access; if it cannot be obtained the
    /// open FAILS (throws) so the caller can tell the user plainly, instead of silently handing back
    /// a read-only mount whose writes would be discarded.</item>
    /// <item><see cref="MountMode.Auto"/> — try read-write first (so edits persist), fall back to
    /// read-only on a read-only file/media or sharing conflict. Historical behaviour.</item>
    /// </list>
    /// </remarks>
    private static (FileStream stream, bool readOnly) OpenImageStream(string path, MountMode mode)
    {
        if (mode == MountMode.ReadOnly)
        {
            var ro = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (ro, true);
        }

        if (mode == MountMode.ReadWrite)
        {
            try
            {
                // FileShare.Read lets other readers peek but blocks a second writer.
                var rw = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                return (rw, false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
            {
                // Explicitly asked for read-write and we cannot get it — do NOT silently degrade.
                throw new IOException(
                    $"Cannot mount '{path}' read-write: the file is read-only, on read-only media, " +
                    $"or already open elsewhere. ({ex.Message})", ex);
            }
        }

        // MountMode.Auto
        try
        {
            var rw = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            return (rw, false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            // Could not get write access: fall back to a read-only mount rather than failing.
            var ro = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (ro, true);
        }
    }

    public bool SupportsUrn(string urn)
    {
        return urn.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) ||
               urn.StartsWith("ndfs://", StringComparison.OrdinalIgnoreCase) ||
               urn.StartsWith("d64://", StringComparison.OrdinalIgnoreCase) ||
               urn.StartsWith("d71://", StringComparison.OrdinalIgnoreCase) ||
               urn.StartsWith("d81://", StringComparison.OrdinalIgnoreCase) ||
               urn.StartsWith("bsd://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets detection confidence scores for specific filesystem types.
    /// Uses the RetroFS library's detection system.
    /// </summary>
    /// <param name="imagePath">Path to the disk image file</param>
    /// <param name="filesystemTypes">Array of filesystem type identifiers to check (e.g., "NDFS", "BSD-V7")</param>
    /// <returns>Dictionary mapping filesystem type to confidence score (0.0 to 1.0)</returns>
    public Dictionary<string, float> GetDetectionConfidences(string imagePath, params string[] filesystemTypes)
    {
        // Use RetroFS library directly - no duplication
        return _driverFactory.GetDetectionConfidences(imagePath, filesystemTypes);
    }

    private FileSystemScheme ExtractScheme(string urn)
    {
        if (urn.StartsWith("bsd://", StringComparison.OrdinalIgnoreCase))
            return FileSystemScheme.V7;
        if (urn.StartsWith("ndfs://", StringComparison.OrdinalIgnoreCase))
            return FileSystemScheme.Ndfs;
        if (urn.StartsWith("d64://", StringComparison.OrdinalIgnoreCase))
            return FileSystemScheme.D64;
        if (urn.StartsWith("d71://", StringComparison.OrdinalIgnoreCase))
            return FileSystemScheme.D71;
        if (urn.StartsWith("d81://", StringComparison.OrdinalIgnoreCase))
            return FileSystemScheme.D81;

        return FileSystemScheme.Unknown;
    }

    private IFileSystemDriver CreateDriverForScheme(FileSystemScheme scheme, string imagePath)
    {
        // Try to create driver based on scheme first
        if (scheme != FileSystemScheme.Unknown)
        {
            var driverTypeName = scheme.ToDriverTypeName();
            if (!string.IsNullOrEmpty(driverTypeName))
            {
                var driver = _driverFactory.Create(driverTypeName);
                if (driver != null)
                {
                    _logger?.LogOperation("CreateDriverForScheme", $"Created {scheme} driver for {imagePath}", LogLevel.Debug);
                    return driver;
                }
                else
                {
                    throw new NotSupportedException($"Filesystem driver '{driverTypeName}' for scheme '{scheme}' is not registered. " +
                        "Please ensure the driver provider is registered in the DriverFactory.");
                }
            }
        }

        // Fallback to auto-detection
        var detectedDriver = _driverFactory.Detect(imagePath);
        if (detectedDriver == null)
        {
            throw new NotSupportedException($"Unable to detect filesystem format for: {imagePath}\n\n" +
                "Possible solutions:\n" +
                "1. Explicitly select the filesystem type from the mount dialog\n" +
                "2. Use a URN scheme (e.g., bsd:///path, d64:///path, ndfs:///path)\n" +
                "3. Ensure the disk image is not corrupted\n" +
                "4. Check that all filesystem drivers are registered");
        }

        return detectedDriver;
    }

    private void ConfigurePartitionForDriver(IFileSystemDriver driver, string urn)
    {
        // Extract partition fragment (e.g., #a, #0, #raw)
        int fragmentIndex = urn.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            string partition = urn.Substring(fragmentIndex + 1);

            // Handle partition selection for partition-supporting drivers
            if (driver is RetroIO.Storage.Core.IPartitionProvider partitionProvider)
            {
                if (partition == "raw")
                {
                    partitionProvider.SetRawMode();
                    _logger?.LogOperation("ConfigurePartition", $"Driver set to raw mode (no partition table)", LogLevel.Debug);
                }
                else if ((partition.Length == 1 && partition[0] >= 'a' && partition[0] <= 'h') || IsDigits(partition))
                {
                    // 'a'-'h' identifies a BSD disk-label partition; a plain digit string (e.g.
                    // "0") identifies an MBR partition index. Both route through the same
                    // SelectPartition call — IPartitionProvider.SelectPartition's own doc comment
                    // already documents both forms ("a", "b" for BSD; "0", "1" for MBR).
                    if (!partitionProvider.SelectPartition(partition))
                    {
                        throw new ArgumentException($"Partition '{partition}' not found or invalid.");
                    }
                    _logger?.LogOperation("ConfigurePartition", $"Driver configured for partition '{partition}'", LogLevel.Debug);
                }
                else
                {
                    throw new ArgumentException($"Invalid partition identifier: '{partition}'. Expected 'a'-'h', a numeric MBR index, or 'raw'.");
                }
            }
        }
        else if (driver is RetroIO.Storage.Core.IPartitionProvider partitionProviderNoPartition)
        {
            // No partition specified - use raw mode for partition-supporting drivers by default
            partitionProviderNoPartition.SetRawMode();
            _logger?.LogOperation("ConfigurePartition", $"Driver set to raw mode (no partition specified)", LogLevel.Debug);
        }
    }

    private static bool IsDigits(string s)
    {
        if (s.Length == 0)
        {
            return false;
        }
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] < '0' || s[i] > '9')
            {
                return false;
            }
        }
        return true;
    }
}
