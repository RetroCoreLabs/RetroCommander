//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RetroIO.Storage.Ndfs.Elements;
using RetroIO.Storage.Ndfs.FileSystems;

namespace RetroCommander.Desktop;

/// <summary>
/// F3-view window for a top-level NDFS entry — i.e. a USER (at the NDFS root, directories are users).
/// Shows the user's identity, quota, dates, friends and password, and offers a password reset. The
/// password is shown BOTH as decimal and as a 6-digit ND-style octal value with a trailing 'B'
/// (e.g. 12345 = 030071B). Reset clears the password to 0 (no password) via the live filesystem;
/// it requires a read-write mount. Mirrors the RetroCore RetroCommander NDUserViewWnd.
/// </summary>
public partial class NdfsUserInfoWindow : Window
{
    private readonly INdfsFileSystem? _ndfs;
    private readonly int _userIndex;
    private readonly bool _isReadOnly;

    /// <summary>A single friend row for the friends list binding.</summary>
    public sealed class FriendRow
    {
        public int Index { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Permissions { get; init; } = string.Empty;
    }

    // Parameterless ctor for the XAML designer only.
    public NdfsUserInfoWindow()
    {
        InitializeComponent();
    }

    /// <param name="ndfs">The live mounted NDFS filesystem.</param>
    /// <param name="userIndex">Index of the user to display.</param>
    /// <param name="isReadOnly">True if the mount is read-only (reset will be blocked).</param>
    public NdfsUserInfoWindow(INdfsFileSystem ndfs, int userIndex, bool isReadOnly)
        : this()
    {
        _ndfs = ndfs;
        _userIndex = userIndex;
        _isReadOnly = isReadOnly;

        Populate();
    }

    /// <summary>
    /// Resolves the user by its name at the NDFS root. Users appear as top-level directories whose
    /// name is the user name; this convenience ctor looks the index up so callers holding only the
    /// selected entry's name can construct the window.
    /// </summary>
    public static NdfsUserInfoWindow? ForUserName(INdfsFileSystem ndfs, string userName, bool isReadOnly)
    {
        var users = ndfs.GetUsers();
        for (int i = 0; i < users.Count; i++)
        {
            if (string.Equals(users[i].UserName, userName, StringComparison.OrdinalIgnoreCase))
                return new NdfsUserInfoWindow(ndfs, users[i].UserIndex, isReadOnly);
        }
        return null;
    }

    /// <summary>Looks the user up by index in the live filesystem.</summary>
    private UserEntry? FindUser()
    {
        if (_ndfs == null)
            return null;

        // GetUser(index) is index-addressed, but not every implementation guarantees the list is
        // dense/index-ordered — match on UserIndex to be safe.
        var users = _ndfs.GetUsers();
        for (int i = 0; i < users.Count; i++)
        {
            if (users[i].UserIndex == _userIndex)
                return users[i];
        }
        return null;
    }

    private void Populate()
    {
        UserEntry? user = FindUser();
        if (user == null)
        {
            Title = "NDFS User Information";
            UserNameText.Text = "(user not found)";
            ResetPasswordButton.IsEnabled = false;
            return;
        }

        Title = $"NDFS User: {user.UserName}";

        // Identity
        UserNameText.Text = user.UserName;
        UserIndexText.Text = user.UserIndex.ToString();
        DirectoryIndexText.Text = user.DirectoryIndex.ToString();
        FlagText.Text = $"0x{user.UF:X2}";
        EnterCountText.Text = user.EnterCount.ToString();

        // Quota
        PagesReservedText.Text = $"{user.PagesReserved:N0} pages";
        PagesUsedText.Text = $"{user.PagesUsed:N0} pages";
        long remaining = (long)user.PagesReserved - user.PagesUsed;
        PagesRemainingText.Text = $"{remaining:N0} pages";

        // Dates
        DateCreatedText.Text = FormatNdDate(user.DateCreated);
        LastEnteredText.Text = FormatNdDate(user.LastDateEntered);

        // Password — decimal AND 6-digit ND octal with a trailing 'B' (e.g. 12345 = 030071B).
        ushort pw = user.Password;
        PasswordDecText.Text = pw.ToString();
        PasswordOctText.Text = FormatOctalWord(pw);
        PasswordStateText.Text = pw == 0 ? "No password (0)" : "Password set";

        // Default access
        DefaultAccessText.Text = $"0x{user.DefaultFileAccess:X4} ({FormatOctalWord(user.DefaultFileAccess)})";

        // Friends
        PopulateFriends(user);

        // Reset availability (read-only mounts cannot write).
        ReadOnlyNotice.IsVisible = _isReadOnly;
        ResetPasswordButton.IsEnabled = !_isReadOnly && pw != 0;
        ToolTip.SetTip(ResetPasswordButton, _isReadOnly
            ? "Disabled: this volume is mounted read-only. Re-mount read-write to reset the password."
            : (pw == 0 ? "The password is already 0 (no password)." : "Clear the password to 0 (no password)."));
    }

    private void PopulateFriends(UserEntry user)
    {
        var rows = new List<FriendRow>();
        if (user.Friends != null)
        {
            for (int i = 0; i < user.Friends.Length; i++)
            {
                var f = user.Friends[i];
                if (f == null || !f.EntryUsed)
                    continue;

                rows.Add(new FriendRow
                {
                    Index = f.FriendUserIndex,
                    Name = ResolveUserName(f.FriendUserIndex),
                    Permissions = f.GetPermissionString(),
                });
            }
        }

        FriendsList.ItemsSource = rows;
        NoFriendsText.IsVisible = rows.Count == 0;
    }

    /// <summary>Resolves a user index to a name for the friends list, falling back to "#index".</summary>
    private string ResolveUserName(int userIndex)
    {
        if (_ndfs == null)
            return $"#{userIndex}";

        var users = _ndfs.GetUsers();
        for (int i = 0; i < users.Count; i++)
        {
            if (users[i].UserIndex == userIndex)
                return users[i].UserName;
        }
        return $"#{userIndex}";
    }

    /// <summary>Formats a 16-bit word as a 6-digit ND-style octal value with a trailing 'B'.</summary>
    private static string FormatOctalWord(ushort value)
        => Convert.ToString(value, 8).PadLeft(6, '0') + "B";

    /// <summary>Decodes a packed ND date to a readable string, or "N/A" when unset/invalid.</summary>
    private static string FormatNdDate(uint raw)
    {
        DateTime? dt = NdDateTime.FromNdDate(raw);
        return dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm") : "N/A";
    }

    private void OnResetPasswordClick(object? sender, RoutedEventArgs e)
    {
        if (_ndfs == null)
            return;

        if (_isReadOnly)
        {
            StatusText.Text = "Cannot reset: volume is mounted read-only. Re-mount read-write.";
            return;
        }

        try
        {
            bool ok = _ndfs.ClearUserPassword(_userIndex);
            if (ok)
            {
                Populate();
                StatusText.Text = "✓ Password reset to 0 (no password).";
            }
            else
            {
                StatusText.Text = "Reset failed (user not found, read-only, or password already 0).";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Reset failed: {ex.Message}";
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
