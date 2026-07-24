using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class TextSearchServiceTests
    {
        [Test]
        public void Find_Forward_MovesPastCurrentSelection()
        {
            TextSearchMatch match = TextSearchService.Find(
                "Alpha beta ALPHA",
                "alpha",
                selectionStart: 0,
                selectionLength: 5,
                forward: true);

            match.Should().Be(new TextSearchMatch(11, 5));
        }

        [Test]
        public void Find_Forward_WrapsToFirstMatch()
        {
            TextSearchMatch match = TextSearchService.Find(
                "Alpha beta ALPHA",
                "alpha",
                selectionStart: 11,
                selectionLength: 5,
                forward: true);

            match.Should().Be(new TextSearchMatch(0, 5));
        }

        [Test]
        public void Find_Backward_WrapsToLastMatch()
        {
            TextSearchMatch match = TextSearchService.Find(
                "Alpha beta ALPHA",
                "alpha",
                selectionStart: 0,
                selectionLength: 5,
                forward: false);

            match.Should().Be(new TextSearchMatch(11, 5));
        }

        [TestCase("", "alpha")]
        [TestCase("Alpha", "")]
        [TestCase("Alpha", "missing")]
        public void Find_NoMatch_ReturnsNone(string text, string query)
        {
            TextSearchService.Find(text, query, 0, 0, forward: true)
                .Should().Be(TextSearchMatch.None);
        }
    }
}
