//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Core.Tests -- Unit tests for mount detection and URN handling
//

using System;
using System.Linq;
using NUnit.Framework;
using RetroCommander.Core.Managers;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local;
using RetroIO.Storage.Core.Utilities;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for mount detection and URN handling business logic.
    /// Tests the logic that prevents false positives and handles mount collisions.
    /// </summary>
    [TestFixture]
    public class MountDetectionTests
    {
        [Test]
        public void MountManager_IsMounted_WithNoMounts_ReturnsFalse()
        {
            // Arrange
            var driverFactory = new DriverFactory();
            var factory = new VolumeProviderFactory(null, driverFactory);
            var panelManager = new PanelManager(factory);
            var mountManager = new MountManager(factory, panelManager);

            // Act
            var result = mountManager.IsMounted("ndfs:///C:/test.ndfs");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void MountManager_MountPoints_InitiallyEmpty()
        {
            // Arrange
            var driverFactory = new DriverFactory();
            var factory = new VolumeProviderFactory(null, driverFactory);
            var panelManager = new PanelManager(factory);
            var mountManager = new MountManager(factory, panelManager);

            // Assert
            Assert.That(mountManager.MountPoints.Count, Is.EqualTo(0));
        }

        [Test]
        public void PathCleaner_ExtractFilePathFromUri_HandlesDifferentSchemes()
        {
            // Test various URN schemes
            var filePath = PathCleaner.ExtractFilePathFromUri("file:///C:/test/file.img");
            Assert.That(filePath, Does.Contain("C:"));
            Assert.That(filePath, Does.Contain("test"));
            Assert.That(filePath, Does.Contain("file.img"));

            var ndfsPath = PathCleaner.ExtractFilePathFromUri("ndfs:///D:/images/disk.ndfs");
            Assert.That(ndfsPath, Does.Contain("D:"));
            Assert.That(ndfsPath, Does.Contain("images"));
            Assert.That(ndfsPath, Does.Contain("disk.ndfs"));

            var bsdPath = PathCleaner.ExtractFilePathFromUri("bsd:///E:/sunos.img");
            Assert.That(bsdPath, Does.Contain("E:"));
            Assert.That(bsdPath, Does.Contain("sunos.img"));
        }

        [Test]
        public void PathCleaner_ExtractFilePathFromUri_RemovesFragment()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri("bsd:///E:/sunos.img#a");

            // Assert
            Assert.That(result, Does.Not.Contain("#"));
            Assert.That(result, Does.Contain("sunos.img"));
        }

        [Test]
        public void PathCleaner_NormalizePath_WindowsDrive_RemovesLeadingSlash()
        {
            // Act
            var result = PathCleaner.NormalizePath("/C:/test/file.img");

            // Assert
            Assert.That(result, Does.Not.StartWith("/"));
            Assert.That(result, Does.Contain("C:"));
        }

        [Test]
        public void PathCleaner_CreateUri_WithSchemeAndPath_CreatesValidUri()
        {
            // Act
            var result = PathCleaner.CreateUri("bsd", "C:/test/file.img");

            // Assert
            Assert.That(result, Does.StartWith("bsd://"));
            Assert.That(result, Does.Contain("C:"));
            Assert.That(result, Does.Contain("file.img"));
        }

        [Test]
        public void PathCleaner_CreateUri_WithFragment_IncludesFragment()
        {
            // Act
            var result = PathCleaner.CreateUri("bsd", "C:/test/file.img", "a");

            // Assert
            Assert.That(result, Does.EndWith("#a"));
        }

        [Test]
        public void MountManager_GetMountPointsByType_WithNoMounts_ReturnsEmpty()
        {
            // Arrange
            var driverFactory = new DriverFactory();
            var factory = new VolumeProviderFactory(null, driverFactory);
            var panelManager = new PanelManager(factory);
            var mountManager = new MountManager(factory, panelManager);

            // Act
            var result = mountManager.GetMountPointsByType("NDFS");

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MountManager_GetRecentMounts_WithNoMounts_ReturnsEmpty()
        {
            // Arrange
            var driverFactory = new DriverFactory();
            var factory = new VolumeProviderFactory(null, driverFactory);
            var panelManager = new PanelManager(factory);
            var mountManager = new MountManager(factory, panelManager);

            // Act
            var result = mountManager.GetRecentMounts(10);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MountManager_Unmount_NonExistentId_ReturnsFalse()
        {
            // Arrange
            var driverFactory = new DriverFactory();
            var factory = new VolumeProviderFactory(null, driverFactory);
            var panelManager = new PanelManager(factory);
            var mountManager = new MountManager(factory, panelManager);

            // Act
            var result = mountManager.Unmount("non-existent-id");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void MountManager_GetMountPoint_NonExistentId_ReturnsNull()
        {
            // Arrange
            var driverFactory = new DriverFactory();
            var factory = new VolumeProviderFactory(null, driverFactory);
            var panelManager = new PanelManager(factory);
            var mountManager = new MountManager(factory, panelManager);

            // Act
            var result = mountManager.GetMountPoint("non-existent-id");

            // Assert
            Assert.That(result, Is.Null);
        }
    }
}

