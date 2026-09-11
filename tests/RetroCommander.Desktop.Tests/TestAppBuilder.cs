//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Desktop.Tests -- Avalonia.Headless bootstrap, so [AvaloniaTest] methods can construct
// and drive real Window/Control instances without an actual display (CI/no-GUI safe).
//

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using RetroCommander.Desktop;

[assembly: AvaloniaTestApplication(typeof(RetroCommander.Desktop.Tests.TestAppBuilder))]

namespace RetroCommander.Desktop.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
