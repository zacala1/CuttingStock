using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.UI.TwoD
{
    public partial class TwoDTab : UserControl
    {
        public sealed class SheetRow
        {
            public int Width { get; set; } = 2440;
            public int Height { get; set; } = 1220;
            public int Quantity { get; set; } = 5;
        }

        public sealed class RectOrderRow
        {
            public int Width { get; set; } = 600;
            public int Height { get; set; } = 400;
            public int Quantity { get; set; } = 4;
            public bool AllowRotation { get; set; } = true;
        }

        private readonly ObservableCollection<SheetRow> _sheets = new();
        private readonly ObservableCollection<RectOrderRow> _orders = new();

        private static readonly Brush[] Palette = new Brush[]
        {
            new SolidColorBrush(Color.FromRgb(108, 174, 222)),
            new SolidColorBrush(Color.FromRgb(244, 177, 131)),
            new SolidColorBrush(Color.FromRgb(168, 208, 141)),
            new SolidColorBrush(Color.FromRgb(255, 217, 102)),
            new SolidColorBrush(Color.FromRgb(206, 156, 200)),
            new SolidColorBrush(Color.FromRgb(241, 144, 144)),
            new SolidColorBrush(Color.FromRgb(132, 198, 184)),
            new SolidColorBrush(Color.FromRgb(216, 198, 232)),
        };

        public TwoDTab()
        {
            InitializeComponent();
            sheetGrid.ItemsSource = _sheets;
            rectOrderGrid.ItemsSource = _orders;
        }

        private void AddSheet_Click(object sender, RoutedEventArgs e) => _sheets.Add(new SheetRow());

        private void DeleteSheet_Click(object sender, RoutedEventArgs e)
        {
            if (sheetGrid.SelectedItem is SheetRow s) _sheets.Remove(s);
        }

        private void AddRectOrder_Click(object sender, RoutedEventArgs e) => _orders.Add(new RectOrderRow());

        private void DeleteRectOrder_Click(object sender, RoutedEventArgs e)
        {
            if (rectOrderGrid.SelectedItem is RectOrderRow o) _orders.Remove(o);
        }

        private void LoadExample_Click(object sender, RoutedEventArgs e)
        {
            _sheets.Clear();
            _orders.Clear();
            _sheets.Add(new SheetRow { Width = 2440, Height = 1220, Quantity = 5 });
            _sheets.Add(new SheetRow { Width = 1220, Height = 1220, Quantity = 5 });
            _orders.Add(new RectOrderRow { Width = 600, Height = 400, Quantity = 6 });
            _orders.Add(new RectOrderRow { Width = 800, Height = 300, Quantity = 4 });
            _orders.Add(new RectOrderRow { Width = 300, Height = 300, Quantity = 8 });
            _orders.Add(new RectOrderRow { Width = 1200, Height = 500, Quantity = 2 });
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            _sheets.Clear();
            _orders.Clear();
            report2DBox.Text = "";
            visualization2DPanel.Children.Clear();
            compare2DBox.Text = "";
            compare2DGrid.ItemsSource = null;
        }

        private List<Sheet> BuildSheets()
        {
            var list = new List<Sheet>();
            foreach (var s in _sheets)
            {
                if (s.Width > 0 && s.Height > 0 && s.Quantity > 0)
                    list.Add(new Sheet(s.Width, s.Height, s.Quantity));
            }
            return list;
        }

        private List<RectOrder> BuildOrders()
        {
            var list = new List<RectOrder>();
            foreach (var o in _orders)
            {
                if (o.Width > 0 && o.Height > 0 && o.Quantity > 0)
                    list.Add(new RectOrder(o.Width, o.Height, o.Quantity, o.AllowRotation));
            }
            return list;
        }

        private SolverOptions2D BuildOptions()
        {
            var o = new SolverOptions2D
            {
                Kerf = ParseInt(kerf2D.Text, 0),
                Trim = ParseInt(trim2D.Text, 0),
                AllowRotation = rotation2D.IsChecked == true,
                AlphaArea = (float)ParseDouble(alphaArea2D.Text, 1.0),
                Stage = ((stage2D.SelectedItem as ComboBoxItem)?.Content?.ToString() == "3") ? 3 : 2,
                TimeLimitMs = Math.Max(1000, ParseInt(timeLimit2D.Text, 30000)),
            };
            return o;
        }

        private static int ParseInt(string s, int fallback) =>
            int.TryParse(s, out var v) ? v : fallback;

        private static double ParseDouble(string s, double fallback) =>
            double.TryParse(s, out var v) ? v : fallback;

        private ICuttingSolver2D GetSelectedSolver() => algoCombo2D.SelectedIndex switch
        {
            0 => new ShelfGuillotineSolver(),
            1 => new ColumnGeneration2DSolver(),
            2 => new StagedMipGuillotineSolver(),
            _ => new ShelfGuillotineSolver(),
        };

        private async void Calculate2D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sheets = BuildSheets();
                var orders = BuildOrders();
                if (sheets.Count == 0 || orders.Count == 0)
                {
                    MessageBox.Show("시트와 주문을 모두 입력하세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var options = BuildOptions();
                var solver = GetSelectedSolver();

                btnCalc2D.IsEnabled = false;
                btnComp2D.IsEnabled = false;
                report2DBox.Text = "계산 중...";

                var result = await Task.Run(() => solver.Solve(sheets, orders, options));
                if (!result.Success)
                {
                    report2DBox.Text = $"실패: {result.ErrorMessage}";
                    return;
                }

                report2DBox.Text = result.GetDetailedReport(options);
                RenderPatterns(result, options);
                result2DTabs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCalc2D.IsEnabled = true;
                btnComp2D.IsEnabled = true;
            }
        }

        private async void Compare2D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sheets = BuildSheets();
                var orders = BuildOrders();
                if (sheets.Count == 0 || orders.Count == 0)
                {
                    MessageBox.Show("시트와 주문을 모두 입력하세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var options = BuildOptions();

                btnCalc2D.IsEnabled = false;
                btnComp2D.IsEnabled = false;
                compare2DBox.Text = "비교 중...";

                ICuttingSolver2D[] solvers =
                {
                    new ShelfGuillotineSolver(),
                    new ColumnGeneration2DSolver(),
                    new StagedMipGuillotineSolver(),
                };

                var rows = new List<ComparisonResult2D>();
                var details = new StringBuilder();

                foreach (var s in solvers)
                {
                    var r = await Task.Run(() => s.Solve(sheets, orders, options));
                    rows.Add(new ComparisonResult2D
                    {
                        AlgorithmName = s.Name,
                        TotalCost = r.TotalCost,
                        WasteArea = r.TotalWasteArea,
                        SheetsUsed = r.SheetsUsed,
                        MaterialEfficiency = r.MaterialEfficiency,
                        ExecutionTimeMs = r.ExecutionTimeMs,
                        Success = r.Success,
                    });
                    details.AppendLine($"=== {s.Name} ===");
                    details.AppendLine(r.Success ? r.GetDetailedReport(options) : ("실패: " + r.ErrorMessage));
                    details.AppendLine();
                }

                int rank = 1;
                foreach (var r in rows.OrderBy(r => r.TotalCost).ThenBy(r => r.SheetsUsed))
                    r.Rank = rank++;

                compare2DGrid.ItemsSource = rows.OrderBy(r => r.Rank).ToList();
                compare2DBox.Text = details.ToString();
                result2DTabs.SelectedIndex = 2;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnCalc2D.IsEnabled = true;
                btnComp2D.IsEnabled = true;
            }
        }

        private void RenderPatterns(SolverResult2D result, SolverOptions2D options)
        {
            visualization2DPanel.Children.Clear();
            const double targetMaxDim = 700.0;

            int patternIdx = 1;
            foreach (var pat in result.Patterns)
            {
                double scale = targetMaxDim / Math.Max(pat.Sheet.Width, pat.Sheet.Height);
                double cw = pat.Sheet.Width * scale;
                double ch = pat.Sheet.Height * scale;

                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 12),
                    Padding = new Thickness(8),
                    Background = Brushes.White,
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = $"패턴 #{patternIdx} — Sheet {pat.Sheet.Width}×{pat.Sheet.Height}  ×{pat.Multiplicity}  | items={pat.Placements.Count}  | eff={pat.Efficiency:F1}%",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4),
                });

                var canvas = new Canvas
                {
                    Width = cw,
                    Height = ch,
                    Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF8)),
                };
                // Sheet outline.
                canvas.Children.Add(new Rectangle
                {
                    Width = cw, Height = ch,
                    Stroke = Brushes.Black, StrokeThickness = 1.2,
                    Fill = Brushes.Transparent,
                });
                // Trim outline.
                if (options.Trim > 0)
                {
                    double tx = options.Trim * scale;
                    var trimRect = new Rectangle
                    {
                        Width = (pat.Sheet.Width - 2 * options.Trim) * scale,
                        Height = (pat.Sheet.Height - 2 * options.Trim) * scale,
                        Stroke = Brushes.DarkGray, StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        Fill = Brushes.Transparent,
                    };
                    Canvas.SetLeft(trimRect, tx);
                    Canvas.SetTop(trimRect, tx);
                    canvas.Children.Add(trimRect);
                }
                // Items.
                foreach (var pl in pat.Placements)
                {
                    var brush = Palette[pl.OrderIndex % Palette.Length];
                    var rect = new Rectangle
                    {
                        Width = pl.Width * scale,
                        Height = pl.Height * scale,
                        Fill = brush,
                        Stroke = Brushes.DimGray,
                        StrokeThickness = 0.6,
                        ToolTip = $"O{pl.OrderIndex} ({pl.Width}×{pl.Height})" + (pl.Rotated ? " ↻" : ""),
                    };
                    Canvas.SetLeft(rect, pl.X * scale);
                    Canvas.SetTop(rect, pl.Y * scale);
                    canvas.Children.Add(rect);

                    var label = new TextBlock
                    {
                        Text = pl.Rotated ? $"O{pl.OrderIndex}↻" : $"O{pl.OrderIndex}",
                        FontSize = 10,
                        Foreground = Brushes.Black,
                    };
                    Canvas.SetLeft(label, pl.X * scale + 2);
                    Canvas.SetTop(label, pl.Y * scale + 2);
                    canvas.Children.Add(label);
                }

                stack.Children.Add(canvas);
                border.Child = stack;
                visualization2DPanel.Children.Add(border);
                patternIdx++;
            }
        }
    }
}
