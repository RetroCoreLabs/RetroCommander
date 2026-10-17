//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Abstractions;

/// <summary>
/// Options for copy operations
/// </summary>
public class CopyOptions
{
    public bool Overwrite { get; set; } = false;
    public bool PreserveProperties { get; set; } = true;

    /// <summary>
    /// When copying a file out of a filesystem with richer metadata than the target can hold
    /// natively (e.g. NDFS -> Windows), write that metadata into a companion ".xat" sidecar file
    /// next to the copy instead of silently dropping it. Has no effect for same-type copies (the
    /// metadata already survives those via the property mapper) or when the source has no such
    /// metadata to begin with. See SmartFileOperations.CopyAsync.
    /// </summary>
    public bool PreserveNdfsMetadataOnExport { get; set; } = true;

    /// <summary>
    /// When copying a file INTO NDFS and a ".xat" sidecar (see <see cref="PreserveNdfsMetadataOnExport"/>)
    /// sits next to the source, read it back and write its NDFS-specific metadata into the new NDFS
    /// file instead of relying on whatever the property mapper produced. The sidecar itself is not
    /// copied as a separate NDFS object — it is consumed as metadata for its paired data file. Has
    /// no effect when the target isn't NDFS, or the source has no matching sidecar. See
    /// SmartFileOperations.CopyAsync.
    /// </summary>
    public bool UseXatMetadataOnImport { get; set; } = true;

    public IProgress<TransferProgress>? Progress { get; set; }
}

/// <summary>
/// Options for move operations
/// </summary>
public class MoveOptions
{
    public bool Overwrite { get; set; } = false;
    public bool PreserveProperties { get; set; } = true;

    /// <summary>
    /// See <see cref="CopyOptions.PreserveNdfsMetadataOnExport"/> — a move is a copy-then-delete,
    /// so this flows through to the same sidecar-writing behavior.
    /// </summary>
    public bool PreserveNdfsMetadataOnExport { get; set; } = true;

    /// <summary>See <see cref="CopyOptions.UseXatMetadataOnImport"/>.</summary>
    public bool UseXatMetadataOnImport { get; set; } = true;

    public IProgress<TransferProgress>? Progress { get; set; }
}

/// <summary>
/// Transfer progress information
/// </summary>
public class TransferProgress
{
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => TotalBytes > 0 ? (BytesTransferred * 100.0 / TotalBytes) : 0;
    public string CurrentFile { get; set; } = string.Empty;
}

/// <summary>
/// Result of a copy operation
/// </summary>
public class CopyResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<string>? PreservedProperties { get; set; }
    public IReadOnlyList<string>? LostProperties { get; set; }

    /// <summary>
    /// True when the entry was intentionally not copied as its own file — currently only for a
    /// ".xat" metadata sidecar consumed by <see cref="CopyOptions.UseXatMetadataOnImport"/> instead
    /// of being written as a separate object. Not a failure: <see cref="Success"/> is still true.
    /// </summary>
    public bool Skipped { get; set; }
    public string? SkipReason { get; set; }

    public static CopyResult SuccessResult(List<string>? preserved = null, List<string>? lost = null) =>
        new() { Success = true, PreservedProperties = preserved, LostProperties = lost };

    public static CopyResult SkippedResult(string reason) =>
        new() { Success = true, Skipped = true, SkipReason = reason };

    public static CopyResult Failure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of a move operation
/// </summary>
public class MoveResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static MoveResult SuccessResult() => new() { Success = true };
    public static MoveResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of a delete operation
/// </summary>
public class DeleteResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static DeleteResult SuccessResult() => new() { Success = true };
    public static DeleteResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of a rename operation
/// </summary>
public class RenameResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static RenameResult SuccessResult() => new() { Success = true };
    public static RenameResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Summary result of a multi-entry copy/move/delete. Per-entry failures do not abort the batch —
/// remaining entries are still attempted, and each failure is recorded here.
/// </summary>
public class BatchOperationResult
{
    public int SucceededCount { get; set; }
    public List<string> FailedNames { get; } = new();
    public List<string> ErrorMessages { get; } = new();

    /// <summary>
    /// Entries intentionally not copied as their own file (see <see cref="CopyResult.Skipped"/>).
    /// Excluded from <see cref="TotalCount"/> — they were never a user-visible transfer to begin
    /// with, so counting them would make "copied X of Y" status text misleading.
    /// </summary>
    public List<string> SkippedNames { get; } = new();

    public int TotalCount => SucceededCount + FailedNames.Count;
    public bool AllSucceeded => FailedNames.Count == 0;
}

/// <summary>
/// Manages file operations with property preservation
/// </summary>
public interface IFileOperationsManager
{
    /// <summary>
    /// Copy file from source to target panel
    /// </summary>
    Task<CopyResult> CopyAsync(
        IFileSystemEntry source,
        IFileSystemPanel targetPanel,
        CopyOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Move file from source to target panel
    /// </summary>
    Task<MoveResult> MoveAsync(
        IFileSystemEntry source,
        IFileSystemPanel sourcePanel,
        IFileSystemPanel targetPanel,
        MoveOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete file
    /// </summary>
    Task<DeleteResult> DeleteAsync(
        IFileSystemEntry entry,
        IFileSystemPanel panel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rename file
    /// </summary>
    Task<RenameResult> RenameAsync(
        IFileSystemEntry entry,
        IFileSystemPanel panel,
        string newName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copy multiple entries to the target panel. Continues past per-entry failures; check
    /// <see cref="BatchOperationResult.AllSucceeded"/> and <see cref="BatchOperationResult.FailedNames"/>
    /// to report which entries failed.
    /// </summary>
    Task<BatchOperationResult> CopyManyAsync(
        IReadOnlyList<IFileSystemEntry> sources,
        IFileSystemPanel targetPanel,
        CopyOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Move multiple entries to the target panel. Continues past per-entry failures.
    /// </summary>
    Task<BatchOperationResult> MoveManyAsync(
        IReadOnlyList<IFileSystemEntry> sources,
        IFileSystemPanel sourcePanel,
        IFileSystemPanel targetPanel,
        MoveOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple entries. Continues past per-entry failures.
    /// </summary>
    Task<BatchOperationResult> DeleteManyAsync(
        IReadOnlyList<IFileSystemEntry> entries,
        IFileSystemPanel panel,
        CancellationToken cancellationToken = default);
}
