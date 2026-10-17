//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;

namespace RetroCommander.Core.Providers;

/// <summary>
/// Factory for creating filesystem panels from URNs
/// </summary>
public interface IVolumeProviderFactory
{
    /// <summary>
    /// The mount mode used by <see cref="CreatePanelAsync(string, CancellationToken)"/> when no
    /// explicit mode is given (i.e. automount). Set from the user's preference.
    /// </summary>
    MountMode DefaultMountMode { get; set; }

    /// <summary>
    /// Create a filesystem panel from a URN, using <see cref="DefaultMountMode"/>.
    /// </summary>
    Task<IFileSystemPanel> CreatePanelAsync(string urn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a filesystem panel from a URN with an explicit mount mode (e.g. the right-click
    /// "Open Read/Write" / "Open Read Only" commands), overriding <see cref="DefaultMountMode"/>.
    /// </summary>
    Task<IFileSystemPanel> CreatePanelAsync(string urn, MountMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if URN is supported
    /// </summary>
    bool SupportsUrn(string urn);
}
