//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// RetroCore Labs - https://github.com/RetroCoreLabs/RetroCommander
// RetroCommander - Dual-pane file manager for retro disk images
//

using System.IO;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using RetroCommander.Core.Providers;
using RetroIO.Storage.Core;

namespace RetroCommander.Tests
{
    /// <summary>
    /// The folder a disk panel is in, however it was typed, gives clean paths for new files.
    /// </summary>
    /// <remarks>
    /// Found 2026-11-23: copy-in to a folder typed as "/SYSTEM/" made the path "/SYSTEM//TPE:BPUN",
    /// and the NDFS driver of the day stored the file under the name "/TPE:BPUN".
    /// </remarks>
    [TestFixture]
    public sealed class DriverAdapterPathTests
    {
        [TestCase("/SYSTEM/", "/SYSTEM")]
        [TestCase("/SYSTEM//", "/SYSTEM")]
        [TestCase("SYSTEM", "/SYSTEM")]
        [TestCase("\\SYSTEM\\SUB\\", "/SYSTEM/SUB")]
        [TestCase("//SYSTEM//SUB", "/SYSTEM/SUB")]
        [TestCase("/", "/")]
        [TestCase("//", "/")]
        [TestCase("", "/")]
        public async Task Navigate_AnyWayTheFolderIsWritten_OneCleanPath(string typed, string expected)
        {
            var driver = new Mock<IFileSystemDriver> { DefaultValue = DefaultValue.Empty };
            driver.Setup(d => d.ListDirectory(It.IsAny<string>())).Returns(System.Array.Empty<DirectoryEntry>());
            driver.Setup(d => d.GetMetadata(It.IsAny<string>()))
                .Returns<string>(p => new FileMetadata { Name = "TPE:BPUN", Path = p, Size = 1 });
            var panel = new DriverAdapterFileSystemPanel("disk", driver.Object, "test://disk");

            await panel.NavigateAsync(typed);
            Assert.That(panel.CurrentPath, Is.EqualTo(expected));

            using var content = new MemoryStream(new byte[] { 1 });
            await panel.CreateFileAsync("TPE:BPUN", content);
            string file = expected == "/" ? "/TPE:BPUN" : expected + "/TPE:BPUN";
            driver.Verify(d => d.WriteFile(file, It.IsAny<byte[]>()), Times.Once);
        }
    }
}
