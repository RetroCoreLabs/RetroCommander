//
// SPDX-License-Identifier: MIT
// Copyright (c) 1985-2026 Ronny Hansen
// HackerCorp Labs — https://github.com/HackerCorpLabs
// RetroFS -- Accessing yesterday's data with today's standards"
//

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RetroCommander.Desktop.ViewModels;

namespace RetroCommander.Desktop.Tests
{
    /// <summary>
    /// Tests for the Favorites feature: the on-disk store round-trips, and the sidebar view-model
    /// adds / dedupes / removes / persists / restores favorites correctly.
    ///
    /// Every test redirects <see cref="FavoritesStore"/> to a throwaway temp directory via the
    /// internal DirectoryOverride seam, so the real user's %APPDATA% favorites file is never touched.
    /// </summary>
    [TestFixture]
    [Category("Favorites")]
    public class FavoritesTests
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "rfs_fav_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            // Redirect all favorites IO into the temp dir for the duration of the test.
            FavoritesStore.DirectoryOverride = _tempDir;
        }

        [TearDown]
        public void TearDown()
        {
            FavoritesStore.DirectoryOverride = null;
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        // -------- FavoritesStore --------

        [Test]
        public void Store_Load_WhenNoFile_ReturnsEmpty()
        {
            List<FavoriteRecord> loaded = FavoritesStore.Load();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Count, Is.EqualTo(0));
        }

        [Test]
        public void Store_SaveThenLoad_RoundTrips()
        {
            var records = new List<FavoriteRecord>
            {
                new FavoriteRecord
                {
                    Name = "Games [D64]",
                    Path = "file:///E:/disks/games.d64",
                    FilesystemType = "D64",
                    OriginType = "Mount"
                },
                new FavoriteRecord
                {
                    Name = "Projects",
                    Path = @"C:\Users\me\Projects",
                    FilesystemType = "",
                    OriginType = "Recent"
                }
            };

            FavoritesStore.Save(records);
            List<FavoriteRecord> loaded = FavoritesStore.Load();

            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded[0].Name, Is.EqualTo("Games [D64]"));
            Assert.That(loaded[0].Path, Is.EqualTo("file:///E:/disks/games.d64"));
            Assert.That(loaded[0].FilesystemType, Is.EqualTo("D64"));
            Assert.That(loaded[0].OriginType, Is.EqualTo("Mount"));
            Assert.That(loaded[1].Path, Is.EqualTo(@"C:\Users\me\Projects"));
            Assert.That(loaded[1].OriginType, Is.EqualTo("Recent"));
        }

        // -------- SidebarItemViewModel.CreateFavorite --------

        [Test]
        public void CreateFavorite_SetsTypeAndOrigin()
        {
            SidebarItemViewModel fav = SidebarItemViewModel.CreateFavorite(
                "Games [D64]", "file:///E:/disks/games.d64", "D64", SidebarItemType.Mount);

            Assert.That(fav.ItemType, Is.EqualTo(SidebarItemType.Favorite));
            Assert.That(fav.OriginType, Is.EqualTo(SidebarItemType.Mount));
            Assert.That(fav.IsPinned, Is.True);
            Assert.That(fav.FilesystemType, Is.EqualTo("D64"));
            Assert.That(fav.Path, Is.EqualTo("file:///E:/disks/games.d64"));
        }

        // -------- NavigationSidebarViewModel --------

        [Test]
        public void Vm_AddFavorite_AppearsAndIsPersisted()
        {
            NavigationSidebarViewModel vm = new NavigationSidebarViewModel();
            Assert.That(vm.FavoritesSection.Items.Count, Is.EqualTo(0), "no favorites at start");

            vm.AddFavorite("Games [D64]", "file:///E:/disks/games.d64", "D64", SidebarItemType.Mount);

            Assert.That(vm.FavoritesSection.Items.Count, Is.EqualTo(1));
            Assert.That(vm.IsFavorite("file:///E:/disks/games.d64"), Is.True);

            // Persisted to disk.
            List<FavoriteRecord> onDisk = FavoritesStore.Load();
            Assert.That(onDisk.Count, Is.EqualTo(1));
            Assert.That(onDisk[0].OriginType, Is.EqualTo("Mount"));
        }

        [Test]
        public void Vm_AddFavorite_IsCaseInsensitiveDuplicateSafe()
        {
            NavigationSidebarViewModel vm = new NavigationSidebarViewModel();

            vm.AddFavorite("Games", "file:///E:/disks/games.d64", "D64", SidebarItemType.Mount);
            // Same path, different case -> must not add a second entry.
            vm.AddFavorite("Games again", "FILE:///E:/DISKS/GAMES.D64", "D64", SidebarItemType.Mount);

            Assert.That(vm.FavoritesSection.Items.Count, Is.EqualTo(1), "duplicate path is ignored");
        }

        [Test]
        public void Vm_RemoveFavorite_RemovesAndPersists()
        {
            NavigationSidebarViewModel vm = new NavigationSidebarViewModel();
            vm.AddFavorite("Games", "file:///E:/disks/games.d64", "D64", SidebarItemType.Mount);
            Assert.That(vm.IsFavorite("file:///E:/disks/games.d64"), Is.True);

            vm.RemoveFavorite("file:///E:/disks/games.d64");

            Assert.That(vm.FavoritesSection.Items.Count, Is.EqualTo(0));
            Assert.That(vm.IsFavorite("file:///E:/disks/games.d64"), Is.False);
            Assert.That(FavoritesStore.Load().Count, Is.EqualTo(0), "removal is persisted");
        }

        [Test]
        public void Vm_RestoresFavoritesOnConstruction()
        {
            // First "session": add two favorites.
            NavigationSidebarViewModel first = new NavigationSidebarViewModel();
            first.AddFavorite("Games", "file:///E:/disks/games.d64", "D64", SidebarItemType.Mount);
            first.AddFavorite("Projects", @"C:\Users\me\Projects", "", SidebarItemType.Recent);

            // Second "session": a fresh view-model must reload them from disk.
            NavigationSidebarViewModel second = new NavigationSidebarViewModel();

            Assert.That(second.FavoritesSection.Items.Count, Is.EqualTo(2));
            Assert.That(second.IsFavorite("file:///E:/disks/games.d64"), Is.True);
            Assert.That(second.IsFavorite(@"C:\Users\me\Projects"), Is.True);

            // OriginType survives the round-trip (drives re-mount vs navigate at click time).
            SidebarItemViewModel? gamesFav = null;
            for (int i = 0; i < second.FavoritesSection.Items.Count; i++)
            {
                if (second.FavoritesSection.Items[i].Path == "file:///E:/disks/games.d64")
                    gamesFav = second.FavoritesSection.Items[i];
            }
            Assert.That(gamesFav, Is.Not.Null);
            Assert.That(gamesFav!.OriginType, Is.EqualTo(SidebarItemType.Mount));
        }
    }
}
