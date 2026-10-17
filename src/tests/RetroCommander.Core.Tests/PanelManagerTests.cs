//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Core.Tests -- Unit tests for PanelManager business logic

using System;
using Moq;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Managers;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for PanelManager business logic.
    /// Tests panel registration, ID generation, and panel retrieval.
    /// </summary>
    [TestFixture]
    public class PanelManagerTests
    {
        private PanelManager _panelManager = null!;
        private Mock<IFileSystemPanel> _mockPanel1;
        private Mock<IFileSystemPanel> _mockPanel2;

        [SetUp]
        public void Setup()
        {
            var driverFactory = new DriverFactory();
            var realFactory = new VolumeProviderFactory(null, driverFactory);
            _panelManager = new PanelManager(realFactory);

            _mockPanel1 = new Mock<IFileSystemPanel>();
            _mockPanel1.Setup(p => p.Id).Returns("test-panel-1");
            _mockPanel1.Setup(p => p.DisplayName).Returns("Test Panel 1");

            _mockPanel2 = new Mock<IFileSystemPanel>();
            _mockPanel2.Setup(p => p.Id).Returns("test-panel-2");
            _mockPanel2.Setup(p => p.DisplayName).Returns("Test Panel 2");
        }

        [TearDown]
        public void TearDown()
        {
            _panelManager = null!;
        }

        [Test]
        public void RegisterPanel_ValidPanel_RegistersSuccessfully()
        {
            // Act
            _panelManager.RegisterPanel(_mockPanel1.Object);

            // Assert
            var retrieved = _panelManager.GetPanel("test-panel-1");
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved, Is.EqualTo(_mockPanel1.Object));
        }

        [Test]
        public void RegisterPanel_NullPanel_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _panelManager.RegisterPanel(null!));
        }

        [Test]
        public void RegisterPanel_MultiplePanels_AllRegistered()
        {
            // Act
            _panelManager.RegisterPanel(_mockPanel1.Object);
            _panelManager.RegisterPanel(_mockPanel2.Object);

            // Assert
            Assert.That(_panelManager.GetPanel("test-panel-1"), Is.Not.Null);
            Assert.That(_panelManager.GetPanel("test-panel-2"), Is.Not.Null);
            Assert.That(_panelManager.AllPanels.Count, Is.EqualTo(2));
        }

        [Test]
        public void RegisterPanel_SameIdTwice_OverwritesPrevious()
        {
            // Arrange
            var mockPanel3 = new Mock<IFileSystemPanel>();
            mockPanel3.Setup(p => p.Id).Returns("test-panel-1");
            mockPanel3.Setup(p => p.DisplayName).Returns("Test Panel 3");

            // Act
            _panelManager.RegisterPanel(_mockPanel1.Object);
            _panelManager.RegisterPanel(mockPanel3.Object);

            // Assert
            var retrieved = _panelManager.GetPanel("test-panel-1");
            Assert.That(retrieved, Is.EqualTo(mockPanel3.Object));
            Assert.That(_panelManager.AllPanels.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetPanel_NonExistentId_ReturnsNull()
        {
            // Act
            var result = _panelManager.GetPanel("non-existent-id");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void RemovePanel_ExistingPanel_RemovesSuccessfully()
        {
            // Arrange
            _panelManager.RegisterPanel(_mockPanel1.Object);
            _panelManager.LeftPanel = _mockPanel1.Object;

            // Act
            var result = _panelManager.RemovePanel("test-panel-1");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_panelManager.GetPanel("test-panel-1"), Is.Null);
            Assert.That(_panelManager.LeftPanel, Is.Null);
        }

        [Test]
        public void RemovePanel_NonExistentPanel_ReturnsFalse()
        {
            // Act
            var result = _panelManager.RemovePanel("non-existent-id");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void RemovePanel_RemovesFromLeftPanel_LeftPanelSetToNull()
        {
            // Arrange
            _panelManager.RegisterPanel(_mockPanel1.Object);
            _panelManager.LeftPanel = _mockPanel1.Object;
            _panelManager.RightPanel = null;

            // Act
            _panelManager.RemovePanel("test-panel-1");

            // Assert
            Assert.That(_panelManager.LeftPanel, Is.Null);
        }

        [Test]
        public void RemovePanel_RemovesFromRightPanel_RightPanelSetToNull()
        {
            // Arrange
            _panelManager.RegisterPanel(_mockPanel1.Object);
            _panelManager.LeftPanel = null;
            _panelManager.RightPanel = _mockPanel1.Object;

            // Act
            _panelManager.RemovePanel("test-panel-1");

            // Assert
            Assert.That(_panelManager.RightPanel, Is.Null);
        }

    }
}

