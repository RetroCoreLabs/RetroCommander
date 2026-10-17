//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroCommander.Desktop;

/// <summary>
/// Tracks navigation history for a panel
/// </summary>
public class NavigationHistory
{
    private readonly Stack<string> _history = new();
    private readonly Stack<string> _forwardHistory = new();
    private bool _isNavigatingBack = false;
    private bool _isNavigatingForward = false;

    /// <summary>
    /// Add a new entry to history. Current location is saved before navigating to new location.
    /// </summary>
    public void AddEntry(string currentPath)
    {
        // Don't add to history if we're navigating back or forward
        if (_isNavigatingBack || _isNavigatingForward)
        {
            return;
        }

        // Don't add if it's the same as the last entry
        if (_history.Count > 0 && _history.Peek() == currentPath)
        {
            return;
        }

        _history.Push(currentPath);
        // Clear forward history when navigating to a new location
        _forwardHistory.Clear();
    }

    /// <summary>
    /// Check if we can go back
    /// </summary>
    public bool CanGoBack()
    {
        return _history.Count > 1; // Need at least 2 entries (current + previous)
    }

    /// <summary>
    /// Get the previous path and remove it from history
    /// </summary>
    public string? GoBack()
    {
        if (!CanGoBack())
        {
            return null;
        }

        // Remove current location
        var current = _history.Pop();

        // Get previous location
        var previous = _history.Peek();

        // Save current to forward history
        _forwardHistory.Push(current);

        _isNavigatingBack = true;
        return previous;
    }

    /// <summary>
    /// Check if we can go forward
    /// </summary>
    public bool CanGoForward()
    {
        return _forwardHistory.Count > 0;
    }

    /// <summary>
    /// Get the next path from forward history
    /// </summary>
    public string? GoForward()
    {
        if (!CanGoForward())
        {
            return null;
        }

        var next = _forwardHistory.Pop();
        _history.Push(next);

        _isNavigatingForward = true;
        return next;
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _forwardHistory.Clear();
    }

    /// <summary>
    /// Mark that navigation has completed (call after navigation finishes)
    /// </summary>
    public void NavigationCompleted()
    {
        _isNavigatingBack = false;
        _isNavigatingForward = false;
    }

    /// <summary>
    /// Get current path (most recent entry)
    /// </summary>
    public string? CurrentPath => _history.Count > 0 ? _history.Peek() : null;
}

