//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Represents a single log entry
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Operation { get; set; }
    public string? FilePath { get; set; }
    public Dictionary<string, string>? Context { get; set; }
    public ExceptionInfo? Exception { get; set; }
    public string? ThreadId { get; set; }
}

/// <summary>
/// Exception information for logging
/// </summary>
public class ExceptionInfo
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public ExceptionInfo? InnerException { get; set; }

    public static ExceptionInfo FromException(Exception ex)
    {
        return new ExceptionInfo
        {
            Type = ex.GetType().Name,
            Message = ex.Message,
            StackTrace = ex.StackTrace,
            InnerException = ex.InnerException != null
                ? FromException(ex.InnerException)
                : null
        };
    }
}
