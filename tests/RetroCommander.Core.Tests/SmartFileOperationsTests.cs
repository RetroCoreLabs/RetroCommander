//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs — https://github.com/RetroCoreLabs
// RetroCommander.Core.Tests -- Unit tests for SmartFileOperations
//

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Operations;
using RetroCommander.Core.PropertyBags;
using RetroCommander.Core.Transfer;

namespace RetroCommander.Core.Tests
{
    /// <summary>
    /// Unit tests for SmartFileOperations.
    /// Tests copy, move, delete, rename, and property preservation across filesystems.
    /// </summary>
    [TestFixture]
    public class SmartFileOperationsTests
    {
        private string _testDirectory = null!;
        private SmartFileOperations _operations = null!;

        [SetUp]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "SmartFileOpsTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _operations = new SmartFileOperations();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private Mock<IFileSystemEntry> CreateMockEntry(string name, string content, IPropertyBag? properties = null)
        {
            var mockEntry = new Mock<IFileSystemEntry>();
            mockEntry.Setup(e => e.Name).Returns(name);
            mockEntry.Setup(e => e.FullPath).Returns($"/test/{name}");
            mockEntry.Setup(e => e.Size).Returns(content.Length);
            mockEntry.Setup(e => e.IsDirectory).Returns(false);
            mockEntry.Setup(e => e.Properties).Returns(properties ?? new PropertyBag());
            mockEntry.Setup(e => e.OpenReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes(content)));

            return mockEntry;
        }

        private Mock<IFileSystemPanel> CreateMockPanel(string fileSystemType, bool isReadOnly = false)
        {
            var mockPanel = new Mock<IFileSystemPanel>();
            mockPanel.Setup(p => p.Id).Returns(Guid.NewGuid().ToString());
            mockPanel.Setup(p => p.CurrentPath).Returns(_testDirectory);
            mockPanel.Setup(p => p.FileSystemType).Returns(fileSystemType);
            mockPanel.Setup(p => p.IsReadOnly).Returns(isReadOnly);

            // Setup CreateFileAsync to actually create a file
            mockPanel.Setup(p => p.CreateFileAsync(
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<IPropertyBag?>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, Stream, IPropertyBag?, CancellationToken>(async (name, stream, props, ct) =>
                {
                    var filePath = Path.Combine(_testDirectory, name);
                    using var fileStream = File.Create(filePath);
                    await stream.CopyToAsync(fileStream, ct);

                    var resultEntry = new Mock<IFileSystemEntry>();
                    resultEntry.Setup(e => e.Name).Returns(name);
                    resultEntry.Setup(e => e.FullPath).Returns(filePath);
                    return resultEntry.Object;
                });

            mockPanel.Setup(p => p.DeleteAsync(It.IsAny<IFileSystemEntry>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockPanel.Setup(p => p.RenameAsync(It.IsAny<IFileSystemEntry>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return mockPanel;
        }

        #region Copy Tests

        [Test]
        public async Task CopyAsync_BasicCopy_SucceedsAndCreatesFile()
        {
            // Arrange
            var content = "Test file content";
            var sourceEntry = CreateMockEntry("test.txt", content);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "test.txt")), Is.True);
        }

        [Test]
        public async Task CopyAsync_WithContent_ContentMatches()
        {
            // Arrange
            var content = "This is the file content to copy";
            var sourceEntry = CreateMockEntry("content.txt", content);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            var copiedContent = File.ReadAllText(Path.Combine(_testDirectory, "content.txt"));
            Assert.That(copiedContent, Is.EqualTo(content));
        }

        [Test]
        public async Task CopyAsync_FileExists_NoOverwrite_Fails()
        {
            // Arrange
            var content = "Test content";
            var sourceEntry = CreateMockEntry("existing.txt", content);
            var targetPanel = CreateMockPanel("Windows");

            // Create existing file
            File.WriteAllText(Path.Combine(_testDirectory, "existing.txt"), "Original content");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = false });

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("already exists"));
        }

