//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Logger interface for RetroCommander
/// </summary>
public interface IRetroCommanderLogger
{
    /// <summary>
    /// Writes a message at the given level.
    /// </summary>
    /// <param name="level">
    /// How serious the message is.
    /// </param>
    /// <param name="message">
    /// The text to log.
    /// </param>
    /// <param name="ex">
    /// An exception to record with the message, or null if there is none.
    /// </param>
    void Log(LogLevel level, string message, Exception? ex = null);

    /// <summary>
    /// Writes a message at the <see cref="LogLevel.Trace"/> level.
    /// </summary>
    /// <param name="message">
    /// The text to log.
    /// </param>
    void LogTrace(string message);

    /// <summary>
    /// Writes a message at the <see cref="LogLevel.Debug"/> level.
    /// </summary>
    /// <param name="message">
    /// The text to log.
    /// </param>
    void LogDebug(string message);

    /// <summary>
    /// Writes a message at the <see cref="LogLevel.Info"/> level.
    /// </summary>
    /// <param name="message">
    /// The text to log.
    /// </param>
    void LogInfo(string message);

    /// <summary>
    /// Writes a message at the <see cref="LogLevel.Warning"/> level.
    /// </summary>
    /// <param name="message">
    /// The text to log.
    /// </param>
    /// <param name="ex">
    /// An exception to record with the message, or null if there is none.
    /// </param>
    void LogWarning(string message, Exception? ex = null);

    /// <summary>
    /// Writes a message at the <see cref="LogLevel.Error"/> level.
    /// </summary>
    /// <param name="message">
    /// The text to log.
    /// </param>
    /// <param name="ex">
    /// An exception to record with the message, or null if there is none.
    /// </param>
    void LogError(string message, Exception? ex = null);

    /// <summary>
    /// Writes a message at the <see cref="LogLevel.Critical"/> level.
    /// </summary>
    /// <param name="message">
    /// The text to log.
    /// </param>
    /// <param name="ex">
    /// An exception to record with the message, or null if there is none.
    /// </param>
    void LogCritical(string message, Exception? ex = null);

    // Context-aware logging
    /// <summary>
    /// Writes a message tagged with the name of the operation it belongs to.
    /// </summary>
    /// <param name="operation">
    /// The name of the operation the message belongs to.
    /// </param>
    /// <param name="details">
    /// The text to log.
    /// </param>
    /// <param name="level">
    /// How serious the message is. Defaults to <see cref="LogLevel.Info"/>.
    /// </param>
    void LogOperation(string operation, string details, LogLevel level = LogLevel.Info);

    /// <summary>
    /// Writes a message about an operation on one file, tagged with the operation name and the file path.
    /// </summary>
    /// <param name="operation">
    /// The name of the operation done on the file.
    /// </param>
    /// <param name="filePath">
    /// The path of the file the operation was done on.
    /// </param>
    /// <param name="level">
    /// How serious the message is. Defaults to <see cref="LogLevel.Info"/>.
    /// </param>
    void LogFileOperation(string operation, string filePath, LogLevel level = LogLevel.Info);

    /// <summary>
    /// Writes the result of a file transfer from one place to another.
    /// </summary>
    /// <param name="source">
    /// Where the data was copied from.
    /// </param>
    /// <param name="destination">
    /// Where the data was copied to.
    /// </param>
    /// <param name="bytesTransferred">
    /// The number of bytes copied.
    /// </param>
    /// <param name="success">
    /// True if the transfer worked, false if it failed.
    /// </param>
    void LogTransfer(string source, string destination, long bytesTransferred, bool success);
}
