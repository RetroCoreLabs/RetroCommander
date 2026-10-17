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
    public LoggingConfiguration Logging { get; set; } = new();
    public CacheConfiguration Cache { get; set; } = new();
    public List<MountHistoryEntry> MountHistory { get; set; } = new();
    public List<Bookmark> Bookmarks { get; set; } = new();
    public UIPreferences UI { get; set; } = new();
    public TransferDefaults Transfer { get; set; } = new();
}

/// <summary>
/// Mount history entry
/// </summary>
public class MountHistoryEntry
{
    public string Urn { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileSystemType { get; set; } = string.Empty;
    public DateTime LastAccessed { get; set; }
    public int AccessCount { get; set; }
}

/// <summary>
/// UI preferences
/// </summary>
public class UIPreferences
{
    public string? LastLeftPanelUrn { get; set; }
    public string? LastRightPanelUrn { get; set; }
    public string Theme { get; set; } = "Default";
    public bool ShowHiddenFiles { get; set; } = false;
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm";
    public bool ConfirmDelete { get; set; } = true;
    public bool ConfirmOverwrite { get; set; } = true;
}

/// <summary>
/// Transfer operation defaults
/// </summary>
public class TransferDefaults
{
    public bool PreserveProperties { get; set; } = true;
    public bool VerifyAfterCopy { get; set; } = false;
    public bool ShowProgressDialog { get; set; } = true;
}

/// <summary>
/// Configuration manager
/// </summary>
public class ConfigurationManager
{
    private readonly string _configFilePath;
    private RetroCommanderConfiguration _config;

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
