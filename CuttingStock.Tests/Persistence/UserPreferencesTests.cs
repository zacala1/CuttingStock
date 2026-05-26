using System.IO;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Persistence;

namespace CuttingStock.Tests.Persistence
{
    /// <summary>
    /// Round-trip + edge-case tests for <see cref="UserPreferences"/> /
    /// <see cref="UserPreferencesStore"/>. Validates defaults, JSON round-trip,
    /// missing-file fallback, corrupt-file fallback, and the MRU push helper.
    /// </summary>
    [TestFixture]
    public class UserPreferencesTests
    {
        private string _tempPath = null!;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"prefs-test-{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var prefs = UserPreferencesStore.Load(_tempPath);

            prefs.WindowWidth.Should().BeGreaterThan(200);
            prefs.WindowHeight.Should().BeGreaterThan(200);
            prefs.LastTopTabIndex.Should().Be(0);
            prefs.Recent1D.Should().BeEmpty();
            prefs.Recent2D.Should().BeEmpty();
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesEveryField()
        {
            var original = new UserPreferences
            {
                WindowWidth = 1500,
                WindowHeight = 900,
                WindowLeft = 100,
                WindowTop = 150,
                WindowMaximized = false,
                LastTopTabIndex = 1,
                LastAlgorithm1D = 2,
                LastAlgorithm2D = 1,
                Recent1D = { "C:/a.cstock1d.json", "C:/b.cstock1d.json" },
                Recent2D = { "C:/c.cstock2d.json" },
            };

            UserPreferencesStore.Save(original, _tempPath);
            var loaded = UserPreferencesStore.Load(_tempPath);

            loaded.WindowWidth.Should().Be(1500);
            loaded.WindowHeight.Should().Be(900);
            loaded.WindowLeft.Should().Be(100);
            loaded.WindowTop.Should().Be(150);
            loaded.LastTopTabIndex.Should().Be(1);
            loaded.LastAlgorithm1D.Should().Be(2);
            loaded.LastAlgorithm2D.Should().Be(1);
            loaded.Recent1D.Should().Equal("C:/a.cstock1d.json", "C:/b.cstock1d.json");
            loaded.Recent2D.Should().Equal("C:/c.cstock2d.json");
        }

        [Test]
        public void Load_CorruptFile_ReturnsDefaultsNoThrow()
        {
            File.WriteAllText(_tempPath, "{this is not valid json :(");

            var act = () => UserPreferencesStore.Load(_tempPath);

            act.Should().NotThrow();
            var prefs = act();
            prefs.WindowWidth.Should().BeGreaterThan(200);  // back to defaults
        }

        [Test]
        public void PushRecent_NewPath_AddsToFront()
        {
            var recent = new System.Collections.Generic.List<string>();
            UserPreferencesStore.PushRecent(recent, "C:/x.json");
            UserPreferencesStore.PushRecent(recent, "C:/y.json");

            recent.Should().Equal("C:/y.json", "C:/x.json");
        }

        [Test]
        public void PushRecent_DuplicatePath_MovesToFront()
        {
            var recent = new System.Collections.Generic.List<string>
            {
                "C:/a.json", "C:/b.json", "C:/c.json",
            };

            UserPreferencesStore.PushRecent(recent, "C:/b.json");

            recent.Should().Equal("C:/b.json", "C:/a.json", "C:/c.json");
        }

        [Test]
        public void PushRecent_CapsAtFive()
        {
            var recent = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 8; i++)
                UserPreferencesStore.PushRecent(recent, $"C:/{i}.json");

            recent.Should().HaveCount(UserPreferences.RecentMax);
            recent[0].Should().Be("C:/7.json");
            recent[^1].Should().Be("C:/3.json");
        }

        [Test]
        public void PushRecent_CaseInsensitiveDedup()
        {
            var recent = new System.Collections.Generic.List<string>();
            UserPreferencesStore.PushRecent(recent, "C:/Foo.json");
            UserPreferencesStore.PushRecent(recent, "c:/foo.json");

            recent.Should().HaveCount(1);
            recent[0].Should().Be("c:/foo.json", "second push wins");
        }

        // ─── Atomic write: temp file is cleaned up + crash-mid-write doesn't strand it ──

        [Test]
        public void Save_LeavesNoTempFileBehind()
        {
            UserPreferencesStore.Save(new UserPreferences { WindowWidth = 1234 }, _tempPath);

            File.Exists(_tempPath).Should().BeTrue();
            var dir = Path.GetDirectoryName(_tempPath)!;
            var baseName = Path.GetFileName(_tempPath);
            // Unique-suffix temp files match "<basename>.<pid>.<guid>.tmp".
            Directory.GetFiles(dir, baseName + ".*.tmp").Should().BeEmpty(
                "atomic write must rename the temp file, not leave it stranded");
        }

        [Test]
        public void Save_OverwritesExistingFileAtomically()
        {
            // First write
            UserPreferencesStore.Save(new UserPreferences { WindowWidth = 1000 }, _tempPath);
            // Second write — must replace, not crash on duplicate
            UserPreferencesStore.Save(new UserPreferences { WindowWidth = 2000 }, _tempPath);

            var loaded = UserPreferencesStore.Load(_tempPath);
            loaded.WindowWidth.Should().Be(2000);
            File.Exists(_tempPath + ".tmp").Should().BeFalse();
        }
    }
}
