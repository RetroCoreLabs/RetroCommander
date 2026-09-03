//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System.Collections.Generic;

namespace RetroCommander.Core.Metadata
{
    /// <summary>
    /// Generic interface for disk-level metadata (volume/disk information, not file-level).
    /// Allows unified display of filesystem-specific details in UI.
    /// </summary>
    public interface IDiskMetadata
    {
        /// <summary>
        /// Filesystem type (e.g., "BSD V7", "FFS", "NDFS", "Commodore DOS", "FAT16").
        /// </summary>
        string FileSystemType { get; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        long FileSizeBytes { get; }

        /// <summary>
        /// Formatted file size string (e.g., "10,485,760 bytes (10.00 MB)").
        /// </summary>
        string FileSizeFormatted { get; }

        /// <summary>
        /// List of warnings about the disk image (e.g., invalid checksums, missing data).
        /// </summary>
        List<string> Warnings { get; }

        /// <summary>
        /// True if metadata extraction encountered errors.
        /// </summary>
        bool HasErrors { get; }

        /// <summary>
        /// Error message if HasErrors is true.
        /// </summary>
        string? ErrorMessage { get; }

        /// <summary>
        /// Gets detailed properties organized by category for UI display.
        /// Each property includes category (for grouping), label, value, and optional icon.
        /// </summary>
        /// <returns>List of metadata properties ready for display</returns>
        List<MetadataProperty> GetDetailedProperties();
    }
}
