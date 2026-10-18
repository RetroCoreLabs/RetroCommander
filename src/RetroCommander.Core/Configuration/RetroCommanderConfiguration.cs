//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.Text.Json;
using RetroCommander.Core.Caching;
using RetroCommander.Core.Logging;
using RetroCommander.Core.UI;

namespace RetroCommander.Core.Configuration;

/// <summary>
/// Main configuration for RetroCommander
/// </summary>
public class RetroCommanderConfiguration
{
    /// <summary>
    /// Gets or sets the logging settings.
    /// </summary>
    public LoggingConfiguration Logging { get; set; } = new();

    /// <summary>
    /// Gets or sets the cache settings.
    /// </summary>
    public CacheConfiguration Cache { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of volumes mounted before, kept by <see cref="ConfigurationManager.UpdateMountHistory"/>.
    /// </summary>
    public List<MountHistoryEntry> MountHistory { get; set; } = new();

    /// <summary>
    /// Gets or sets the saved bookmarks.
    /// </summary>
    public List<Bookmark> Bookmarks { get; set; } = new();

    /// <summary>
    /// Gets or sets the user interface preferences.
    /// </summary>
    public UIPreferences UI { get; set; } = new();

    /// <summary>
    /// Gets or sets the default settings for copy and move operations.
    /// </summary>
    public TransferDefaults Transfer { get; set; } = new();
}

/// <summary>
/// Mount history entry
/// </summary>
public class MountHistoryEntry
{
    /// <summary>
    /// Gets or sets the URN of the mounted volume. History entries are matched on this value.
    /// </summary>
    public string Urn { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name shown for the volume.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file system type name, for example <c>NDFS</c> or <c>D64</c>.
    /// </summary>
    public string FileSystemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the volume was last mounted, in UTC.
    /// </summary>
    public DateTime LastAccessed { get; set; }

    /// <summary>
    /// Gets or sets how many times the volume has been mounted.
    /// </summary>
    public int AccessCount { get; set; }
}

/// <summary>
/// UI preferences
/// </summary>
public class UIPreferences
{
    /// <summary>
    /// Gets or sets the URN of the volume last shown in the left panel, or null if none was saved.
    /// </summary>
    public string? LastLeftPanelUrn { get; set; }

    /// <summary>
    /// Gets or sets the URN of the volume last shown in the right panel, or null if none was saved.
    /// </summary>
    public string? LastRightPanelUrn { get; set; }

    /// <summary>
    /// Gets or sets the name of the colour theme. Defaults to <c>Default</c>.
    /// </summary>
    public string Theme { get; set; } = "Default";

    /// <summary>
    /// Gets or sets whether hidden files are listed. Defaults to false.
    /// </summary>
    public bool ShowHiddenFiles { get; set; } = false;

    /// <summary>
    /// Gets or sets the .NET format string for dates and times. Defaults to <c>yyyy-MM-dd HH:mm</c>.
    /// </summary>
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Gets or sets whether to ask before deleting. Defaults to true.
    /// </summary>
    public bool ConfirmDelete { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to ask before overwriting an existing file. Defaults to true.
    /// </summary>
    public bool ConfirmOverwrite { get; set; } = true;
}

/// <summary>
/// Transfer operation defaults
/// </summary>
public class TransferDefaults
{
    /// <summary>
    /// Gets or sets whether file properties are carried over when copying. Defaults to true.
    /// </summary>
    public bool PreserveProperties { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a copy is checked after it is written. Defaults to false.
    /// </summary>
    public bool VerifyAfterCopy { get; set; } = false;

    /// <summary>
    /// Gets or sets whether a progress dialog is shown during a transfer. Defaults to true.
    /// </summary>
    public bool ShowProgressDialog { get; set; } = true;
}

/// <summary>
/// Configuration manager
/// </summary>
public class ConfigurationManager
{
    private readonly string _configFilePath;
    private RetroCommanderConfiguration _config;

    /// <summary>
    /// Creates the manager and loads the configuration file right away.
    /// </summary>
    /// <param name="configFilePath">
    /// Path of the JSON configuration file. When null, <c>RetroCommander\config.json</c> under the local application data folder is used.
    /// </param>
    public ConfigurationManager(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? GetDefaultConfigPath();
        _config = Load();
    }

    /// <summary>
    /// Current configuration
    /// </summary>
    public RetroCommanderConfiguration Current => _config;

    /// <summary>
    /// Load configuration from file
    /// </summary>
    public RetroCommanderConfiguration Load()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<RetroCommanderConfiguration>(json);
                return config ?? new RetroCommanderConfiguration();
            }
        }
        catch
        {
            // If load fails, return defaults
        }

        return new RetroCommanderConfiguration();
    }

    /// <summary>
    /// Save configuration to file
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Add or update mount history
    /// </summary>
    public void UpdateMountHistory(string urn, string displayName, string fileSystemType)
    {
        var existing = _config.MountHistory.FirstOrDefault(h => h.Urn == urn);
        if (existing != null)
        {
            existing.LastAccessed = DateTime.UtcNow;
            existing.AccessCount++;
        }
        else
        {
            _config.MountHistory.Add(new MountHistoryEntry
            {
                Urn = urn,
                DisplayName = displayName,
                FileSystemType = fileSystemType,
                LastAccessed = DateTime.UtcNow,
                AccessCount = 1
            });
        }

        // Keep only last 50 entries
        if (_config.MountHistory.Count > 50)
        {
            _config.MountHistory = _config.MountHistory
                .OrderByDescending(h => h.LastAccessed)
                .Take(50)
                .ToList();
        }

        Save();
    }

    /// <summary>
    /// Add bookmark
    /// </summary>
    public void AddBookmark(Bookmark bookmark)
    {
        if (!_config.Bookmarks.Any(b => b.Urn == bookmark.Urn))
        {
            _config.Bookmarks.Add(bookmark);
            Save();
        }
    }

    /// <summary>
    /// Remove bookmark
    /// </summary>
    public void RemoveBookmark(string bookmarkId)
    {
        var bookmark = _config.Bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
        if (bookmark != null)
        {
            _config.Bookmarks.Remove(bookmark);
            Save();
        }
    }

    /// <summary>
    /// Get recent mounts
    /// </summary>
    public List<RecentMount> GetRecentMounts(int count = 10)
    {
        return _config.MountHistory
            .OrderByDescending(h => h.LastAccessed)
            .Take(count)
            .Select(h => new RecentMount
            {
                Urn = h.Urn,
                DisplayName = h.DisplayName,
                FileSystemType = h.FileSystemType,
                LastAccessed = h.LastAccessed,
                Icon = GetIconForFileSystemType(h.FileSystemType)
            })
            .ToList();
    }

    private string GetIconForFileSystemType(string type)
    {
        return type switch
        {
            "Windows" => "📁",
            "NDFS" => "💾",
            "Azure" => "☁️",
            "TNFS" => "🌐",
            "CBM" or "D64" or "D71" or "D81" => "💾",
            _ => "📄"
        };
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "RetroCommander", "config.json");
    }
}
