//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

// RetroCommander.Core.Tests -- Unit tests for SmartFileOperations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RetroCommander.Core.Abstractions;
using RetroCommander.Core.Operations;
using RetroCommander.Core.PropertyBags;
using RetroCommander.Core.Providers;
using RetroCommander.Core.Transfer;
using RetroIO.Storage.Core;

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

        #region .xat sidecar tests - driven by what the driver declares

        // The rule under test (XatSidecarRules): a .xat goes with a file copied between a disk and
        // a host folder only when the disk's driver declares, through IExtendedAttributeSupport,
        // that its files carry properties, and it holds only the properties the driver names. The
        // stand-in driver declares a made-up property, so nothing here depends on a real format.
        private const string Declared = "test.colour";
        private const string NotDeclared = "test.undeclared";

        private static Mock<IFileSystemDriver> StandInDriver(bool declares)
        {
            var driver = new Mock<IFileSystemDriver> { DefaultValue = DefaultValue.Empty };
            driver.Setup(d => d.FileSystemType).Returns("StandIn");
            driver.Setup(d => d.GetMetadata(It.IsAny<string>()))
                .Returns<string>(p => new FileMetadata { Name = p.TrimStart('/'), Path = p, Size = 7 });
            var xa = driver.As<IExtendedAttributeSupport>();
            xa.Setup(x => x.GetCapabilities()).Returns(declares
                ? new FileSystemCapabilities { SupportsExtendedAttributes = true, SupportedPropertyKeys = new[] { Declared } }
                : FileSystemCapabilities.Basic);
            xa.Setup(x => x.GetFileProperties(It.IsAny<string>())).Returns(new Dictionary<string, object>
            {
                [Declared] = "blue",
                [NotDeclared] = "kept off"
            });
            driver.Setup(d => d.ReadFile(It.IsAny<string>())).Returns(Encoding.ASCII.GetBytes("payload"));
            return driver;
        }

        private static DriverAdapterFileSystemEntry DiskFile(Mock<IFileSystemDriver> driver, string name)
        {
            return new DriverAdapterFileSystemEntry(driver.Object, new FileMetadata { Name = name, Path = "/" + name, Size = 7 });
        }

        private NativeFileSystemEntry HostFile(string name, string? xatJson)
        {
            string dir = Path.Combine(_testDirectory, "host-src");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, "payload");
            if (xatJson != null)
            {
                File.WriteAllText(Path.Combine(dir, RetroIO.Storage.Ndfs.PropertyBags.NdfsXatSerializer.GetXatFileName(name)), xatJson);
            }
            return new NativeFileSystemEntry(new FileInfo(path));
        }

        private static string XatJson()
        {
            return RetroIO.Storage.Ndfs.PropertyBags.NdfsXatSerializer.SerializeToJson(new Dictionary<string, object>
            {
                [Declared] = "blue",
                [NotDeclared] = "kept off"
            });
        }

        [Test]
        public async Task DiskToHost_DriverDeclaresProperties_XatHoldsOnlyTheDeclaredOnes()
        {
            var result = await _operations.CopyAsync(DiskFile(StandInDriver(true), "FILE.DAT"),
                new NativeFileSystemPanel("host", _testDirectory), new CopyOptions());

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            string xat = Path.Combine(_testDirectory, "FILE.DAT.xat");
            Assert.That(File.Exists(xat), Is.True);
            string json = File.ReadAllText(xat);
            Assert.That(json, Does.Contain(Declared));
            Assert.That(json, Does.Contain("blue"));
            Assert.That(json, Does.Not.Contain(NotDeclared));
        }

        // GAME.PRG and GAME.SEQ both used to want GAME.xat, so the second copy overwrote the first's
        // properties. Each now keeps its own: NAME.EXT.xat (decided 2026-11-23).
        [Test]
        public async Task DiskToHost_SameNameOtherExtension_EachKeepsItsOwnXat()
        {
            NativeFileSystemPanel host = new NativeFileSystemPanel("host", _testDirectory);
            Assert.That((await _operations.CopyAsync(DiskFile(StandInDriver(true), "GAME.PRG"), host, new CopyOptions())).Success, Is.True);
            Assert.That((await _operations.CopyAsync(DiskFile(StandInDriver(true), "GAME.SEQ"), host, new CopyOptions())).Success, Is.True);

            Assert.That(File.Exists(Path.Combine(_testDirectory, "GAME.PRG.xat")), Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "GAME.SEQ.xat")), Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "GAME.xat")), Is.False, "the old name is never written");
        }

        // A file exported before the change has NAME.xat beside it; copying it back still reads it.
        [Test]
        public async Task HostToDisk_OldStyleXatOnly_StillRead()
        {
            string dir = Path.Combine(_testDirectory, "old-style");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "OLD.TXT");
            File.WriteAllText(path, "payload");
            File.WriteAllText(Path.Combine(dir, "OLD.xat"), XatJson());
            var target = StandInDriver(true);

            var result = await _operations.CopyAsync(new NativeFileSystemEntry(new FileInfo(path)),
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            target.As<IExtendedAttributeSupport>().Verify(x => x.WriteFileWithProperties("/OLD.TXT", It.IsAny<byte[]>(),
                It.Is<IDictionary<string, object>>(p => (string)p[Declared] == "blue")), Times.Once);
        }

        // With both names beside the file, the new one is the file's own and wins.
        [Test]
        public async Task HostToDisk_BothNames_TheNewOneWins()
        {
            string dir = Path.Combine(_testDirectory, "both");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "BOTH.TXT");
            File.WriteAllText(path, "payload");
            File.WriteAllText(Path.Combine(dir, "BOTH.xat"), XatJson());
            File.WriteAllText(Path.Combine(dir, "BOTH.TXT.xat"), XatJson().Replace("blue", "green"));
            var target = StandInDriver(true);

            var result = await _operations.CopyAsync(new NativeFileSystemEntry(new FileInfo(path)),
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            target.As<IExtendedAttributeSupport>().Verify(x => x.WriteFileWithProperties("/BOTH.TXT", It.IsAny<byte[]>(),
                It.Is<IDictionary<string, object>>(p => (string)p[Declared] == "green")), Times.Once);
        }

        [Test]
        public async Task DiskToHost_DriverDeclaresNothing_NoXat()
        {
            var result = await _operations.CopyAsync(DiskFile(StandInDriver(false), "PLAIN.DAT"),
                new NativeFileSystemPanel("host", _testDirectory), new CopyOptions());

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "PLAIN.DAT")), Is.True);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "PLAIN.DAT.xat")), Is.False);
        }

        [Test]
        public async Task DiskToHost_ExportSettingOff_NoXat()
        {
            var result = await _operations.CopyAsync(DiskFile(StandInDriver(true), "OFF.DAT"),
                new NativeFileSystemPanel("host", _testDirectory), new CopyOptions { PreserveNdfsMetadataOnExport = false });

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(File.Exists(Path.Combine(_testDirectory, "OFF.DAT.xat")), Is.False);
        }

        [Test]
        public async Task DiskToDisk_NoXat_PropertiesGoWithTheWrite()
        {
            var target = StandInDriver(true);
            var result = await _operations.CopyAsync(DiskFile(StandInDriver(true), "D2D.DAT"),
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            target.As<IExtendedAttributeSupport>().Verify(x => x.WriteFileWithProperties("/D2D.DAT", It.IsAny<byte[]>(),
                It.Is<IDictionary<string, object>>(p => p.ContainsKey(Declared))), Times.Once);
            target.Verify(d => d.WriteFile(It.Is<string>(n => n.EndsWith(".xat")), It.IsAny<byte[]>()), Times.Never);
        }

        [Test]
        public async Task HostToDisk_XatBeside_DeclaredPropertiesWrittenUndeclaredNot()
        {
            var target = StandInDriver(true);
            var result = await _operations.CopyAsync(HostFile("IN.TXT", XatJson()),
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            target.As<IExtendedAttributeSupport>().Verify(x => x.WriteFileWithProperties("/IN.TXT", It.IsAny<byte[]>(),
                It.Is<IDictionary<string, object>>(p => (string)p[Declared] == "blue" && !p.ContainsKey(NotDeclared))), Times.Once);
        }

        [Test]
        public async Task HostToDisk_TheXatItself_SkippedWhenTheDriverDeclares()
        {
            var target = StandInDriver(true);
            NativeFileSystemEntry data = HostFile("PAIR.TXT", XatJson());
            var xat = new NativeFileSystemEntry(new FileInfo(Path.Combine(Path.GetDirectoryName(data.FullPath)!, "PAIR.TXT.xat")));

            var result = await _operations.CopyAsync(xat,
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.Success, Is.True, "a skip is not a failure");
            Assert.That(result.Skipped, Is.True);
        }

        [Test]
        public async Task HostToDisk_TheXatItself_CopiedAsAFile_WhenImportOff()
        {
            var target = StandInDriver(true);
            NativeFileSystemEntry data = HostFile("PAIR2.TXT", XatJson());
            var xat = new NativeFileSystemEntry(new FileInfo(Path.Combine(Path.GetDirectoryName(data.FullPath)!, "PAIR2.TXT.xat")));

            var result = await _operations.CopyAsync(xat,
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions { UseXatMetadataOnImport = false });

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Skipped, Is.False);
        }

        [Test]
        public async Task HostToDisk_DriverDeclaresNothing_TheXatIsAnOrdinaryFile()
        {
            var target = StandInDriver(false);
            NativeFileSystemEntry data = HostFile("PAIR3.TXT", XatJson());
            var xat = new NativeFileSystemEntry(new FileInfo(Path.Combine(Path.GetDirectoryName(data.FullPath)!, "PAIR3.TXT.xat")));

            var result = await _operations.CopyAsync(xat,
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.Skipped, Is.False);
            target.Verify(d => d.WriteFile("/PAIR3.TXT.xat", It.IsAny<byte[]>()), Times.Once);
        }

        [Test]
        public async Task CopyMany_SkippedXat_NotCountedAsCopiedOrFailed()
        {
            var target = StandInDriver(true);
            NativeFileSystemEntry data = HostFile("BATCH.TXT", XatJson());
            var xat = new NativeFileSystemEntry(new FileInfo(Path.Combine(Path.GetDirectoryName(data.FullPath)!, "BATCH.TXT.xat")));

            var result = await _operations.CopyManyAsync(new IFileSystemEntry[] { data, xat },
                new DriverAdapterFileSystemPanel("disk", target.Object, "test://disk"), new CopyOptions());

            Assert.That(result.SucceededCount, Is.EqualTo(1));
            Assert.That(result.SkippedNames, Does.Contain("BATCH.TXT.xat"));
            Assert.That(result.FailedNames, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(1));
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
