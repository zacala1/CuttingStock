using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CuttingStock.Core.Domain;

namespace CuttingStock.Core.Persistence
{
    /// <summary>
    /// Round-trips a user's input scenario (rows + parameters) to JSON so they can
    /// reload a setup later or share it with colleagues. Pure data DTOs to keep the
    /// on-disk format decoupled from the WPF row types.
    /// </summary>
    public static class ScenarioService
    {
        private const string Schema1D = "cutting-stock-1d/v1";
        private const string Schema2D = "cutting-stock-2d/v1";

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ─── 1D ────────────────────────────────────────────────────

        public sealed class Stock1DDto
        {
            public int Length { get; set; }
            public int Quantity { get; set; }
        }

        public sealed class Order1DDto
        {
            public int Length { get; set; }
            public int Quantity { get; set; }
        }

        public sealed class Options1DDto
        {
            public float Alpha { get; set; } = 1.0f;
            public float Beta { get; set; } = 500.0f;
            public int Gamma { get; set; } = 100;
            public int Delta { get; set; } = 100;
            public int Kerf { get; set; }
            public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.SmallToLarge;
            public bool EnableWelding { get; set; }
        }

        public sealed class Scenario1D
        {
            public string Schema { get; set; } = Schema1D;
            public List<Stock1DDto> Stocks { get; set; } = new();
            public List<Order1DDto> Orders { get; set; } = new();
            public Options1DDto Parameters { get; set; } = new();
        }

        public static void Save1D(string path, Scenario1D scenario)
        {
            scenario.Schema = Schema1D;
            File.WriteAllText(path, JsonSerializer.Serialize(scenario, Json));
        }

        public static Scenario1D Load1D(string path)
        {
            var text = File.ReadAllText(path);
            var scenario = JsonSerializer.Deserialize<Scenario1D>(text, Json)
                ?? throw new InvalidDataException("Empty or invalid 1D scenario file.");
            if (!string.Equals(scenario.Schema, Schema1D, StringComparison.Ordinal))
                throw new InvalidDataException($"Expected schema '{Schema1D}' but got '{scenario.Schema}'.");
            return scenario;
        }

        // ─── 2D ────────────────────────────────────────────────────

        public sealed class Sheet2DDto
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Quantity { get; set; }
        }

        public sealed class Order2DDto
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int Quantity { get; set; }
            public bool AllowRotation { get; set; } = true;
        }

        public sealed class Options2DDto
        {
            public int Kerf { get; set; }
            public int Trim { get; set; }
            public float AlphaArea { get; set; } = 1f;
            public bool AllowRotation { get; set; } = true;
            public int Stage { get; set; } = 2;
            public int TimeLimitMs { get; set; } = 30000;
            public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.LargeToSmall;
        }

        public sealed class Scenario2D
        {
            public string Schema { get; set; } = Schema2D;
            public List<Sheet2DDto> Sheets { get; set; } = new();
            public List<Order2DDto> Orders { get; set; } = new();
            public Options2DDto Options { get; set; } = new();
        }

        public static void Save2D(string path, Scenario2D scenario)
        {
            scenario.Schema = Schema2D;
            File.WriteAllText(path, JsonSerializer.Serialize(scenario, Json));
        }

        public static Scenario2D Load2D(string path)
        {
            var text = File.ReadAllText(path);
            var scenario = JsonSerializer.Deserialize<Scenario2D>(text, Json)
                ?? throw new InvalidDataException("Empty or invalid 2D scenario file.");
            if (!string.Equals(scenario.Schema, Schema2D, StringComparison.Ordinal))
                throw new InvalidDataException($"Expected schema '{Schema2D}' but got '{scenario.Schema}'.");
            return scenario;
        }
    }
}
