//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Logging severity levels
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// The most detailed messages, for tracing what the code does step by step.
    /// </summary>
    Trace = 0,    // Detailed debugging info

    /// <summary>
    /// Messages that help a developer find a problem.
    /// </summary>
    Debug = 1,    // Development diagnostics

    /// <summary>
    /// General information about what the program is doing.
    /// </summary>
    Info = 2,     // General information

    /// <summary>
    /// Something went wrong or looks wrong, but the program carries on.
    /// </summary>
    Warning = 3,  // Potentially harmful situations

    /// <summary>
    /// An operation failed.
    /// </summary>
    Error = 4,    // Error events

    /// <summary>
    /// A failure serious enough that the program may not be able to carry on.
    /// </summary>
    Critical = 5  // Critical failures
}
