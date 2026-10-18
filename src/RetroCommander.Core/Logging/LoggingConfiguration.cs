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
    /// When true, <see cref="UnifiedLogger"/> writes each line it writes to the log file to standard output as well.
    /// </remarks>
    public bool EnableConsoleOutput { get; set; } = false;

    /// <summary>
    /// Gets or sets the largest size of the log file, in megabytes. The default is 10.
    /// </summary>
    /// <remarks>
    /// When the log file grows past this size, <see cref="UnifiedLogger"/> moves it aside to a file ending in .old
    /// and starts a new one. Zero or less turns this off.
    /// </remarks>
    public int MaxLogFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Gets or sets how many days old log files are kept. The default is 30.
    /// </summary>
    /// <remarks>
    /// When <see cref="UnifiedLogger"/> starts, it deletes files next to the log file that look like old logs
    /// (same name start, ending in .log or .old) and were last written more than this many days ago.
    /// Zero or less turns this off.
    /// </remarks>
    public int MaxLogFileAgeDays { get; set; } = 30;
}
