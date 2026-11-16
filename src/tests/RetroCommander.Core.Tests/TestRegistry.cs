//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// Shared by RetroCommander.Core.Tests and RetroCommander.Desktop.Tests (the Desktop test project
// compiles this same file in), so the throw-away registry handling exists once.

using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace RetroCommander.Tests.Shared
{
    /// <summary>
    /// Throw-away registry keys for file association tests.
    /// </summary>
    /// <remarks>
    /// Cleanup is done in try/finally inside the helper, NOT in NUnit [SetUp]/[TearDown]: under
    /// Avalonia's [AvaloniaTest] the TearDown did not run (measured 2026-11-16: every UI test left
    /// its key behind), so a helper that owns its own cleanup is the only reliable way.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal static class TestRegistry
    {
        /// <summary>
        /// Parent of every throw-away key, under HKEY_CURRENT_USER.
        /// </summary>
        public const string TestRootPath = @"Software\RetroCommanderTests";

        /// <summary>
        /// The real per-user classes key, under HKEY_CURRENT_USER.
        /// </summary>
        public const string UserClassesPath = @"Software\Classes";

        /// <summary>
        /// Test body that receives a registry key.
        /// </summary>
        /// <param name="classes">
        /// The key the test may write below
        /// </param>
        public delegate void RegistryTestBody(RegistryKey classes);

        /// <summary>
        /// Runs a test body against a fresh empty key that plays the part of the user's classes key,
        /// then deletes it (and the shared parent when it is empty), whatever the body does.
        /// </summary>
        /// <param name="body">
        /// The test
        /// </param>
        public static void WithThrowAwayClassesKey(RegistryTestBody body)
        {
            string keyPath = TestRootPath + "\\" + Guid.NewGuid().ToString("N");
            RegistryKey classes = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
            try
            {
                body(classes);
            }
            finally
            {
                classes.Dispose();
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                DeleteTestRootIfEmpty();
            }
        }

        /// <summary>
        /// Removes the shared parent key when no test is using it any more.
        /// </summary>
        public static void DeleteTestRootIfEmpty()
        {
            using (RegistryKey? parent = Registry.CurrentUser.OpenSubKey(TestRootPath))
            {
                if (parent == null || parent.SubKeyCount > 0 || parent.ValueCount > 0)
                {
                    return;
                }
            }

            try
            {
                Registry.CurrentUser.DeleteSubKey(TestRootPath, throwOnMissingSubKey: false);
            }
            catch (InvalidOperationException)
            {
                // The Core and Desktop test projects run at the same time: the other one created
                // its key under the parent between the check above and this delete. The parent is
                // in use, so keeping it is right; that test's own cleanup removes it.
            }
        }
    }
}
