//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System.Text;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Providers;
using RetroCommander.Core.PropertyBags;
using RetroCommander.Core.Transfer;
using RetroIO.Storage.Ndfs.PropertyBags;

namespace RetroCommander.Core.Operations;

/// <summary>
/// Smart file operations with property preservation
/// </summary>
public class SmartFileOperations : IFileOperationsManager
{
    private readonly PropertyMapperRegistry _mapperRegistry;
    private readonly IRetroCommanderLogger? _logger;

    public SmartFileOperations(PropertyMapperRegistry? mapperRegistry = null, IRetroCommanderLogger? logger = null)
    {
        _mapperRegistry = mapperRegistry ?? PropertyMapperRegistry.CreateDefault();
        _logger = logger;
    }

    public async Task<CopyResult> CopyAsync(
        IFileSystemEntry source,
        IFileSystemPanel targetPanel,
        CopyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CopyOptions();

        try
        {
            // A ".xat" sidecar is metadata for its paired data file, not an independent file of its
            // own — when importing into NDFS with UseXatMetadataOnImport enabled, don't write it as
            // a separate NDFS object. Its content is consumed directly from disk by
            // TryReadNdfsMetadataSidecar for the paired data file's own copy below, so this is safe
            // regardless of which order the two entries appear in a batch.
            if (options.UseXatMetadataOnImport && targetPanel.FileSystemType == "NDFS" &&
                NdfsXatSerializer.IsXatFile(source.Name))
            {
                return CopyResult.SkippedResult(
                    $"'{source.Name}' is an NDFS metadata sidecar, used for its paired file instead of copied separately");
            }

            _logger?.LogFileOperation("Copy Started", source.FullPath, LogLevel.Info);
            _logger?.LogInfo(
                $"CopyAsync diag: source.Name='{source.Name}' source.FullPath='{source.FullPath}' " +
                $"source.Size={source.Size} source.IsDirectory={source.IsDirectory} " +
                $"sourceType={source.GetType().Name} sourceHash={source.GetHashCode()} " +
                $"target.CurrentPath='{targetPanel.CurrentPath}' target.FileSystemType='{targetPanel.FileSystemType}'");

            // Check if target file exists. On a native Windows target, the visible file will be
            // written under the sanitized name (see NativeFileSystemPanel.SanitizeWindowsFileName —
            // e.g. NDFS's "NAME:TYPE" becomes "NAME.TYPE" because ':' is an NTFS ADS marker), so the
            // existence check must use that same name or it will never detect a real collision.
            var targetName = targetPanel.FileSystemType == "Windows"
                ? NativeFileSystemPanel.SanitizeWindowsFileName(source.Name)
                : source.Name;
            var targetPath = Path.Combine(targetPanel.CurrentPath, targetName);
            if (File.Exists(targetPath) && !options.Overwrite)
            {
                return CopyResult.Failure($"File already exists: {source.Name}");
            }

            // 1. Get source properties
            var sourceProps = source.Properties;

            // 2. Get appropriate property mapper
            var sourceType = GetFileSystemType(source, null);
            var targetType = targetPanel.FileSystemType;
            var mapper = _mapperRegistry.GetMapper(sourceType, targetType);

            // 3. Create target property bag
            var targetProps = new PropertyBag();
            if (options.PreserveProperties)
            {
                mapper.MapProperties(sourceProps, targetProps);
            }

            // 3b. Importing into NDFS: if a ".xat" sidecar sits next to the source, its NDFS
            // metadata supplements/overrides whatever the mapper produced (the mapper alone only
            // carries StandardProperties.* across for a non-NDFS source — see
            // PropertyMapperRegistry.CreateDefault). This is the counterpart to the export step below.
            if (options.UseXatMetadataOnImport && targetType == "NDFS")
            {
                var importedNdfsProps = TryReadNdfsMetadataSidecar(source);
                if (importedNdfsProps != null)
                {
                    foreach (var kvp in importedNdfsProps)
                    {
                        targetProps.SetProperty(kvp.Key, kvp.Value);
                    }
                }
            }

            // 4. Track which properties are preserved
            var preserved = new List<string>();
            var lost = new List<string>();

            if (options.PreserveProperties)
            {
                var allSourceProps = sourceProps.GetAllProperties();
                var allTargetProps = targetProps.GetAllProperties();

                foreach (var prop in allSourceProps)
                {
                    if (allTargetProps.ContainsKey(prop.Key))
                        preserved.Add(prop.Key);
                    else
                        lost.Add(prop.Key);
                }
            }

            // 5. Read source content
            long bytesTransferred = 0;
            using (var sourceStream = await source.OpenReadAsync(cancellationToken))
            {
                _logger?.LogInfo(
                    $"CopyAsync diag: opened source stream type={sourceStream.GetType().Name} " +
                    $"CanSeek={sourceStream.CanSeek} " +
                    $"Length={(sourceStream.CanSeek ? sourceStream.Length.ToString() : "n/a")} " +
                    $"Position={(sourceStream.CanSeek ? sourceStream.Position.ToString() : "n/a")}");

                // Report initial progress
                options.Progress?.Report(new TransferProgress
                {
                    BytesTransferred = 0,
                    TotalBytes = source.Size,
                    CurrentFile = source.Name
                });

                // Create wrapper stream for progress reporting
                var progressStream = new ProgressStream(sourceStream, source.Size, options.Progress, source.Name);

                // 6. Create target file with mapped properties
                var targetEntry = await targetPanel.CreateFileAsync(
                    source.Name,
                    progressStream,
                    targetProps,
                    cancellationToken);

                bytesTransferred = source.Size;

                _logger?.LogInfo(
                    $"CopyAsync diag: CreateFileAsync returned targetEntry.Name='{targetEntry.Name}' " +
                    $"targetEntry.FullPath='{targetEntry.FullPath}' targetEntry.Size={targetEntry.Size}");

                // 7. Export NDFS-specific metadata (owner, access bits, page layout, ...) as a
                // ".xat" sidecar file when copying out of NDFS into a filesystem that can't hold it
                // natively. Same-type copies (NDFS->NDFS) don't need this — DirectPropertyMapper
                // already carried the raw "ndfs." properties into targetProps above.
                if (options.PreserveProperties && options.PreserveNdfsMetadataOnExport && targetType != "NDFS")
                {
                    await TryWriteNdfsMetadataSidecarAsync(sourceProps, targetPanel, targetEntry, cancellationToken);
                }
            }

            _logger?.LogTransfer(source.FullPath, targetPath, bytesTransferred, true);

            return CopyResult.SuccessResult(preserved, lost);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Copy failed: {source.FullPath} -> {targetPanel.CurrentPath}/{source.Name}", ex);
            return CopyResult.Failure(ex.Message);
        }
    }

