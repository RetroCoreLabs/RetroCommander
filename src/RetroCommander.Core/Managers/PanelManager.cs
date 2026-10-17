//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Providers;

namespace RetroCommander.Core.Managers;

/// <summary>
/// Manages filesystem panels
/// </summary>
public class PanelManager
{
    private readonly Dictionary<string, IFileSystemPanel> _panels = new();
    private readonly IVolumeProviderFactory _providerFactory;
    private int _nextPanelId = 0;

    public PanelManager(IVolumeProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    /// <summary>
    /// Left panel (primary)
    /// </summary>
    public IFileSystemPanel? LeftPanel { get; set; }

    /// <summary>
    /// Right panel (secondary)
    /// </summary>
    public IFileSystemPanel? RightPanel { get; set; }

    /// <summary>
    /// All managed panels
    /// </summary>
    public IReadOnlyDictionary<string, IFileSystemPanel> AllPanels => _panels;

    /// <summary>
    /// Create a new panel from URN
    /// </summary>
    public async Task<IFileSystemPanel> CreatePanelAsync(
        string urn,
        CancellationToken cancellationToken = default)
    {
        var panel = await _providerFactory.CreatePanelAsync(urn, cancellationToken);
        _panels[panel.Id] = panel;
        return panel;
    }

    /// <summary>
    /// Register an existing panel
    /// </summary>
    public void RegisterPanel(IFileSystemPanel panel)
    {
        if (panel == null)
            throw new ArgumentNullException(nameof(panel));

        _panels[panel.Id] = panel;
    }

    /// <summary>
    /// Get panel by ID
    /// </summary>
    public IFileSystemPanel? GetPanel(string panelId)
    {
        return _panels.TryGetValue(panelId, out var panel) ? panel : null;
    }

    /// <summary>
    /// Remove panel
    /// </summary>
    public bool RemovePanel(string panelId)
    {
        if (_panels.TryGetValue(panelId, out var panel))
        {
            if (LeftPanel == panel) LeftPanel = null;
            if (RightPanel == panel) RightPanel = null;
            bool removed = _panels.Remove(panelId);

            // CRITICAL: dispose the panel so its driver -> block device -> FileStream are flushed
            // and closed. Without this, an unmount never wrote pending changes back to the image
            // (newly created users/files were lost on remount) and the file handle leaked.
            if (panel is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return removed;
        }
        return false;
    }

    /// <summary>
    /// Get next panel ID
    /// </summary>
    internal string GetNextPanelId()
    {
        return $"panel-{_nextPanelId++}";
    }
}
