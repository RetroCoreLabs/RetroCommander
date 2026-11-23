//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.Text;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Logging;
using RetroCommander.Core.PropertyBags;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Transfer;
using RetroIO.Storage.Ndfs.PropertyBags;
using RetroIO.Storage.Core;

namespace RetroCommander.Core.Operations;

/// <summary>
/// Smart file operations with property preservation
/// </summary>
public class SmartFileOperations : IFileOperationsManager
{
    private readonly PropertyMapperRegistry _mapperRegistry;
    private readonly IRetroCommanderLogger? _logger;

    /// <summary>
    /// Creates the file operations service.
    /// </summary>
    /// <param name="mapperRegistry">
    /// Registry that picks the property mapper for a source and target file system type.
    /// When null, <see cref="PropertyMapperRegistry.CreateDefault"/> is used.
    /// </param>
    /// <param name="logger">
    /// Optional logger; when null nothing is logged.
    /// </param>
    public SmartFileOperations(PropertyMapperRegistry? mapperRegistry = null, IRetroCommanderLogger? logger = null)
    {
        _mapperRegistry = mapperRegistry ?? PropertyMapperRegistry.CreateDefault();
        _logger = logger;
    }

    /// <summary>
    /// Copies one file into the current folder of <paramref name="targetPanel"/>, mapping its properties to the target file system.
    /// </summary>
    /// <remarks>
    /// Steps, in order:
    ///  - A ".xat" sidecar going into NDFS with <see cref="CopyOptions.UseXatMetadataOnImport"/> set is skipped, not copied.
    ///  - If a file with the target name already exists on the host disk and <see cref="CopyOptions.Overwrite"/> is false, the copy fails.
    ///  - When <see cref="CopyOptions.PreserveProperties"/> is set, the source properties are mapped and the kept and lost property names are recorded.
    ///  - When importing into NDFS, NDFS metadata from a ".xat" sidecar next to the source is added on top of the mapped properties.
    ///  - The content is written with <see cref="IFileSystemPanel.CreateFileAsync"/>, reporting progress to <see cref="CopyOptions.Progress"/>.
    ///  - When copying out to a non-NDFS target, NDFS metadata is written to a ".xat" sidecar if <see cref="CopyOptions.PreserveNdfsMetadataOnExport"/> is set.
    /// Errors are caught and returned as a failed result, not thrown.
    /// </remarks>
    /// <param name="source">
    /// The file to copy.
    /// </param>
    /// <param name="targetPanel">
    /// The panel whose current folder receives the copy.
    /// </param>
    /// <param name="options">
    /// Copy settings; when null, a default <see cref="CopyOptions"/> is used.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the copy.
    /// </param>
    /// <returns>
    /// A result that says whether the copy worked, was skipped, or failed, and which properties were kept or lost.
    /// </returns>
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
            // Whether the target's driver keeps file properties is its own declaration (XatSidecarRules).
            FileSystemCapabilities? targetDeclared = XatSidecarRules.Declared(targetPanel);
            FileSystemCapabilities? sourceDeclared = XatSidecarRules.Declared(source);
            if (options.UseXatMetadataOnImport && targetDeclared != null && source is NativeFileSystemEntry &&
                NdfsXatSerializer.IsXatFile(source.Name))
            {
                return CopyResult.SkippedResult(
                    $"'{source.Name}' holds the properties of the file beside it, used for that file instead of copied separately");
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
            // Only the properties the target's driver declares are taken from the .xat.
            if (options.UseXatMetadataOnImport && targetDeclared != null && source is NativeFileSystemEntry)
            {
                var imported = TryReadXatSidecar(source, targetDeclared);
                if (imported != null)
                {
                    foreach (var kvp in imported)
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
            string? landedPath = null;
            using (var sourceStream = await OpenConvertedAsync(source, options.Convert, cancellationToken))
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
                var progressStream = new ProgressStream(sourceStream, sourceStream.CanSeek ? sourceStream.Length : source.Size, options.Progress, source.Name);

                // 6. Create target file with mapped properties
                var targetEntry = await targetPanel.CreateFileAsync(
                    source.Name,
                    progressStream,
                    targetProps,
                    cancellationToken);

                bytesTransferred = source.Size;
                landedPath = targetEntry.FullPath;

                _logger?.LogInfo(
                    $"CopyAsync diag: CreateFileAsync returned targetEntry.Name='{targetEntry.Name}' " +
                    $"targetEntry.FullPath='{targetEntry.FullPath}' targetEntry.Size={targetEntry.Size}");

                // 7. Export NDFS-specific metadata (owner, access bits, page layout, ...) as a
                // ".xat" sidecar file when copying out of NDFS into a filesystem that can't hold it
                // natively. Same-type copies (NDFS->NDFS) don't need this — DirectPropertyMapper
                // already carried the raw "ndfs." properties into targetProps above.
                // Copied out to a host folder: the properties the source's driver declares go in
                // NAME.xat beside the file. A copy between two disks carries them in targetProps.
                if (options.PreserveProperties && options.PreserveNdfsMetadataOnExport && sourceDeclared != null &&
                    targetPanel is NativeFileSystemPanel)
                {
                    await TryWriteXatSidecarAsync(sourceProps, sourceDeclared, targetPanel, targetEntry, cancellationToken);
                }
            }

            _logger?.LogTransfer(source.FullPath, targetPath, bytesTransferred, true);

            var done = CopyResult.SuccessResult(preserved, lost);
            done.TargetPath = landedPath;
            return done;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Copy failed: {source.FullPath} -> {targetPanel.CurrentPath}/{source.Name}", ex);
            return CopyResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Moves one file by copying it with <see cref="CopyAsync"/> and then deleting the source.
    /// </summary>
    /// <remarks>
    /// The source is deleted only if the copy succeeded. The result of the delete step is not checked.
    /// Errors are caught and returned as a failed result, not thrown.
    /// </remarks>
    /// <param name="source">
    /// The file to move.
    /// </param>
    /// <param name="sourcePanel">
    /// The panel the file is deleted from after the copy.
    /// </param>
    /// <param name="targetPanel">
    /// The panel whose current folder receives the file.
    /// </param>
    /// <param name="options">
    /// Move settings, passed on to the copy step; when null, a default <see cref="MoveOptions"/> is used.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the move.
    /// </param>
    /// <returns>
    /// A result that says whether the move worked, with the error message if it did not.
    /// </returns>
    public async Task<MoveResult> MoveAsync(
        IFileSystemEntry source,
        IFileSystemPanel sourcePanel,
        IFileSystemPanel targetPanel,
        MoveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MoveOptions();

        // Moving a file into the folder it is already in, on the same volume: the copy would land
        // on the file itself and the delete below would then remove the only copy. Found with
        // retrocommander-cli, where it emptied a disk image.
        if (!string.IsNullOrEmpty(targetPanel.VolumeUrn) &&
            string.Equals(sourcePanel.VolumeUrn, targetPanel.VolumeUrn, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(FolderOf(source.FullPath), FolderOf(targetPanel.CurrentPath + "/x"), StringComparison.OrdinalIgnoreCase))
        {
            return MoveResult.Failure($"'{source.Name}' is already in {targetPanel.CurrentPath}");
        }

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

    /// <summary>
    /// Deletes one entry through <paramref name="panel"/>.
    /// </summary>
    /// <remarks>
    /// Errors are caught and returned as a failed result, not thrown.
    /// </remarks>
    /// <param name="entry">
    /// The entry to delete.
    /// </param>
    /// <param name="panel">
    /// The panel that holds the entry.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the delete.
    /// </param>
    /// <returns>
    /// A result that says whether the delete worked, with the error message if it did not.
    /// </returns>
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

    /// <summary>
    /// Renames one entry through <paramref name="panel"/>.
    /// </summary>
    /// <remarks>
    /// Errors are caught and returned as a failed result, not thrown.
    /// </remarks>
    /// <param name="entry">
    /// The entry to rename.
    /// </param>
    /// <param name="panel">
    /// The panel that holds the entry.
    /// </param>
    /// <param name="newName">
    /// The new name for the entry.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to cancel the rename.
    /// </param>
    /// <returns>
    /// A result that says whether the rename worked, with the error message if it did not.
    /// </returns>
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

    /// <summary>
    /// Copies each entry in turn with <see cref="CopyAsync"/>, carrying on past failures.
    /// </summary>
    /// <param name="sources">
    /// The files to copy, in the order they are copied.
    /// </param>
    /// <param name="targetPanel">
    /// The panel whose current folder receives the copies.
    /// </param>
    /// <param name="options">
    /// Copy settings used for every entry; when null, defaults are used.
    /// </param>
    /// <param name="cancellationToken">
    /// Token passed to each copy.
    /// </param>
    /// <returns>
    /// The number copied, plus the names and error messages of those that failed and the names of those skipped.
    /// </returns>
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

    /// <summary>
    /// Moves each entry in turn with <see cref="MoveAsync"/>, carrying on past failures.
    /// </summary>
    /// <param name="sources">
    /// The files to move, in the order they are moved.
    /// </param>
    /// <param name="sourcePanel">
    /// The panel the files are deleted from after each copy.
    /// </param>
    /// <param name="targetPanel">
    /// The panel whose current folder receives the files.
    /// </param>
    /// <param name="options">
    /// Move settings used for every entry; when null, defaults are used.
    /// </param>
    /// <param name="cancellationToken">
    /// Token passed to each move.
    /// </param>
    /// <returns>
    /// The number moved, plus the names and error messages of those that failed.
    /// </returns>
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

    /// <summary>
    /// Deletes each entry in turn with <see cref="DeleteAsync"/>, carrying on past failures.
    /// </summary>
    /// <param name="entries">
    /// The entries to delete, in the order they are deleted.
    /// </param>
    /// <param name="panel">
    /// The panel that holds the entries.
    /// </param>
    /// <param name="cancellationToken">
    /// Token passed to each delete.
    /// </param>
    /// <returns>
    /// The number deleted, plus the names and error messages of those that failed.
    /// </returns>
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
    /// Opens a file for copying: as it is, or, with a conversion, its whole content converted.
    /// </summary>
    private static async Task<Stream> OpenConvertedAsync(IFileSystemEntry source, ContentConversion? convert, CancellationToken cancellationToken)
    {
        Stream raw = await source.OpenReadAsync(cancellationToken);
        if (convert == null)
            return raw;

        using (raw)
        {
            using var all = new MemoryStream();
            await raw.CopyToAsync(all, cancellationToken);
            return new MemoryStream(convert(all.ToArray()), writable: false);
        }
    }
    /// <summary>
    /// Writes NAME.xat beside a file just copied into a host folder, holding the properties the
    /// source disk's driver declares (see <see cref="XatSidecarRules"/>). Nothing is written when
    /// the file has none of them.
    /// </summary>
    private static async Task TryWriteXatSidecarAsync(
        IPropertyBag sourceProps,
        FileSystemCapabilities declared,
        IFileSystemPanel targetPanel,
        IFileSystemEntry targetEntry,
        CancellationToken cancellationToken)
    {
        var kept = XatSidecarRules.Pick(sourceProps.GetAllProperties(), declared);
        if (kept.Count == 0)
            return;

        var xatName = NdfsXatSerializer.GetXatFileName(targetEntry.Name);
        var json = NdfsXatSerializer.SerializeToJson(kept);
        using var xatStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await targetPanel.CreateFileAsync(xatName, xatStream, null, cancellationToken);
    }

    /// <summary>
    /// Reads NAME.xat beside a host file being copied onto a disk and returns the properties in it
    /// that the target disk's driver declares, or null when there is no .xat.
    /// </summary>
    /// <remarks>
    /// A .xat that is there but cannot be read is an error and fails the copy: copying the file
    /// without the properties it says it has would lose them without a word.
    /// </remarks>
    private static Dictionary<string, object>? TryReadXatSidecar(IFileSystemEntry source, FileSystemCapabilities declared)
    {
        var directory = Path.GetDirectoryName(source.FullPath);
        if (string.IsNullOrEmpty(directory))
            return null;

        var xatPath = Path.Combine(directory, NdfsXatSerializer.GetXatFileName(source.Name));
        if (!File.Exists(xatPath))
            return null;

        var parsed = NdfsXatSerializer.DeserializeFromJson(File.ReadAllText(xatPath));
        return XatSidecarRules.Pick(parsed, declared);
    }

    // The folder part of a path inside a volume, with forward slashes: "/SUB/A" -> "/SUB", "/A" -> "/".
    private static string FolderOf(string path)
    {
        string p = path.Replace('\\', '/');
        int slash = p.LastIndexOf('/');
        if (slash <= 0)
            return "/";
        return p.Substring(0, slash).TrimEnd('/') is { Length: > 0 } folder ? folder : "/";
    }

    private string GetFileSystemType(IFileSystemEntry entry, IFileSystemPanel? panel)
    {
        // A file on a mounted disk: its driver says what it is. The property checks below are
        // only for entries that have no driver behind them.
        if (entry is DriverAdapterFileSystemEntry onDisk)
            return onDisk.Driver.FileSystemType;

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
