using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using CuttingStock.Core.Domain;
using CuttingStock.UI.ViewModels;

namespace CuttingStock.UI.Services
{
    /// <summary>
    /// Turns a <see cref="SolverResult"/> into the row/block models the 1D
    /// visualization tab binds against. Pulled out of MainWindow so the
    /// ViewModel can prepare the data without touching the visual tree
    /// directly; the View still owns the actual ItemsControl that renders it.
    /// </summary>
    public static class VisualizationService
    {
        /// <summary>One viewport row's total pixel width — chosen empirically
        /// for the 1D tab layout.</summary>
        public const double BarTotalWidth = 750.0;

        public sealed record Result(List<VisualizationRow> Rows, List<LegendItem> Legend);

        /// <summary>
        /// Group cutting plans by pattern signature and produce one bar row per
        /// distinct pattern, plus a legend of every cut length that appears.
        /// </summary>
        /// <param name="result">Solver output.</param>
        /// <param name="gamma">Leftover threshold for waste vs. reusable scrap colouring.</param>
        /// <param name="viewportWidth">Width in pixels the longest stock bar should occupy.</param>
        public static Result Build(SolverResult result, int gamma, double viewportWidth = BarTotalWidth)
        {
            var rows = new List<VisualizationRow>();
            var legend = new List<LegendItem>();
            var random = new Random(12345);
            var colorCache = new Dictionary<int, Brush>();

            var grouped = result.CuttingPlans
                .GroupBy(PatternKey)
                .Select(g => (Plan: g.First(), Count: g.Count()))
                .ToList();

            int groupNum = 1;
            foreach (var (plan, count) in grouped)
            {
                double scale = viewportWidth / Math.Max(1, plan.StockLength);
                var cutLengths = string.Join(" + ", plan.Cuts.Select(c => $"{c.Length}"));
                string countLabel = count > 1 ? $"  [x {count}개]" : "";
                string effPercent = plan.StockLength > 0
                    ? $"{100.0 * plan.Cuts.Sum(c => c.Length) / plan.StockLength:F1}%"
                    : "0%";

                var row = new VisualizationRow
                {
                    InfoText = $"#{groupNum}: 재고 {plan.StockLength}mm → [{cutLengths}] 잔여 {plan.Leftover}mm (효율 {effPercent}){countLabel}",
                };

                foreach (var cut in plan.Cuts)
                {
                    EnsureColor(colorCache, legend, random, cut.Length);
                    double width = cut.Length * scale;
                    double pct = 100.0 * cut.Length / plan.StockLength;
                    string label = width > 45 ? $"{cut.Length}" : string.Empty;

                    row.Blocks.Add(new VisualizationBlock
                    {
                        Width = width,
                        Color = colorCache[cut.Length],
                        BorderColor = Brushes.White,
                        ToolTip = $"{cut.Length}mm ({pct:F1}%)",
                        Text = label,
                        TextColor = IsBright(colorCache[cut.Length]) ? Brushes.Black : Brushes.White,
                    });
                }

                if (plan.Leftover > 0)
                {
                    double wWidth = plan.Leftover * scale;
                    bool isWaste = plan.Leftover < gamma;
                    var bg = isWaste ? Brushes.MistyRose : Brushes.LightGray;
                    var border = isWaste ? Brushes.IndianRed : Brushes.Gray;
                    string wasteLabel = isWaste ? "낭비" : "재사용";
                    row.Blocks.Add(new VisualizationBlock
                    {
                        Width = wWidth,
                        Color = bg,
                        BorderColor = border,
                        ToolTip = $"잔여 {plan.Leftover}mm ({wasteLabel})",
                        Text = wWidth > 45 ? $"{plan.Leftover}" : string.Empty,
                        TextColor = isWaste ? Brushes.DarkRed : Brushes.DimGray,
                    });
                }

                rows.Add(row);
                groupNum++;
            }

            var sortedLegend = legend
                .OrderBy(i => int.TryParse(i.Label.Replace("mm", string.Empty), out var n) ? n : int.MaxValue)
                .ToList();
            sortedLegend.Add(new LegendItem { Color = Brushes.LightGray, Label = "잔여 (재사용)" });
            sortedLegend.Add(new LegendItem { Color = Brushes.MistyRose, Label = "잔여 (낭비)" });

            return new Result(rows, sortedLegend);
        }

        private static string PatternKey(CuttingPlan plan)
        {
            var cuts = string.Join(",", plan.Cuts.Select(c => c.Length).OrderBy(l => l));
            return $"{plan.StockLength}|{cuts}|{plan.Leftover}";
        }

        private static void EnsureColor(Dictionary<int, Brush> cache, List<LegendItem> legend, Random random, int length)
        {
            if (cache.ContainsKey(length)) return;
            double hue = (cache.Count * 137.508) % 360;   // golden angle
            var color = HslToRgb(hue, 0.55, 0.55);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            cache[length] = brush;
            legend.Add(new LegendItem { Color = brush, Label = $"{length}mm" });
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r, g, b;
            if (h < 60)       { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else              { r = c; g = 0; b = x; }
            return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        private static bool IsBright(Brush brush)
        {
            if (brush is SolidColorBrush scb)
            {
                double bri = scb.Color.R * 0.299 + scb.Color.G * 0.587 + scb.Color.B * 0.114;
                return bri > 128;
            }
            return true;
        }
    }
}
