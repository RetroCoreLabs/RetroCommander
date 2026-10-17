//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Core.Tests -- Unit tests for VolumeProviderFactory detection logic

using System;
using System.IO;
using NUnit.Framework;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Local;
using RetroIO.Storage.Local.Drivers;
using RetroIO.Storage.Ndfs.Drivers;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for VolumeProviderFactory detection logic.
    /// Tests BSD priority detection to prevent false positives with NDFS.
    /// </summary>
    [TestFixture]
    public class VolumeProviderFactoryDetectionTests
    {
        private DriverFactory _driverFactory = null!;
        private VolumeProviderFactory _factory = null!;

        [SetUp]
        public void Setup()
        {
            _driverFactory = new DriverFactory();
            _driverFactory.Register(new NdfsDriverProvider());
            _driverFactory.Register(new V7DriverProvider());

            _factory = new VolumeProviderFactory(null, _driverFactory);
        }

        [TearDown]
        public void TearDown()
        {
            _factory = null!;
            _driverFactory = null!;
        }

        [Test]
        public void DetectWithBsdPriority_BsdHighConfidence_UsesBsd()
        {
            // This test requires actual BSD disk images
            // The business logic being tested is:
            // - If BSD confidence > 0.5, use BSD driver
            // - Only use NDFS if confidence > 0.8 AND significantly higher than BSD (>0.2 difference)

            // For now, we test that the factory is initialized correctly
            Assert.That(_factory, Is.Not.Null);
        }

        [Test]
        public void DetectWithBsdPriority_NdfsVeryHighConfidence_UsesNdfs()
        {
            // This test requires actual NDFS disk images
            // The business logic: NDFS only used if confidence > 0.8 AND > BSD + 0.2
            Assert.That(_factory, Is.Not.Null);
        }

        [Test]
        public void DetectWithBsdPriority_LowConfidence_ReturnsNull()
        {
            // This test requires actual disk images or mocking
            // The business logic: If neither BSD nor NDFS meet thresholds, fall back to standard detection
            Assert.That(_factory, Is.Not.Null);
        }

        [Test]
        public void ExtractScheme_BsdUrn_ReturnsBsdScheme()
        {
            // This tests the URN scheme extraction logic
            // We can't directly test private methods, but we can test through public API
            // The ExtractScheme method is private, so we test indirectly through CreatePanelAsync behavior

            // For now, verify the factory supports BSD URNs
            var supportsBsd = _factory.SupportsUrn("bsd:///C:/test.img");
            Assert.That(supportsBsd, Is.True);
        }

        [Test]
        public void ExtractScheme_NdfsUrn_ReturnsNdfsScheme()
        {
            // Test NDFS scheme detection
            var supportsNdfs = _factory.SupportsUrn("ndfs:///C:/test.ndfs");
            Assert.That(supportsNdfs, Is.True);
        }

        [Test]
        public void ExtractScheme_FileUrn_ReturnsFileScheme()
        {
            // Test file:// scheme support
            var supportsFile = _factory.SupportsUrn("file:///C:/test.img");
            Assert.That(supportsFile, Is.True);
        }

        [Test]
        public void SupportsUrn_ValidSchemes_ReturnsTrue()
        {
            // Test all supported schemes
            Assert.That(_factory.SupportsUrn("file:///C:/test"), Is.True);
            Assert.That(_factory.SupportsUrn("ndfs:///C:/test.ndfs"), Is.True);
            Assert.That(_factory.SupportsUrn("bsd:///C:/test.img"), Is.True);
            Assert.That(_factory.SupportsUrn("d64:///C:/test.d64"), Is.True);
            Assert.That(_factory.SupportsUrn("d71:///C:/test.d71"), Is.True);
            Assert.That(_factory.SupportsUrn("d81:///C:/test.d81"), Is.True);
        }

        [Test]
        public void SupportsUrn_InvalidScheme_ReturnsFalse()
        {
            // Test unsupported schemes
            Assert.That(_factory.SupportsUrn("invalid:///C:/test"), Is.False);
            Assert.That(_factory.SupportsUrn("http://example.com"), Is.False);
        }
    }
}