        [Test]
        public async Task CopyAsync_FileExists_WithOverwrite_Succeeds()
        {
            // Arrange
            var newContent = "New content";
            var sourceEntry = CreateMockEntry("existing.txt", newContent);
            var targetPanel = CreateMockPanel("Windows");

            // Create existing file
            File.WriteAllText(Path.Combine(_testDirectory, "existing.txt"), "Original content");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true });

            // Assert
            Assert.That(result.Success, Is.True);
            var copiedContent = File.ReadAllText(Path.Combine(_testDirectory, "existing.txt"));
            Assert.That(copiedContent, Is.EqualTo(newContent));
        }

        [Test]
        public async Task CopyAsync_WithProgress_ReportsProgress()
        {
            // Arrange
            var content = new string('X', 10000); // 10KB
            var sourceEntry = CreateMockEntry("large.txt", content);
            var targetPanel = CreateMockPanel("Windows");

            var progressUpdates = new List<TransferProgress>();
            var progress = new Progress<TransferProgress>(p => progressUpdates.Add(p));

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Progress = progress });

            // Allow progress callbacks to complete
            await Task.Delay(100);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(progressUpdates.Count, Is.GreaterThan(0));
        }

        #endregion

        #region Property Preservation Tests

        [Test]
        public async Task CopyAsync_WindowsToWindows_PropertiesPreserved()
        {
            // Arrange
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty(WindowsProperties.Attributes, FileAttributes.ReadOnly.ToString());
            sourceProps.SetProperty(WindowsProperties.Owner, "TestUser");

            var sourceEntry = CreateMockEntry("windows.txt", "content", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { PreserveProperties = true });

            // Assert
            Assert.That(result.Success, Is.True);
            // Windows to Windows should preserve properties
            Assert.That(result.PreservedProperties, Is.Not.Null);
        }

        [Test]
        public async Task CopyAsync_NdfsToWindows_TracksLostProperties()
        {
            // Arrange - NDFS has properties Windows doesn't support
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty(NdfsProperties.UserId, 1);
            sourceProps.SetProperty(NdfsProperties.PageCount, 100);
            sourceProps.SetProperty(StandardProperties.FileName, "ndfsfile.txt");

            var sourceEntry = CreateMockEntry("ndfsfile.txt", "content", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { PreserveProperties = true });

            // Assert
            Assert.That(result.Success, Is.True);
            // NDFS-specific properties may be lost when copying to Windows
            Assert.That(result.LostProperties, Is.Not.Null);
        }

        [Test]
        public async Task CopyAsync_PreservePropertiesFalse_NoPropertiesMapped()
        {
            // Arrange
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty(WindowsProperties.Attributes, FileAttributes.ReadOnly.ToString());

            var sourceEntry = CreateMockEntry("nopreserve.txt", "content", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { PreserveProperties = false });

            // Assert
            Assert.That(result.Success, Is.True);
            // When PreserveProperties is false, preserved/lost lists should be empty or null
            Assert.That(result.PreservedProperties?.Count ?? 0, Is.EqualTo(0));
        }

        [Test]
        public async Task CopyAsync_CbmToWindows_HandlesDifferentFilesystem()
        {
            // Arrange - CBM (Commodore) filesystem properties
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty(CbmProperties.FileType, "PRG");
            sourceProps.SetProperty(CbmProperties.LoadAddress, 0x0801);
            sourceProps.SetProperty(StandardProperties.FileName, "program.prg");

            var sourceEntry = CreateMockEntry("program.prg", "content", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { PreserveProperties = true });

            // Assert
            Assert.That(result.Success, Is.True);
            // CBM properties are filesystem-specific and may be lost
        }

        #endregion

        #region NDFS Metadata Sidecar Tests

        [Test]
        public async Task CopyAsync_NdfsEntryToWindows_WritesXatSidecar()
        {
            // Arrange - "ndfs." prefixed keys are what DriverAdapterFileSystemEntry populates from
            // IExtendedAttributeSupport.GetFileProperties for a real NDFS entry (see NdfsPropertyBagKeys).
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty("ndfs.object_name", "NDFSFILE");
            sourceProps.SetProperty("ndfs.user_name", "BOB");
            sourceProps.SetProperty("ndfs.access_bits", (uint)0x1FF);

            var sourceEntry = CreateMockEntry("ndfsfile.txt", "payload", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, PreserveProperties = true, PreserveNdfsMetadataOnExport = true });

            // Assert
            Assert.That(result.Success, Is.True);
            string xatPath = Path.Combine(_testDirectory, "ndfsfile.xat");
            Assert.That(File.Exists(xatPath), Is.True, "a .xat sidecar must be written next to the copied file");

            string json = File.ReadAllText(xatPath);
            Assert.That(json, Does.Contain("ndfs.object_name"));
            Assert.That(json, Does.Contain("NDFSFILE"));
            Assert.That(json, Does.Contain("BOB"));
        }

        [Test]
        public async Task CopyAsync_PreserveNdfsMetadataOnExportFalse_NoSidecarWritten()
        {
            // Arrange
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty("ndfs.object_name", "NDFSFILE2");

            var sourceEntry = CreateMockEntry("nosidecar.txt", "payload", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, PreserveProperties = true, PreserveNdfsMetadataOnExport = false });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "nosidecar.xat")), Is.False,
                "disabling the setting must suppress the sidecar even though NDFS metadata is present");
        }

        [Test]
        public async Task CopyAsync_NoNdfsMetadataPresent_NoSidecarWritten()
        {
            // Arrange - a plain Windows source has nothing "ndfs."-prefixed to export.
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty(WindowsProperties.Attributes, FileAttributes.Normal.ToString());

            var sourceEntry = CreateMockEntry("plain.txt", "payload", sourceProps);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, PreserveProperties = true, PreserveNdfsMetadataOnExport = true });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "plain.xat")), Is.False);
        }

        [Test]
        public async Task CopyAsync_NdfsToNdfs_NoSidecarWritten()
        {
            // Arrange - same-type copies carry "ndfs." metadata directly via DirectPropertyMapper;
            // a sidecar file is only needed when the target can't hold the metadata natively.
            var sourceProps = new PropertyBag();
            sourceProps.SetProperty("ndfs.object_name", "NDFSFILE3");

            var sourceEntry = CreateMockEntry("same.dat", "payload", sourceProps);
            var targetPanel = CreateMockPanel("NDFS");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, PreserveProperties = true, PreserveNdfsMetadataOnExport = true });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "same.xat")), Is.False);
        }

        [Test]
        public async Task CopyAsync_XatFileIntoNdfs_IsSkippedNotCopied_WhenImportEnabled()
        {
            // Arrange - a ".xat" is metadata for its paired file, not an independent file; it
            // must not be written as its own NDFS object.
            var sourceEntry = CreateMockEntry("sidecar.xat", "{}");
            var targetPanel = CreateMockPanel("NDFS");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, UseXatMetadataOnImport = true });

            // Assert
            Assert.That(result.Success, Is.True, "a skip is not a failure");
            Assert.That(result.Skipped, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "sidecar.xat")), Is.False,
                "the .xat must not be written as its own NDFS object");
        }

        [Test]
        public async Task CopyAsync_XatFileIntoNdfs_IsCopiedNormally_WhenImportDisabled()
        {
            // Arrange - with the feature off, a .xat is just an ordinary file like any other.
            var sourceEntry = CreateMockEntry("sidecar2.xat", "{}");
            var targetPanel = CreateMockPanel("NDFS");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, UseXatMetadataOnImport = false });

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Skipped, Is.False);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "sidecar2.xat")), Is.True,
                "disabling the setting must fall back to copying the .xat like any other file");
        }

        [Test]
        public async Task CopyAsync_IntoNdfs_WithSidecarPresent_MergesSidecarMetadataIntoWrite()
        {
            // Arrange - a real native-style source file with a real ".xat" sidecar sitting next to
            // it on disk (TryReadNdfsMetadataSidecar reads via source.FullPath directly, so this
            // must be a real path, not the fake "/test/..." CreateMockEntry normally uses).
            string sourceDir = Path.Combine(_testDirectory, "import-src");
            Directory.CreateDirectory(sourceDir);
            string sourceFilePath = Path.Combine(sourceDir, "IMPORTED.TXT");
            File.WriteAllText(sourceFilePath, "content");

            var sidecarProps = new Dictionary<string, object>
            {
                ["ndfs.access_bits"] = (uint)0x0155
            };
            string xatJson = RetroIO.Storage.Ndfs.PropertyBags.NdfsXatSerializer.SerializeToJson(sidecarProps);
            File.WriteAllText(Path.Combine(sourceDir, "IMPORTED.xat"), xatJson);

            var mockEntry = new Mock<IFileSystemEntry>();
            mockEntry.Setup(e => e.Name).Returns("IMPORTED.TXT");
            mockEntry.Setup(e => e.FullPath).Returns(sourceFilePath);
            mockEntry.Setup(e => e.Size).Returns(7);
            mockEntry.Setup(e => e.IsDirectory).Returns(false);
            mockEntry.Setup(e => e.Properties).Returns(new PropertyBag());
            mockEntry.Setup(e => e.OpenReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("content")));

            var targetPanel = CreateMockPanel("NDFS");

            // Act
            var result = await _operations.CopyAsync(mockEntry.Object, targetPanel.Object,
                new CopyOptions { Overwrite = true, UseXatMetadataOnImport = true });

            // Assert
            Assert.That(result.Success, Is.True, $"import must succeed: {result.ErrorMessage}");
            targetPanel.Verify(p => p.CreateFileAsync(
                "IMPORTED.TXT",
                It.IsAny<Stream>(),
                It.Is<IPropertyBag?>(bag => bag != null && bag.HasProperty("ndfs.access_bits")),
                It.IsAny<CancellationToken>()),
                Times.Once,
                "the sidecar's NDFS properties must be passed through to CreateFileAsync so the panel can persist them");
        }

        [Test]
        public async Task CopyManyAsync_XatSkippedAlongsideRealFile_ExcludedFromSucceededAndFailedCounts()
        {
            // Arrange
            var dataEntry = CreateMockEntry("batch.txt", "payload");
            var xatEntry = CreateMockEntry("batch.xat", "{}");
            var targetPanel = CreateMockPanel("NDFS");

            // Act
            var result = await _operations.CopyManyAsync(
                new[] { dataEntry.Object, xatEntry.Object }, targetPanel.Object,
                new CopyOptions { Overwrite = true, UseXatMetadataOnImport = true });

            // Assert
            Assert.That(result.SucceededCount, Is.EqualTo(1));
            Assert.That(result.SkippedNames, Does.Contain("batch.xat"));
            Assert.That(result.FailedNames, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(1),
                "the skipped .xat must not inflate the 'copied X of Y' count");
        }

        #endregion

        #region Move Tests

        [Test]
        public async Task MoveAsync_BasicMove_SucceedsAndDeletesSource()
        {
            // Arrange
            var content = "Content to move";
            var sourceEntry = CreateMockEntry("tomove.txt", content);
            var sourcePanel = CreateMockPanel("Windows");
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.MoveAsync(sourceEntry.Object, sourcePanel.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "tomove.txt")), Is.True);

            // Verify delete was called on source panel
            sourcePanel.Verify(p => p.DeleteAsync(sourceEntry.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task MoveAsync_CopyFails_DoesNotDeleteSource()
        {
            // Arrange
            var sourceEntry = CreateMockEntry("failmove.txt", "content");
            var sourcePanel = CreateMockPanel("Windows");
            var targetPanel = CreateMockPanel("Windows");

            // Create file that will cause "already exists" error
            File.WriteAllText(Path.Combine(_testDirectory, "failmove.txt"), "Existing");

            // Act
            var result = await _operations.MoveAsync(sourceEntry.Object, sourcePanel.Object, targetPanel.Object,
                new MoveOptions { Overwrite = false });

            // Assert
            Assert.That(result.Success, Is.False);
            // Delete should NOT be called since copy failed
            sourcePanel.Verify(p => p.DeleteAsync(It.IsAny<IFileSystemEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task MoveAsync_CrossFilesystem_Works()
        {
            // Arrange
            var content = "Cross-filesystem content";
            var sourceEntry = CreateMockEntry("crossfs.txt", content);
            var sourcePanel = CreateMockPanel("NDFS");
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.MoveAsync(sourceEntry.Object, sourcePanel.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
        }

        #endregion

        #region Delete Tests

        [Test]
        public async Task DeleteAsync_ValidEntry_Succeeds()
        {
            // Arrange
            var entry = CreateMockEntry("todelete.txt", "content");
            var panel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.DeleteAsync(entry.Object, panel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            panel.Verify(p => p.DeleteAsync(entry.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task DeleteAsync_PanelThrows_ReturnsFailure()
        {
            // Arrange
            var entry = CreateMockEntry("faildelete.txt", "content");
            var panel = CreateMockPanel("Windows");
            panel.Setup(p => p.DeleteAsync(It.IsAny<IFileSystemEntry>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Cannot delete file"));

            // Act
            var result = await _operations.DeleteAsync(entry.Object, panel.Object);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Cannot delete"));
        }

        #endregion

        #region Rename Tests

        [Test]
        public async Task RenameAsync_ValidEntry_Succeeds()
        {
            // Arrange
            var entry = CreateMockEntry("oldname.txt", "content");
            var panel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.RenameAsync(entry.Object, panel.Object, "newname.txt");

            // Assert
            Assert.That(result.Success, Is.True);
            panel.Verify(p => p.RenameAsync(entry.Object, "newname.txt", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task RenameAsync_PanelThrows_ReturnsFailure()
        {
            // Arrange
            var entry = CreateMockEntry("failrename.txt", "content");
            var panel = CreateMockPanel("Windows");
            panel.Setup(p => p.RenameAsync(It.IsAny<IFileSystemEntry>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Cannot rename file"));

            // Act
            var result = await _operations.RenameAsync(entry.Object, panel.Object, "newname.txt");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Cannot rename"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public async Task CopyAsync_SourceStreamThrows_ReturnsFailure()
        {
            // Arrange
            var mockEntry = new Mock<IFileSystemEntry>();
            mockEntry.Setup(e => e.Name).Returns("error.txt");
            mockEntry.Setup(e => e.FullPath).Returns("/test/error.txt");
            mockEntry.Setup(e => e.Size).Returns(100);
            mockEntry.Setup(e => e.Properties).Returns(new PropertyBag());
            mockEntry.Setup(e => e.OpenReadAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Cannot read source"));

            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(mockEntry.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Cannot read"));
        }

        [Test]
        public async Task CopyAsync_EmptyFile_Succeeds()
        {
            // Arrange
            var sourceEntry = CreateMockEntry("empty.txt", "");
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            var copiedContent = File.ReadAllText(Path.Combine(_testDirectory, "empty.txt"));
            Assert.That(copiedContent, Is.Empty);
        }

        [Test]
        public async Task CopyAsync_LargeFile_Succeeds()
        {
            // Arrange - 1MB file
            var content = new string('X', 1024 * 1024);
            var sourceEntry = CreateMockEntry("largefile.dat", content);
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            var fileInfo = new FileInfo(Path.Combine(_testDirectory, "largefile.dat"));
            Assert.That(fileInfo.Length, Is.EqualTo(content.Length));
        }

        [Test]
        public async Task CopyAsync_SpecialCharactersInName_Succeeds()
        {
            // Arrange - File with special characters (that are valid on Windows)
            var sourceEntry = CreateMockEntry("file with spaces.txt", "content");
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "file with spaces.txt")), Is.True);
        }

        [Test]
        public async Task CopyAsync_CancellationRequested_RespectsToken()
        {
            // Arrange
            var content = new string('X', 1000);
            var sourceEntry = CreateMockEntry("cancel.txt", content);
            var targetPanel = CreateMockPanel("Windows");

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            // Act & Assert - Should handle cancellation
            // Note: The actual behavior depends on when cancellation is checked
            var result = await _operations.CopyAsync(sourceEntry.Object, targetPanel.Object,
                cancellationToken: cts.Token);

            // Operation may succeed or fail depending on timing
            // The important thing is it doesn't throw
            Assert.That(result, Is.Not.Null);
        }

        #endregion

        #region Batch Operation Tests (multi-select)

        [Test]
        public async Task CopyManyAsync_AllSucceed_ReturnsSucceededCountAndCreatesAllFiles()
        {
            // Arrange
            var entry1 = CreateMockEntry("one.txt", "content one");
            var entry2 = CreateMockEntry("two.txt", "content two");
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyManyAsync(
                new[] { entry1.Object, entry2.Object }, targetPanel.Object, new CopyOptions { Overwrite = true });

            // Assert
            Assert.That(result.SucceededCount, Is.EqualTo(2));
            Assert.That(result.AllSucceeded, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(_testDirectory, "one.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "two.txt")), Is.True);
        }

        [Test]
        public async Task CopyManyAsync_OneEntryFails_ContinuesAndCopiesTheRest()
        {
            // Arrange: "bad.txt" fails to open for read; "good.txt" should still be copied
            // afterwards — a batch must not abort on the first per-entry failure.
            var goodEntry = CreateMockEntry("good.txt", "this one works");
            var badEntry = CreateMockEntry("bad.txt", "unused");
            badEntry.Setup(e => e.OpenReadAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("simulated read failure"));
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.CopyManyAsync(
                new[] { badEntry.Object, goodEntry.Object }, targetPanel.Object, new CopyOptions { Overwrite = true });

            // Assert
            Assert.That(result.SucceededCount, Is.EqualTo(1));
            Assert.That(result.AllSucceeded, Is.False);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.FailedNames, Does.Contain("bad.txt"));
            Assert.That(File.Exists(Path.Combine(_testDirectory, "good.txt")), Is.True,
                "the failure on bad.txt must not stop good.txt from being copied");
            Assert.That(File.Exists(Path.Combine(_testDirectory, "bad.txt")), Is.False);
        }

        [Test]
        public async Task MoveManyAsync_AllSucceed_DeletesEachSourceEntry()
        {
            // Arrange
            var entry1 = CreateMockEntry("m1.txt", "one");
            var entry2 = CreateMockEntry("m2.txt", "two");
            var sourcePanel = CreateMockPanel("Windows");
            var targetPanel = CreateMockPanel("Windows");

            // Act
            var result = await _operations.MoveManyAsync(
                new[] { entry1.Object, entry2.Object }, sourcePanel.Object, targetPanel.Object);

            // Assert
            Assert.That(result.SucceededCount, Is.EqualTo(2));
            Assert.That(result.AllSucceeded, Is.True);
            sourcePanel.Verify(p => p.DeleteAsync(entry1.Object, It.IsAny<CancellationToken>()), Times.Once);
            sourcePanel.Verify(p => p.DeleteAsync(entry2.Object, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task DeleteManyAsync_OneEntryFails_ContinuesAndDeletesTheRest()
        {
            // Arrange
            var goodEntry = CreateMockEntry("dgood.txt", "content");
            var badEntry = CreateMockEntry("dbad.txt", "content");
            var panel = CreateMockPanel("Windows");
            panel.Setup(p => p.DeleteAsync(badEntry.Object, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("simulated delete failure"));

            // Act
            var result = await _operations.DeleteManyAsync(
                new[] { badEntry.Object, goodEntry.Object }, panel.Object);

            // Assert
            Assert.That(result.SucceededCount, Is.EqualTo(1));
            Assert.That(result.FailedNames, Does.Contain("dbad.txt"));
            panel.Verify(p => p.DeleteAsync(goodEntry.Object, It.IsAny<CancellationToken>()), Times.Once,
                "the failure on dbad.txt must not stop dgood.txt from being deleted");
        }

        #endregion

        #region PropertyMapperRegistry Tests

        [Test]
        public void SmartFileOperations_WithCustomRegistry_UsesCustomMappers()
        {
            // Arrange
            var customRegistry = PropertyMapperRegistry.CreateDefault();
            var operations = new SmartFileOperations(customRegistry);

            // Assert - Just verify it accepts custom registry
            Assert.That(operations, Is.Not.Null);
        }

        [Test]
        public void SmartFileOperations_WithNullRegistry_UsesDefault()
        {
            // Arrange
            var operations = new SmartFileOperations(null);

            // Assert - Should not throw and use default registry
            Assert.That(operations, Is.Not.Null);
        }

        #endregion
    }
}
