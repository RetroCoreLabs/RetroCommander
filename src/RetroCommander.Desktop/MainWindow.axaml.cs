//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Caching;
using RetroCommander.Core.Configuration;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Managers;
using RetroCommander.Core.Operations;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Services;
using RetroCommander.Core.Transfer;
using RetroCommander.Desktop.Controls;
using RetroCommander.Desktop.Themes;
using RetroCommander.Desktop.ViewModels;
using RetroIO.Storage.Core;
using RetroIO.Storage.Core.Utilities;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.BlockDevices;
using RetroIO.Storage.Local.Drivers;
using RetroIO.Storage.Ndfs.BlockDevices;
using RetroIO.Storage.Ndfs.Drivers;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop;

public partial class MainWindow : Window
{
    private readonly PanelManager _panelManager;
    private readonly MountManager _mountManager;
    private readonly VolumeProviderFactory _factory;
    private readonly SmartFileOperations _fileOps;
    private readonly UnifiedLogger _logger;
    private readonly string _logFilePath;
    private readonly DriverFactory _driverFactory;
    private readonly FileSystemMetadataService _metadataService;

    private IFileSystemPanel? _leftPanel;
    private IFileSystemPanel? _rightPanel;
    private bool _leftPanelActive = true;

    // Auto-unmount is "armed" only after the initial default panels are in place. This stops the
    // very first panel refreshes at startup from wiping any pre-existing/persisted mounts before
    // the user has had a chance to look at anything.
    private bool _autoUnmountArmed;

    private readonly NavigationHistory _leftHistory = new();
    private readonly NavigationHistory _rightHistory = new();

    private ObservableCollection<FileEntryViewModel> _leftEntries = new();
    private ObservableCollection<FileEntryViewModel> _rightEntries = new();

    // Drag-and-drop state. A single shared start point/flag is safe because only one pointer
    // device can be down on one grid at a time; PointerPressed on a grid always overwrites this
    // right before that grid's own PointerMoved handler can fire.
    private Avalonia.Point _dragStartPoint;
    private bool _dragInProgress;
    private const double DragStartThreshold = 6.0;
    private const string DragEntriesFormat = "RetroCommander.SelectedEntries";

    // Snapshot of SelectedEntries taken just before a click, via a Tunnel-phase PointerPressed
    // handler that is guaranteed to run before the DataGrid's own click-to-select logic (Tunnel
    // always finishes before Direct/Bubble). Recovered in TryBeginDragAsync when a plain click on
    // an already-multi-selected row would otherwise collapse the drag down to just that one row.
    private readonly List<IFileSystemEntry> _preClickSelection = new();

    public MainWindow()
    {
        InitializeComponent();
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://RetroCommander/Assets/RetroCommander-icon.png")));

        // Initialize core components
        var loggingConfig = new LoggingConfiguration
        {
            FileLogLevel = LogLevel.Debug,
            UILogLevel = LogLevel.Warning
        };
        _logFilePath = loggingConfig.LogFilePath;
        _logger = new UnifiedLogger(loggingConfig);

        var cacheManager = new FileSystemCacheManager(new CacheConfiguration());

        // Use centralized driver registration from RetroFS library (NO business logic in UI!)
        _driverFactory = DriverFactoryBuilder.CreateWithAllDrivers();
        _metadataService = new FileSystemMetadataService(_driverFactory);

        _factory = new VolumeProviderFactory(null, _driverFactory, cacheManager, _logger);

        // Apply the user's automount preference (Auto / ReadOnly / ReadWrite). The right-click
        // "Open Read/Write" / "Open Read Only" commands override this per-mount.
        _factory.DefaultMountMode = RetroCommanderSettings.Load().GetDefaultMountMode();

        _panelManager = new PanelManager(_factory);
        _mountManager = new MountManager(_factory, _panelManager, _logger);

        var propertyRegistry = PropertyMapperRegistry.CreateDefault();
        _fileOps = new SmartFileOperations(propertyRegistry, _logger);

        // Debug: Add test items FIRST to verify binding works
        _leftEntries.Add(new FileEntryViewModel(null, "TEST - If you see this, binding works"));
        _rightEntries.Add(new FileEntryViewModel(null, "TEST - If you see this, binding works"));

        Console.WriteLine($"DEBUG: Added test items. Left count: {_leftEntries.Count}, Right count: {_rightEntries.Count}");

        // Enable double-click to navigate
        LeftPanel.DoubleTapped += OnLeftDoubleClick;
        RightPanel.DoubleTapped += OnRightDoubleClick;

        // Drag-and-drop between panels: DragDrop.AllowDrop="True" is set in XAML; the drag is
        // triggered manually from OnLeftPanelPointerMoved/OnRightPanelPointerMoved (Avalonia's
        // DataGrid has no built-in row-drag gesture), and Drop/DragOver are wired here since
        // Avalonia's DragDrop events are attached routed events with no XAML CLR-event shortcut.
        LeftPanel.AddHandler(Avalonia.Input.DragDrop.DropEvent, OnPanelDrop);
        LeftPanel.AddHandler(Avalonia.Input.DragDrop.DragOverEvent, OnPanelDragOver);
        RightPanel.AddHandler(Avalonia.Input.DragDrop.DropEvent, OnPanelDrop);
        RightPanel.AddHandler(Avalonia.Input.DragDrop.DragOverEvent, OnPanelDragOver);

        // Tunnel-phase (fires before the DataGrid's own click-to-select handling) so we can
        // snapshot the selection as it stood immediately before this click — see
        // _preClickSelection and TryBeginDragAsync for why.
        LeftPanel.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnPanelPreviewPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        RightPanel.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnPanelPreviewPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Keyboard shortcuts
        KeyDown += OnWindowKeyDown;

        // Set ItemsSource when window is fully loaded
        Loaded += (s, e) =>
        {
            Console.WriteLine("DEBUG: Window Loaded event - setting ItemsSource");
            LeftPanel.ItemsSource = _leftEntries;
            RightPanel.ItemsSource = _rightEntries;
            Console.WriteLine($"DEBUG: ItemsSource set. Left: {_leftEntries.Count}, Right: {_rightEntries.Count}");

            // Wire up sidebar event handlers
            NavigationSidebar.ItemSelected += OnSidebarItemSelected;
            NavigationSidebar.UnmountRequested += OnSidebarUnmountRequested;
            NavigationSidebar.PropertiesRequested += OnSidebarPropertiesRequested;
            NavigationSidebar.MountRequested += OnSidebarMountRequested;

            // Wire up unified toolbar's Back button (history is owned here in MainWindow)
            QuickAccess.BackRequested += OnBackClick;

            // Wire up unified toolbar's "Mount New..." flyout item to the same Mount dialog
            // flow the main toolbar's Mount button uses.
            QuickAccess.MountNewRequested += OnMountClick;

            // Wire up the toolbar address bar (type a path + Enter to navigate the active panel)
            QuickAccess.AddressNavigationRequested += OnAddressNavigationRequested;

            // Load existing mounts from MountManager and populate sidebar
            LoadExistingMounts();

            // Initialize navigation toolbar
            InitializeNavigationToolbar();

            // Initialize back button state
            UpdateBackButtonState();
        };

        // Auto-mount default Windows paths after window is loaded
        Opened += async (s, e) => await InitializeDefaultPanelsAsync();
    }

    private void InitializeNavigationToolbar()
    {
        // Populate drive list - NOW IN QuickAccessBar
        /* DISABLED - DriveCombo removed from UI, now in QuickAccessBar
        var drives = System.IO.DriveInfo.GetDrives();
        DriveCombo.Items.Clear();

        for (int i = 0; i < drives.Length; i++)
        {
            var drive = drives[i];
            if (drive.IsReady)
            {
                var label = drive.VolumeLabel;
                var item = string.IsNullOrEmpty(label)
                    ? $"{drive.Name}"
                    : $"{drive.Name} ({label})";
                DriveCombo.Items.Add(item);
            }
            else
            {
                DriveCombo.Items.Add(drive.Name);
            }
        }

        if (DriveCombo.Items.Count > 0)
            DriveCombo.SelectedIndex = 0;
        */

        // Update path box with current panel path - NOW in panel headers
        // UpdatePathBox(); // DISABLED - PathBox removed
    }

    // Pushes the active panel's current path into the toolbar address bar (QuickAccessBar).
    private void UpdatePathBox()
    {
        var activePanel = _leftPanelActive ? _leftPanel : _rightPanel;
        if (activePanel != null)
        {
            QuickAccess.SetAddressPath(activePanel.CurrentPath);
        }
    }

