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
using RetroIO.Storage.Ndfs.Security;

namespace RetroCommander.Desktop;

/// <summary>
/// F3-view window for a top-level NDFS entry — i.e. a USER (at the NDFS root, directories are users).
/// Shows the user's identity, quota, dates, friends and password, and offers a password reset,
/// a default-file-access change and friend add / change / delete (the SINTRAN commands
/// @SET-DEFAULT-FILE-ACCESS, @CREATE-FRIEND, @SET-FRIEND-ACCESS, @DELETE-FRIEND). The
/// password is shown BOTH as decimal and as a 6-digit ND-style octal value with a trailing 'B'
/// (e.g. 12345 = 030071B). Reset clears the password to 0 (no password) via the live filesystem;
/// it requires a read-write mount. Mirrors the RetroCore RetroCommander NDUserViewWnd.
/// </summary>
public partial class NdfsUserInfoWindow : Window
{
    private readonly INdfsFileSystem? _ndfs;
    private readonly int _userIndex;
    private readonly bool _isReadOnly;

    /// <summary>
    /// A single friend row for the friends list binding.
    /// </summary>
    public sealed class FriendRow
    {
        /// <summary>
        /// Gets the NDFS user index of the friend.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// Gets the friend's user name, looked up from <see cref="Index"/>.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the friend's access rights as text, as the NDFS friend entry formats them.
        /// </summary>
        public string Permissions { get; init; } = string.Empty;

        /// <summary>
        /// Gets the same rights in SINTRAN's written form (<c>RWA</c>, or <c>N</c> for none), the
        /// form the rights box takes.
        /// </summary>
        public string Rights { get; init; } = string.Empty;
    }

    // Parameterless ctor for the XAML designer only.
    /// <summary>
    /// Creates an empty window with no user shown. Meant for the XAML designer only.
    /// </summary>
    public NdfsUserInfoWindow()
    {
        InitializeComponent();
    }

    /// <param name="ndfs">
    /// The live mounted NDFS filesystem.
    /// </param>
    /// <param name="userIndex">
    /// Index of the user to display.
    /// </param>
    /// <param name="isReadOnly">
    /// True if the mount is read-only (reset will be blocked).
    /// </param>
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

    /// <summary>
    /// Looks the user up by index in the live filesystem.
    /// </summary>
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

        // Default access, in SINTRAN's own written form first (public,friend,own), then the raw word.
        DefaultAccessSintranText.Text = SintranAccessForm.FormatAccessTriple(user.DefaultFileAccess);
        DefaultAccessText.Text = $"0x{user.DefaultFileAccess:X4} ({FormatOctalWord(user.DefaultFileAccess)})";

        // Friends
        PopulateFriends(user);

