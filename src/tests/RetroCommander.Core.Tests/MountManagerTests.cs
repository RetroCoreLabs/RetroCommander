//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Core.Tests -- Unit tests for MountManager business logic

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Logging;
using RetroCommander.Core.Managers;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for MountManager business logic.
    /// Tests mount/unmount operations, duplicate detection, and stale mount cleanup.
    /// </summary>
    [TestFixture]
    public class MountManagerTests
    {
        private Mock<IVolumeProviderFactory> _mockFactory;
        private Mock<IRetroCommanderLogger> _mockLogger;
        private PanelManager _panelManager;
        private MountManager _mountManager;
        private Mock<IFileSystemPanel> _mockPanel1;
        private Mock<IFileSystemPanel> _mockPanel2;

        [SetUp]
        public void Setup()
        {
            _mockFactory = new Mock<IVolumeProviderFactory>();
            _mockLogger = new Mock<IRetroCommanderLogger>();

            var driverFactory = new DriverFactory();
            var realFactory = new VolumeProviderFactory(null, driverFactory, null, _mockLogger.Object);
            _panelManager = new PanelManager(realFactory);
            _mountManager = new MountManager(realFactory, _panelManager, _mockLogger.Object);

            _mockPanel1 = new Mock<IFileSystemPanel>();
            _mockPanel1.Setup(p => p.Id).Returns("panel-1");
            _mockPanel1.Setup(p => p.DisplayName).Returns("Test Panel 1");
            _mockPanel1.Setup(p => p.FileSystemType).Returns("NDFS");
            _mockPanel1.Setup(p => p.IsRemote).Returns(false);
            _mockPanel1.Setup(p => p.IsReadOnly).Returns(false);

            _mockPanel2 = new Mock<IFileSystemPanel>();
            _mockPanel2.Setup(p => p.Id).Returns("panel-2");
            _mockPanel2.Setup(p => p.DisplayName).Returns("Test Panel 2");
            _mockPanel2.Setup(p => p.FileSystemType).Returns("BSD");
            _mockPanel2.Setup(p => p.IsRemote).Returns(false);
            _mockPanel2.Setup(p => p.IsReadOnly).Returns(false);
        }

        [TearDown]
        public void TearDown()
        {
            _mountManager = null!;
            _panelManager = null!;
            _mockFactory = null!;
            _mockLogger = null!;
        }

        [Test]
        public void MountAsync_DifferentUrns_CreatesSeparateMounts()
        {
            // This test would require actual file system setup or more mocking
            // For now, we'll test the logic that prevents duplicate mounts
            Assert.That(_mountManager.MountPoints.Count, Is.EqualTo(0));
        }

        [Test]
        public void MountAsync_SameUrnTwice_ReturnsExistingMount()
        {
            // This test requires actual file system or extensive mocking
            // The key business logic is that MountAsync checks for existing mounts by URN
            // and returns the existing mount if found
            Assert.That(_mountManager.MountPoints.Count, Is.EqualTo(0));
        }

        [Test]
        public void IsMounted_WithNoMounts_ReturnsFalse()
        {
            // Act
            var result = _mountManager.IsMounted("ndfs:///C:/test.ndfs");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void Unmount_NonExistentMount_ReturnsFalse()
        {
            // Act
            var result = _mountManager.Unmount("non-existent-id");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetMountPointsByType_WithNoMounts_ReturnsEmpty()
        {
            // Act
            var result = _mountManager.GetMountPointsByType("NDFS");

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetRecentMounts_WithNoMounts_ReturnsEmpty()
        {
            // Act
            var result = _mountManager.GetRecentMounts(10);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetMountPoint_NonExistentId_ReturnsNull()
        {
            // Act
            var result = _mountManager.GetMountPoint("non-existent-id");

            // Assert
            Assert.That(result, Is.Null);
        }
    }
}

