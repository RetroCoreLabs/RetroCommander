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
    /// Links disk image extensions (".d64", ".img", ...) to RetroCommander in Windows, for the
    /// current user only, so a double-click in Explorer starts RetroCommander with the file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything is written under the user's own classes key (HKEY_CURRENT_USER\Software\Classes),
    /// which needs no administrator rights. The layout is the one Windows documents for file types:
    ///  - <c>RetroCommander.DiskImage</c> is one file type ("ProgID") with the exe's icon.
    ///  - Its <c>shell\open\command</c> is the quoted exe path followed by <c>"%1"</c>.
    ///  - Each <c>.ext</c> key's default value names that file type.
    ///  - Each <c>.ext\OpenWithProgids</c> lists it too, so it shows in Explorer's "Open with" list.
    /// </para>
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
    public sealed class FileAssociationService : IDisposable
    {
        private readonly RegistryKey _classesRoot;
        private readonly bool _ownsClassesRoot;
        private readonly string _exePath;
        private readonly string _progId;
        private readonly bool _notifyShell;

        /// <summary>
        /// The file type name ("ProgID") RetroCommander registers its extensions under.
        /// </summary>
        public const string DefaultProgId = "RetroCommander.DiskImage";

        private const string FileTypeName = "Retro disk image";
        private const string OpenWithProgIdsKey = "OpenWithProgids";
        private const string OpenCommandKey = @"shell\open\command";
        private const string DefaultIconKey = "DefaultIcon";

        // SHChangeNotify: "the file type links changed", no items (SHCNF_IDLIST with null items).
        private const int ShcneAssocChanged = 0x08000000;
        private const uint ShcnfIdList = 0x0000;

        // Characters that would make an "extension" name another key or a multi-part extension.
        private static readonly char[] s_notAllowedInExtension = { '\\', '/', ' ', '.' };

        /// <summary>
        /// Creates a service that writes below the given key instead of the real classes key.
        /// </summary>
        /// <param name="classesRoot">
        /// Writable key that plays the part of HKEY_CURRENT_USER\Software\Classes; the caller keeps
        /// ownership (tests pass a throw-away key here)
        /// </param>
        /// <param name="exePath">
        /// Full path of the RetroCommander exe that opened files should start
        /// </param>
        /// <param name="notifyShell">
        /// True to tell Explorer that file links changed after each change
        /// </param>
        /// <param name="progId">
        /// File type name to register under; <see cref="DefaultProgId"/> unless a test needs its
        /// own so it cannot disturb the real one
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="classesRoot"/> is null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="exePath"/> or <paramref name="progId"/> is empty
        /// </exception>
        public FileAssociationService(RegistryKey classesRoot, string exePath, bool notifyShell, string progId = DefaultProgId)
            : this(classesRoot, ownsClassesRoot: false, exePath, notifyShell, progId)
        {
        }

        private FileAssociationService(RegistryKey classesRoot, bool ownsClassesRoot, string exePath, bool notifyShell, string progId)
        {
            ArgumentNullException.ThrowIfNull(classesRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(progId);

            _classesRoot = classesRoot;
            _ownsClassesRoot = ownsClassesRoot;
            _exePath = exePath;
            _notifyShell = notifyShell;
            _progId = progId;
        }

        /// <summary>
        /// Gets the exe path that linked files will start.
        /// </summary>
        public string ExePath => _exePath;

        /// <summary>
        /// Gets the file type name ("ProgID") this service registers under.
        /// </summary>
        public string ProgId => _progId;

        /// <summary>
        /// Gets the exact command line Windows is given for opening a file, for example
        /// <c>"C:\Apps\RetroCommander.exe" "%1"</c>.
        /// </summary>
        public string OpenCommand => "\"" + _exePath + "\" \"%1\"";

        /// <summary>
        /// Creates a service for the real Windows registry: it writes to the current user's classes
        /// key and tells Explorer about each change. Dispose it when done.
        /// </summary>
        /// <param name="exePath">
        /// Full path of the RetroCommander exe that opened files should start
        /// </param>
        /// <returns>
        /// A service for the current user's file links
        /// </returns>
        public static FileAssociationService ForCurrentUser(string exePath)
        {
            RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
            return new FileAssociationService(classes, ownsClassesRoot: true, exePath, notifyShell: true, DefaultProgId);
        }

        /// <summary>
        /// Reports whether an extension is linked to RetroCommander, and to which copy.
        /// </summary>
        /// <param name="extension">
        /// Extension with or without the leading dot, any case, for example ".d64"
        /// </param>
        /// <returns>
        /// The extension's state
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="extension"/> is not a file extension
        /// </exception>
        public FileAssociationState GetState(string extension)
        {
            if (!NamesOurFileType(NormaliseExtension(extension)))
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
            using RegistryKey? commandKey = _classesRoot.OpenSubKey(_progId + "\\" + OpenCommandKey);
            return commandKey?.GetValue(null) as string;
        }

        /// <summary>
        /// Makes the registry match a set of choices: every extension in
        /// <paramref name="toLink"/> opens with this exe, and RetroCommander's link is removed from
        /// every extension in <paramref name="toUnlink"/>. When nothing is linked, RetroCommander's
        /// file type is removed too. Explorer is told once at the end.
        /// </summary>
        /// <param name="toLink">
        /// Extensions to link (re-linking one that is linked is harmless and updates the exe path)
        /// </param>
        /// <param name="toUnlink">
        /// Extensions to unlink; links that name another program are left as they are
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when an entry of <paramref name="toLink"/> or <paramref name="toUnlink"/> is not a
        /// file extension; nothing after that entry is written
        /// </exception>
        public void Apply(ReadOnlySpan<string> toLink, ReadOnlySpan<string> toUnlink)
        {
            if (toLink.Length > 0)
            {
                WriteFileType();
            }

            for (int i = 0; i < toLink.Length; i++)
            {
                LinkOne(NormaliseExtension(toLink[i]));
            }

            for (int i = 0; i < toUnlink.Length; i++)
            {
                UnlinkOne(NormaliseExtension(toUnlink[i]));
            }

            if (toLink.Length == 0)
            {
                _classesRoot.DeleteSubKeyTree(_progId, throwOnMissingSubKey: false);
            }

            if (_notifyShell)
            {
                SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Closes the registry key when this service opened it (<see cref="ForCurrentUser"/>).
        /// </summary>
        public void Dispose()
        {
            if (_ownsClassesRoot)
            {
                _classesRoot.Dispose();
            }
        }

        // Lower case with exactly one leading dot, so ".D64", "d64" and ".d64" are the same key.
        // Throws on anything that is not a plain extension, so no other key can be written.
        internal static string NormaliseExtension(string extension)
        {
            ArgumentNullException.ThrowIfNull(extension);
            string trimmed = extension.Trim();
            if (trimmed.StartsWith('.'))
            {
                trimmed = trimmed.Substring(1);
            }

            if (trimmed.Length == 0 || trimmed.IndexOfAny(s_notAllowedInExtension) >= 0)
            {
                throw new ArgumentException($"'{extension}' is not a file extension.", nameof(extension));
            }

            return "." + trimmed.ToLowerInvariant();
        }

        // True when the extension's default value, or its OpenWithProgids list, names our file type.
        private bool NamesOurFileType(string ext)
        {
            using RegistryKey? extKey = _classesRoot.OpenSubKey(ext);
            if (extKey == null)
            {
                return false;
            }

            if (string.Equals(extKey.GetValue(null) as string, _progId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            using RegistryKey? openWith = extKey.OpenSubKey(OpenWithProgIdsKey);
            return openWith != null && openWith.GetValue(_progId) != null;
        }

        // Writes the RetroCommander file type: its name, its icon and the command that opens a file.
        private void WriteFileType()
        {
            using RegistryKey progIdKey = _classesRoot.CreateSubKey(_progId, writable: true);
            progIdKey.SetValue(null, FileTypeName, RegistryValueKind.String);

            using (RegistryKey iconKey = progIdKey.CreateSubKey(DefaultIconKey, writable: true))
            {
                iconKey.SetValue(null, "\"" + _exePath + "\",0", RegistryValueKind.String);
            }

            using RegistryKey commandKey = progIdKey.CreateSubKey(OpenCommandKey, writable: true);
            commandKey.SetValue(null, OpenCommand, RegistryValueKind.String);
        }

        // Points one extension at our file type and lists it under "Open with".
        private void LinkOne(string ext)
        {
            using RegistryKey extKey = _classesRoot.CreateSubKey(ext, writable: true);
            extKey.SetValue(null, _progId, RegistryValueKind.String);

            using RegistryKey openWith = extKey.CreateSubKey(OpenWithProgIdsKey, writable: true);
            // An empty value of type "none" is how Windows lists a ProgID under OpenWithProgids.
            openWith.SetValue(_progId, Array.Empty<byte>(), RegistryValueKind.None);
        }

        // Removes our file type from one extension, then any key that is left empty.
        private void UnlinkOne(string ext)
        {
            using (RegistryKey? extKey = _classesRoot.OpenSubKey(ext, writable: true))
            {
                if (extKey == null)
                {
                    return;
                }

                if (string.Equals(extKey.GetValue(null) as string, _progId, StringComparison.OrdinalIgnoreCase))
                {
                    extKey.DeleteValue(string.Empty, throwOnMissingValue: false);
                }

                using (RegistryKey? openWith = extKey.OpenSubKey(OpenWithProgIdsKey, writable: true))
                {
                    openWith?.DeleteValue(_progId, throwOnMissingValue: false);
                }

                DeleteIfEmpty(extKey, OpenWithProgIdsKey);
            }

            DeleteIfEmpty(_classesRoot, ext);
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

        // Tells Explorer that file links changed, so icons and "Open with" refresh without a restart.
        // DllImport, not LibraryImport: LibraryImport needs AllowUnsafeBlocks for the whole Core
        // project, which is not worth it for one call with plain value parameters.
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}
