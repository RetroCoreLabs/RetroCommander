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
    /// <summary>
    /// When false (the default), the copy fails if a file with the same name already exists
    /// in the target folder. When true, the existing file is not treated as an error.
    /// </summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// When true (the default), the source file's properties are mapped onto the new file and
    /// the result lists which properties were kept and which were lost.
    /// </summary>
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

    /// <summary>
    /// Receives progress reports while the file content is copied, or null for no reports.
    /// </summary>
    public IProgress<TransferProgress>? Progress { get; set; }
}

/// <summary>
/// Options for move operations
/// </summary>
public class MoveOptions
{
    /// <summary>
    /// Passed on to the copy step of the move; see <see cref="CopyOptions.Overwrite"/>.
    /// </summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// Passed on to the copy step of the move; see <see cref="CopyOptions.PreserveProperties"/>.
    /// </summary>
    public bool PreserveProperties { get; set; } = true;

    /// <summary>
    /// See <see cref="CopyOptions.PreserveNdfsMetadataOnExport"/> — a move is a copy-then-delete,
    /// so this flows through to the same sidecar-writing behavior.
    /// </summary>
    public bool PreserveNdfsMetadataOnExport { get; set; } = true;

    /// <summary>
    /// See <see cref="CopyOptions.UseXatMetadataOnImport"/>.
    /// </summary>
    public bool UseXatMetadataOnImport { get; set; } = true;

    /// <summary>
    /// Passed on to the copy step of the move; see <see cref="CopyOptions.Progress"/>.
    /// </summary>
    public IProgress<TransferProgress>? Progress { get; set; }
}

/// <summary>
/// Transfer progress information
/// </summary>
public class TransferProgress
{
    /// <summary>
    /// Number of bytes of the current file transferred so far.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Total size of the current file in bytes.
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Share of the current file transferred so far, from 0 to 100. Returns 0 when
    /// <see cref="TotalBytes"/> is zero or less.
    /// </summary>
    public double PercentComplete => TotalBytes > 0 ? (BytesTransferred * 100.0 / TotalBytes) : 0;

    /// <summary>
    /// Name of the file being transferred.
    /// </summary>
    public string CurrentFile { get; set; } = string.Empty;
}

/// <summary>
/// Result of a copy operation
/// </summary>
public class CopyResult
{
    /// <summary>
    /// True when the copy finished, or the entry was skipped on purpose (see <see cref="Skipped"/>).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Why the copy failed, or null when it did not fail.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Names of the source properties that the new file also has, or null when not recorded.
    /// </summary>
    public IReadOnlyList<string>? PreservedProperties { get; set; }

    /// <summary>
    /// Names of the source properties that the new file does not have, or null when not recorded.
    /// </summary>
    public IReadOnlyList<string>? LostProperties { get; set; }

    /// <summary>
    /// True when the entry was intentionally not copied as its own file — currently only for a
    /// ".xat" metadata sidecar consumed by <see cref="CopyOptions.UseXatMetadataOnImport"/> instead
    /// of being written as a separate object. Not a failure: <see cref="Success"/> is still true.
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    /// Why the entry was skipped, or null when <see cref="Skipped"/> is false.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Creates a result for a finished copy.
    /// </summary>
    /// <param name="preserved">
    /// Names of the source properties the new file also has, or null.
    /// </param>
    /// <param name="lost">
    /// Names of the source properties the new file does not have, or null.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> set to true and the two property lists filled in.
    /// </returns>
    public static CopyResult SuccessResult(List<string>? preserved = null, List<string>? lost = null) =>
        new() { Success = true, PreservedProperties = preserved, LostProperties = lost };

    /// <summary>
    /// Creates a result for an entry that was not copied on purpose.
    /// </summary>
    /// <param name="reason">
    /// Why the entry was skipped.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> and <see cref="Skipped"/> set to true and
    /// <see cref="SkipReason"/> set to <paramref name="reason"/>.
    /// </returns>
    public static CopyResult SkippedResult(string reason) =>
        new() { Success = true, Skipped = true, SkipReason = reason };

    /// <summary>
    /// Creates a result for a failed copy.
    /// </summary>
    /// <param name="error">
    /// Why the copy failed.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> set to false and <see cref="ErrorMessage"/> set to
    /// <paramref name="error"/>.
    /// </returns>
    public static CopyResult Failure(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of a move operation
/// </summary>
public class MoveResult
{
    /// <summary>
    /// True when the move finished.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Why the move failed, or null when it did not fail.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a result for a finished move.
    /// </summary>
    /// <returns>
    /// A result with <see cref="Success"/> set to true.
    /// </returns>
    public static MoveResult SuccessResult() => new() { Success = true };

    /// <summary>
    /// Creates a result for a failed move.
    /// </summary>
    /// <param name="error">
    /// Why the move failed.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> set to false and <see cref="ErrorMessage"/> set to
    /// <paramref name="error"/>.
    /// </returns>
    public static MoveResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of a delete operation
/// </summary>
public class DeleteResult
{
    /// <summary>
    /// True when the delete finished.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Why the delete failed, or null when it did not fail.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a result for a finished delete.
    /// </summary>
    /// <returns>
    /// A result with <see cref="Success"/> set to true.
    /// </returns>
    public static DeleteResult SuccessResult() => new() { Success = true };

    /// <summary>
    /// Creates a result for a failed delete.
    /// </summary>
    /// <param name="error">
    /// Why the delete failed.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> set to false and <see cref="ErrorMessage"/> set to
    /// <paramref name="error"/>.
    /// </returns>
    public static DeleteResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Result of a rename operation
/// </summary>
public class RenameResult
{
    /// <summary>
    /// True when the rename finished.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Why the rename failed, or null when it did not fail.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a result for a finished rename.
    /// </summary>
    /// <returns>
    /// A result with <see cref="Success"/> set to true.
    /// </returns>
    public static RenameResult SuccessResult() => new() { Success = true };

    /// <summary>
    /// Creates a result for a failed rename.
    /// </summary>
    /// <param name="error">
    /// Why the rename failed.
    /// </param>
    /// <returns>
    /// A result with <see cref="Success"/> set to false and <see cref="ErrorMessage"/> set to
    /// <paramref name="error"/>.
    /// </returns>
    public static RenameResult Failure(string error) => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Summary result of a multi-entry copy/move/delete. Per-entry failures do not abort the batch —
/// remaining entries are still attempted, and each failure is recorded here.
/// </summary>
public class BatchOperationResult
{
    /// <summary>
    /// Number of entries that were handled without error.
    /// </summary>
    public int SucceededCount { get; set; }

    /// <summary>
    /// Names of the entries that failed.
    /// </summary>
    public List<string> FailedNames { get; } = new();

    /// <summary>
    /// Error messages recorded for the failed entries.
    /// </summary>
    public List<string> ErrorMessages { get; } = new();

    /// <summary>
    /// Entries intentionally not copied as their own file (see <see cref="CopyResult.Skipped"/>).
    /// Excluded from <see cref="TotalCount"/> — they were never a user-visible transfer to begin
    /// with, so counting them would make "copied X of Y" status text misleading.
    /// </summary>
    public List<string> SkippedNames { get; } = new();

    /// <summary>
    /// <see cref="SucceededCount"/> plus the number of <see cref="FailedNames"/>. Skipped
    /// entries are not counted.
    /// </summary>
    public int TotalCount => SucceededCount + FailedNames.Count;

    /// <summary>
    /// True when <see cref="FailedNames"/> is empty.
    /// </summary>
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