    // The user typed a path in the toolbar address bar and pressed Enter — navigate the active panel.
    private async void OnAddressNavigationRequested(object? sender, string path)
    {
        try
        {
            SetStatus($"Navigating to {path}...");
            await NavigateToPathAsync(path, _leftPanelActive);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Address navigation failed: {ex.Message}", ex);
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async Task InitializeDefaultPanelsAsync()
    {
        try
        {
            SetStatus("Initializing panels...");

            // Mount My Documents on left
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var docsUrn = "file:///" + docsPath.Replace("\\", "/");

            SetStatus($"Mounting left: {docsPath}");
            _leftPanel = await _factory.CreatePanelAsync(docsUrn);
            await RefreshLeftPanelAsync();

            // Mount Temp on right
            var tempPath = System.IO.Path.GetTempPath();
            var tempUrn = "file:///" + tempPath.Replace("\\", "/");

            SetStatus($"Mounting right: {tempPath}");
            _rightPanel = await _factory.CreatePanelAsync(tempUrn);
            await RefreshRightPanelAsync();

            SetStatus($"Ready - Left: {_leftEntries.Count}, Right: {_rightEntries.Count}");

            // Initialize back button state
            UpdateBackButtonState();

            // Default panels are now in place; auto-unmount may start acting on navigation.
            _autoUnmountArmed = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Error initializing: {ex.Message}");
            _logger?.LogError($"InitializeDefaultPanels failed", ex);
        }
    }

    /// <summary>
    /// Reference-counted auto-unmount. When the setting is enabled, any mounted disk image that
    /// neither panel is currently "inside" is unmounted, so the Mounts list cannot grow forever.
    ///
    /// "Inside" == a panel's Id equals the mount's Id. The panel object (and therefore its Id) is
    /// preserved across <c>NavigateIntoAsync</c>, so an Id match covers the mount's root AND every
    /// subdirectory. Navigating a panel to a different volume creates a new panel (new Id), so the
    /// old mount stops matching and becomes eligible. Mounting always opens the disk into a panel,
    /// so a freshly-mounted disk is never considered idle here.
    /// </summary>
    private void MaybeAutoUnmountIdleMounts()
    {
        if (!_autoUnmountArmed)
            return;
        if (!UiSettings.Instance.AutoUnmountIdleDisks)
            return;

        string? leftId = _leftPanel?.Id;
        string? rightId = _rightPanel?.Id;

        // Snapshot the mounts first: Unmount() mutates the underlying collection.
        var mounts = new List<MountPoint>(_mountManager.MountPoints.Values);
        for (int i = 0; i < mounts.Count; i++)
        {
            MountPoint mp = mounts[i];

            bool inUse =
                (leftId != null && string.Equals(leftId, mp.Id, StringComparison.Ordinal)) ||
                (rightId != null && string.Equals(rightId, mp.Id, StringComparison.Ordinal));

            if (inUse)
                continue;

            // Pinned favorites are kept: a favorited disk is never auto-unmounted.
            if (NavigationSidebar.ViewModel.IsFavorite(mp.Urn))
                continue;

            if (_mountManager.Unmount(mp.Id))
            {
                RemoveMountFromUi(mp.Urn);
                _logger?.LogInfo($"Auto-unmounted idle disk: {mp.DisplayName} ({mp.Urn})");
                SetStatus($"Auto-unmounted {mp.DisplayName}");
            }
        }
    }

    private async Task RefreshLeftPanelAsync()
    {
        if (_leftPanel == null) return;

        // The panel may have just been (un)mounted onto a different filesystem — keep the
        // NDFS-only menu commands in step with what is actually mounted.
        UpdateNdfsMenuVisibility();

        try
        {
            await _leftPanel.RefreshAsync();
            _leftEntries.Clear();

            // Add parent directory if not at root
            if (_leftPanel.CanNavigateUp())
            {
                _leftEntries.Add(new FileEntryViewModel(null, ".."));
            }

            for (int i = 0; i < _leftPanel.Entries.Count; i++)
            {
                _leftEntries.Add(new FileEntryViewModel(_leftPanel.Entries[i]));
            }

            LeftPanelPath.Text = _leftPanel.CurrentPath;
            LeftPanelFsType.Text = _leftPanel.FileSystemType;
            LeftPanelStatus.Text = $"Left: {_leftEntries.Count}";
            SetStatus($"Left panel: {_leftEntries.Count} items displayed");

            // Reflect the active panel's path in the toolbar address bar
            UpdatePathBox();

            // Add to navigation history
            _leftHistory.AddEntry(_leftPanel.CurrentPath);
            _leftHistory.NavigationCompleted();
            UpdateBackButtonState();

            // The left panel may now be inside a different volume; reference-counted auto-unmount
            // reaps any mount that neither panel is viewing anymore.
            MaybeAutoUnmountIdleMounts();
        }
        catch (Exception ex)
        {
            SetStatus($"Error refreshing left: {ex.Message}");
            _logger?.LogError($"RefreshLeftPanel failed: {ex.Message}", ex);
            _leftHistory.NavigationCompleted();
        }
    }

    private async Task RefreshRightPanelAsync()
    {
        if (_rightPanel == null) return;

        // The panel may have just been (un)mounted onto a different filesystem — keep the
        // NDFS-only menu commands in step with what is actually mounted.
        UpdateNdfsMenuVisibility();

        try
        {
            await _rightPanel.RefreshAsync();
            _rightEntries.Clear();

            // Add parent directory if not at root
            if (_rightPanel.CanNavigateUp())
            {
                _rightEntries.Add(new FileEntryViewModel(null, ".."));
            }

            for (int i = 0; i < _rightPanel.Entries.Count; i++)
            {
                _rightEntries.Add(new FileEntryViewModel(_rightPanel.Entries[i]));
            }

            RightPanelPath.Text = _rightPanel.CurrentPath;
            RightPanelFsType.Text = _rightPanel.FileSystemType;
            RightPanelStatus.Text = $"Right: {_rightEntries.Count}";
            SetStatus($"Right panel: {_rightEntries.Count} items displayed");

            // Reflect the active panel's path in the toolbar address bar
            UpdatePathBox();

            // Add to navigation history
            _rightHistory.AddEntry(_rightPanel.CurrentPath);
            _rightHistory.NavigationCompleted();
            UpdateBackButtonState();

            // The right panel may now be inside a different volume; reference-counted auto-unmount
            // reaps any mount that neither panel is viewing anymore.
            MaybeAutoUnmountIdleMounts();
        }
        catch (Exception ex)
        {
            SetStatus($"Error refreshing right: {ex.Message}");
            _logger?.LogError($"RefreshRightPanel failed: {ex.Message}", ex);
            _rightHistory.NavigationCompleted();
        }
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    // Event Handlers
    private async void OnMountClick(object? sender, RoutedEventArgs? e)
    {
        var dialog = new MountDialog(_driverFactory);
        var result = await dialog.ShowDialog<string?>(this);

        if (result != null)
        {
            try
            {
                var panel = await _factory.CreatePanelAsync(result);
                if (_leftPanelActive)
                {
                    _leftPanel = panel;
                    await RefreshLeftPanelAsync();
                }
                else
                {
                    _rightPanel = panel;
                    await RefreshRightPanelAsync();
                }
                SetStatus($"Mounted: {result}");
            }
            catch (Exception ex)
            {
                SetStatus($"Mount failed: {ex.Message}");
            }
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Resolves the entries an operation should act on: the multi-select list when more than one
    /// row is selected, falling back to the single anchor selection otherwise. Panels populate
    /// SelectedEntries from every selection change (see OnLeftSelectionChanged/OnRightSelectionChanged),
    /// so this stays in sync with both mouse multi-select (Ctrl/Shift-click) and keyboard nav.
    /// </summary>
    private static IReadOnlyList<IFileSystemEntry> GetSelection(IFileSystemPanel panel)
    {
        if (panel.SelectedEntries.Count > 0)
            return panel.SelectedEntries;
        if (panel.SelectedEntry != null)
            return new IFileSystemEntry[] { panel.SelectedEntry };
        return Array.Empty<IFileSystemEntry>();
    }

    /// <summary>
    /// Formats a batch result for the status bar. Per-entry failures don't abort the batch (see
    /// BatchOperationResult), so a partial failure is reported as a count + failed-name summary
    /// rather than silently swallowed or aborting the whole operation.
    /// </summary>
    private static string FormatBatchStatus(string verb, BatchOperationResult result, IReadOnlyList<IFileSystemEntry> selection)
    {
        // A skipped ".xat" sidecar (see BatchOperationResult.SkippedNames) was consumed as metadata
        // for its paired file, not copied as its own item — note it rather than let it silently
        // vanish from the count, so "1 item" doesn't read as if the user's 2-item selection lost one.
        var skippedSuffix = result.SkippedNames.Count > 0
            ? $" ({result.SkippedNames.Count} .xat used as metadata, not copied separately)"
            : string.Empty;

        if (result.AllSucceeded)
        {
            var successText = result.TotalCount == 1 && selection.Count > 0
                ? $"✓ {verb} {selection[0].Name}"
                : $"✓ {verb} {result.SucceededCount} items";
            return successText + skippedSuffix;
        }

        return $"⚠ {verb} {result.SucceededCount} of {result.TotalCount} " +
               $"({result.FailedNames.Count} failed: {string.Join(", ", result.FailedNames)}){skippedSuffix}";
    }

    /// <summary>
    /// Maps a source entry's name to what it will be called once written into the target panel's
    /// naming convention — e.g. NDFS's colon separator maps to a Windows target's period, see
    /// NativeFileSystemPanel.SanitizeWindowsFileName — so an existing-file check compares against
    /// the name that will actually land on disk, not the source's own name.
    /// </summary>
    private static string GetDestinationName(IFileSystemPanel targetPanel, string sourceName)
    {
        return targetPanel.FileSystemType == "Windows"
            ? NativeFileSystemPanel.SanitizeWindowsFileName(sourceName)
            : sourceName;
    }

    private static bool TargetHasEntryNamed(IFileSystemPanel targetPanel, string destName)
    {
        for (int i = 0; i < targetPanel.Entries.Count; i++)
        {
            if (string.Equals(targetPanel.Entries[i].Name, destName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Prompts once per selected entry that would collide with an existing file already listed in
    /// the target panel, and returns the entries the user actually agreed to write (everything
    /// non-conflicting, plus every conflict the user chose to overwrite). Shared by
    /// CopyEntriesAsync/MoveEntriesAsync so both the toolbar buttons and drag-and-drop (which
    /// funnel through the same two methods) get the same "file exists, overwrite?" protection.
    /// </summary>
    private async Task<List<IFileSystemEntry>> ResolveOverwritesAsync(IFileSystemPanel targetPanel, IReadOnlyList<IFileSystemEntry> selection)
    {
        var toProcess = new List<IFileSystemEntry>();
        for (int i = 0; i < selection.Count; i++)
        {
            var destName = GetDestinationName(targetPanel, selection[i].Name);
            if (TargetHasEntryNamed(targetPanel, destName))
            {
                var overwrite = await ShowConfirmationDialogAsync(
                    "File Exists",
                    $"'{destName}' already exists in {targetPanel.CurrentPath}.\n\nOverwrite it?");
                if (!overwrite)
                    continue;
            }
            toProcess.Add(selection[i]);
        }
        return toProcess;
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs? e)
    {
        if (_leftPanel == null || _rightPanel == null) return;

        var sourcePanel = _leftPanelActive ? _leftPanel : _rightPanel;
        var targetPanel = _leftPanelActive ? _rightPanel : _leftPanel;

        await CopyEntriesAsync(sourcePanel, targetPanel, GetSelection(sourcePanel));
    }

    /// <summary>
    /// Shared copy path for the Copy button/F5 and drag-and-drop (OnPanelDrop) — both act on a
    /// caller-supplied entry list rather than re-reading panel selection, since a drop's payload
    /// was snapshotted at drag-start time.
    /// </summary>
    private async Task CopyEntriesAsync(IFileSystemPanel sourcePanel, IFileSystemPanel targetPanel, IReadOnlyList<IFileSystemEntry> selection)
    {
        if (selection.Count == 0)
        {
            SetStatus("No file selected");
            return;
        }

        try
        {
            var toProcess = await ResolveOverwritesAsync(targetPanel, selection);
            if (toProcess.Count == 0)
            {
                SetStatus("Copy cancelled");
                return;
            }

            _logger?.LogInfo(
                $"CopyEntriesAsync diag: sourcePanel.FileSystemType='{sourcePanel.FileSystemType}' " +
                $"targetPanel.FileSystemType='{targetPanel.FileSystemType}' " +
                $"selection.Count={toProcess.Count} sourcePanel.Entries.Count={sourcePanel.Entries.Count}");

            SetStatus(toProcess.Count == 1 ? $"Copying {toProcess[0].Name}..." : $"Copying {toProcess.Count} items...");

            var options = new CopyOptions
            {
                Overwrite = true,
                PreserveProperties = true,
                PreserveNdfsMetadataOnExport = UiSettings.Instance.PreserveNdfsMetadataOnExport,
                UseXatMetadataOnImport = UiSettings.Instance.UseXatMetadataOnImport
            };

            var result = await _fileOps.CopyManyAsync(toProcess, targetPanel, options);

            await (ReferenceEquals(targetPanel, _rightPanel) ? RefreshRightPanelAsync() : RefreshLeftPanelAsync());
            SetStatus(FormatBatchStatus("Copied", result, toProcess));
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async void OnMoveClick(object? sender, RoutedEventArgs? e)
    {
        if (_leftPanel == null || _rightPanel == null) return;

        var sourcePanel = _leftPanelActive ? _leftPanel : _rightPanel;
        var targetPanel = _leftPanelActive ? _rightPanel : _leftPanel;

        await MoveEntriesAsync(sourcePanel, targetPanel, GetSelection(sourcePanel));
    }

    /// <summary>
    /// Shared move path for the Move button/F6 and drag-and-drop (Ctrl-drop, see OnPanelDrop).
    /// Always confirms first — a move deletes the source, so it gets the same "are you sure"
    /// treatment as Delete rather than firing immediately like Copy.
    /// </summary>
    private async Task MoveEntriesAsync(IFileSystemPanel sourcePanel, IFileSystemPanel targetPanel, IReadOnlyList<IFileSystemEntry> selection)
    {
        if (selection.Count == 0)
        {
            SetStatus("No file selected");
            return;
        }

        // Check if source filesystem is read-only (need to delete from source)
        if (sourcePanel.IsReadOnly)
        {
            SetStatus("Cannot move: source filesystem is read-only");
            return;
        }

        // Check if target filesystem is read-only
        if (targetPanel.IsReadOnly)
        {
            SetStatus("Cannot move: target filesystem is read-only");
            return;
        }

        var toProcess = await ResolveOverwritesAsync(targetPanel, selection);
        if (toProcess.Count == 0)
        {
            SetStatus("Move cancelled");
            return;
        }

        var confirmMessage = toProcess.Count == 1
            ? $"Move '{toProcess[0].Name}' to {targetPanel.CurrentPath}?"
            : $"Move these {toProcess.Count} items to {targetPanel.CurrentPath}?";

        var confirmed = await ShowConfirmationDialogAsync("Confirm Move", confirmMessage);
        if (!confirmed)
        {
            SetStatus("Move cancelled");
            return;
        }

        try
        {
            SetStatus(toProcess.Count == 1 ? $"Moving {toProcess[0].Name}..." : $"Moving {toProcess.Count} items...");

            var options = new MoveOptions
            {
                Overwrite = true,
                PreserveProperties = true,
                PreserveNdfsMetadataOnExport = UiSettings.Instance.PreserveNdfsMetadataOnExport,
                UseXatMetadataOnImport = UiSettings.Instance.UseXatMetadataOnImport
            };

            var result = await _fileOps.MoveManyAsync(toProcess, sourcePanel, targetPanel, options);

            // Refresh both panels since files moved from source to target
            await RefreshLeftPanelAsync();
            await RefreshRightPanelAsync();
            SetStatus(FormatBatchStatus("Moved", result, toProcess));
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs? e)
    {
        if (_leftPanel == null || _rightPanel == null) return;

        var activePanel = _leftPanelActive ? _leftPanel : _rightPanel;
        var selection = GetSelection(activePanel);

        if (selection.Count == 0)
        {
            SetStatus("No file selected");
            return;
        }

        // Check if filesystem is read-only
        if (activePanel.IsReadOnly)
        {
            SetStatus("Cannot delete: filesystem is read-only");
            return;
        }

        // Show confirmation dialog
        var confirmMessage = selection.Count == 1
            ? $"Are you sure you want to delete '{selection[0].Name}'?\n\nThis action cannot be undone."
            : $"Are you sure you want to delete these {selection.Count} items?\n\nThis action cannot be undone.";

        var confirmed = await ShowConfirmationDialogAsync("Confirm Delete", confirmMessage);

        if (!confirmed)
        {
            SetStatus("Delete cancelled");
            return;
        }

        try
        {
            SetStatus(selection.Count == 1 ? $"Deleting {selection[0].Name}..." : $"Deleting {selection.Count} items...");

            var result = await _fileOps.DeleteManyAsync(selection, activePanel);

            await (_leftPanelActive ? RefreshLeftPanelAsync() : RefreshRightPanelAsync());
            SetStatus(FormatBatchStatus("Deleted", result, selection));
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (_leftPanel == null || _rightPanel == null) return;

        var activePanel = _leftPanelActive ? _leftPanel : _rightPanel;

        if (activePanel.SelectedEntry == null)
        {
            SetStatus("No file selected");
            return;
        }

        // Check if filesystem is read-only
        if (activePanel.IsReadOnly)
        {
            SetStatus("Cannot rename: filesystem is read-only");
            return;
        }

        var entry = activePanel.SelectedEntry;

        // Show rename dialog
        var newName = await ShowInputDialogAsync("Rename", $"Enter new name for '{entry.Name}':", entry.Name);

        if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name)
        {
            SetStatus("Rename cancelled");
            return;
        }

        try
        {
            SetStatus($"Renaming {entry.Name} to {newName}...");

            var result = await _fileOps.RenameAsync(entry, activePanel, newName);

            if (result.Success)
            {
                await (_leftPanelActive ? RefreshLeftPanelAsync() : RefreshRightPanelAsync());
                SetStatus($"Renamed to {newName}");
            }
            else
            {
                SetStatus($"Rename failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (_leftPanelActive)
            await RefreshLeftPanelAsync();
        else
            await RefreshRightPanelAsync();

        UpdatePathBox(); // Update path after refresh
        SetStatus("Refreshed");
    }

    private static string GetDisplayVersion()
    {
        var info = (System.Reflection.Assembly.GetEntryAssembly() ?? typeof(MainWindow).Assembly)
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
        if (info.Length == 0)
            return "unknown";
        var text = ((System.Reflection.AssemblyInformationalVersionAttribute)info[0]).InformationalVersion;
        var plus = text.IndexOf('+');
        return plus >= 0 ? text.Substring(0, plus) : text;
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var about = new Window
        {
            Title = "About RetroCommander",
            Width = 380,
            Height = 270,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        // Apply the current theme's window background so the dialog isn't a plain white box.
        if (this.TryFindResource("FluentBackgroundPrimaryBrush", out var bgVal) && bgVal is Avalonia.Media.IBrush bg)
            about.Background = bg;

        // Build themed content using the shared fluent text classes (theme-aware foregrounds).
        var title = new TextBlock
        {
            Text = "RetroCommander",
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        title.Classes.Add("fluent-text-header");

        var subtitle = new TextBlock
        {
            // InformationalVersion carries the full version, including a -rc1 style suffix, and the
            // release build sets it from the git tag. The +commit part the SDK appends is cut off.
            Text = "Retro Filesystem Manager\nVersion " + GetDisplayVersion(),
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        subtitle.Classes.Add("fluent-text");

        var body = new TextBlock
        {
            Text = "Built with RetroCommander.Core\nSupports NDFS, D64, D71, D81, BSD/FFS and more!",
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        body.Classes.Add("fluent-text-secondary");

        var ok = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 12, 0, 0)
        };
        ok.Classes.Add("dialog-primary");
        ok.Click += (_, _) => about.Close();

        about.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children = { title, subtitle, body, ok }
        };

        await about.ShowDialog(this);
    }

    private async void OnLogClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // No local Background/Foreground overrides here — same convention as every other
            // ad-hoc dialog in this file (e.g. helpWindow, mountDialog): letting the controls
            // inherit the app's live theme (RetroCommander.Desktop.Themes.ThemeManager, switchable to
            // Light/Dark/Solarized Dark/Nord/High Contrast) keeps background and foreground paired
            // correctly. Previously this TextBox force-set white/black while the Window/Button
            // stayed unstyled, so on a dark theme the unstyled controls rendered dark-on-dark next
            // to the hardcoded textbox (reported: "black on black").
            var logWindow = new Window
            {
                Title = "Log Viewer",
                Width = 800,
                Height = 500,
                MinWidth = 600,
                MinHeight = 300
            };

            var textBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                FontFamily = "Consolas",
                FontSize = 11
            };

            // Read last 500 lines from log file (was 20 - too short to see a full copy operation's
            // diagnostic trail: source/target identity, stream length, and CreateFileAsync result).
            void RefreshLogText() => textBox.Text = GetLastLogEntries(500);
            RefreshLogText();

            var refreshButton = new Button
            {
                Content = "Refresh"
            };
            refreshButton.Click += (_, _) => RefreshLogText();

            var scrollViewer = new ScrollViewer
            {
                Content = textBox,
                Margin = new Avalonia.Thickness(10)
            };

            var layout = new DockPanel();
            DockPanel.SetDock(refreshButton, Avalonia.Controls.Dock.Top);
            refreshButton.Margin = new Avalonia.Thickness(10, 10, 10, 0);
            refreshButton.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            layout.Children.Add(refreshButton);
            layout.Children.Add(scrollViewer);

            logWindow.Content = layout;
            await logWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Failed to open log: {ex.Message}");
        }
    }

    private string GetLastLogEntries(int count)
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                return "Log file not found.";
            }

            var lines = File.ReadAllLines(_logFilePath);
            if (lines.Length == 0)
            {
                return "Log file is empty.";
            }

            // Get last 'count' lines (no LINQ - index directly into the array)
            int startIndex = Math.Max(0, lines.Length - count);
            int takeCount = lines.Length - startIndex;
            string[] lastLines = new string[takeCount];
            for (int i = 0; i < takeCount; i++)
            {
                lastLines[i] = lines[startIndex + i];
            }

            return string.Join(Environment.NewLine, lastLines);
        }
        catch (Exception ex)
        {
            return $"Error reading log file: {ex.Message}";
        }
    }

    /// <summary>
    /// Pulls the underlying IFileSystemEntry out of every currently-selected DataGrid row.
    /// Uses vm.Entry directly rather than name-matching against panel.Entries (fragile for
    /// directories, whose VM decorates the name as "[name]") and skips the ".." parent-directory
    /// pseudo-row (vm.Entry is null for it).
    /// </summary>
    private static List<IFileSystemEntry> CollectSelectedEntries(System.Collections.IList? selectedItems)
    {
        var result = new List<IFileSystemEntry>();
        if (selectedItems == null)
            return result;

        for (int i = 0; i < selectedItems.Count; i++)
        {
            if (selectedItems[i] is FileEntryViewModel vm && vm.Entry != null)
            {
                result.Add(vm.Entry);
            }
        }
        return result;
    }

    private void OnLeftSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetActivePanel(true);
        if (_leftPanel == null) return;

        var entries = CollectSelectedEntries(LeftPanel.SelectedItems);
        _leftPanel.SelectedEntries = entries;

        if (LeftPanel.SelectedItem is FileEntryViewModel vm && vm.Entry != null)
        {
            _leftPanel.SelectedEntry = vm.Entry;
        }

        SetStatus(entries.Count > 1 ? $"Selected: {entries.Count} items" :
                  entries.Count == 1 ? $"Selected: {entries[0].Name}" : "Ready");
    }

    private void OnRightSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetActivePanel(false);
        if (_rightPanel == null) return;

        var entries = CollectSelectedEntries(RightPanel.SelectedItems);
        _rightPanel.SelectedEntries = entries;

        if (RightPanel.SelectedItem is FileEntryViewModel vm && vm.Entry != null)
        {
            _rightPanel.SelectedEntry = vm.Entry;
        }

        SetStatus(entries.Count > 1 ? $"Selected: {entries.Count} items" :
                  entries.Count == 1 ? $"Selected: {entries[0].Name}" : "Ready");
    }

    /// <summary>
    /// Tunnel-phase handler (see the AddHandler call in the constructor) that snapshots the
    /// panel's current SelectedEntries before the DataGrid's own click-to-select logic runs, so a
    /// drag started on an already-multi-selected row can recover the full selection (see
    /// TryBeginDragAsync) instead of only the single row the click collapsed onto.
    /// </summary>
    private void OnPanelPreviewPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var panel = ReferenceEquals(sender, LeftPanel) ? _leftPanel : ReferenceEquals(sender, RightPanel) ? _rightPanel : null;

        _preClickSelection.Clear();
        if (panel != null)
        {
            for (int i = 0; i < panel.SelectedEntries.Count; i++)
            {
                _preClickSelection.Add(panel.SelectedEntries[i]);
            }
        }
    }

    private void OnLeftPanelClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        SetActivePanel(true);
        if (ReferenceEquals(sender, LeftPanel))
        {
            _dragStartPoint = e.GetPosition(LeftPanel);
        }
    }

    private void OnRightPanelClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        SetActivePanel(false);
        if (ReferenceEquals(sender, RightPanel))
        {
            _dragStartPoint = e.GetPosition(RightPanel);
        }
    }

    private async void OnLeftPanelPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_leftPanel != null)
            await TryBeginDragAsync(LeftPanel, _leftPanel, e);
    }

    private async void OnLeftPropertiesClick(object? sender, RoutedEventArgs e)
    {
        await ShowPropertiesAsync(_leftPanel);
    }

    private async void OnRightPropertiesClick(object? sender, RoutedEventArgs e)
    {
        await ShowPropertiesAsync(_rightPanel);
    }

    /// <summary>
    /// Right-click "Properties" (see MainWindow.axaml's DataGrid.ContextMenu). Uses the panel's
    /// selection directly rather than the multi-select GetSelection helper (see
    /// SmartFileOperations) — Properties is a single-item concept (unlike Copy/Move/Delete, which
    /// operate on a batch), so only the anchor SelectedEntry is relevant here.
    /// </summary>
    private async Task ShowPropertiesAsync(IFileSystemPanel? panel)
    {
        if (panel?.SelectedEntry == null)
        {
            SetStatus("No file selected");
            return;
        }

        var dialog = new PropertiesDialog(panel.SelectedEntry);
        await dialog.ShowDialog(this);
    }

    /// <summary>
    /// Extracts the physical image file path from a panel's volume URN. Mounted disk images arrive
    /// either as "file:///E:/path/img.ndfs" or as "&lt;scheme&gt;://E:/path/img.ndfs" (e.g. ndfs://),
    /// so strip whatever scheme is present and normalise the separators. Returns the URN unchanged
    /// if it carries no scheme (already a plain path).
    /// </summary>
    private static string ResolveImagePath(string volumeUrn)
    {
        if (string.IsNullOrEmpty(volumeUrn))
            return string.Empty;

        string path = volumeUrn;

        int schemeIndex = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
            path = path.Substring(schemeIndex + 3);

        // "file:///E:/x" leaves a leading '/' before the drive letter — drop it.
        if (path.Length > 2 && path[0] == '/' && char.IsLetter(path[1]) && path[2] == ':')
            path = path.Substring(1);

        // Strip any fragment (e.g. partition selector "#a").
        int hash = path.IndexOf('#');
        if (hash >= 0)
            path = path.Substring(0, hash);

        return path.Replace('/', System.IO.Path.DirectorySeparatorChar);
    }

    // Right-click "Open Read/Write" / "Open Read Only" — mount the selected disk image with an
    // explicit mode, overriding the automount preference for this one mount.
    private void OnLeftOpenReadWriteClick(object? sender, RoutedEventArgs e)
        => _ = OpenSelectedWithModeAsync(_leftPanel, MountMode.ReadWrite, true);

    private void OnLeftOpenReadOnlyClick(object? sender, RoutedEventArgs e)
        => _ = OpenSelectedWithModeAsync(_leftPanel, MountMode.ReadOnly, true);

    private void OnRightOpenReadWriteClick(object? sender, RoutedEventArgs e)
        => _ = OpenSelectedWithModeAsync(_rightPanel, MountMode.ReadWrite, false);

    private void OnRightOpenReadOnlyClick(object? sender, RoutedEventArgs e)
        => _ = OpenSelectedWithModeAsync(_rightPanel, MountMode.ReadOnly, false);

    /// <summary>
    /// Mounts the panel's currently-selected disk image with an explicit <see cref="MountMode"/>,
    /// replacing that panel's contents. Read-write is required for operations that persist back to
    /// the image (e.g. NDFS rename / clear-entered); when ReadWrite cannot be obtained the factory
    /// throws rather than silently degrading, and we surface that plainly.
    /// </summary>
    private async Task OpenSelectedWithModeAsync(IFileSystemPanel? panel, MountMode mode, bool isLeft)
    {
        if (panel?.SelectedEntry == null)
        {
            SetStatus("No file selected");
            return;
        }

        var entry = panel.SelectedEntry;
        if (entry.IsDirectory)
        {
            SetStatus("Select a disk image file (not a directory) to open read/write or read-only.");
            return;
        }

        string modeLabel = mode == MountMode.ReadWrite ? "read/write" : "read-only";

        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(panel.CurrentPath, entry.Name));
            if (!File.Exists(fullPath))
            {
                SetStatus($"Cannot open: '{fullPath}' not found.");
                return;
            }

            SetStatus($"Mounting '{entry.Name}' {modeLabel}...");

            string urn = "file:///" + fullPath.Replace("\\", "/");
            var newPanel = await _factory.CreatePanelAsync(urn, mode);

            if (isLeft)
            {
                _leftPanel = newPanel;
                await RefreshLeftPanelAsync();
            }
            else
            {
                _rightPanel = newPanel;
                await RefreshRightPanelAsync();
            }

            UpdatePathBox();
            SetStatus($"✓ Mounted '{entry.Name}' {modeLabel}"
                      + (newPanel.IsReadOnly && mode == MountMode.ReadWrite ? " (driver reports read-only)" : ""));
        }
        catch (IOException ex)
        {
            // MountMode.ReadWrite could not obtain write access — say so instead of degrading.
            SetStatus($"✗ Cannot mount {modeLabel}: {ex.Message}");
            _logger?.LogError($"Open {modeLabel} failed for '{entry.Name}'", ex);
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Open {modeLabel} failed: {ex.Message}");
            _logger?.LogError($"Open {modeLabel} failed for '{entry.Name}'", ex);
        }
    }

    // File menu → uses the active panel; context menu → the specific panel.
    private void OnDiskInfoClick(object? sender, RoutedEventArgs e)
        => _ = ShowDiskInfoAsync(_leftPanelActive ? _leftPanel : _rightPanel);

    private void OnLeftDiskInfoClick(object? sender, RoutedEventArgs e)
        => _ = ShowDiskInfoAsync(_leftPanel);

    private void OnRightDiskInfoClick(object? sender, RoutedEventArgs e)
        => _ = ShowDiskInfoAsync(_rightPanel);

    /// <summary>
    /// Shows disk information for any mounted volume.
    ///
    /// NDFS panels are routed to the dedicated NDFS window, which can do things the generic
    /// one cannot (rename the volume, clear the entered flag, edit the SINTRAN initial-command
    /// buffer). Everything else gets the filesystem-agnostic window, driven by whatever the
    /// driver reports through IVolumeInfoProvider — or a generic description when it reports
    /// nothing, so no filesystem is left with a dead menu item.
    /// </summary>
    private async Task ShowDiskInfoAsync(IFileSystemPanel? panel)
    {
        if (panel == null)
        {
            SetStatus("Disk Info: no active panel.");
            return;
        }

        // NDFS has a richer, editable screen already - prefer it.
        if (string.Equals(panel.FileSystemType, "NDFS", StringComparison.OrdinalIgnoreCase))
        {
            await ShowNdfsDiskInfoAsync(panel);
            return;
        }

        if (panel is not DriverAdapterFileSystemPanel adapter || adapter.Driver == null)
        {
            SetStatus($"Disk Info: '{panel.FileSystemType}' panels do not expose a disk image.");
            return;
        }

        try
        {
            var window = new DiskInfoWindow(
                adapter.Driver,
                ResolveImagePath(panel.VolumeUrn) ?? string.Empty,
                panel.IsReadOnly);

            await window.ShowDialog(this);
        }
        catch (Exception ex)
        {
            SetStatus($"Disk Info failed: {ex.Message}");
            _logger?.LogError("Disk Info failed", ex);
        }
    }

    private void OnNdfsDiskInfoClick(object? sender, RoutedEventArgs e)
        => _ = ShowNdfsDiskInfoAsync(_leftPanelActive ? _leftPanel : _rightPanel);

    private void OnLeftNdfsDiskInfoClick(object? sender, RoutedEventArgs e)
        => _ = ShowNdfsDiskInfoAsync(_leftPanel);

    private void OnRightNdfsDiskInfoClick(object? sender, RoutedEventArgs e)
        => _ = ShowNdfsDiskInfoAsync(_rightPanel);

    /// <summary>
    /// Opens the post-mount NDFS disk info window for a mounted NDFS panel. The window operates on
    /// the SAME live mounted filesystem the panel holds open (reached via the driver adapter), so
    /// its Rename / Clear-Entered actions persist without opening a second handle to the image.
    /// Read-write actions only succeed when the volume was mounted read-write.
    /// </summary>
    private async Task ShowNdfsDiskInfoAsync(IFileSystemPanel? panel)
    {
        if (panel == null)
            return;

        if (!string.Equals(panel.FileSystemType, "NDFS", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("NDFS Disk Info: the active panel is not an NDFS volume.");
            return;
        }

        // Reach the live driver behind the panel adapter.
        if (panel is not DriverAdapterFileSystemPanel adapter ||
            adapter.Driver is not NdfsDriver ndfsDriver)
        {
            SetStatus("NDFS Disk Info: could not reach the live NDFS driver for this panel.");
            return;
        }

        var ndfs = ndfsDriver.GetFileSystem();
        if (ndfs == null)
        {
            SetStatus("NDFS Disk Info: filesystem is not mounted.");
            return;
        }

        try
        {
            // Hand the window everything it needs — the live filesystem, the metadata service, the
            // image path, and the mount's read-only state — and let it fetch its own data.
            var window = new NdfsDiskInfoWindow(
                ndfs,
                _metadataService,
                ResolveImagePath(panel.VolumeUrn),
                panel.IsReadOnly);

            await window.ShowDialog(this);

            // A rename changes the volume label / directory name — refresh the panel that owns it.
            if (ReferenceEquals(panel, _leftPanel))
                await RefreshLeftPanelAsync();
            else
                await RefreshRightPanelAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"NDFS Disk Info failed: {ex.Message}");
            _logger?.LogError("NDFS Disk Info failed", ex);
        }
    }

    private async void OnRightPanelPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_rightPanel != null)
            await TryBeginDragAsync(RightPanel, _rightPanel, e);
    }

    /// <summary>
    /// Manually starts an Avalonia drag gesture once the pointer has moved past a small threshold
    /// while its button is still held over a grid with an active selection. Avalonia's DataGrid
    /// has no built-in "drag a selected row" gesture, so this is the trigger that makes
    /// DragDrop.DoDragDrop fire; OnPanelDrop on the other panel completes the operation.
    /// </summary>
    private async Task TryBeginDragAsync(DataGrid grid, IFileSystemPanel panel, Avalonia.Input.PointerEventArgs e)
    {
        if (_dragInProgress) return;
        if (!e.GetCurrentPoint(grid).Properties.IsLeftButtonPressed) return;

        var current = e.GetPosition(grid);
        var dx = current.X - _dragStartPoint.X;
        var dy = current.Y - _dragStartPoint.Y;
        if (Math.Abs(dx) < DragStartThreshold && Math.Abs(dy) < DragStartThreshold) return;

        var selection = GetSelection(panel);

        // A plain click on a row that was already part of a multi-selection collapses the
        // DataGrid's live selection down to just that row before this handler ever runs. If the
        // clicked row was in the pre-click snapshot, restore the full prior selection so the drag
        // carries every selected file, not just the one under the pointer.
        if (selection.Count == 1 && _preClickSelection.Count > 1)
        {
            bool wasInPreClickSelection = false;
            for (int i = 0; i < _preClickSelection.Count; i++)
            {
                if (ReferenceEquals(_preClickSelection[i], selection[0]))
                {
                    wasInPreClickSelection = true;
                    break;
                }
            }
            if (wasInPreClickSelection)
                selection = _preClickSelection;
        }

        if (selection.Count == 0) return;

        _dragInProgress = true;
        try
        {
            var data = new Avalonia.Input.DataObject();
            data.Set(DragEntriesFormat, new DragPayload(panel, selection));
            await Avalonia.Input.DragDrop.DoDragDrop(
                e, data, Avalonia.Input.DragDropEffects.Copy | Avalonia.Input.DragDropEffects.Move);
        }
        finally
        {
            _dragInProgress = false;
        }
    }

    /// <summary>
    /// Effect shown while hovering during a drag: Move when Ctrl is held, Copy otherwise. Also
    /// blocks dropping a panel's own selection back onto itself. A completed move still confirms
    /// via MoveEntriesAsync regardless of gesture — this only decides which operation Drop performs.
    /// </summary>
    private void OnPanelDragOver(object? sender, Avalonia.Input.DragEventArgs e)
    {
        var payload = e.Data.Get(DragEntriesFormat) as DragPayload;
        var targetPanel = ReferenceEquals(sender, LeftPanel) ? _leftPanel : _rightPanel;

        if (payload == null || targetPanel == null || ReferenceEquals(payload.SourcePanel, targetPanel))
        {
            e.DragEffects = Avalonia.Input.DragDropEffects.None;
            return;
        }

        e.DragEffects = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0
            ? Avalonia.Input.DragDropEffects.Move
            : Avalonia.Input.DragDropEffects.Copy;
    }

    private async void OnPanelDrop(object? sender, Avalonia.Input.DragEventArgs e)
    {
        var payload = e.Data.Get(DragEntriesFormat) as DragPayload;
        var targetPanel = ReferenceEquals(sender, LeftPanel) ? _leftPanel : _rightPanel;

        if (payload == null || targetPanel == null || ReferenceEquals(payload.SourcePanel, targetPanel))
            return;

        if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0)
        {
            await MoveEntriesAsync(payload.SourcePanel, targetPanel, payload.Entries);
        }
        else
        {
            await CopyEntriesAsync(payload.SourcePanel, targetPanel, payload.Entries);
        }
    }

    /// <summary>
    /// Drag payload snapshotted at drag-start (see TryBeginDragAsync) — the source panel and the
    /// exact entries selected at that moment, so the drop handler doesn't need to re-read live
    /// panel selection (which could theoretically change during the drag).
    /// </summary>
    private sealed class DragPayload
    {
        public DragPayload(IFileSystemPanel sourcePanel, IReadOnlyList<IFileSystemEntry> entries)
        {
            SourcePanel = sourcePanel;
            Entries = entries;
        }

        public IFileSystemPanel SourcePanel { get; }
        public IReadOnlyList<IFileSystemEntry> Entries { get; }
    }

    private void SetActivePanel(bool leftActive)
    {
        _leftPanelActive = leftActive;
        UpdatePanelVisualState();
        UpdatePathBox();
        UpdateNdfsMenuVisibility();
    }

    /// <summary>True when the panel is showing a mounted NDFS volume.</summary>
    private static bool IsNdfsPanel(IFileSystemPanel? panel)
    {
        return panel != null &&
               string.Equals(panel.FileSystemType, "NDFS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Shows the "NDFS Disk Info..." commands only where they make sense: the File-menu entry
    /// follows the ACTIVE panel, while each panel's right-click entry follows that panel. On any
    /// non-NDFS volume they are hidden entirely rather than shown and failing when clicked.
    /// Called whenever the active panel changes or either panel is refreshed/re-mounted.
    /// </summary>
    private void UpdateNdfsMenuVisibility()
    {
        var activePanel = _leftPanelActive ? _leftPanel : _rightPanel;

        if (NdfsDiskInfoMenuItem != null)
            NdfsDiskInfoMenuItem.IsVisible = IsNdfsPanel(activePanel);

        if (LeftNdfsDiskInfoMenuItem != null)
            LeftNdfsDiskInfoMenuItem.IsVisible = IsNdfsPanel(_leftPanel);

        if (RightNdfsDiskInfoMenuItem != null)
            RightNdfsDiskInfoMenuItem.IsVisible = IsNdfsPanel(_rightPanel);
    }

    private void UpdatePanelVisualState()
    {
        if (_leftPanelActive)
        {
            LeftPanelBorder.Classes.Remove("panel-inactive");
            LeftPanelBorder.Classes.Add("panel-active");
            RightPanelBorder.Classes.Remove("panel-active");
            RightPanelBorder.Classes.Add("panel-inactive");
        }
        else
        {
            RightPanelBorder.Classes.Remove("panel-inactive");
            RightPanelBorder.Classes.Add("panel-active");
            LeftPanelBorder.Classes.Remove("panel-active");
            LeftPanelBorder.Classes.Add("panel-inactive");
        }
        UpdateBackButtonState();
    }

    private void UpdateBackButtonState()
    {
        var history = _leftPanelActive ? _leftHistory : _rightHistory;
        // Back button now lives inside the unified toolbar (QuickAccessBar control).
        QuickAccess.SetBackEnabled(history.CanGoBack());
    }

    private async void OnBackClick(object? sender, RoutedEventArgs e)
    {
        var history = _leftPanelActive ? _leftHistory : _rightHistory;
        var previousPath = history.GoBack();

        if (previousPath != null)
        {
            try
            {
                await NavigateToPathAsync(previousPath, _leftPanelActive);
            }
            catch (Exception ex)
            {
                SetStatus($"✗ Failed to navigate back: {ex.Message}");
                _logger?.LogError($"Failed to navigate back", ex);
                history.NavigationCompleted();
            }
        }
    }

    private async void OnLeftDoubleClick(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        e.Handled = true; // Prevent event from bubbling up

        if (_leftPanel == null || LeftPanel.SelectedItem is not FileEntryViewModel vm) return;

        if (vm.IsParentDirectory)
        {
            await _leftPanel.NavigateUpAsync();
            await RefreshLeftPanelAsync();
        }
        else if (vm.Entry != null && vm.Entry.IsDirectory)
        {
            await _leftPanel.NavigateIntoAsync(vm.Entry);
            await RefreshLeftPanelAsync();
        }
        else if (vm.Entry != null && !vm.Entry.IsDirectory)
        {
            // Check if it's a disk image - if so, mount it
            if (IsDiskImage(vm.Entry.Name))
            {
                await MountDiskImageAsync(vm.Entry, _leftPanelActive);
            }
        }
    }

    private async void OnRightDoubleClick(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        e.Handled = true; // Prevent event from bubbling up

        if (_rightPanel == null || RightPanel.SelectedItem is not FileEntryViewModel vm) return;

        if (vm.IsParentDirectory)
        {
            await _rightPanel.NavigateUpAsync();
            await RefreshRightPanelAsync();
        }
        else if (vm.Entry != null && vm.Entry.IsDirectory)
        {
            await _rightPanel.NavigateIntoAsync(vm.Entry);
            await RefreshRightPanelAsync();
        }
        else if (vm.Entry != null && !vm.Entry.IsDirectory)
        {
            // Check if it's a disk image - if so, mount it
            if (IsDiskImage(vm.Entry.Name))
            {
                await MountDiskImageAsync(vm.Entry, !_leftPanelActive);
            }
        }
    }

    // Midnight Commander style keyboard shortcuts
    private async void OnWindowKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Avalonia.Input.Key.F1:
                OnF1Help(null, null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F2:
                OnF2NewImage(null, null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F3:
                OnF3View(null, null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F5:
                await OnF5CopyAsync();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F6:
                await OnF6MoveAsync();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F8:
                await OnF8DeleteAsync();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F9:
                OnF9Mount(null, null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.F10:
                OnF10Quit(null, null);
                e.Handled = true;
                break;
            case Avalonia.Input.Key.Tab:
                // Switch active panel
                SetActivePanel(!_leftPanelActive);
                if (_leftPanelActive)
                    LeftPanel.Focus();
                else
                    RightPanel.Focus();
                e.Handled = true;
                break;
        }
    }

    // F-Key Handlers
    private void OnF1Help(object? sender, RoutedEventArgs? e)
    {
        var helpText = @"RetroCommander - Midnight Commander Style

F1  - Help (this screen)
F2  - Create new NDFS disk image
F3  - View file (Text/Hex modes, Bit 7 clearing for NDFS)
F5  - Copy files
F6  - Move files
F7  - Make directory
F8  - Delete files
F9  - Mount filesystem
F10 - Quit

Tab        - Switch panels
Enter      - Navigate into directory
Backspace  - Go up one level
Ctrl+C     - Copy
Ctrl+X     - Cut/Move
Delete     - Delete

File Viewer (F3):
- Text mode with word wrap
- Hex mode with ASCII sidebar
- Clear Bit 7 option for NDFS parity files";

        var helpWindow = new Window
        {
            Title = "RetroCommander Help",
            Width = 500,
            Height = 400,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = helpText,
                    Margin = new Avalonia.Thickness(20),
                    FontFamily = "Consolas",
                    FontSize = 12
                }
            }
        };
        helpWindow.Show();
    }

    /// <summary>
    /// F2 / File → New Image. ONE window for every format (NDFS, PC, Commodore): pick the format on
    /// the left, configure it on the right. This replaces the old flow, which popped a format-chooser
    /// window in FRONT of two separate dialogs and kept Commodore on a wholly separate menu item —
    /// three entry points for one task. Mounting is now a checkbox in the dialog rather than a
    /// follow-up yes/no prompt.
    /// </summary>
    private async void OnF2NewImage(object? sender, RoutedEventArgs? e)
    {
        var dialog = new NewImageDialog();
        bool created = await dialog.ShowDialog<bool>(this);

        if (!created || string.IsNullOrEmpty(dialog.CreatedImagePath))
            return;

        string path = dialog.CreatedImagePath!;
        SetStatus($"✓ Created image: {System.IO.Path.GetFileName(path)}");

        if (!dialog.ShouldMount)
            return;

        // Let auto-detection pick the driver rather than hard-coding a scheme per format.
        var urn = "file:///" + path.Replace("\\", "/");
        await MountNewlyCreatedImageAsync(urn, path);
    }

    /// <summary>
    /// Small format-picker shown before either creation dialog — lets "New Image" (F2) cover more
    /// than just NDFS without merging NDFS's and PC's very different option sets into one dialog.
    /// </summary>
    private async Task<string?> ShowNewImageFormatChoiceAsync()
    {
        var chooserWindow = new Window
        {
            Title = "New Disk Image",
            Width = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SizeToContent = SizeToContent.Height
        };

        var ndfsButton = new Button { Content = "NDFS Disk...", Padding = new Avalonia.Thickness(20, 10), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var pcButton = new Button { Content = "PC Disk (Floppy/HDD, FAT12/16)...", Padding = new Avalonia.Thickness(20, 10), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var cancelButton = new Button { Content = "Cancel", Padding = new Avalonia.Thickness(20, 8) };

        ndfsButton.Click += (s, ev) => chooserWindow.Close("NDFS");
        pcButton.Click += (s, ev) => chooserWindow.Close("PC");
        cancelButton.Click += (s, ev) => chooserWindow.Close(null);

        chooserWindow.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(15),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Select disk image format:", FontWeight = Avalonia.Media.FontWeight.Bold },
                ndfsButton,
                pcButton,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Margin = new Avalonia.Thickness(0, 10, 0, 0),
                    Children = { cancelButton }
                }
            }
        };

        return await chooserWindow.ShowDialog<string?>(this);
    }

    private async Task CreateAndOfferMountNdfsImageAsync()
    {
        var dialog = new CreateNdfsImageDialog();
        var result = await dialog.ShowDialog<string?>(this);
        if (result == null)
        {
            return;
        }

        SetStatus($"✓ Created NDFS image: {System.IO.Path.GetFileName(result)}");

        bool mountNow = await ShowYesNoDialogAsync("Mount New Image?", "NDFS image created successfully!\n\nWould you like to mount it now?");
        if (!mountNow)
        {
            return;
        }

        var urn = "ndfs:///" + result.Replace("\\", "/");
        await MountNewlyCreatedImageAsync(urn, result);
    }

    private async Task CreateAndOfferMountPcImageAsync()
    {
        var dialog = new CreatePcImageDialog();
        var result = await dialog.ShowDialog<string?>(this);
        if (result == null)
        {
            return;
        }

        SetStatus($"✓ Created PC disk image: {System.IO.Path.GetFileName(result)}");

        bool mountNow = await ShowYesNoDialogAsync("Mount New Image?", "PC disk image created successfully!\n\nWould you like to mount it now?");
        if (!mountNow)
        {
            return;
        }

        // Hard disk images have a real MBR partition (mount needs an explicit "#0" fragment,
        // same convention as mounting any other MBR-partitioned image); floppies have no
        // partition table and mount in raw mode with no fragment. Detect which one this is by
        // reading the actual MBR back rather than threading a media-type flag through the dialog.
        string fragment = DetectMbrPartitionFragment(result);
        var urn = "file:///" + result.Replace("\\", "/") + fragment;
        await MountNewlyCreatedImageAsync(urn, result);
    }

    private static string DetectMbrPartitionFragment(string path)
    {
        try
        {
            using var fileStream = System.IO.File.OpenRead(path);
            byte[] mbrBytes = new byte[512];
            int read = fileStream.Read(mbrBytes, 0, 512);
            if (read == 512)
            {
                var table = RetroIO.Storage.Local.MBR.Structures.MbrPartitionTable.Parse(mbrBytes);
                if (table.IsValid() && table.GetPartitionCount() > 0)
                {
                    return "#0";
                }
            }
        }
        catch
        {
            // Not a valid MBR (e.g. a plain floppy image) — mount in raw mode.
        }

        return string.Empty;
    }

    private async Task<bool> ShowYesNoDialogAsync(string title, string message)
    {
        var mountDialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var yesButton = new Button { Content = "Yes", Padding = new Avalonia.Thickness(20, 8), Margin = new Avalonia.Thickness(0, 0, 10, 0) };
        var noButton = new Button { Content = "No", Padding = new Avalonia.Thickness(20, 8) };

        yesButton.Click += (s, ev) => mountDialog.Close(true);
        noButton.Click += (s, ev) => mountDialog.Close(false);

        var buttonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { yesButton, noButton }
        };

        mountDialog.Content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(15),
            Children =
            {
                new TextBlock
                {
                    [Grid.RowProperty] = 0,
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 0, 0, 15)
                },
                buttonsPanel
            }
        };
        Grid.SetRow(buttonsPanel, 1);

        return await mountDialog.ShowDialog<bool>(this);
    }

    private async Task MountNewlyCreatedImageAsync(string urn, string createdFilePath)
    {
        try
        {
            // Mount through the MountManager (not a bare CreatePanelAsync) so the new image
            // is registered as a real mount and shows up in the sidebar Mounts list and the
            // toolbar dropdown — same as mounting an existing image.
            var mountResult = await _mountManager.MountAsync(urn);
            if (!mountResult.HasValue)
            {
                SetStatus($"✗ Failed to mount: {System.IO.Path.GetFileName(createdFilePath)}");
                return;
            }

            var mp = mountResult.Value.MountPoint;
            var panel = mountResult.Value.Panel;

            AddMountToUi(mp.DisplayName, mp.Urn, mp.FileSystemType);

            if (_leftPanelActive)
            {
                _leftPanel = panel;
                await RefreshLeftPanelAsync();
            }
            else
            {
                _rightPanel = panel;
                await RefreshRightPanelAsync();
            }

            SetStatus($"✓ Mounted new image: {System.IO.Path.GetFileName(createdFilePath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Failed to mount: {ex.Message}");
        }
    }

    private void OnF3View(object? sender, RoutedEventArgs? e)
    {
        _ = OnF3ViewAsync();
    }

    private async Task OnF3ViewAsync()
    {
        var panel = _leftPanelActive ? _leftPanel : _rightPanel;

        // NDFS: F3 on a TOP-LEVEL entry (which is a user, since at the NDFS root directories are users)
        // shows the user-info / password view instead of trying to view a "file".
        if (panel?.SelectedEntry != null && await TryShowNdfsUserInfoAsync(panel, panel.SelectedEntry))
            return;

        if (panel?.SelectedEntry == null || panel.SelectedEntry.IsDirectory)
        {
            SetStatus("Select a file to view");
            return;
        }

        var entry = panel.SelectedEntry;
        SetStatus($"Loading: {entry.Name}...");

        try
        {
            // Check file size limit (10 MB)
            const long maxSize = 10 * 1024 * 1024;
            if (entry.Size > maxSize)
            {
                SetStatus($"File too large to view ({entry.Size / (1024 * 1024):F1} MB). Maximum: 10 MB");
                return;
            }

            // Read file content
            byte[] data;
            using (var stream = await entry.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                data = ms.ToArray();
            }

            // Show file viewer dialog
            var dialog = new FileViewerDialog(entry.Name, data);
            await dialog.ShowDialog(this);

            SetStatus($"Viewed: {entry.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error viewing file: {ex.Message}");
            _logger.LogError($"Error viewing file {entry.Name}", ex);
        }
    }

    /// <summary>
    /// If the selected entry is a top-level NDFS user (a directory at the NDFS root), opens the NDFS
    /// user-info window (identity, quota, dates, friends, password in decimal + ND octal, and a
    /// password reset) and returns true. Returns false for anything else so normal F3 file-viewing
    /// proceeds.
    /// </summary>
    private async Task<bool> TryShowNdfsUserInfoAsync(IFileSystemPanel panel, IFileSystemEntry entry)
    {
        // Only a top-level NDFS directory is a user. Inside a user dir, CurrentPath != "/".
        if (!entry.IsDirectory)
            return false;
        if (!string.Equals(panel.FileSystemType, "NDFS", StringComparison.OrdinalIgnoreCase))
            return false;
        if (panel.CurrentPath != "/")
            return false;

        // Reach the live NDFS driver behind the panel adapter (same pattern as the disk-info window).
        if (panel is not DriverAdapterFileSystemPanel adapter || adapter.Driver is not NdfsDriver ndfsDriver)
            return false;

        var ndfs = ndfsDriver.GetFileSystem();
        if (ndfs == null)
            return false;

        var window = NdfsUserInfoWindow.ForUserName(ndfs, entry.Name, panel.IsReadOnly);
        if (window == null)
        {
            SetStatus($"NDFS: '{entry.Name}' is not a user.");
            return true;   // still an NDFS top-level directory — don't fall through to file viewing
        }

        try
        {
            await window.ShowDialog(this);
            SetStatus($"Viewed NDFS user '{entry.Name}'.");
        }
        catch (Exception ex)
        {
            SetStatus($"NDFS user info failed: {ex.Message}");
            _logger?.LogError($"NDFS user info failed for '{entry.Name}'", ex);
        }
        return true;
    }

    private void OnF4Edit(object? sender, RoutedEventArgs e)
    {
        _ = OnF4EditAsync();
    }

    private async Task OnF4EditAsync()
    {
        var panel = _leftPanelActive ? _leftPanel : _rightPanel;
        if (panel?.SelectedEntry == null || panel.SelectedEntry.IsDirectory)
        {
            SetStatus("Select a file to edit");
            return;
        }

        if (panel.IsReadOnly)
        {
            SetStatus("Cannot edit: filesystem is read-only");
            return;
        }

        var entry = panel.SelectedEntry;

        // Same limit as F3 View — this is a plain in-memory TextBox editor, not a streaming one.
        const long maxSize = 10 * 1024 * 1024;
        if (entry.Size > maxSize)
        {
            SetStatus($"File too large to edit ({entry.Size / (1024 * 1024):F1} MB). Maximum: 10 MB");
            return;
        }

        SetStatus($"Loading: {entry.Name}...");

        try
        {
            byte[] data;
            using (var stream = await entry.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                data = ms.ToArray();
            }

            // A text editor is unsafe for binary data (see FileViewerDialog.IsBinaryData) — the
            // Latin-1 round-trip preserves every byte, but a user editing what looks like garbage
            // text is far more likely to corrupt it by accident than to make an intended change.
            if (FileViewerDialog.IsBinaryData(data))
            {
                SetStatus($"'{entry.Name}' looks like binary data — use F3 View instead of editing it as text");
                return;
            }

            var dialog = new EditFileDialog(entry.Name, data);
            await dialog.ShowDialog(this);

            if (!dialog.Saved)
            {
                SetStatus("Edit cancelled");
                return;
            }

            using (var saveStream = new MemoryStream(dialog.EditedData))
            {
                await panel.CreateFileAsync(entry.Name, saveStream);
            }

            await (_leftPanelActive ? RefreshLeftPanelAsync() : RefreshRightPanelAsync());
            SetStatus($"Saved: {entry.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error editing file: {ex.Message}");
            _logger.LogError($"Error editing file {entry.Name}", ex);
        }
    }

    private void OnF5Copy(object? sender, RoutedEventArgs e)
    {
        _ = OnF5CopyAsync();
    }

    private async Task OnF5CopyAsync()
    {
        OnCopyClick(null, null);
        await Task.CompletedTask;
    }

    private void OnF6Move(object? sender, RoutedEventArgs e)
    {
        _ = OnF6MoveAsync();
    }

    private async Task OnF6MoveAsync()
    {
        OnMoveClick(null, null);
        await Task.CompletedTask;
    }

    private void OnF7Mkdir(object? sender, RoutedEventArgs e)
    {
        _ = OnF7MkdirAsync();
    }

    private async Task OnF7MkdirAsync()
    {
        if (_leftPanel == null || _rightPanel == null) return;

        var activePanel = _leftPanelActive ? _leftPanel : _rightPanel;

        if (activePanel.IsReadOnly)
        {
            SetStatus("Cannot create directory: filesystem is read-only");
            return;
        }

        // NDFS has no nested directories — a root-level "directory" IS a user. Word the prompt
        // accordingly so the user understands what they are creating on an NDFS volume.
        bool isNdfs = string.Equals(activePanel.FileSystemType, "NDFS", StringComparison.OrdinalIgnoreCase);
        string title = isNdfs ? "New User" : "New Directory";
        string message = isNdfs
            ? "Enter the name of the new NDFS user:"
            : "Enter the name of the new directory:";

        var name = await ShowInputDialogAsync(title, message, "");
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Create cancelled");
            return;
        }
        name = name.Trim();

        try
        {
            SetStatus($"Creating '{name}'...");
            await activePanel.CreateDirectoryAsync(name);
            await (_leftPanelActive ? RefreshLeftPanelAsync() : RefreshRightPanelAsync());
            SetStatus(isNdfs ? $"✓ Created user '{name}'" : $"✓ Created directory '{name}'");
        }
        catch (NotSupportedException)
        {
            SetStatus($"Cannot create directory: {activePanel.FileSystemType} does not support it");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Create failed: {ex.Message}");
            _logger?.LogError($"F7 Mkdir failed for '{name}'", ex);
        }
    }

    private void OnF8Delete(object? sender, RoutedEventArgs e)
    {
        _ = OnF8DeleteAsync();
    }

    private async Task OnF8DeleteAsync()
    {
        OnDeleteClick(null, null);
        await Task.CompletedTask;
    }

    private void OnF9Mount(object? sender, RoutedEventArgs? e)
    {
        OnMountClick(null, null);
    }

    private void OnF10Quit(object? sender, RoutedEventArgs? e)
    {
        Close();
    }

    // Command Line - DISABLED (CommandLine UI element removed)
    /* DISABLED - CommandLine removed from UI
    private async void OnCommandLineKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            var command = CommandLine.Text?.Trim();
            if (string.IsNullOrEmpty(command)) return;

            // Check if it's a path navigation
            if (System.IO.Directory.Exists(command) || command.StartsWith("file:///"))
            {
                try
                {
                    var urn = command.StartsWith("file:///") ? command : "file:///" + command.Replace("\\", "/");
                    var panel = await _factory.CreatePanelAsync(urn);

                    if (_leftPanelActive)
                    {
                        _leftPanel = panel;
                        await RefreshLeftPanelAsync();
                    }
                    else
                    {
                        _rightPanel = panel;
                        await RefreshRightPanelAsync();
                    }

                    SetStatus($"Navigated to: {command}");
                    CommandLine.Text = "";
                }
                catch (Exception ex)
                {
                    SetStatus($"Error: {ex.Message}");
                }
            }
            else
            {
                SetStatus($"Command not recognized: {command}");
                CommandLine.Text = "";
            }

            e.Handled = true;
        }
    }
    */ // END DISABLED CommandLine method

    // Helper: Check if file is a disk image
    private bool IsDiskImage(string filename)
    {
        var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
        return ext == ".d64" || ext == ".d71" || ext == ".d81" ||
               ext == ".img" || ext == ".ndfs" ||
               ext == ".t64" || ext == ".vdk" || ext == ".dmk";
    }

    // Helper: Detect URN scheme from file extension
    private string DetectUrnScheme(string filename)
    {
        var ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".d64" => "d64://",
            ".d71" => "d71://",
            ".d81" => "d81://",
            ".ndfs" => "ndfs://",
            ".img" => "file:///", // IMG uses file provider with auto-detection (can be BSD, NDFS, etc.)
            ".t64" => "file:///", // T64 uses file provider
            ".vdk" => "file:///", // VDK uses file provider with driver detection
            ".dmk" => "file:///", // DMK uses file provider with driver detection
            _ => "file:///"
        };
    }

    // Helper: Detect filesystem types and return confidence scores
    // Uses RetroFS library directly through VolumeProviderFactory
    private Dictionary<string, float> DetectFilesystemTypes(string imagePath)
    {
        // Check ALL registered filesystem types automatically (Commodore, containers, NDFS, BSD,
        // FAT, Dragon, ...) instead of a hard-coded subset — passing no types asks the factory to
        // score every registered provider.
        var confidences = _factory.GetDetectionConfidences(imagePath);

        // Map "V7" to "BSD" for compatibility with GenericMountDialog
        if (confidences.TryGetValue("V7", out float v7Confidence))
        {
            confidences["BSD"] = v7Confidence;
            confidences.Remove("V7");
        }

        _logger?.LogInfo($"Detection confidence for {Path.GetFileName(imagePath)} - NDFS: {confidences.GetValueOrDefault("NDFS", 0):F2}, BSD: {confidences.GetValueOrDefault("BSD", 0):F2}, FFS: {confidences.GetValueOrDefault("FFS", 0):F2}");

        return confidences;
    }

    // Helper: Mount disk image file
    private bool _isMounting = false; // Guard to prevent double-mounting

    private async Task MountDiskImageAsync(IFileSystemEntry entry, bool mountOnLeft)
    {
        // Prevent double-mounting
        if (_isMounting)
        {
            _logger?.LogWarning($"Mount already in progress, ignoring duplicate mount request for {entry.Name}");
            return;
        }

        _isMounting = true;

        try
        {
            SetStatus($"Mounting {entry.Name}...");

            // Get full path
            var sourcePanel = mountOnLeft ? _leftPanel : _rightPanel;
            if (sourcePanel == null)
            {
                _isMounting = false;
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(sourcePanel.CurrentPath, entry.Name));

            // Detect filesystem types
            var detectionConfidences = DetectFilesystemTypes(fullPath);

            // Determine if we should show generic mount dialog
            float maxConfidence = detectionConfidences.Values.DefaultIfEmpty(0).Max();
            string detectedType = detectionConfidences.Count > 0
                ? detectionConfidences.OrderByDescending(x => x.Value).First().Key
                : "";

            // ALWAYS show dialog for FFS/BSD to allow partition selection
            bool needsPartitionSelection = detectedType.StartsWith("FFS") || detectedType.StartsWith("BSD");
            bool showGenericDialog = maxConfidence < 0.8f || detectionConfidences.Count == 0 || needsPartitionSelection;

            string urn;

            if (showGenericDialog)
            {
                // Show generic mount dialog for uncertain detection
                var dialog = new GenericMountDialog(fullPath, detectionConfidences);
                var result = await dialog.ShowDialog<bool>(this);

                if (!result || string.IsNullOrEmpty(dialog.SelectedUrn))
                {
                    SetStatus("Mount cancelled");
                    _isMounting = false;
                    return;
                }

                urn = dialog.SelectedUrn;

                // Save "don't show again" preference if set
                if (dialog.DontShowAgain)
                {
                    var settings = RetroCommanderSettings.Load();
                    // Note: We could add a setting for generic dialog, but for now just continue
                }
            }
            else
            {
                // Auto-detect URN scheme based on file extension and detection
                var scheme = DetectUrnScheme(entry.Name);
                urn = scheme + fullPath.Replace("\\", "/");

                bool isNdfs = scheme == "ndfs://";

                // If using file:// scheme, use detection results
                if (!isNdfs && scheme == "file:///")
                {
                    if (detectionConfidences.TryGetValue("NDFS", out float ndfsConfidence) &&
                        detectionConfidences.TryGetValue("BSD", out float bsdConfidence))
                    {
                        // Only use NDFS if confidence is very high AND significantly higher than BSD
                        if (ndfsConfidence > 0.8f && ndfsConfidence > bsdConfidence + 0.2f)
                        {
                            isNdfs = true;
                            urn = "ndfs://" + fullPath.Replace("\\", "/");
                            _logger?.LogInfo($"Detected as NDFS with confidence {ndfsConfidence:F2} (BSD: {bsdConfidence:F2})");
                        }
                        else if (bsdConfidence > 0.5f)
                        {
                            urn = "bsd://" + fullPath.Replace("\\", "/");
                            _logger?.LogInfo($"Detected as BSD with confidence {bsdConfidence:F2} (NDFS: {ndfsConfidence:F2})");
                        }
                    }
                }

                // Show info dialogs if needed
                if (isNdfs)
                {
                    var settings = RetroCommanderSettings.Load();
                    if (!settings.SkipNdfsInfoDialog)
                    {
                        bool? result = await ShowNdfsInfoDialogAsync(fullPath);
                        if (result != true)
                        {
                            SetStatus("Mount cancelled");
                            _isMounting = false;
                            return;
                        }
                    }
                }
                // Note: BSD/FFS special-case handling removed - GenericMountDialog now shows detailed info for all filesystem types
            }

            _logger?.LogInfo($"Mounting disk image: {entry.Name} as {urn}");

            // Use MountManager to mount the volume
            var mountOptions = new MountOptions
            {
                DisplayName = entry.Name,
                AutoNavigateToPath = false
            };

            var mountResult = await _mountManager.MountAsync(urn, mountOptions);

            if (mountResult.HasValue)
            {
                var (mountPoint, panel) = mountResult.Value;

                // Mount on the active panel (not opposite)
                if (_leftPanelActive)
                {
                    _leftPanel = panel;
                    await RefreshLeftPanelAsync();
                    SetStatus($"✓ Mounted {entry.Name} on left panel");
                }
                else
                {
                    _rightPanel = panel;
                    await RefreshRightPanelAsync();
                    SetStatus($"✓ Mounted {entry.Name} on right panel");
                }

                // Add to sidebar Mounts section
                AddMountToUi(
                    mountPoint.DisplayName,
                    mountPoint.Urn,
                    mountPoint.FileSystemType);
            }
            else
            {
                // Mount failed - show generic dialog to allow retry with different settings
                SetStatus($"✗ Mount failed: Could not mount {entry.Name}");
                _logger?.LogError($"MountManager.MountAsync returned null for {urn}");

                // Show generic dialog to allow user to override filesystem type
                var retryDialog = new GenericMountDialog(fullPath, detectionConfidences);
                var retryResult = await retryDialog.ShowDialog<bool>(this);

                if (retryResult && !string.IsNullOrEmpty(retryDialog.SelectedUrn))
                {
                    // Retry with user-selected URN
                    var retryMountResult = await _mountManager.MountAsync(retryDialog.SelectedUrn, mountOptions);
                    if (retryMountResult.HasValue)
                    {
                        var (mountPoint, panel) = retryMountResult.Value;
                        if (_leftPanelActive)
                        {
                            _leftPanel = panel;
                            await RefreshLeftPanelAsync();
                            SetStatus($"✓ Mounted {entry.Name} on left panel");
                        }
                        else
                        {
                            _rightPanel = panel;
                            await RefreshRightPanelAsync();
                            SetStatus($"✓ Mounted {entry.Name} on right panel");
                        }
                        AddMountToUi(
                            mountPoint.DisplayName,
                            mountPoint.Urn,
                            mountPoint.FileSystemType);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Mount failed: {ex.Message}");
            _logger?.LogError($"Failed to mount {entry.Name}", ex);

            // On mount failure, show generic dialog to allow retry
            try
            {
                var sourcePanel = mountOnLeft ? _leftPanel : _rightPanel;
                if (sourcePanel != null)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(sourcePanel.CurrentPath, entry.Name));
                    var detectionConfidences = DetectFilesystemTypes(fullPath);

                    var retryDialog = new GenericMountDialog(fullPath, detectionConfidences);
                    var retryResult = await retryDialog.ShowDialog<bool>(this);

                    if (retryResult && !string.IsNullOrEmpty(retryDialog.SelectedUrn))
                    {
                        // Retry with user-selected URN
                        var mountOptions = new MountOptions
                        {
                            DisplayName = entry.Name,
                            AutoNavigateToPath = false
                        };

                        var retryMountResult = await _mountManager.MountAsync(retryDialog.SelectedUrn, mountOptions);
                        if (retryMountResult.HasValue)
                        {
                            var (mountPoint, panel) = retryMountResult.Value;
                            if (_leftPanelActive)
                            {
                                _leftPanel = panel;
                                await RefreshLeftPanelAsync();
                                SetStatus($"✓ Mounted {entry.Name} on left panel");
                            }
                            else
                            {
                                _rightPanel = panel;
                                await RefreshRightPanelAsync();
                                SetStatus($"✓ Mounted {entry.Name} on right panel");
                            }
                            AddMountToUi(
                                mountPoint.DisplayName,
                                mountPoint.Urn,
                                mountPoint.FileSystemType);
                        }
                    }
                }
            }
            catch (Exception retryEx)
            {
                _logger?.LogError($"Failed to show retry dialog: {retryEx.Message}", retryEx);
            }
        }
        finally
        {
            _isMounting = false;
        }
    }

    // Note: ShowBsdInfoDialogAsync removed - BSD/FFS details now shown in GenericMountDialog Details tab

    // Helper: Show NDFS info dialog
    private async Task<bool?> ShowNdfsInfoDialogAsync(string imagePath)
    {
        try
        {
            // Use metadata service to read NDFS info (NO business logic in UI!)
            var metadata = _metadataService.ReadNdfsMetadata(imagePath);
            if (metadata == null || metadata.HasErrors)
            {
                _logger?.LogError($"Failed to read NDFS metadata: {metadata?.ErrorMessage ?? "Unknown error"}");
                return true; // Continue with mount
            }

            // Show dialog
            var dialog = new NdfsInfoDialog(metadata);
            var result = await dialog.ShowDialog<bool>(this);

            // Save "don't show again" preference
            if (dialog.DontShowAgain)
            {
                var settings = RetroCommanderSettings.Load();
                settings.SkipNdfsInfoDialog = true;
                settings.Save();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to read NDFS metadata: {ex.Message}", ex);
            // If metadata read fails, it's likely not an NDFS file
            // Don't show dialog and continue with mount (will use auto-detection)
            return true;
        }
    }

    // Navigation Toolbar Handlers - DISABLED (UI elements removed, now in QuickAccessBar/Sidebar)

    /* DISABLED - DriveCombo removed from UI
    private async void OnDriveChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DriveCombo.SelectedItem == null) return;

        var driveText = DriveCombo.SelectedItem.ToString();
        if (string.IsNullOrEmpty(driveText)) return;

        // Extract drive letter (e.g., "C:\" from "C:\ (Windows)")
        var driveLetter = driveText.Substring(0, 3); // "C:\"

        await NavigateToPathAsync(driveLetter);
    }
    */ // END DISABLED OnDriveChanged

    // Shows a Properties details window for a sidebar item (path + mount info).
    private async void OnSidebarPropertiesRequested(object? sender, SidebarItemSelectedEventArgs e)
    {
        var item = e.Item;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Name:        {item.Name}");
        sb.AppendLine($"Type:        {item.ItemType}");
        sb.AppendLine($"Path:        {item.Path}");

        if (item.ItemType == SidebarItemType.Mount)
        {
            var mp = _mountManager.MountPoints.Values.FirstOrDefault(m => m.Urn == item.Path)
                     ?? _mountManager.MountPoints.Values.FirstOrDefault(m =>
                         string.Equals(m.DisplayName, item.Name, StringComparison.OrdinalIgnoreCase));
            if (mp != null)
            {
                sb.AppendLine($"URN:         {mp.Urn}");
                sb.AppendLine($"Filesystem:  {mp.FileSystemType}");
                sb.AppendLine($"Read-only:   {(mp.IsReadOnly ? "Yes" : "No")}");
            }
        }
        else if (!string.IsNullOrEmpty(item.FilesystemType))
        {
            sb.AppendLine($"Filesystem:  {item.FilesystemType}");
        }

        var dialog = new Window
        {
            Title = $"Properties — {item.Name}",
            Width = 480,
            CanResize = false,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (this.TryFindResource("FluentBackgroundPrimaryBrush", out var bg) && bg is Avalonia.Media.IBrush brush)
            dialog.Background = brush;

        var text = new TextBlock
        {
            Text = sb.ToString().TrimEnd(),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontFamily = "Consolas, monospace",
            Margin = new Avalonia.Thickness(16)
        };
        text.Classes.Add("fluent-text");

        var close = new Button { Content = "Close", IsCancel = true };
        close.Classes.Add("dialog-primary");
        close.Click += (_, _) => dialog.Close();
        var footer = new Border();
        footer.Classes.Add("dialog-footer");
        footer.Child = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { close }
        };
        Grid.SetRow(footer, 1);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(text);
        root.Children.Add(footer);
        dialog.Content = root;

        await dialog.ShowDialog(this);
    }

    // Opens the Settings/Preferences dialog (theme picker lives here).
    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog();
        await dialog.ShowDialog(this);
    }

    // Creates a blank Commodore disk image (D64/D71/D81) via CbmImageFactory.
    private async void OnNewCbmDisk(object? sender, RoutedEventArgs e)
    {
        // --- Small themed dialog: type + name + id ---
        var typeCombo = new ComboBox { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, SelectedIndex = 0 };
        typeCombo.Classes.Add("fluent-combobox");
        typeCombo.Items.Add(new ComboBoxItem { Content = "D64 — 1541 (170 KB)" });
        typeCombo.Items.Add(new ComboBoxItem { Content = "D71 — 1571 (340 KB)" });
        typeCombo.Items.Add(new ComboBoxItem { Content = "D81 — 1581 (800 KB)" });

        var nameBox = new TextBox { Text = "BLANK", MaxLength = 16, Watermark = "Disk name (max 16)" };
        nameBox.Classes.Add("fluent-textbox");
        var idBox = new TextBox { Text = "01", MaxLength = 2, Width = 70 };
        idBox.Classes.Add("fluent-textbox");

        var createBtn = new Button { Content = "Create..." };
        createBtn.Classes.Add("dialog-primary");
        var cancelBtn = new Button { Content = "Cancel", IsCancel = true };
        cancelBtn.Classes.Add("dialog-secondary");

        var dialog = new Window
        {
            Title = "New Commodore Disk",
            Width = 420,
            CanResize = false,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (this.TryFindResource("FluentBackgroundPrimaryBrush", out var bg) && bg is Avalonia.Media.IBrush brush)
            dialog.Background = brush;

        bool create = false;
        createBtn.Click += (_, _) => { create = true; dialog.Close(); };
        cancelBtn.Click += (_, _) => dialog.Close();

        TextBlock Label(string t)
        {
            var tb = new TextBlock { Text = t, Margin = new Avalonia.Thickness(0, 6, 0, 2) };
            tb.Classes.Add("fluent-text");
            return tb;
        }

        var idRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Children = { Label("Disk ID:"), idBox } };

        var body = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 4,
            Children = { Label("Disk type:"), typeCombo, Label("Disk name:"), nameBox, idRow }
        };

        var footer = new Border();
        footer.Classes.Add("dialog-footer");
        footer.Child = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 10,
            Children = { createBtn, cancelBtn }
        };
        Grid.SetRow(footer, 1);

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        root.Children.Add(body);
        root.Children.Add(footer);
        dialog.Content = root;

        await dialog.ShowDialog(this);
        if (!create) return;

        RetroIO.Storage.Local.CbmDiskType type = typeCombo.SelectedIndex switch
        {
            1 => RetroIO.Storage.Local.CbmDiskType.D71,
            2 => RetroIO.Storage.Local.CbmDiskType.D81,
            _ => RetroIO.Storage.Local.CbmDiskType.D64
        };

        string ext = RetroIO.Storage.Local.CbmImageFactory.ExtensionFor(type);
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Commodore Disk Image",
            DefaultExtension = ext.TrimStart('.'),
            SuggestedFileName = $"{(string.IsNullOrWhiteSpace(nameBox.Text) ? "blank" : nameBox.Text)}{ext}",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Commodore Disk Images") { Patterns = new[] { "*.d64", "*.d71", "*.d81" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });
        if (file == null) return;

        try
        {
            string path = file.Path.LocalPath;
            string name = string.IsNullOrWhiteSpace(nameBox.Text) ? "BLANK" : nameBox.Text!.Trim();
            string id = string.IsNullOrWhiteSpace(idBox.Text) ? "01" : idBox.Text!.Trim();
            RetroIO.Storage.Local.CbmImageFactory.CreateBlankFile(type, path, name, id);
            SetStatus($"✓ Created {type} image: {System.IO.Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Create Commodore disk failed: {ex.Message}", ex);
            SetStatus($"Error creating disk: {ex.Message}");
        }
    }

    // Fired when the user clicks a drive/folder/mount/recent entry in the Quick Access bar.
    // Drive and folder entries carry a real filesystem path; we navigate the active panel there.
    private async void OnQuickAccessItemSelected(object? sender, QuickAccessItemSelectedEventArgs e)
    {
        try
        {
            string path = e.Item.Path;
            if (string.IsNullOrEmpty(path)) return;

            SetStatus($"Navigating to {e.Item.DisplayName}...");
            await NavigateToPathAsync(path, _leftPanelActive);

            // Record the visit so it shows up in the Recents list/dropdowns.
            string fsType = string.IsNullOrEmpty(e.Item.FilesystemType) ? "Windows" : e.Item.FilesystemType;
            NavigationSidebar.ViewModel.AddRecentItem(path, fsType);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Quick access navigation failed: {ex.Message}");
            SetStatus($"Error: {ex.Message}");
        }
    }

    private async void OnQuickAccessClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag == null) return;

        var folder = button.Tag.ToString();
        string? path = folder switch
        {
            "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Downloads" => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            "Pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            _ => null
        };

        if (path != null)
        {
            await NavigateToPathAsync(path);
        }
    }

    /* DISABLED - PathBox removed from UI
    private async void OnPathBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            var path = PathBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                await NavigateToPathAsync(path);
            }
            e.Handled = true;
        }
    }

    private async void OnGoClick(object? sender, RoutedEventArgs e)
    {
        // DISABLED - PathBox removed from UI
        /* var path = PathBox.Text?.Trim();
        if (!string.IsNullOrEmpty(path))
        {
            await NavigateToPathAsync(path);
        }
    }
    */ // END DISABLED PathBox handlers

    private async Task NavigateToPathAsync(string path, bool? targetPanel = null)
    {
        try
        {
            if (string.IsNullOrEmpty(path))
            {
                SetStatus("✗ Invalid path");
                return;
            }

            SetStatus($"Navigating to {path}...");

            // Construct URN
            var urn = path.StartsWith("file:///") || path.Contains("://")
                ? path
                : "file:///" + path.Replace("\\", "/");

            // Create panel for this path
            var panel = await _factory.CreatePanelAsync(urn);

            // Navigate to target panel (use active panel if not specified)
            bool navigateToLeft = targetPanel ?? _leftPanelActive;
            if (navigateToLeft)
            {
                _leftPanel = panel;
                await RefreshLeftPanelAsync();
            }
            else
            {
                _rightPanel = panel;
                await RefreshRightPanelAsync();
            }

            UpdatePathBox();
            SetStatus($"✓ Navigated to {path}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Navigation failed: {ex.Message}");
            _logger?.LogError($"Failed to navigate to {path}", ex);
            throw; // Re-throw so caller can handle it
        }
    }

    // Load existing mounts from MountManager and populate sidebar
    // Registers a mount in BOTH mount surfaces: the sidebar "Mounts" section and the toolbar's
    // "Mounts" dropdown (QuickAccessBar). Keeping them in sync here means every mount path only
    // has to call one method.
    private void AddMountToUi(string name, string urn, string fileSystemType)
    {
        NavigationSidebar.ViewModel.AddMount(name, urn, fileSystemType);
        QuickAccess.ViewModel.AddMount(name, urn, fileSystemType);
    }

    // Removes a mount from both the sidebar and the toolbar dropdown.
    private void RemoveMountFromUi(string path)
    {
        NavigationSidebar.ViewModel.RemoveMount(path);
        QuickAccess.ViewModel.RemoveMount(path);
    }

    private void LoadExistingMounts()
    {
        try
        {
            foreach (var mountPoint in _mountManager.MountPoints.Values)
            {
                AddMountToUi(
                    mountPoint.DisplayName,
                    mountPoint.Urn,
                    mountPoint.FileSystemType);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load existing mounts: {ex.Message}", ex);
        }
    }

    // Open a Mount favorite: reuse the live mount if it is still mounted, otherwise re-mount the
    // disk image from the favorite's URN (which encodes the file path + scheme) and re-register it
    // in the Mounts surfaces. This is what makes favorites survive auto-unmount.
    private async Task OpenFavoriteMountAsync(SidebarItemViewModel favorite, bool toLeft)
    {
        try
        {
            string urn = favorite.Path;

            // 1) Already mounted? Just open its panel.
            MountPoint? live = _mountManager.MountPoints.Values.FirstOrDefault(mp =>
                string.Equals(mp.Urn, urn, StringComparison.OrdinalIgnoreCase));

            IFileSystemPanel? panel = live != null ? _panelManager.GetPanel(live.Id) : null;

            // 2) Not mounted (or panel gone) -> (re)mount from the URN.
            if (panel == null)
            {
                SetStatus($"Mounting {favorite.Name}...");
                var result = await _mountManager.MountAsync(urn, new MountOptions { AutoNavigateToPath = false });
                if (!result.HasValue)
                {
                    SetStatus($"✗ Failed to mount favorite: {favorite.Name}");
                    return;
                }

                panel = result.Value.Panel;
                MountPoint mp = result.Value.MountPoint;

                // Re-register in the sidebar Mounts list + toolbar dropdown if it isn't there yet.
                AddMountToUi(mp.DisplayName, mp.Urn, mp.FileSystemType);
            }

            if (toLeft)
            {
                _leftPanel = panel;
                await RefreshLeftPanelAsync();
            }
            else
            {
                _rightPanel = panel;
                await RefreshRightPanelAsync();
            }
            SetStatus($"✓ Opened favorite {favorite.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Failed to open favorite: {ex.Message}");
            _logger?.LogError($"OpenFavoriteMount failed for {favorite.Name}", ex);
        }
    }

    // New Fluent UI Event Handlers
    private async void OnSidebarItemSelected(object? sender, SidebarItemSelectedEventArgs e)
    {
        try
        {
            _logger?.LogInfo($"Sidebar item selected: {e.Item.Name} -> {e.Item.Path}");

            // Honour an explicit "Open in Left/Right Panel" choice; a normal click uses the active panel.
            bool toLeft = e.PanelExplicit ? e.OpenInLeftPanel : _leftPanelActive;

            // A Favorite is a shortcut. A Mount favorite re-mounts by URN if its disk is no longer
            // mounted (important once auto-unmount is enabled); any other kind just navigates.
            if (e.Item.ItemType == SidebarItemType.Favorite)
            {
                if (e.Item.OriginType == SidebarItemType.Mount)
                {
                    await OpenFavoriteMountAsync(e.Item, toLeft);
                }
                else
                {
                    await NavigateToPathAsync(e.Item.Path, toLeft);
                }
                return;
            }

            // If it's already a mount, just navigate to it (it's already mounted)
            if (e.Item.ItemType == SidebarItemType.Mount)
            {
                _logger?.LogInfo($"Mount item clicked: {e.Item.Name} -> {e.Item.Path}");

                // Try multiple strategies to find the mount point
                MountPoint? mountPoint = null;

                // Strategy 1: Exact URN match
                mountPoint = _mountManager.MountPoints.Values.FirstOrDefault(mp => mp.Urn == e.Item.Path);

                // Strategy 2: Try matching by normalized file path
                if (mountPoint == null)
                {
                    // Extract file path from sidebar item path
                    string sidebarPath = e.Item.Path;
                    if (sidebarPath.StartsWith("file:///"))
                    {
                        sidebarPath = sidebarPath.Substring(8).Replace("/", "\\");
                    }
                    else if (sidebarPath.Contains("://"))
                    {
                        // Extract path from URN schemes (ndfs://, bsd://, etc.)
                        sidebarPath = PathCleaner.ExtractFilePathFromUri(sidebarPath).Replace("/", "\\");
                    }

                    // Normalize to absolute path
                    if (Path.IsPathRooted(sidebarPath))
                    {
                        sidebarPath = Path.GetFullPath(sidebarPath);
                    }

                    // Try to find mount by matching file paths
                    mountPoint = _mountManager.MountPoints.Values.FirstOrDefault(mp =>
                    {
                        var mpPath = mp.Urn;
                        if (mpPath.StartsWith("file:///"))
                        {
                            mpPath = mpPath.Substring(8).Replace("/", "\\");
                        }
                        else if (mpPath.Contains("://"))
                        {
                            mpPath = PathCleaner.ExtractFilePathFromUri(mpPath).Replace("/", "\\");
                        }

                        // Normalize to absolute path
                        if (Path.IsPathRooted(mpPath))
                        {
                            mpPath = Path.GetFullPath(mpPath);
                        }

                        return string.Equals(mpPath, sidebarPath, StringComparison.OrdinalIgnoreCase);
                    });
                }

                // Strategy 3: Try matching by display name (fallback)
                if (mountPoint == null)
                {
                    mountPoint = _mountManager.MountPoints.Values.FirstOrDefault(mp =>
                        string.Equals(mp.DisplayName, e.Item.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (mountPoint != null)
                {
                    // Get the panel for this mount
                    var panel = _panelManager.GetPanel(mountPoint.Id);
                    if (panel == null)
                    {
                        // Panel not found - try to get it from MountManager (will recreate if needed)
                        _logger?.LogWarning($"Panel not found for mount {mountPoint.Id}, requesting from MountManager");
                        var mountResult = await _mountManager.MountAsync(mountPoint.Urn, new MountOptions { AutoNavigateToPath = false });
                        if (mountResult.HasValue)
                        {
                            panel = mountResult.Value.Panel;
                            mountPoint = mountResult.Value.MountPoint; // Update in case ID changed
                        }
                    }

                    if (panel != null)
                    {
                        // Navigate to the mounted panel (honouring an explicit panel choice)
                        if (toLeft)
                        {
                            _leftPanel = panel;
                            await RefreshLeftPanelAsync();
                            SetStatus($"✓ Navigated to {e.Item.Name}");
                        }
                        else
                        {
                            _rightPanel = panel;
                            await RefreshRightPanelAsync();
                            SetStatus($"✓ Navigated to {e.Item.Name}");
                        }

                        _logger?.LogInfo($"Successfully navigated to mounted volume: {mountPoint.Urn} (ID: {mountPoint.Id})");
                        return;
                    }
                    else
                    {
                        _logger?.LogError($"Failed to get panel for mount {mountPoint.Id} even after MountManager request");
                        SetStatus($"✗ Failed to access mount: {e.Item.Name}");
                        return; // Don't try to remount
                    }
                }
                else
                {
                    _logger?.LogWarning($"Mount point not found for {e.Item.Path}, but item is marked as Mount type");
                    SetStatus($"✗ Mount not found: {e.Item.Name}. It may have been unmounted.");
                    return; // Don't try to remount - the mount should already exist
                }
            }

            // Extract actual file path from URN if needed
            string actualPath = e.Item.Path;
            _logger?.LogInfo($"Extracting path from sidebar item: {actualPath}");

            if (actualPath.StartsWith("file:///"))
            {
                // Extract path: file:///D:/path/file.img -> D:/path/file.img
                actualPath = actualPath.Substring(8);
                // Normalize separators
                actualPath = actualPath.Replace("/", "\\");
                // Ensure it's an absolute path with drive letter
                if (!Path.IsPathRooted(actualPath) || string.IsNullOrEmpty(Path.GetPathRoot(actualPath)))
                {
                    // Try to resolve relative to current directory
                    actualPath = Path.GetFullPath(actualPath);
                }
                else
                {
                    // Normalize to absolute path even if already rooted
                    actualPath = Path.GetFullPath(actualPath);
                }
                _logger?.LogInfo($"Extracted from file:/// URN: {actualPath}");
            }
            else if (actualPath.Contains("://"))
            {
                // Extract path from other URN schemes (ndfs://, bsd://, etc.)
                // Use PathCleaner instead of Uri.LocalPath to avoid path conversion issues
                actualPath = PathCleaner.ExtractFilePathFromUri(actualPath);
                _logger?.LogInfo($"Extracted from URN using PathCleaner: {actualPath}");
                // Normalize separators
                actualPath = actualPath.Replace("/", "\\");
                // Ensure it's an absolute path
                if (!Path.IsPathRooted(actualPath))
                {
                    actualPath = Path.GetFullPath(actualPath);
                }
                else
                {
                    // Normalize to absolute path even if already rooted
                    actualPath = Path.GetFullPath(actualPath);
                }
                _logger?.LogInfo($"Normalized path: {actualPath}");
            }
            else
            {
                // Already a file path, ensure it's absolute
                actualPath = Path.GetFullPath(actualPath);
                _logger?.LogInfo($"Normalized direct path: {actualPath}");
            }

            // Final check - ensure path exists and is absolute
            if (!Path.IsPathRooted(actualPath))
            {
                _logger?.LogError($"Path is not absolute after extraction: {actualPath} (original: {e.Item.Path})");
                SetStatus($"✗ Invalid path: {actualPath}");
                return;
            }

            // Check if it's a directory (drive or user folder) - navigate directly
            if (Directory.Exists(actualPath))
            {
                _logger?.LogInfo($"Navigating to directory: {actualPath}");
                await NavigateToPathAsync(actualPath, toLeft);

                // Add to recents
                NavigationSidebar.ViewModel.AddRecentItem(actualPath, "Windows");

                _logger?.LogInfo($"Successfully navigated to directory: {actualPath}");
                return;
            }

            // Check if the path is a disk image file
            var fileName = Path.GetFileName(actualPath);
            if (File.Exists(actualPath) && IsDiskImage(fileName))
            {
                // Detect filesystem types
                var detectionConfidences = DetectFilesystemTypes(actualPath);

                // Determine if we should show generic mount dialog
                float maxConfidence = detectionConfidences.Values.DefaultIfEmpty(0).Max();
                string detectedType = detectionConfidences.Count > 0
                    ? detectionConfidences.OrderByDescending(x => x.Value).First().Key
                    : "";

                // ALWAYS show dialog for FFS/BSD to allow partition selection
                bool needsPartitionSelection = detectedType.StartsWith("FFS") || detectedType.StartsWith("BSD");
                bool showGenericDialog = maxConfidence < 0.8f || detectionConfidences.Count == 0 || needsPartitionSelection;

                string urn;

                if (showGenericDialog)
                {
                    // Show generic mount dialog for uncertain detection
                    var dialog = new GenericMountDialog(actualPath, detectionConfidences);
                    var result = await dialog.ShowDialog<bool>(this);

                    if (!result || string.IsNullOrEmpty(dialog.SelectedUrn))
                    {
                        SetStatus("Mount cancelled");
                        return;
                    }

                    urn = dialog.SelectedUrn;
                }
                else
                {
                    // Auto-detect URN scheme based on filename and detection
                    var scheme = DetectUrnScheme(fileName);
                    urn = scheme + actualPath.Replace("\\", "/");

                    bool isNdfs = scheme == "ndfs://";

                    // If using file:// scheme, use detection results
                    if (!isNdfs && scheme == "file:///")
                    {
                        if (detectionConfidences.TryGetValue("NDFS", out float ndfsConfidence) &&
                            detectionConfidences.TryGetValue("BSD", out float bsdConfidence))
                        {
                            // Only use NDFS if confidence is very high AND significantly higher than BSD
                            if (ndfsConfidence > 0.8f && ndfsConfidence > bsdConfidence + 0.2f)
                            {
                                isNdfs = true;
                                urn = "ndfs://" + actualPath.Replace("\\", "/");
                                _logger?.LogInfo($"Detected as NDFS with confidence {ndfsConfidence:F2} (BSD: {bsdConfidence:F2})");
                            }
                            else if (bsdConfidence > 0.5f)
                            {
                                urn = "bsd://" + actualPath.Replace("\\", "/");
                                _logger?.LogInfo($"Detected as BSD with confidence {bsdConfidence:F2} (NDFS: {ndfsConfidence:F2})");
                            }
                        }
                    }

                    // Show info dialogs if needed
                    if (isNdfs)
                    {
                        var settings = RetroCommanderSettings.Load();
                        if (!settings.SkipNdfsInfoDialog)
                        {
                            bool? result = await ShowNdfsInfoDialogAsync(actualPath);
                            if (result != true)
                            {
                                SetStatus("Mount cancelled");
                                return;
                            }
                        }
                    }
                    // Note: BSD/FFS special-case handling removed - GenericMountDialog now shows detailed info for all filesystem types
                }

                // Mount the disk image instead of navigating to it
                SetStatus($"Mounting {e.Item.Name}...");

                _logger?.LogInfo($"Detected disk image, mounting with URN: {urn}");

                // Use MountManager to mount
                var mountOptions = new MountOptions
                {
                    DisplayName = fileName,
                    AutoNavigateToPath = false
                };

                var mountResult = await _mountManager.MountAsync(urn, mountOptions);

                if (mountResult.HasValue)
                {
                    var (mountPoint, panel) = mountResult.Value;

                    _logger?.LogInfo($"Mount successful, mounting on {(_leftPanelActive ? "left" : "right")} panel");

                    // Mount on active panel
                    if (_leftPanelActive)
                    {
                        _leftPanel = panel;
                        await RefreshLeftPanelAsync();
                        SetStatus($"✓ Mounted {e.Item.Name} on left panel");
                    }
                    else
                    {
                        _rightPanel = panel;
                        await RefreshRightPanelAsync();
                        SetStatus($"✓ Mounted {e.Item.Name} on right panel");
                    }

                    // Add to sidebar Mounts section if not already there
                    AddMountToUi(
                        mountPoint.DisplayName,
                        mountPoint.Urn,
                        mountPoint.FileSystemType);

                    _logger?.LogInfo($"Successfully mounted {actualPath}");
                    return; // Important: return here to avoid navigation
                }
                else
                {
                    // Mount failed - show generic dialog to allow retry with different settings
                    SetStatus($"✗ Mount failed: Could not mount {e.Item.Name}");
                    _logger?.LogError($"MountManager.MountAsync returned null for {urn}");

                    // Show generic dialog to allow user to override filesystem type
                    var retryDialog = new GenericMountDialog(actualPath, detectionConfidences);
                    var retryResult = await retryDialog.ShowDialog<bool>(this);

                    if (retryResult && !string.IsNullOrEmpty(retryDialog.SelectedUrn))
                    {
                        // Retry with user-selected URN
                        var retryMountResult = await _mountManager.MountAsync(retryDialog.SelectedUrn, mountOptions);
                        if (retryMountResult.HasValue)
                        {
                            var (mountPoint, panel) = retryMountResult.Value;
                            if (_leftPanelActive)
                            {
                                _leftPanel = panel;
                                await RefreshLeftPanelAsync();
                                SetStatus($"✓ Mounted {e.Item.Name} on left panel");
                            }
                            else
                            {
                                _rightPanel = panel;
                                await RefreshRightPanelAsync();
                                SetStatus($"✓ Mounted {e.Item.Name} on right panel");
                            }
                            AddMountToUi(
                                mountPoint.DisplayName,
                                mountPoint.Urn,
                                mountPoint.FileSystemType);
                        }
                    }
                    return; // Don't try to navigate if mount failed
                }
            }

            // Regular navigation for directories/drives
            SetStatus($"Navigating to {e.Item.Name}...");
            await NavigateToPathAsync(e.Item.Path, toLeft);

            // Add to recents
            NavigationSidebar.ViewModel.AddRecentItem(e.Item.Path, e.Item.FilesystemType);

            _logger?.LogInfo($"Successfully navigated to {e.Item.Path}");
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Navigation failed: {ex.Message}");
            _logger?.LogError($"Error navigating to {e.Item.Path}: {ex.Message}", ex);
        }
    }

    private void OnSidebarMountRequested(object? sender, SidebarItemSelectedEventArgs e)
    {
        // NavigationSidebar only raises this for SidebarItemType.Drive (see CanMount in
        // NavigationSidebarViewModel). MountDialog has no path-seeding support, so this opens the
        // exact same "Mount New..." flow the toolbar's Mount button already uses, into whichever
        // panel is currently active — same behavior as OnMountClick.
        _logger?.LogInfo($"Mount requested from sidebar: {e.Item.Name}");
        OnMountClick(sender, null);
    }

    private async void OnSidebarUnmountRequested(object? sender, SidebarItemSelectedEventArgs e)
    {
        try
        {
            if (e.Item.ItemType != SidebarItemType.Mount)
                return;

            _logger?.LogInfo($"Unmount requested: {e.Item.Name} -> {e.Item.Path}");
            SetStatus($"Unmounting {e.Item.Name}...");

            // Find mount point by URN/path
            var mountPoint = _mountManager.MountPoints.Values.FirstOrDefault(mp => mp.Urn == e.Item.Path);

            if (mountPoint != null)
            {
                // Unmount via MountManager
                var success = _mountManager.Unmount(mountPoint.Id);

                if (success)
                {
                    // Remove from sidebar
                    RemoveMountFromUi(e.Item.Path);

                    // If this mount is currently displayed in a panel, clear it
                    if (_leftPanel?.Id == mountPoint.Id)
                    {
                        _leftPanel = null;
                        _leftEntries.Clear();
                    }
                    if (_rightPanel?.Id == mountPoint.Id)
                    {
                        _rightPanel = null;
                        _rightEntries.Clear();
                    }

                    SetStatus($"✓ Unmounted {e.Item.Name}");
                    _logger?.LogInfo($"Successfully unmounted {e.Item.Name}");
                }
                else
                {
                    SetStatus($"✗ Failed to unmount {e.Item.Name}");
                    _logger?.LogError($"Unmount failed for {e.Item.Name}");
                }
            }
            else
            {
                SetStatus($"✗ Mount point not found: {e.Item.Name}");
                _logger?.LogWarning($"Mount point not found in MountManager: {e.Item.Path}");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Unmount failed: {ex.Message}");
            _logger?.LogError($"Error unmounting {e.Item.Name}: {ex.Message}", ex);
        }
    }

    private async void OnLeftPanelRowClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Panel row clicks are handled by OnLeftDoubleClick - keep this for compatibility
    }

    private async void OnRightPanelRowClick(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // Panel row clicks are handled by OnRightDoubleClick - keep this for compatibility
    }

    private async void OnLeftPanelKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && _leftPanel != null && LeftPanel.SelectedItem is FileEntryViewModel vm)
        {
            if (vm.IsParentDirectory)
            {
                await _leftPanel.NavigateUpAsync();
                await RefreshLeftPanelAsync();
            }
            else if (vm.Entry != null && vm.Entry.IsDirectory)
            {
                await _leftPanel.NavigateIntoAsync(vm.Entry);
                await RefreshLeftPanelAsync();
            }
            else if (vm.Entry != null && IsDiskImage(vm.Entry.Name))
            {
                await MountDiskImageAsync(vm.Entry, true);
            }
        }
    }

    private async void OnRightPanelKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter && _rightPanel != null && RightPanel.SelectedItem is FileEntryViewModel vm)
        {
            if (vm.IsParentDirectory)
            {
                await _rightPanel.NavigateUpAsync();
                await RefreshRightPanelAsync();
            }
            else if (vm.Entry != null && vm.Entry.IsDirectory)
            {
                await _rightPanel.NavigateIntoAsync(vm.Entry);
                await RefreshRightPanelAsync();
            }
            else if (vm.Entry != null && IsDiskImage(vm.Entry.Name))
            {
                await MountDiskImageAsync(vm.Entry, false);
            }
        }
    }

    private async void OnLeftUpClick(object? sender, RoutedEventArgs e)
    {
        if (_leftPanel != null)
        {
            if (_leftPanel.CanNavigateUp())
            {
                await _leftPanel.NavigateUpAsync();
                await RefreshLeftPanelAsync();
            }
            else
            {
                SetStatus("Already at root directory");
            }
        }
    }

    private async void OnRightUpClick(object? sender, RoutedEventArgs e)
    {
        if (_rightPanel != null)
        {
            if (_rightPanel.CanNavigateUp())
            {
                await _rightPanel.NavigateUpAsync();
                await RefreshRightPanelAsync();
            }
            else
            {
                SetStatus("Already at root directory");
            }
        }
    }

    private async void OnLeftRefreshClick(object? sender, RoutedEventArgs e)
    {
        await RefreshLeftPanelAsync();
    }

    private async void OnRightRefreshClick(object? sender, RoutedEventArgs e)
    {
        await RefreshRightPanelAsync();
    }

    private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var yesButton = new Button
        {
            Content = "Yes",
            Padding = new Avalonia.Thickness(20, 8),
            Margin = new Avalonia.Thickness(0, 0, 10, 0)
        };
        var noButton = new Button
        {
            Content = "No",
            Padding = new Avalonia.Thickness(20, 8)
        };

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        yesButton.Click += (s, e) => dialog.Close(true);
        noButton.Click += (s, e) => dialog.Close(false);

        var buttonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { yesButton, noButton }
        };

        dialog.Content = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(15),
            Children =
            {
                new TextBlock
                {
                    [Grid.RowProperty] = 0,
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Avalonia.Thickness(0, 0, 0, 15)
                },
                buttonsPanel
            }
        };
        Grid.SetRow(buttonsPanel, 1);

        return await dialog.ShowDialog<bool>(this);
    }

    private async Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
    {
        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };

        var okButton = new Button
        {
            Content = "OK",
            Padding = new Avalonia.Thickness(20, 8),
            Margin = new Avalonia.Thickness(0, 0, 10, 0)
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(20, 8)
        };

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        string? result = null;
        okButton.Click += (s, e) =>
        {
            result = textBox.Text;
            dialog.Close();
        };
        cancelButton.Click += (s, e) => dialog.Close();

        var buttonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { okButton, cancelButton }
        };

        dialog.Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Avalonia.Thickness(15),
            Children =
            {
                new TextBlock
                {
                    [Grid.RowProperty] = 0,
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                textBox,
                buttonsPanel
            }
        };
        Grid.SetRow(textBox, 1);
        Grid.SetRow(buttonsPanel, 2);

        // Select all text when dialog opens
        dialog.Opened += (s, e) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        await dialog.ShowDialog(this);
        return result;
    }
}

public class FileEntryViewModel
{
    private readonly IFileSystemEntry? _entry;
    private readonly string? _parentDirName;

    public FileEntryViewModel(IFileSystemEntry? entry, string? parentDirName = null)
    {
        _entry = entry;
        _parentDirName = parentDirName;
    }

    public IFileSystemEntry? Entry => _entry;
    public bool IsParentDirectory => _parentDirName != null;

    public string Name
    {
        get
        {
            if (_parentDirName != null) return _parentDirName;
            if (_entry == null) return "";
            return _entry.IsDirectory ? $"[{_entry.Name}]" : _entry.Name;
        }
    }

    public string SizeFormatted
    {
        get
        {
            if (_parentDirName != null) return "<UP>";
            if (_entry == null) return "";
            return _entry.IsDirectory ? "<DIR>" : $"{_entry.Size:N0}";
        }
    }

    public string ModifiedFormatted
    {
        get
        {
            if (_parentDirName != null) return "";
            return _entry?.ModifiedDate?.ToString("yyyy-MM-dd HH:mm") ?? "";
        }
    }
}
