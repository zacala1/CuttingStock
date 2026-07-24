using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class RecentScenarioServiceTests
    {
        [Test]
        public void BuildEntries_ProjectsDisplayNameAndExistenceWithoutWpf()
        {
            string[] paths = ["one.json", "missing.json"];

            var entries = RecentScenarioService.BuildEntries(
                paths,
                exists: path => path == "one.json",
                displayName: path => $"name:{path}");

            entries.Should().Equal(
                new RecentScenarioEntry("one.json", "name:one.json", true),
                new RecentScenarioEntry("missing.json", "name:missing.json", false));
        }

        [Test]
        public void Touch_DeduplicatesCaseInsensitivelyCapsAndMovesToFront()
        {
            var recent = new List<string>();
            for (int index = 0; index < 7; index++)
                RecentScenarioService.Touch(recent, $"C:/{index}.json");

            RecentScenarioService.Touch(recent, "c:/4.JSON");

            recent.Should().HaveCount(5);
            recent[0].Should().Be("c:/4.JSON");
            recent.Count(path => path.Equals("C:/4.json", StringComparison.OrdinalIgnoreCase))
                .Should().Be(1);
        }

        [Test]
        public void RemoveAndClear_UpdateTheSuppliedHistory()
        {
            var recent = new List<string> { "one.json", "two.json" };

            RecentScenarioService.Remove(recent, "ONE.JSON").Should().BeTrue();
            recent.Should().Equal("two.json");

            RecentScenarioService.Clear(recent);
            recent.Should().BeEmpty();
        }
    }
}
