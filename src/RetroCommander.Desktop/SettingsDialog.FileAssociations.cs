//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Services;

namespace RetroCommander.Desktop
{
    // Settings > File associations: one tick box per disk image extension the drivers know.
    // A tick links the extension to this RetroCommander.exe for the current Windows user, an
    // untick removes RetroCommander's link. Every change is written at once, like the rest of
    // this dialog (an earlier Apply button lost ticks made without pressing it).
    public partial class SettingsDialog
    {
        // One tick box per known extension, in KnownDiskImageTypes.Extensions order; Tag holds
        // the extension (".d64"). Built once; only IsChecked changes afterwards.
        private readonly List<CheckBox> _associationChecks = new();

        // Null when file links cannot be written (not Windows, or no exe of our own to link to).
        private FileAssociationService? _associations;

        // True while tick boxes are set from code (showing the registry, Tick all/none), so the
        // change handler does not write the registry once per box.
        private bool _settingAssociationChecks;

        /// <summary>
        /// Gets or sets the log that file link changes and failures are written to. Optional.
        /// </summary>
        public IRetroCommanderLogger? Logger { get; set; }

        // Fill the section. Uses the given service (tests), or one for this user and this exe.
        private void PopulateFileAssociations(FileAssociationService? injected)
        {
            if (!OperatingSystem.IsWindows())
            {
                FileAssociationsSection.IsVisible = false;
                return;
            }

            if (injected == null)
            {
                if (!TryGetOwnExePath(out string exePath))
                {
                    FileAssociationExePath.Text = "RetroCommander was not started from its own .exe, so there is no program to link files to. Start RetroCommander.exe to use this.";
                    AssociationTickAllButton.IsEnabled = false;
                    AssociationTickNoneButton.IsEnabled = false;
                    return;
                }

                injected = FileAssociationService.ForCurrentUser(exePath);
            }

            _associations = injected;
            Closed += OnClosedDisposeAssociations;
            FileAssociationExePath.Text = "Files will open with: " + _associations.ExePath;
            BuildAssociationChecks();
            ShowRegistryState();
        }

        // Environment.ProcessPath is the running exe. When the app is started through
        // "dotnet RetroCommander.dll" it is dotnet.exe, which must never be linked.
        private static bool TryGetOwnExePath(out string exePath)
        {
            exePath = Environment.ProcessPath ?? string.Empty;
            return exePath.Length > 0 &&
                   !string.Equals(Path.GetFileNameWithoutExtension(exePath), "dotnet", StringComparison.OrdinalIgnoreCase);
        }

        // Creates the tick boxes, unticked and with the change handler attached.
        private void BuildAssociationChecks()
        {
            IReadOnlyList<string> extensions = KnownDiskImageTypes.Extensions;
            for (int i = 0; i < extensions.Count; i++)
            {
                CheckBox check = new CheckBox
                {
                    Content = extensions[i] + "  -  " + KnownDiskImageTypes.Describe(extensions[i]),
                    Tag = extensions[i]
                };
                check.IsCheckedChanged += OnAssociationCheckChanged;
                _associationChecks.Add(check);
                FileAssociationList.Children.Add(check);
            }
        }

        // Ticks exactly the extensions the registry links to RetroCommander, so the boxes always
        // show what Windows has, not what was clicked. Returns how many are linked.
        [SupportedOSPlatform("windows")]
        private int ShowRegistryState()
        {
            int linked = 0;
            int linkedToOtherCopy = 0;
            _settingAssociationChecks = true;
            try
            {
                for (int i = 0; i < _associationChecks.Count; i++)
                {
                    FileAssociationState state = _associations!.GetState(ExtensionOf(_associationChecks[i]));
                    _associationChecks[i].IsChecked = state != FileAssociationState.NotLinked;
                    if (state != FileAssociationState.NotLinked)
                    {
                        linked++;
                    }

                    if (state == FileAssociationState.LinkedToOtherCopy)
                    {
                        linkedToOtherCopy++;
                    }
                }
            }
            finally
            {
                _settingAssociationChecks = false;
            }

            if (linkedToOtherCopy > 0)
            {
                FileAssociationStatus.Text = $"These types open another copy of RetroCommander ({_associations!.ReadRegisteredCommand()}). Change any tick to point them all at this one.";
            }

            return linked;
        }

        // A tick box changed: write the links at once.
        private void OnAssociationCheckChanged(object? sender, RoutedEventArgs e)
        {
            if (!_settingAssociationChecks)
            {
                ApplyAssociations();
            }
        }

        private void OnAssociationTickAllClick(object? sender, RoutedEventArgs e)
        {
            SetAllAssociationChecks(true);
        }

        private void OnAssociationTickNoneClick(object? sender, RoutedEventArgs e)
        {
            SetAllAssociationChecks(false);
        }

        // Sets every box, then writes the registry once.
        private void SetAllAssociationChecks(bool value)
        {
            _settingAssociationChecks = true;
            try
            {
                for (int i = 0; i < _associationChecks.Count; i++)
                {
                    _associationChecks[i].IsChecked = value;
                }
            }
            finally
            {
                _settingAssociationChecks = false;
            }

            ApplyAssociations();
        }

        // Link every ticked extension to this exe, remove RetroCommander's link from every
        // unticked one, then show what the registry now holds.
        private void ApplyAssociations()
        {
            if (_associations == null || !OperatingSystem.IsWindows())
            {
                return;
            }

            List<string> toLink = new();
            List<string> toUnlink = new();
            for (int i = 0; i < _associationChecks.Count; i++)
            {
                List<string> target = _associationChecks[i].IsChecked == true ? toLink : toUnlink;
                target.Add(ExtensionOf(_associationChecks[i]));
            }

            try
            {
                _associations.Apply(CollectionsMarshal.AsSpan(toLink), CollectionsMarshal.AsSpan(toUnlink));
                int linked = ShowRegistryState();

                Logger?.LogInfo($"File associations: {linked} type(s) open with {_associations.ExePath}: {string.Join(" ", toLink)}");
                if (linked != toLink.Count)
                {
                    Logger?.LogWarning($"File associations: {toLink.Count} ticked but {linked} linked in the registry");
                    FileAssociationStatus.Text = $"Only {linked} of the {toLink.Count} ticked type(s) are linked. See the log.";
                }
                else
                {
                    FileAssociationStatus.Text = linked == 0
                        ? "No disk image types open in RetroCommander now."
                        : $"{linked} type(s) now open in RetroCommander. If Windows asks which program to use the first time, pick RetroCommander.";
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError("Changing the file associations failed", ex);
                FileAssociationStatus.Text = "Could not change the file links: " + ex.Message;
                ShowRegistryState();
            }
        }

        // Closes the registry key the service opened. Only hooked up on Windows.
        [SupportedOSPlatform("windows")]
        private void OnClosedDisposeAssociations(object? sender, EventArgs e)
        {
            _associations?.Dispose();
            _associations = null;
        }

        private static string ExtensionOf(CheckBox check)
        {
            return (string)check.Tag!;
        }
    }
}
