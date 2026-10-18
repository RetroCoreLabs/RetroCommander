//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Caching;
using RetroCommander.Core.Configuration;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Managers;
using RetroCommander.Core.Operations;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Transfer;
using RetroIO.Storage.Local;

namespace RetroCommander.Core;

/// <summary>
/// Simple demo showing core functionality
/// </summary>
public static class CoreDemo
{
    public static async Task RunDemoAsync()
    {
        Console.WriteLine("=== RetroCommander.Core Demo ===\n");

        // 1. Set up infrastructure
        var loggingConfig = new LoggingConfiguration
        {
            FileLogLevel = LogLevel.Debug,
            UILogLevel = LogLevel.Warning
        };
        using var logger = new UnifiedLogger(loggingConfig);
        logger.LogInfo("RetroCommander.Core Demo Started");

        var cacheManager = new FileSystemCacheManager(new CacheConfiguration());
        var driverFactory = new DriverFactory();
        var factory = new VolumeProviderFactory(null, driverFactory, cacheManager, logger);
        var panelManager = new PanelManager(factory);
        var propertyRegistry = PropertyMapperRegistry.CreateDefault();
        var fileOps = new SmartFileOperations(propertyRegistry, logger);

        // 2. Demo 1: Native Windows file operations
        await DemoNativeFileOperations(factory, fileOps);

        // 3. Demo 2: NDFS mounting (if test image exists)
        await DemoNdfsMounting(factory);

        Console.WriteLine("\n=== All Demos Complete ===");
    }

    private static async Task DemoNativeFileOperations(
        VolumeProviderFactory factory,
        SmartFileOperations fileOps)
    {
        Console.WriteLine("\n=== Demo 1: Native Windows File Operations ===");

        // Create panels using factory
        var sourceUrn = "file:///" + Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).Replace("\\", "/");
        var targetUrn = "file:///" + Path.GetTempPath().Replace("\\", "/");

        var sourcePanel = await factory.CreatePanelAsync(sourceUrn);
        var targetPanel = await factory.CreatePanelAsync(targetUrn);

        Console.WriteLine($"Source: {sourcePanel.CurrentPath}");
        Console.WriteLine($"Target: {targetPanel.CurrentPath}");

        // Refresh and find a file
        await sourcePanel.RefreshAsync();
        Console.WriteLine($"\nSource panel has {sourcePanel.Entries.Count} entries");

        if (sourcePanel.Entries.Count == 0)
        {
            Console.WriteLine("Creating test file...");
            var testFilePath = Path.Combine(sourcePanel.CurrentPath, "retrocommander-test.txt");
            await File.WriteAllTextAsync(testFilePath, "RetroCommander Core Test\nCreated: " + DateTime.Now);
            await sourcePanel.RefreshAsync();
        }

        var firstFile = sourcePanel.Entries.FirstOrDefault(e => !e.IsDirectory);
        if (firstFile == null)
        {
            Console.WriteLine("No files found to copy!");
            return;
        }

        sourcePanel.SelectedEntry = firstFile;
        Console.WriteLine($"\nSelected: {firstFile.Name} ({firstFile.Size:N0} bytes)");
        Console.WriteLine($"  Properties: {firstFile.Properties.GetAllProperties().Count}");

        // Copy with progress
        Console.WriteLine("\nCopying file...");
        var progress = new Progress<TransferProgress>(p =>
        {
            Console.Write($"\rProgress: {p.PercentComplete:F1}%");
        });

        var copyOptions = new CopyOptions
        {
            Overwrite = true,
            PreserveProperties = true,
            Progress = progress
        };

        var result = await fileOps.CopyAsync(firstFile, targetPanel, copyOptions);
        Console.WriteLine();

        if (result.Success)
        {
            Console.WriteLine("✓ Copy successful!");
            Console.WriteLine($"  Preserved properties: {result.PreservedProperties?.Count ?? 0}");
        }
        else
        {
            Console.WriteLine($"✗ Copy failed: {result.ErrorMessage}");
        }
    }

    private static async Task DemoNdfsMounting(VolumeProviderFactory factory)
    {
        Console.WriteLine("\n=== Demo 2: NDFS Disk Image Mounting ===");

        // Look for test NDFS images, relative to the repository's src folder. They are found by
        // walking up from the running program's folder, so no machine-specific path is needed.
        var testImagePaths = new[]
        {
            Path.Combine("demo", "test-images", "ndfs", "test.ndfs"),
            "test.ndfs"
        };

        string? ndfsImagePath = null;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && ndfsImagePath == null)
        {
            for (int i = 0; i < testImagePaths.Length; i++)
            {
                string candidate = Path.Combine(dir.FullName, testImagePaths[i]);
                if (File.Exists(candidate))
                {
                    ndfsImagePath = candidate;
                    break;
                }
            }
            dir = dir.Parent;
        }

        if (ndfsImagePath == null)
        {
            Console.WriteLine("No NDFS test images found. Skipping NDFS demo.");
            Console.WriteLine("Expected locations:");
            for (int i = 0; i < testImagePaths.Length; i++)
            {
                Console.WriteLine($"  - {testImagePaths[i]}");
            }
            return;
        }

        Console.WriteLine($"Found NDFS image: {ndfsImagePath}");

        // Mount via URN
        var urn = "ndfs://" + ndfsImagePath.Replace("\\", "/");
        Console.WriteLine($"Mounting: {urn}");

        try
        {
            var ndfsPanel = await factory.CreatePanelAsync(urn);
            Console.WriteLine($"✓ Mounted successfully!");
            Console.WriteLine($"  Panel ID: {ndfsPanel.Id}");
            Console.WriteLine($"  Display: {ndfsPanel.DisplayName}");
            Console.WriteLine($"  Type: {ndfsPanel.FileSystemType}");
            Console.WriteLine($"  Read-only: {ndfsPanel.IsReadOnly}");

            // Refresh and list contents
            await ndfsPanel.RefreshAsync();
            Console.WriteLine($"\nDirectory contents ({ndfsPanel.Entries.Count} entries):");

            int displayCount = Math.Min(10, ndfsPanel.Entries.Count);
            for (int i = 0; i < displayCount; i++)
            {
                var entry = ndfsPanel.Entries[i];
                var typeIndicator = entry.IsDirectory ? "[DIR]" : "     ";
                Console.WriteLine($"  {typeIndicator} {entry.Name,-30} {entry.Size,10:N0} bytes");
            }

            if (ndfsPanel.Entries.Count > 10)
            {
                Console.WriteLine($"  ... and {ndfsPanel.Entries.Count - 10} more entries");
            }

            // Show properties of first file
            var firstFile = ndfsPanel.Entries.FirstOrDefault(e => !e.IsDirectory);
            if (firstFile != null)
            {
                Console.WriteLine($"\nFile properties for: {firstFile.Name}");
                var allProps = firstFile.Properties.GetAllProperties();
                for (int i = 0; i < allProps.Count; i++)
                {
                    var kvp = allProps.ElementAt(i);
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            if (ndfsPanel is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to mount NDFS image: {ex.Message}");
        }
    }
}
