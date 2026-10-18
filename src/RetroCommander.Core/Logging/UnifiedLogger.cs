//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using RetroCommander.Core.Abstractions;

namespace RetroCommander.Core.Logging;

/// <summary>
/// Unified logger that writes to file and shows UI notifications
/// </summary>
public class UnifiedLogger : IRetroCommanderLogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly IUserNotificationService? _notificationService;
    private readonly LogLevel _fileLogLevel;
    private readonly LogLevel _uiLogLevel;
    private readonly bool _consoleOutput;
    private readonly long _maxLogFileBytes;
    private readonly object _fileLock = new();
    private readonly BlockingCollection<LogEntry> _logQueue;
    private readonly Task _logWriter;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;

    /// <summary>
    /// Creates the logger, makes sure the log folder exists, and starts the background task that writes entries to the file.
    /// </summary>
    /// <param name="config">
    /// The log file path and the lowest levels for the file and for user notifications.
    /// </param>
    /// <param name="notificationService">
    /// The service used to show entries to the user, or null to show nothing.
    /// </param>
    public UnifiedLogger(LoggingConfiguration config, IUserNotificationService? notificationService = null)
    {
        _logFilePath = config.LogFilePath;
        _notificationService = notificationService;
        _fileLogLevel = config.FileLogLevel;
        _uiLogLevel = config.UILogLevel;
        _consoleOutput = config.EnableConsoleOutput;
        _maxLogFileBytes = config.MaxLogFileSizeMB * 1024L * 1024L;

        _logQueue = new BlockingCollection<LogEntry>();
        _cts = new CancellationTokenSource();

        // Ensure log directory exists
        var logDir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        DeleteOldLogFiles(config.MaxLogFileAgeDays);

        // Start background log writer
        _logWriter = Task.Run(() => ProcessLogQueueAsync(_cts.Token));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The entry is queued for the log file if <paramref name="level"/> is at or above the file level,
    /// and shown to the user on a background task if it is at or above the notification level.
    /// </remarks>
    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Source = GetCallerInfo(),
            ThreadId = Environment.CurrentManagedThreadId.ToString(),
            Exception = ex != null ? ExceptionInfo.FromException(ex) : null
        };

        // Enqueue for file logging
        if (level >= _fileLogLevel)
        {
            _logQueue.Add(entry);
        }

        // Show UI notification for errors/warnings
        if (level >= _uiLogLevel && _notificationService != null)
        {
            Task.Run(() => ShowUINotification(entry));
        }
    }

    /// <inheritdoc/>
    public void LogTrace(string message) => Log(LogLevel.Trace, message);

    /// <inheritdoc/>
    public void LogDebug(string message) => Log(LogLevel.Debug, message);

    /// <inheritdoc/>
    public void LogInfo(string message) => Log(LogLevel.Info, message);

    /// <inheritdoc/>
    public void LogWarning(string message, Exception? ex = null) => Log(LogLevel.Warning, message, ex);

    /// <inheritdoc/>
    public void LogError(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);

    /// <inheritdoc/>
    public void LogCritical(string message, Exception? ex = null) => Log(LogLevel.Critical, message, ex);

    /// <inheritdoc/>
    public void LogOperation(string operation, string details, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = details,
            Operation = operation,
            Source = GetCallerInfo(),
            ThreadId = Environment.CurrentManagedThreadId.ToString()
        };

        if (level >= _fileLogLevel)
        {
            _logQueue.Add(entry);
        }

        if (level >= _uiLogLevel && _notificationService != null)
        {
            Task.Run(() => ShowUINotification(entry));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The message text is the operation name, a colon, and the file path.
    /// </remarks>
    public void LogFileOperation(string operation, string filePath, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = $"{operation}: {filePath}",
            Operation = operation,
            FilePath = filePath,
            Source = GetCallerInfo(),
            ThreadId = Environment.CurrentManagedThreadId.ToString()
        };

        if (level >= _fileLogLevel)
        {
            _logQueue.Add(entry);
        }

        if (level >= _uiLogLevel && _notificationService != null)
        {
            Task.Run(() => ShowUINotification(entry));
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A transfer that worked is logged at <see cref="LogLevel.Info"/>, a failed one at <see cref="LogLevel.Error"/>.
    /// Only a failed transfer is shown to the user, and only when the notification level allows it. The paths, byte count and result are also stored in the entry's context.
    /// </remarks>
    public void LogTransfer(string source, string destination, long bytesTransferred, bool success)
    {
        var level = success ? LogLevel.Info : LogLevel.Error;
        var message = success
            ? $"Transfer completed: {bytesTransferred:N0} bytes from {source} to {destination}"
            : $"Transfer failed: {source} to {destination}";

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Operation = "Transfer",
            Source = GetCallerInfo(),
            Context = new Dictionary<string, string>
            {
                ["SourcePath"] = source,
                ["DestinationPath"] = destination,
                ["BytesTransferred"] = bytesTransferred.ToString(),
                ["Success"] = success.ToString()
            },
            ThreadId = Environment.CurrentManagedThreadId.ToString()
        };

        if (level >= _fileLogLevel)
        {
            _logQueue.Add(entry);
        }

        if (!success && level >= _uiLogLevel && _notificationService != null)
        {
            Task.Run(() => ShowUINotification(entry));
        }
    }

    private async Task ProcessLogQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var entry = _logQueue.Take(cancellationToken);
                await WriteToFileAsync(entry);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
                break;
            }
            catch (Exception ex)
            {
                // Log to debug output if file logging fails
                Debug.WriteLine($"Logging failed: {ex.Message}");
            }
        }
    }

    private Task WriteToFileAsync(LogEntry entry)
    {
        var logLine = FormatLogEntry(entry);

        lock (_fileLock)
        {
            File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
        }

        if (_consoleOutput)
        {
            Console.WriteLine(logLine);
        }

        CheckLogRotation();
        return Task.CompletedTask;
    }

    private string FormatLogEntry(LogEntry entry)
    {
        var sb = new StringBuilder();

        // Timestamp [Level] Source: Message
        sb.Append($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} ");
        sb.Append($"[{entry.Level}] ");

        if (!string.IsNullOrEmpty(entry.Source))
        {
            sb.Append($"{entry.Source}: ");
        }

        sb.Append(entry.Message);

        // Add operation if present
        if (!string.IsNullOrEmpty(entry.Operation))
        {
            sb.Append($" (Operation: {entry.Operation})");
        }

        // Add file path if present
        if (!string.IsNullOrEmpty(entry.FilePath))
        {
            sb.Append($" (File: {entry.FilePath})");
        }

        // Add context
        if (entry.Context != null && entry.Context.Count > 0)
        {
            sb.Append(" [");
            sb.Append(string.Join(", ", entry.Context.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            sb.Append("]");
        }

        // Add exception
        if (entry.Exception != null)
        {
            sb.AppendLine();
            sb.Append($"  Exception: {entry.Exception.Type}: {entry.Exception.Message}");
            if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
            {
                sb.AppendLine();
                sb.Append($"  Stack Trace: {entry.Exception.StackTrace}");
            }
        }

        return sb.ToString();
    }

    private void ShowUINotification(LogEntry entry)
    {
        if (_notificationService == null) return;

        var title = entry.Operation ?? entry.Level.ToString();
        var message = entry.Message;

        if (entry.Exception != null)
        {
            message += $"\n\nError: {entry.Exception.Message}";
        }

        switch (entry.Level)
        {
            case LogLevel.Critical:
            case LogLevel.Error:
                _notificationService.ShowError(title, message);
                break;
            case LogLevel.Warning:
                _notificationService.ShowWarning(title, message);
                break;
            case LogLevel.Info:
                _notificationService.ShowInfo(title, message);
                break;
        }
    }

    private string GetCallerInfo()
    {
        var stackTrace = new StackTrace(2, false);
        var frame = stackTrace.GetFrame(0);
        var method = frame?.GetMethod();

        if (method == null) return "Unknown";

        return $"{method.DeclaringType?.Name}.{method.Name}";
    }

    private void CheckLogRotation()
    {
        var fileInfo = new FileInfo(_logFilePath);
        if (_maxLogFileBytes > 0 && fileInfo.Exists && fileInfo.Length > _maxLogFileBytes)
        {
            var archivePath = $"{_logFilePath}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.old";
            lock (_fileLock)
            {
                if (File.Exists(_logFilePath))
                {
                    File.Move(_logFilePath, archivePath);
                }
            }
        }
    }

    private void DeleteOldLogFiles(int maxAgeDays)
    {
        if (maxAgeDays <= 0)
        {
            return;
        }

        var logDir = Path.GetDirectoryName(_logFilePath);
        if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
        {
            return;
        }

        // The default log name carries the date (retrocommander-20261018.log), so "the same kind of file"
        // is the name up to the date: strip the trailing digits and match on that start.
        var prefix = Path.GetFileNameWithoutExtension(_logFilePath).TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        var files = Directory.GetFiles(logDir);

        for (var i = 0; i < files.Length; i++)
        {
            var path = files[i];
            var name = Path.GetFileName(path);
            var looksLikeLog = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".old", StringComparison.OrdinalIgnoreCase));

            if (!looksLikeLog || string.Equals(path, _logFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file that is open elsewhere stays until the next start; cleaning up must not stop logging.
                Debug.WriteLine($"Could not delete old log file {name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Stops the background writer, waits up to five seconds for it to finish, and frees the queue.
    /// </summary>
    /// <remarks>
    /// Entries still waiting in the queue when the writer stops are not written.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts.Cancel();
        _logWriter?.Wait(TimeSpan.FromSeconds(5));
        _logQueue?.Dispose();
        _cts?.Dispose();
    }
}
