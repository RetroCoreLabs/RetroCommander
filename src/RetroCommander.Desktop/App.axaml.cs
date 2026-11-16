//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RetroCommander.Desktop.Themes;

namespace RetroCommander.Desktop;

/// <summary>
/// The Avalonia application object for RetroCommander.
/// </summary>
/// <remarks>
/// It loads App.axaml, sets up the colour theme, and opens <see cref="MainWindow"/> when running as a desktop app.
/// </remarks>
public partial class App : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        // Register per-theme brush dictionaries and apply the persisted theme BEFORE any
        // window is created, so the very first frame is painted with the correct colours.
        ThemeManager.Instance.Initialize(this);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();

            // A file passed on the command line (for example a disk image double-clicked in
            // Explorer through a file association) is mounted in the left panel at startup.
            string[]? args = desktop.Args;
            if (args != null && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                window.StartupImagePath = args[0];
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
