//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

namespace RetroCommander.Core.Logging;

/// <summary>
/// Configuration for logging system
/// </summary>
public class LoggingConfiguration
{
    public string LogFilePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroCommander",
            "Logs",
            $"retrocommander-{DateTime.UtcNow:yyyyMMdd}.log");

    public LogLevel FileLogLevel { get; set; } = LogLevel.Debug;
    public LogLevel UILogLevel { get; set; } = LogLevel.Warning;
    public bool EnableConsoleOutput { get; set; } = false;
    public int MaxLogFileSizeMB { get; set; } = 10;
    public int MaxLogFileAgeDays { get; set; } = 30;
}
