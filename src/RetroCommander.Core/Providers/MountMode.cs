//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Providers;

/// <summary>
/// How a disk image should be opened when mounting.
/// </summary>
/// <remarks>
/// Some filesystem operations require a read-write mount to persist — for example NDFS's
/// rename-directory and clear-entered-flag actions, which write back to page 0. Forcing
/// <see cref="ReadWrite"/> makes a failure to obtain write access explicit rather than silently
/// degrading to a throwaway read-only mount.
/// </remarks>
public enum MountMode
{
    /// <summary>
    /// Try read-write first; fall back to read-only if the file cannot be opened for writing
    /// (read-only file/media, or a sharing conflict). This is the historical behaviour.
    /// </summary>
    Auto = 0,

    /// <summary>Always mount read-only, even when write access is available.</summary>
    ReadOnly = 1,

    /// <summary>
    /// Require read-write. If write access cannot be obtained the mount FAILS with a clear error
    /// rather than silently falling back to read-only.
    /// </summary>
    ReadWrite = 2
}