    public async Task<MoveResult> MoveAsync(
        IFileSystemEntry source,
        IFileSystemPanel sourcePanel,
        IFileSystemPanel targetPanel,
        MoveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MoveOptions();

        try
        {
            // Copy first
            var copyResult = await CopyAsync(
                source,
                targetPanel,
                new CopyOptions
                {
                    Overwrite = options.Overwrite,
                    PreserveProperties = options.PreserveProperties,
                    PreserveNdfsMetadataOnExport = options.PreserveNdfsMetadataOnExport,
                    UseXatMetadataOnImport = options.UseXatMetadataOnImport,
                    Progress = options.Progress
                },
                cancellationToken);

            if (!copyResult.Success)
                return MoveResult.Failure(copyResult.ErrorMessage!);

            // Then delete source
            await DeleteAsync(source, sourcePanel, cancellationToken);

            return MoveResult.SuccessResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Move failed: {source.FullPath}", ex);
            return MoveResult.Failure(ex.Message);
        }
    }

    public async Task<DeleteResult> DeleteAsync(
        IFileSystemEntry entry,
        IFileSystemPanel panel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogFileOperation("Delete", entry.FullPath, LogLevel.Info);
            await panel.DeleteAsync(entry, cancellationToken);
            return DeleteResult.SuccessResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Delete failed: {entry.FullPath}", ex);
            return DeleteResult.Failure(ex.Message);
        }
    }

    public async Task<RenameResult> RenameAsync(
        IFileSystemEntry entry,
        IFileSystemPanel panel,
        string newName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogFileOperation("Rename", $"{entry.FullPath} -> {newName}", LogLevel.Info);
            await panel.RenameAsync(entry, newName, cancellationToken);
            return RenameResult.SuccessResult();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Rename failed: {entry.FullPath} -> {newName}", ex);
            return RenameResult.Failure(ex.Message);
        }
    }

    public async Task<BatchOperationResult> CopyManyAsync(
        IReadOnlyList<IFileSystemEntry> sources,
        IFileSystemPanel targetPanel,
        CopyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var batchResult = new BatchOperationResult();
        for (int i = 0; i < sources.Count; i++)
        {
            var copyResult = await CopyAsync(sources[i], targetPanel, options, cancellationToken);
            if (copyResult.Skipped)
            {
                batchResult.SkippedNames.Add(sources[i].Name);
            }
            else if (copyResult.Success)
            {
                batchResult.SucceededCount++;
            }
            else
            {
                batchResult.FailedNames.Add(sources[i].Name);
                batchResult.ErrorMessages.Add(copyResult.ErrorMessage ?? "Unknown error");
            }
        }
        return batchResult;
    }

    public async Task<BatchOperationResult> MoveManyAsync(
        IReadOnlyList<IFileSystemEntry> sources,
        IFileSystemPanel sourcePanel,
        IFileSystemPanel targetPanel,
        MoveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var batchResult = new BatchOperationResult();
        for (int i = 0; i < sources.Count; i++)
        {
            var moveResult = await MoveAsync(sources[i], sourcePanel, targetPanel, options, cancellationToken);
            if (moveResult.Success)
            {
                batchResult.SucceededCount++;
            }
            else
            {
                batchResult.FailedNames.Add(sources[i].Name);
                batchResult.ErrorMessages.Add(moveResult.ErrorMessage ?? "Unknown error");
            }
        }
        return batchResult;
    }

    public async Task<BatchOperationResult> DeleteManyAsync(
        IReadOnlyList<IFileSystemEntry> entries,
        IFileSystemPanel panel,
        CancellationToken cancellationToken = default)
    {
        var batchResult = new BatchOperationResult();
        for (int i = 0; i < entries.Count; i++)
        {
            var deleteResult = await DeleteAsync(entries[i], panel, cancellationToken);
            if (deleteResult.Success)
            {
                batchResult.SucceededCount++;
            }
            else
            {
                batchResult.FailedNames.Add(entries[i].Name);
                batchResult.ErrorMessages.Add(deleteResult.ErrorMessage ?? "Unknown error");
            }
        }
        return batchResult;
    }

    /// <summary>
    /// Writes real NDFS metadata (from DriverAdapterFileSystemEntry.ExtractProperties, keyed under
    /// the "ndfs." prefix — see NdfsPropertyBagKeys) as a JSON ".xat" sidecar next to a just-copied
    /// file, using the entry's actual on-disk name (post-sanitization, e.g. NativeFileSystemPanel's
    /// colon->period mapping) rather than the source's name, so the sidecar pairs with the real
    /// file and never itself hits the NTFS-ADS-colon issue. No-ops if the source carried no NDFS
    /// metadata (e.g. it isn't an NDFS entry, or metadata population failed).
    /// </summary>
    private static async Task TryWriteNdfsMetadataSidecarAsync(
        IPropertyBag sourceProps,
        IFileSystemPanel targetPanel,
        IFileSystemEntry targetEntry,
        CancellationToken cancellationToken)
    {
        var ndfsMetadata = ExtractNdfsProperties(sourceProps);
        if (ndfsMetadata.Count == 0)
            return;

        var xatName = NdfsXatSerializer.GetXatFileName(targetEntry.Name);
        var json = NdfsXatSerializer.SerializeToJson(ndfsMetadata);
        using var xatStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await targetPanel.CreateFileAsync(xatName, xatStream, null, cancellationToken);
    }

    /// <summary>
    /// Reads a ".xat" sidecar sitting next to <paramref name="source"/> (same directory, filename
    /// derived via NdfsXatSerializer.GetXatFileName) and returns just its NDFS-specific properties.
    /// Deliberately reads via source.FullPath directly rather than requiring a concrete
    /// NativeFileSystemEntry/panel reference: for a native source this resolves to a real sidecar
    /// path; for any other source type FullPath is a virtual filesystem path that can never resolve
    /// to a real sidecar, so this safely (and testably, without a live filesystem panel) no-ops.
    /// Best-effort — a missing, foreign, or corrupt sidecar must never fail the copy.
    /// </summary>
    private static IDictionary<string, object>? TryReadNdfsMetadataSidecar(IFileSystemEntry source)
    {
        try
        {
            var directory = Path.GetDirectoryName(source.FullPath);
            if (string.IsNullOrEmpty(directory))
                return null;

            var xatPath = Path.Combine(directory, NdfsXatSerializer.GetXatFileName(source.Name));
            if (!File.Exists(xatPath))
                return null;

            var json = File.ReadAllText(xatPath);
            var parsed = NdfsXatSerializer.DeserializeFromJson(json);
            return ExtractNdfsProperties(parsed);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Filters a property bag down to just the NDFS-specific ("ndfs."-prefixed) entries, for
    /// exporting as a .xat sidecar (see TryWriteNdfsMetadataSidecarAsync). The "is this an NDFS
    /// key" rule itself lives in exactly one place — NdfsPropertyBagKeys.IsNdfsProperty — this and
    /// its IDictionary&lt;string,object&gt; overload below are thin, differently-shaped adapters
    /// over that single rule (IPropertyBag and NdfsXatSerializer's output are two genuinely
    /// different CLR shapes — an IPropertyBag value can be null, NdfsXatSerializer's cannot — so one
    /// generic method can't cover both without extra allocation/casting for no real benefit).
    /// </summary>
    private static Dictionary<string, object> ExtractNdfsProperties(IPropertyBag bag)
    {
        var result = new Dictionary<string, object>();
        var all = bag.GetAllProperties();
        foreach (var kvp in all)
        {
            if (kvp.Value != null && NdfsPropertyBagKeys.IsNdfsProperty(kvp.Key))
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    /// <summary>See <see cref="ExtractNdfsProperties(IPropertyBag)"/>.</summary>
    private static Dictionary<string, object> ExtractNdfsProperties(IDictionary<string, object> properties)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in properties)
        {
            if (kvp.Value != null && NdfsPropertyBagKeys.IsNdfsProperty(kvp.Key))
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

    private string GetFileSystemType(IFileSystemEntry entry, IFileSystemPanel? panel)
    {
        // Try to determine from entry properties first
        if (entry.Properties.HasProperty(NdfsProperties.UserId))
            return "NDFS";

        if (entry.Properties.HasProperty(AzureProperties.ETag))
            return "Azure";

        if (entry.Properties.HasProperty(CbmProperties.FileType))
            return "CBM";

        if (entry.Properties.HasProperty(WindowsProperties.Attributes))
            return "Windows";

        // Fallback to panel type if available
        return panel?.FileSystemType ?? "Unknown";
    }
}

/// <summary>
/// Stream wrapper that reports progress
/// </summary>
internal class ProgressStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _totalBytes;
    private readonly IProgress<TransferProgress>? _progress;
    private readonly string _fileName;
    private long _bytesRead;

    public ProgressStream(Stream innerStream, long totalBytes, IProgress<TransferProgress>? progress, string fileName)
    {
        _innerStream = innerStream;
        _totalBytes = totalBytes;
        _progress = progress;
        _fileName = fileName;
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _innerStream.Read(buffer, offset, count);
        _bytesRead += bytesRead;

        _progress?.Report(new TransferProgress
        {
            BytesTransferred = _bytesRead,
            TotalBytes = _totalBytes,
            CurrentFile = _fileName
        });

        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        _bytesRead += bytesRead;

        _progress?.Report(new TransferProgress
        {
            BytesTransferred = _bytesRead,
            TotalBytes = _totalBytes,
            CurrentFile = _fileName
        });

        return bytesRead;
    }

    public override void Flush() => _innerStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
    public override void SetLength(long value) => _innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream?.Dispose();
        }
        base.Dispose(disposing);
    }
}
