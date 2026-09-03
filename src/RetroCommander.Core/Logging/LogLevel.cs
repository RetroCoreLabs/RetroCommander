//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Logging severity levels
/// </summary>
public enum LogLevel
{
    Trace = 0,    // Detailed debugging info
    Debug = 1,    // Development diagnostics
    Info = 2,     // General information
    Warning = 3,  // Potentially harmful situations
    Error = 4,    // Error events
    Critical = 5  // Critical failures
}
