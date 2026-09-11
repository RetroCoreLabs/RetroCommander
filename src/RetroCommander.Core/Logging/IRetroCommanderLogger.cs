//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen 
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Logger interface for RetroCommander
/// </summary>
public interface IRetroCommanderLogger
{
    void Log(LogLevel level, string message, Exception? ex = null);
    void LogTrace(string message);
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message, Exception? ex = null);
    void LogError(string message, Exception? ex = null);
    void LogCritical(string message, Exception? ex = null);

    // Context-aware logging
    void LogOperation(string operation, string details, LogLevel level = LogLevel.Info);
    void LogFileOperation(string operation, string filePath, LogLevel level = LogLevel.Info);
    void LogTransfer(string source, string destination, long bytesTransferred, bool success);
}
