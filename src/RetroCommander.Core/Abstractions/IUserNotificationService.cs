//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Abstractions;

/// <summary>
/// UI-agnostic notification service for showing messages to users
/// </summary>
public interface IUserNotificationService
{
    /// <summary>
    /// Show error message to user
    /// </summary>
    void ShowError(string title, string message);

    /// <summary>
    /// Show warning message to user
    /// </summary>
    void ShowWarning(string title, string message);

    /// <summary>
    /// Show informational message to user
    /// </summary>
    void ShowInfo(string title, string message);

    /// <summary>
    /// Show confirmation dialog and get user response
    /// </summary>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>
    /// Show progress notification (for long-running operations)
    /// </summary>
    IProgressNotification? ShowProgress(string title, string message);
}

/// <summary>
/// Progress notification that can be updated and closed
/// </summary>
public interface IProgressNotification : IDisposable
{
    /// <summary>
    /// Update progress message
    /// </summary>
    void UpdateMessage(string message);

    /// <summary>
    /// Update progress percentage (0-100)
    /// </summary>
    void UpdateProgress(double percentage);

    /// <summary>
    /// Close the progress notification
    /// </summary>
    void Close();
}
