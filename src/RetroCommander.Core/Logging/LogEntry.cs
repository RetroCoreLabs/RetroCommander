//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Represents a single log entry
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets or sets when the entry was made. <see cref="UnifiedLogger"/> sets it in UTC.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets how serious the entry is.
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the text of the entry.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where the entry came from, as "TypeName.MethodName" when <see cref="UnifiedLogger"/> fills it in.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the name of the operation the entry belongs to, or null if none was given.
    /// </summary>
    public string? Operation { get; set; }

    /// <summary>
    /// Gets or sets the path of the file the entry is about, or null if it is not about one file.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets extra name and value pairs written with the entry, or null if there are none.
    /// </summary>
    public Dictionary<string, string>? Context { get; set; }

    /// <summary>
    /// Gets or sets the exception recorded with the entry, or null if there is none.
    /// </summary>
    public ExceptionInfo? Exception { get; set; }

    /// <summary>
    /// Gets or sets the managed thread id of the thread that made the entry, as text.
    /// </summary>
    public string? ThreadId { get; set; }
}

/// <summary>
/// Exception information for logging
/// </summary>
public class ExceptionInfo
{
    /// <summary>
    /// Gets or sets the short type name of the exception, without the namespace.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message of the exception.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stack trace of the exception, or null if it has none.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the inner exception, or null if there is none.
    /// </summary>
    public ExceptionInfo? InnerException { get; set; }

    /// <summary>
    /// Copies the type name, message and stack trace out of an exception, and does the same for each inner exception in turn.
    /// </summary>
    /// <param name="ex">
    /// The exception to copy.
    /// </param>
    /// <returns>
    /// A new <see cref="ExceptionInfo"/> holding the copied details.
    /// </returns>
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
