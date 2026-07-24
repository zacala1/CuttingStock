using System.IO;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Persistence;

namespace CuttingStock.Tests.Persistence
{
    /// <summary>
    /// Round-trip and schema-guard tests for <see cref="ScenarioService"/>.
    /// Ensures the JSON format stays stable and rejects mismatched schemas.
    /// </summary>
    [TestFixture]
    public class ScenarioServiceTests
    {
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "cstock-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void Save1D_Then_Load1D_RoundTrips()
        {
            var path = Path.Combine(_tempDir, "1d.json");
            var s = new ScenarioService.Scenario1D
            {
                Stocks = { new() { Length = 12000, Quantity = 20 }, new() { Length = 6000, Quantity = 10 } },
                Orders = { new() { Length = 5000, Quantity = 5 } },
                Parameters = new ScenarioService.Options1DDto
                {
                    Alpha = 1.5f, Beta = 750f, Gamma = 200, Delta = 150,
                    Kerf = 3, UsageOrder = StockUsageOrder.LargeToSmall, EnableWelding = true,
                },
            };

            ScenarioService.Save1D(path, s);
            var loaded = ScenarioService.Load1D(path);

            loaded.Stocks.Should().HaveCount(2);
            loaded.Stocks[0].Length.Should().Be(12000);
            loaded.Stocks[0].Quantity.Should().Be(20);
            loaded.Orders.Should().HaveCount(1);
            loaded.Orders[0].Length.Should().Be(5000);
            loaded.Parameters.Alpha.Should().Be(1.5f);
            loaded.Parameters.Beta.Should().Be(750f);
            loaded.Parameters.Gamma.Should().Be(200);
            loaded.Parameters.Delta.Should().Be(150);
            loaded.Parameters.Kerf.Should().Be(3);
            loaded.Parameters.UsageOrder.Should().Be(StockUsageOrder.LargeToSmall);
            loaded.Parameters.EnableWelding.Should().BeTrue();
        }

        [Test]
        public void Save2D_Then_Load2D_RoundTrips()
        {
            var path = Path.Combine(_tempDir, "2d.json");
            var s = new ScenarioService.Scenario2D
            {
                Sheets = { new() { Width = 2440, Height = 1220, Quantity = 5 } },
                Orders = { new() { Width = 600, Height = 400, Quantity = 6, AllowRotation = false } },
                Options = new ScenarioService.Options2DDto
                {
                    Kerf = 4, Trim = 10, AlphaArea = 0.5f, AllowRotation = false,
                    Stage = 3, TimeLimitMs = 60000, UsageOrder = StockUsageOrder.SmallToLarge,
                },
            };

            ScenarioService.Save2D(path, s);
            var loaded = ScenarioService.Load2D(path);

            loaded.Sheets.Should().HaveCount(1);
            loaded.Sheets[0].Width.Should().Be(2440);
            loaded.Orders[0].AllowRotation.Should().BeFalse();
            loaded.Options.Kerf.Should().Be(4);
            loaded.Options.Stage.Should().Be(3);
            loaded.Options.AlphaArea.Should().Be(0.5f);
            loaded.Options.UsageOrder.Should().Be(StockUsageOrder.SmallToLarge);
        }

        [Test]
        public void Load1D_RejectsMismatchedSchema()
        {
            var path = Path.Combine(_tempDir, "wrong-schema.json");
            // Hand-craft a JSON with a 2D schema string so we can verify the guard fires.
            File.WriteAllText(path, "{\"schema\":\"cutting-stock-2d/v1\",\"stocks\":[],\"orders\":[],\"parameters\":{}}");

            var act = () => ScenarioService.Load1D(path);
            act.Should().Throw<InvalidDataException>("the schema tag must be enforced to prevent mixing 1D and 2D files");
        }

        [Test]
        public void Save1D_ProducesPrettyPrintedCamelCaseJson()
        {
            var path = Path.Combine(_tempDir, "format.json");
            ScenarioService.Save1D(path, new ScenarioService.Scenario1D());
            var text = File.ReadAllText(path);

            text.Should().Contain("\"schema\":");        // camelCase
            text.Should().Contain("cutting-stock-1d/v1"); // stable identifier
            text.Should().Contain("\n");                  // pretty-printed
        }

        [Test]
        public void DetectKind_UsesSchemaInsteadOfFileName()
        {
            var oneDPath = Path.Combine(_tempDir, "ambiguous-one.json");
            var twoDPath = Path.Combine(_tempDir, "ambiguous-two.json");
            ScenarioService.Save1D(oneDPath, new ScenarioService.Scenario1D());
            ScenarioService.Save2D(twoDPath, new ScenarioService.Scenario2D());

            ScenarioService.DetectKind(oneDPath).Should().Be(ScenarioKind.OneD);
            ScenarioService.DetectKind(twoDPath).Should().Be(ScenarioKind.TwoD);
        }

        [Test]
        public void DetectKind_RejectsUnknownSchema()
        {
            var path = Path.Combine(_tempDir, "unknown.json");
            File.WriteAllText(path, "{\"schema\":\"other/v1\"}");

            var act = () => ScenarioService.DetectKind(path);

            act.Should().Throw<InvalidDataException>()
                .WithMessage("*Unsupported scenario schema*");
        }
    }
}