        // Every change below writes the pack, so none of it is offered on a read-only mount.
        DefaultAccessBox.IsEnabled = !_isReadOnly;
        SetDefaultAccessButton.IsEnabled = !_isReadOnly;
        FriendNameBox.IsEnabled = !_isReadOnly;
        FriendRightsBox.IsEnabled = !_isReadOnly;
        AddFriendButton.IsEnabled = !_isReadOnly;
        SetFriendButton.IsEnabled = !_isReadOnly;
        DeleteFriendButton.IsEnabled = !_isReadOnly;

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
                    // The friend word keeps its rights in bits 8-12.
                    Rights = SintranAccessForm.FormatAccessRights(f.Bits >> 8),
                });
            }
        }

        FriendsList.ItemsSource = rows;
        NoFriendsText.IsVisible = rows.Count == 0;
    }

    /// <summary>
    /// Resolves a user index to a name for the friends list, falling back to "#index".
    /// </summary>
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

    /// <summary>
    /// Formats a 16-bit word as a 6-digit ND-style octal value with a trailing 'B'.
    /// </summary>
    private static string FormatOctalWord(ushort value)
        => Convert.ToString(value, 8).PadLeft(6, '0') + "B";

    /// <summary>
    /// Decodes a packed ND date to a readable string, or "N/A" when unset/invalid.
    /// </summary>
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

    /// <summary>
    /// Fills the friend boxes from the selected row, so Change rights and Delete act on it.
    /// </summary>
    private void OnFriendSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FriendsList.SelectedItem is not FriendRow row)
            return;

        FriendNameBox.Text = string.IsNullOrEmpty(row.Name) || row.Name.StartsWith('#')
            ? row.Index.ToString()
            : row.Name;
        FriendRightsBox.Text = row.Rights;
    }

    /// <summary>
    /// Sets the user's default file access from the PUBLIC,FRIEND,OWN box
    /// (SINTRAN <c>@SET-DEFAULT-FILE-ACCESS</c>).
    /// </summary>
    private void OnSetDefaultAccessClick(object? sender, RoutedEventArgs e)
    {
        RunChange(user =>
        {
            ushort before = user.DefaultFileAccess;
            ushort after = SintranAccessForm.ApplyAccessTriple(before, DefaultAccessBox.Text ?? string.Empty);
            if (after == before)
                return "No change: the default access is already " + SintranAccessForm.FormatAccessTriple(before) + ".";

            _ndfs!.SetDefaultFileAccess(user.UserName, after);
            DefaultAccessBox.Text = string.Empty;
            return $"✓ Default access {SintranAccessForm.FormatAccessTriple(before)} -> {SintranAccessForm.FormatAccessTriple(after)}.";
        });
    }

    /// <summary>
    /// Adds the friend in the name box (SINTRAN <c>@CREATE-FRIEND</c>), with RWA when no rights
    /// are typed.
    /// </summary>
    private void OnAddFriendClick(object? sender, RoutedEventArgs e)
    {
        RunChange(user =>
        {
            string friend = RequireFriendName();
            string rights = string.IsNullOrWhiteSpace(FriendRightsBox.Text) ? "RWA" : FriendRightsBox.Text.Trim();
            _ndfs!.AddFriend(user.UserName, friend, rights);
            return $"✓ Added friend {friend} with {rights}.";
        });
    }

    /// <summary>
    /// Changes the rights of the friend in the name box (SINTRAN <c>@SET-FRIEND-ACCESS</c>).
    /// </summary>
    private void OnSetFriendClick(object? sender, RoutedEventArgs e)
    {
        RunChange(user =>
        {
            string friend = RequireFriendName();
            string rights = (FriendRightsBox.Text ?? string.Empty).Trim();
            if (rights.Length == 0)
                return "No change: type the new rights, e.g. RW, or N for none.";

            _ndfs!.SetFriendAccess(user.UserName, friend, rights);
            return $"✓ Friend {friend} now has {rights.ToUpperInvariant()}.";
        });
    }

    /// <summary>
    /// Removes the friend in the name box (SINTRAN <c>@DELETE-FRIEND</c>).
    /// </summary>
    private void OnDeleteFriendClick(object? sender, RoutedEventArgs e)
    {
        RunChange(user =>
        {
            string friend = RequireFriendName();
            _ndfs!.RemoveFriend(user.UserName, friend);
            FriendNameBox.Text = string.Empty;
            FriendRightsBox.Text = string.Empty;
            return $"✓ Deleted friend {friend}.";
        });
    }

    /// <summary>
    /// The friend name box, trimmed; refuses an empty box.
    /// </summary>
    private string RequireFriendName()
    {
        string name = (FriendNameBox.Text ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new ArgumentException("Type the friend's user name or number first.");
        return name;
    }

    /// <summary>
    /// Runs one change on the pack, then shows the user again and the outcome in the status line.
    /// The library writes each change to the device as it is made and refuses bad input before
    /// changing anything, so an error here leaves the pack as it was.
    /// </summary>
    /// <param name="change">
    /// The change; returns the status text.
    /// </param>
    private void RunChange(Func<UserEntry, string> change)
    {
        if (_ndfs == null)
            return;

        if (_isReadOnly)
        {
            StatusText.Text = "Cannot change: volume is mounted read-only. Re-mount read-write.";
            return;
        }

        UserEntry? user = FindUser();
        if (user == null)
        {
            StatusText.Text = "User not found.";
            return;
        }

        string status;
        try
        {
            status = change(user);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is System.IO.IOException)
        {
            StatusText.Text = $"Not changed: {ex.Message}";
            return;
        }

        Populate();
        StatusText.Text = status;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
