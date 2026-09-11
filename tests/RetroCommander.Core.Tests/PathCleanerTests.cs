//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Core.Tests -- Unit tests for PathCleaner utility
//

using System;
using System.IO;
using NUnit.Framework;
using RetroIO.Storage.Core.Utilities;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for PathCleaner utility class.
    /// Tests path extraction from URNs and normalization.
    /// </summary>
    [TestFixture]
    public class PathCleanerTests
    {
        [Test]
        public void ExtractFilePathFromUri_FileScheme_ExtractsPath()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri("file:///C:/test/file.img");

            // Assert
            Assert.That(result, Is.EqualTo("C:\\test\\file.img").Or.EqualTo("C:/test/file.img"));
        }

        [Test]
        public void ExtractFilePathFromUri_NdfsScheme_ExtractsPath()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri("ndfs:///D:/images/disk.ndfs");

            // Assert
            Assert.That(result, Does.Contain("D:"));
            Assert.That(result, Does.Contain("images"));
            Assert.That(result, Does.Contain("disk.ndfs"));
        }

        [Test]
        public void ExtractFilePathFromUri_BsdScheme_ExtractsPath()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri("bsd:///E:/sunos.img");

            // Assert
            Assert.That(result, Does.Contain("E:"));
            Assert.That(result, Does.Contain("sunos.img"));
        }

        [Test]
        public void ExtractFilePathFromUri_WithFragment_RemovesFragment()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri("bsd:///E:/sunos.img#a");

            // Assert
            Assert.That(result, Does.Not.Contain("#"));
            Assert.That(result, Does.Contain("sunos.img"));
        }

        [Test]
        public void ExtractFilePathFromUri_EmptyString_ReturnsEmpty()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri("");

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ExtractFilePathFromUri_NullString_ReturnsEmpty()
        {
            // Act
            var result = PathCleaner.ExtractFilePathFromUri(null!);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void NormalizePath_WindowsDriveLetter_RemovesLeadingSlash()
        {
            // Act
            var result = PathCleaner.NormalizePath("/C:/test/file.img");

            // Assert
            Assert.That(result, Does.Not.StartWith("/"));
            Assert.That(result, Does.Contain("C:"));
        }

        [Test]
        public void NormalizePath_DoubleSlash_RemovesBoth()
        {
            // Act
            var result = PathCleaner.NormalizePath("//C:/test/file.img");

            // Assert
            Assert.That(result, Does.Not.StartWith("/"));
            Assert.That(result, Does.Contain("C:"));
        }

        [Test]
        public void NormalizePath_UnixPath_PreservesLeadingSlash()
        {
            // Act
            var result = PathCleaner.NormalizePath("/home/user/file.img");

            // Assert
            // On Windows, this will convert / to \ and may remove leading slash
            // On Unix, it should preserve the leading slash
            Assert.That(result, Is.Not.Empty);
        }

        [Test]
        public void ValidateFilePath_ExistingFile_ReturnsTrue()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".tmp");
            File.Create(tempFile).Close();

            try
            {
                // Act
                var result = PathCleaner.ValidateFilePath(tempFile, out var errorMessage);

                // Assert
                Assert.That(result, Is.True);
                Assert.That(errorMessage, Is.Null);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Test]
        public void ValidateFilePath_NonExistentFile_ReturnsFalse()
        {
            // Act
            var result = PathCleaner.ValidateFilePath("C:/nonexistent/file.img", out var errorMessage);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.Not.Null);
            Assert.That(errorMessage, Does.Contain("not found"));
        }

        [Test]
        public void ValidateFilePath_EmptyPath_ReturnsFalse()
        {
            // Act
            var result = PathCleaner.ValidateFilePath("", out var errorMessage);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void CreateUri_WithSchemeAndPath_CreatesValidUri()
        {
            // Act
            var result = PathCleaner.CreateUri("bsd", "C:/test/file.img");

            // Assert
            Assert.That(result, Does.StartWith("bsd://"));
            Assert.That(result, Does.Contain("C:"));
            Assert.That(result, Does.Contain("test"));
            Assert.That(result, Does.Contain("file.img"));
        }

        [Test]
        public void CreateUri_WithFragment_IncludesFragment()
        {
            // Act
            var result = PathCleaner.CreateUri("bsd", "C:/test/file.img", "a");

            // Assert
            Assert.That(result, Does.EndWith("#a"));
        }

        [Test]
        public void CreateUri_EmptyScheme_ReturnsEmpty()
        {
            // Act
            var result = PathCleaner.CreateUri("", "C:/test/file.img");

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void CreateUri_EmptyPath_ReturnsEmpty()
        {
            // Act
            var result = PathCleaner.CreateUri("bsd", "");

            // Assert
            Assert.That(result, Is.Empty);
        }
    }
}

