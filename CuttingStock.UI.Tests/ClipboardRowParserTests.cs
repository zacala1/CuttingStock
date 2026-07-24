using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class ClipboardRowParserTests
    {
        [Test]
        public void ParseLengthQuantityRows_AcceptsSupportedSeparatorsAndSkipsInvalidRows()
        {
            const string text = "12000\t2\r\n6000,3\nbad;4\n5000;-1";

            var rows = ClipboardRowParser.ParseLengthQuantityRows(text);

            rows.Should().Equal(
                new LengthQuantityInput(12000, 2),
                new LengthQuantityInput(6000, 3));
        }

        [Test]
        public void ParseSheetRows_RequiresThreePositiveIntegers()
        {
            const string text = "2440\t1220\t3\r\n1000,500,2\n100;200\n0;200;1";

            var rows = ClipboardRowParser.ParseSheetRows(text);

            rows.Should().Equal(
                new SheetInput(2440, 1220, 3),
                new SheetInput(1000, 500, 2));
        }

        [TestCase("true", true)]
        [TestCase("1", true)]
        [TestCase("yes", true)]
        [TestCase("y", true)]
        [TestCase("false", false)]
        [TestCase("0", false)]
        [TestCase("no", false)]
        [TestCase("n", false)]
        [TestCase("unknown", true)]
        public void ParseRectOrderRows_ParsesRotationAliases(string value, bool expected)
        {
            var rows = ClipboardRowParser.ParseRectOrderRows($"100,50,2,{value}");

            rows.Should().ContainSingle();
            rows[0].AllowRotation.Should().Be(expected);
        }

        [Test]
        public void ParseRectOrderRows_DefaultsMissingRotationToTrue()
        {
            var rows = ClipboardRowParser.ParseRectOrderRows("100;50;2");

            rows.Should().Equal(new RectOrderInput(100, 50, 2, true));
        }
    }
}
