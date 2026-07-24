using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.UI.Services
{
    public sealed record TwoDRenderProjection(
        string AlgorithmName,
        int Trim,
        ReadOnlyCollection<TwoDPatternProjection> Patterns);

    public sealed record TwoDPatternProjection(
        int SheetWidth,
        int SheetHeight,
        int Multiplicity,
        double Efficiency,
        ReadOnlyCollection<TwoDPlacementProjection> Placements);

    public readonly record struct TwoDPlacementProjection(
        int OrderIndex,
        int X,
        int Y,
        int Width,
        int Height,
        bool Rotated);

    public sealed record TwoDChartProjection(
        ReadOnlyCollection<string> Labels,
        ReadOnlyCollection<double> SheetsUsed,
        ReadOnlyCollection<double> MaterialEfficiency,
        ReadOnlyCollection<double> ExecutionTimeMs)
    {
        public static TwoDChartProjection Empty { get; } = new(
            Array.AsReadOnly(Array.Empty<string>()),
            Array.AsReadOnly(Array.Empty<double>()),
            Array.AsReadOnly(Array.Empty<double>()),
            Array.AsReadOnly(Array.Empty<double>()));
    }

    /// <summary>Builds plain data consumed by the 2D Canvas and LiveCharts view.</summary>
    public static class TwoDProjectionService
    {
        public static TwoDRenderProjection BuildRender(
            string algorithmName,
            SolverResult2D result,
            SolverOptions2D options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(algorithmName);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(options);

            var patterns = result.Patterns
                .Select(pattern => new TwoDPatternProjection(
                    pattern.Sheet.Width,
                    pattern.Sheet.Height,
                    pattern.Multiplicity,
                    pattern.Efficiency,
                    pattern.Placements
                        .Select(placement => new TwoDPlacementProjection(
                            placement.OrderIndex,
                            placement.X,
                            placement.Y,
                            placement.Width,
                            placement.Height,
                            placement.Rotated))
                        .ToArray()
                        .AsReadOnly()))
                .ToArray();

            return new TwoDRenderProjection(
                algorithmName,
                options.Trim,
                patterns.AsReadOnly());
        }

        public static TwoDChartProjection BuildChart(
            IEnumerable<ComparisonResult2D> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            var successful = rows.Where(row => row.Success).ToList();
            if (successful.Count == 0) return TwoDChartProjection.Empty;

            return new TwoDChartProjection(
                successful.Select(row => AbbreviateName(row.AlgorithmName)).ToArray().AsReadOnly(),
                successful.Select(row => (double)row.SheetsUsed).ToArray().AsReadOnly(),
                successful.Select(row => row.MaterialEfficiency).ToArray().AsReadOnly(),
                successful.Select(row => row.ExecutionTimeMs).ToArray().AsReadOnly());
        }

        public static ComparisonResult2D? SelectBestRow(
            IEnumerable<ComparisonResult2D> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows
                .Where(row => row.Success)
                .OrderBy(row => row.TotalCost)
                .ThenBy(row => row.SheetsUsed)
                .FirstOrDefault();
        }

        private static string AbbreviateName(string name)
        {
            int parenthesis = name.IndexOf('(');
            if (parenthesis > 0 && parenthesis < name.Length - 1)
            {
                return name[..parenthesis].TrimEnd() +
                       Environment.NewLine +
                       name[parenthesis..];
            }

            return name;
        }
    }
}
