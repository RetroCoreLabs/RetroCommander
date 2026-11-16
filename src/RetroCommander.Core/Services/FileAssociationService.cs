//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace RetroCommander.Core.Services
{
    /// <summary>
    /// What Windows currently does with one file extension, as far as RetroCommander is concerned.
    /// </summary>
    public enum FileAssociationState
    {
        /// <summary>The extension is not linked to RetroCommander.</summary>
        NotLinked,

        /// <summary>The extension is linked to RetroCommander at the exe path given.</summary>
        Linked,

        /// <summary>
        /// The extension is linked to RetroCommander, but to an exe at a different path (the
        /// program was moved or a different copy was linked). Opening a file would start that copy.
        /// </summary>
        LinkedToOtherCopy
    }

    /// <summary>
    /// Links disk image extensions (".d64", ".img", ...) to RetroCommander in Windows, for the
    /// current user only, so a double-click in Explorer starts RetroCommander with the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything is written under the user's own classes key (HKEY_CURRENT_USER\Software\Classes),
    /// which needs no administrator rights. The layout is the one Windows documents for file types:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>RetroCommander.DiskImage</c> - one file type ("ProgID") whose <c>shell\open\command</c> is
    /// <c>"&lt;exe&gt;" "%1"</c> and whose icon is the exe's icon.
    /// </description></item>
    /// <item><description>
    /// <c>.ext</c> - its default value is set to the file type, and the file type is added under
    /// <c>.ext\OpenWithProgids</c> so it also appears in Explorer's "Open with" list.
    /// </description></item>
    /// </list>
    /// <para>
    /// Windows 10 and 11 do NOT let a program make itself the default by writing the registry: if
    /// the user has already picked a program for an extension (the protected "UserChoice"), that
    /// choice wins, and on first use Windows may ask which program to use. This class reports what
    /// it wrote; it does not claim Windows will use it.
    /// </para>
    /// <para>
    /// Unlinking only removes what this class wrote: the extension's default value is cleared only
    /// when it still names RetroCommander's file type, so another program's link is never touched.
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class FileAssociationService
    {
        /// <summary>
        /// The file type name ("ProgID") RetroCommander registers its extensions under.
        /// </summary>
        public const string ProgId = "RetroCommander.DiskImage";

        private const string OpenWithProgIdsKey = "OpenWithProgids";
        private const string OpenCommandKey = @"shell\open\command";

        private readonly RegistryKey _classesRoot;
        private readonly string _exePath;
        private readonly bool _notifyShell;

        /// <summary>
        /// Creates a service that writes to the current user's classes key and tells Explorer
        /// about each change.
        /// </summary>
        /// <param name="exePath">
        /// Full path of the RetroCommander exe that opened files should start
        /// </param>
        /// <returns>
        /// A service for the real Windows registry
        /// </returns>
        public static FileAssociationService ForCurrentUser(string exePath)
        {
            RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
            return new FileAssociationService(classes, exePath, notifyShell: true);
        }

        /// <summary>
        /// Creates a service that writes below the given key instead of the real classes key.
        /// </summary>
        /// <param name="classesRoot">
        /// Writable key that plays the part of HKEY_CURRENT_USER\Software\Classes (tests pass a
        /// throw-away key here)
        /// </param>
        /// <param name="exePath">
        /// Full path of the RetroCommander exe that opened files should start
        /// </param>
        /// <param name="notifyShell">
        /// True to tell Explorer that file links changed after each write
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="classesRoot"/> is null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="exePath"/> is empty
        /// </exception>
        public FileAssociationService(RegistryKey classesRoot, string exePath, bool notifyShell)
        {
            _classesRoot = classesRoot ?? throw new ArgumentNullException(nameof(classesRoot));
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new ArgumentException("The exe path cannot be empty.", nameof(exePath));
            }

            _exePath = exePath;
            _notifyShell = notifyShell;
        }

        /// <summary>
        /// Gets the exe path that linked files will start.
        /// </summary>
        public string ExePath => _exePath;

        /// <summary>
        /// Gets the exact command line Windows is given for opening a file, for example
        /// <c>"C:\Apps\RetroCommander.exe" "%1"</c>.
        /// </summary>
        public string OpenCommand => "\"" + _exePath + "\" \"%1\"";

        /// <summary>
        /// Reports whether an extension is linked to RetroCommander, and to which copy.
        /// </summary>
        /// <param name="extension">
        /// Extension with the leading dot, for example ".d64"
        /// </param>
        /// <returns>
        /// The extension's state
        /// </returns>
        public FileAssociationState GetState(string extension)
        {
            string ext = NormaliseExtension(extension);

            using RegistryKey? extKey = _classesRoot.OpenSubKey(ext);
            if (extKey == null)
            {
                return FileAssociationState.NotLinked;
            }

            bool isDefault = string.Equals(extKey.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase);
            bool inOpenWith;
            using (RegistryKey? openWith = extKey.OpenSubKey(OpenWithProgIdsKey))
            {
                inOpenWith = openWith != null && openWith.GetValue(ProgId) != null;
            }

            if (!isDefault && !inOpenWith)
            {
                return FileAssociationState.NotLinked;
            }

            string? command = ReadRegisteredCommand();
            if (command == null)
            {
                // The extension names our file type but the file type itself is gone.
                return FileAssociationState.NotLinked;
            }

            return string.Equals(command, OpenCommand, StringComparison.OrdinalIgnoreCase)
                ? FileAssociationState.Linked
                : FileAssociationState.LinkedToOtherCopy;
        }

        /// <summary>
        /// Gets the command line currently stored for RetroCommander's file type, or null when the
        /// file type is not registered. Shows which exe a "linked to another copy" link points at.
        /// </summary>
        /// <returns>
        /// The stored command line, or null
        /// </returns>
        public string? ReadRegisteredCommand()
        {
            using RegistryKey? commandKey = _classesRoot.OpenSubKey(ProgId + "\\" + OpenCommandKey);
            return commandKey?.GetValue(null) as string;
        }

        /// <summary>
        /// Links the given extensions to this RetroCommander exe. Re-linking an extension that is
        /// already linked is harmless and updates the exe path.
        /// </summary>
        /// <param name="extensions">
        /// Extensions with the leading dot, for example ".d64"
        /// </param>
        public void Link(ReadOnlySpan<string> extensions)
        {
            WriteProgId();

            for (int i = 0; i < extensions.Length; i++)
            {
                string ext = NormaliseExtension(extensions[i]);
                using RegistryKey extKey = _classesRoot.CreateSubKey(ext, writable: true);
                extKey.SetValue(null, ProgId, RegistryValueKind.String);

                using RegistryKey openWith = extKey.CreateSubKey(OpenWithProgIdsKey, writable: true);
                // An empty value of type "none" is how Windows lists a ProgID under OpenWithProgids.
                openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            NotifyShell();
        }

        /// <summary>
        /// Removes RetroCommander's link from the given extensions. Links that name another
        /// program are left as they are. When no extension is linked any more, RetroCommander's
        /// file type is removed too.
        /// </summary>
        /// <param name="extensions">
        /// Extensions with the leading dot, for example ".d64"
        /// </param>
        /// <param name="allKnownExtensions">
        /// Every extension RetroCommander can link; used to decide whether the file type is still
        /// in use by any of them
        /// </param>
        public void Unlink(ReadOnlySpan<string> extensions, ReadOnlySpan<string> allKnownExtensions)
        {
            for (int i = 0; i < extensions.Length; i++)
            {
                string ext = NormaliseExtension(extensions[i]);
                using RegistryKey? extKey = _classesRoot.OpenSubKey(ext, writable: true);
                if (extKey == null)
                {
                    continue;
                }

                if (string.Equals(extKey.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase))
                {
                    extKey.DeleteValue(string.Empty, throwOnMissingValue: false);
                }

                using (RegistryKey? openWith = extKey.OpenSubKey(OpenWithProgIdsKey, writable: true))
                {
                    if (openWith != null)
                    {
                        openWith.DeleteValue(ProgId, throwOnMissingValue: false);
                    }
                }

                DeleteIfEmpty(extKey, OpenWithProgIdsKey);
            }

            for (int i = 0; i < extensions.Length; i++)
            {
                DeleteIfEmpty(_classesRoot, NormaliseExtension(extensions[i]));
            }

            bool stillUsed = false;
            for (int i = 0; i < allKnownExtensions.Length && !stillUsed; i++)
            {
                stillUsed = GetState(allKnownExtensions[i]) != FileAssociationState.NotLinked;
            }

            if (!stillUsed)
            {
                _classesRoot.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
            }

            NotifyShell();
        }

        // Writes the RetroCommander file type: its name, its icon and the command that opens a file.
        private void WriteProgId()
        {
            using RegistryKey progIdKey = _classesRoot.CreateSubKey(ProgId, writable: true);
            progIdKey.SetValue(null, "Retro disk image", RegistryValueKind.String);

            using (RegistryKey iconKey = progIdKey.CreateSubKey("DefaultIcon", writable: true))
            {
                iconKey.SetValue(null, "\"" + _exePath + "\",0", RegistryValueKind.String);
            }

            using RegistryKey commandKey = progIdKey.CreateSubKey(OpenCommandKey, writable: true);
            commandKey.SetValue(null, OpenCommand, RegistryValueKind.String);
        }

        // Deletes a subkey when it has no values and no subkeys left (so unlinking leaves no
        // empty keys behind). A key holding anything another program wrote is kept.
        private static void DeleteIfEmpty(RegistryKey parent, string name)
        {
            using (RegistryKey? key = parent.OpenSubKey(name))
            {
                if (key == null || key.ValueCount > 0 || key.SubKeyCount > 0)
                {
                    return;
                }
            }

            parent.DeleteSubKey(name, throwOnMissingSubKey: false);
        }

        // Lower case with exactly one leading dot, so ".D64", "d64" and ".d64" are the same key.
        private static string NormaliseExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                throw new ArgumentException("An extension cannot be empty.", nameof(extension));
            }

            string trimmed = extension.Trim().TrimStart('.').ToLowerInvariant();
            if (trimmed.Length == 0 || trimmed.IndexOfAny(new[] { '\\', '/', ' ' }) >= 0)
            {
                throw new ArgumentException($"'{extension}' is not a file extension.", nameof(extension));
            }

            return "." + trimmed;
        }

        // Tells Explorer that file links changed, so icons and "Open with" refresh without a
        // restart. SHCNE_ASSOCCHANGED with SHCNF_IDLIST and no items is the documented call.
        private void NotifyShell()
        {
            if (_notifyShell)
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
