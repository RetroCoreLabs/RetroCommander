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
    /// <summary>
    /// Gets or sets the full path of the log file.
    /// </summary>
    /// <remarks>
    /// The default is a file named retrocommander-yyyyMMdd.log, dated with the UTC day the object is made,
    /// in the folder RetroCommander, then Logs, under the local application data folder.
    /// </remarks>
    public string LogFilePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RetroCommander",
            "Logs",
            $"retrocommander-{DateTime.UtcNow:yyyyMMdd}.log");

    /// <summary>
    /// Gets or sets the lowest level that is written to the log file. The default is <see cref="LogLevel.Debug"/>.
    /// </summary>
    public LogLevel FileLogLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Gets or sets the lowest level that is shown to the user as a notification. The default is <see cref="LogLevel.Warning"/>.
    /// </summary>
    public LogLevel UILogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets whether log entries should also go to the console. The default is false.
    /// </summary>
    /// <remarks>
    /// Nothing in the code reads this setting at present; <see cref="UnifiedLogger"/> does not write to the console.
    /// </remarks>
    public bool EnableConsoleOutput { get; set; } = false;

    /// <summary>
    /// Gets or sets the largest size of the log file, in megabytes. The default is 10.
    /// </summary>
    /// <remarks>
    /// Nothing in the code reads this setting at present; <see cref="UnifiedLogger"/> moves the file aside at a fixed 10 MB.
    /// </remarks>
    public int MaxLogFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Gets or sets how many days old log files are kept. The default is 30.
    /// </summary>
    /// <remarks>
    /// Nothing in the code reads this setting at present; no old log files are deleted.
    /// </remarks>
    public int MaxLogFileAgeDays { get; set; } = 30;
}
