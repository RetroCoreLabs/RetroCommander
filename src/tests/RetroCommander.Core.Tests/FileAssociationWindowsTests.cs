//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using NUnit.Framework;
using RetroCommander.Core.Services;
using RetroCommander.Tests.Shared;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Proves that what <see cref="FileAssociationService"/> writes WORKS IN WINDOWS: Windows' own
    /// file-type lookup (AssocQueryString, the same lookup Explorer uses to open a file) is asked
    /// which command opens the extension, and the command line it returns is split the way Windows
    /// splits a program's command line, to check RetroCommander would receive the file as its first
    /// argument.
    /// </summary>
    /// <remarks>
    /// These tests write to the REAL per-user classes key, because that is the only place Windows
    /// looks. To never disturb a real link they use an extension and a file type name that are
    /// unique to each test run (".rctest" plus random letters, "RetroCommander.Test." plus a guid),
    /// and delete both in a finally block.
    /// </remarks>
    [TestFixture]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public class FileAssociationWindowsTests
    {
        // AssocQueryString flags: ASSOCF_INIT_IGNOREUNKNOWN stops Windows answering an unknown
        // extension with its "How do you want to open this file?" chooser (OpenWith.exe "%1"),
        // so an unknown extension really reports as unknown. ASSOCSTR_COMMAND; S_OK means found.
        private const int AssocfInitIgnoreUnknown = 0x00000400;
        private const int AssocstrCommand = 1;
        private const int SOk = 0;

        // Test body that gets a service on the real classes key and the made-up extension to use.
        private delegate void UniqueExtensionTestBody(FileAssociationService service, string extension);

        [Test]
        public void LinkedExtension_WindowsFindsOurOpenCommand_UnlinkedItDoesNot()
        {
            WithUniqueExtensionInRealClasses((service, ext) =>
            {
                Assert.That(QueryOpenCommand(ext), Is.Null, "the made-up extension must be unknown before the test");

                service.Apply(new[] { ext }, ReadOnlySpan<string>.Empty);
                Assert.That(QueryOpenCommand(ext), Is.EqualTo(service.OpenCommand),
                    "Windows must resolve the extension to RetroCommander's open command");

                service.Apply(ReadOnlySpan<string>.Empty, new[] { ext });
                Assert.That(QueryOpenCommand(ext), Is.Null, "after unlinking, Windows must no longer know the extension");
            });
        }

        [Test]
        public void WindowsOpenCommand_GivesThePathWithSpacesAsFirstArgument()
        {
            WithUniqueExtensionInRealClasses((service, ext) =>
            {
                service.Apply(new[] { ext }, ReadOnlySpan<string>.Empty);
                string? command = QueryOpenCommand(ext);
                Assert.That(command, Is.Not.Null);

                // Explorer replaces %1 with the file's full path, then starts the program.
                string imagePath = @"C:\My Disks\Commodore games\Game Disk 1.d64";
                string[] argv = SplitLikeWindows(command!.Replace("%1", imagePath));

                Assert.That(argv.Length, Is.EqualTo(2), string.Join(" | ", argv));
                Assert.That(argv[0], Is.EqualTo(service.ExePath));
                // argv[1] is what the program sees as args[0]: App hands it to MainWindow.StartupImagePath.
                Assert.That(argv[1], Is.EqualTo(imagePath));
            });
        }

        // Runs the body with a service on the REAL user classes key, using a made-up extension and
        // file type name unique to this run, and removes both whatever happens.
        private static void WithUniqueExtensionInRealClasses(UniqueExtensionTestBody body)
        {
            string ext = ".rctest" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string progId = "RetroCommander.Test." + Guid.NewGuid().ToString("N");
            // A real exe, so nothing in Windows can refuse it as missing: this test runner itself.
            string exePath = Environment.ProcessPath!;
            Assert.That(File.Exists(exePath), Is.True);

            using RegistryKey classes = Registry.CurrentUser.CreateSubKey(TestRegistry.UserClassesPath, writable: true);
            try
            {
                body(new FileAssociationService(classes, exePath, notifyShell: false, progId), ext);
            }
            finally
            {
                classes.DeleteSubKeyTree(ext, throwOnMissingSubKey: false);
                classes.DeleteSubKeyTree(progId, throwOnMissingSubKey: false);
            }
        }

        // Asks Windows which command opens the extension; null when Windows does not know it.
        private static string? QueryOpenCommand(string extension)
        {
            uint size = 1024;
            StringBuilder buffer = new StringBuilder((int)size);
            int hr = AssocQueryStringW(AssocfInitIgnoreUnknown, AssocstrCommand, extension, "open", buffer, ref size);
            return hr == SOk ? buffer.ToString() : null;
        }

        // Splits a command line exactly as Windows does for a starting program.
        private static string[] SplitLikeWindows(string commandLine)
        {
            IntPtr argv = CommandLineToArgvW(commandLine, out int count);
            Assert.That(argv, Is.Not.EqualTo(IntPtr.Zero));
            try
            {
                string[] args = new string[count];
                for (int i = 0; i < count; i++)
                {
                    args[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!;
                }

                return args;
            }
            finally
            {
                LocalFree(argv);
            }
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int AssocQueryStringW(int flags, int str, string pszAssoc, string? pszExtra, StringBuilder pszOut, ref uint pcchOut);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}
