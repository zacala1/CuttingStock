using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using CuttingStock.UI.Services;
using Microsoft.Win32;

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

        // Last computed state for export.
        private SolverResult2D? _lastResult;
        private SolverOptions2D? _lastOptions;
        private ICuttingSolver2D? _lastSolver;
        private List<ComparisonResult2D> _lastCompareRows = new();

        private static readonly Regex IntegerRegex = new("[^0-9]+", RegexOptions.Compiled);
        private static readonly Regex DecimalRegex = new("[^0-9.]+", RegexOptions.Compiled);

        public TwoDTab()
        {
            InitializeComponent();
            sheetGrid.ItemsSource = _sheets;
            rectOrderGrid.ItemsSource = _orders;
        }

        // ─── Inputs ─────────────────────────────────────────────────

        private void AddSheet_Click(object sender, RoutedEventArgs e) => _sheets.Add(new SheetRow());

        private void DeleteSheet_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedRows(sheetGrid, _sheets);
        }

        private void AddRectOrder_Click(object sender, RoutedEventArgs e) => _orders.Add(new RectOrderRow());

        private void DeleteRectOrder_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedRows(rectOrderGrid, _orders);
        }

        private static void DeleteSelectedRows<T>(DataGrid grid, ObservableCollection<T> collection)
        {
            var selected = grid.SelectedItems.Cast<T>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("삭제할 항목을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (selected.Count > 1 &&
                MessageBox.Show($"{selected.Count}개 항목을 삭제하시겠습니까?", "삭제 확인",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            foreach (var item in selected) collection.Remove(item);
        }

        private void LoadExample_Click(object sender, RoutedEventArgs e)
        {
            _sheets.Clear();
            _orders.Clear();
            _sheets.Add(new SheetRow { Width = 2440, Height = 1220, Quantity = 5 });
            _sheets.Add(new SheetRow { Width = 1220, Height = 1220, Quantity = 5 });
            _orders.Add(new RectOrderRow { Width = 600,  Height = 400, Quantity = 6 });
            _orders.Add(new RectOrderRow { Width = 800,  Height = 300, Quantity = 4 });
            _orders.Add(new RectOrderRow { Width = 300,  Height = 300, Quantity = 8 });
            _orders.Add(new RectOrderRow { Width = 1200, Height = 500, Quantity = 2 });
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_sheets.Count + _orders.Count == 0) return;
            if (MessageBox.Show("시트와 주문 데이터를 모두 삭제하시겠습니까?", "전체 초기화",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            _sheets.Clear();
            _orders.Clear();
            report2DBox.Text = "";
            visualization2DPanel.Children.Clear();
            visualization2DScroll.Visibility = Visibility.Collapsed;
            visualization2DPlaceholder.Visibility = Visibility.Visible;
            compare2DBox.Text = "";
            compare2DGrid.ItemsSource = null;
            compare2DContent.Visibility = Visibility.Collapsed;
            compare2DPlaceholder.Visibility = Visibility.Visible;
            btnExport2DCsv.IsEnabled = false;
            btnExport2DExcel.IsEnabled = false;
            btnExportComp2DCsv.IsEnabled = false;
            btnExportComp2DExcel.IsEnabled = false;
            _lastResult = null;
            _lastCompareRows.Clear();
        }

        // ─── DataGrid validation + paste ─────────────────────────────

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (e.EditingElement is TextBox tb)
            {
                if (!int.TryParse(tb.Text, out int v) || v <= 0)
                {
                    MessageBox.Show("양의 정수를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    e.Cancel = true;
                }
            }
        }

        private void DataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.V &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                var grid = sender as DataGrid;
                if (grid == null) return;
                bool isSheet = grid.ItemsSource == _sheets;
                PasteFromClipboard(isSheet);
                e.Handled = true;
            }
        }

        private void PasteFromClipboard(bool isSheet)
        {
            try
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;
                int added = 0;
                foreach (var row in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cols = row.Split(new[] { '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length >= 3 &&
                        int.TryParse(cols[0].Trim(), out int w) &&
                        int.TryParse(cols[1].Trim(), out int h) &&
                        int.TryParse(cols[2].Trim(), out int q) &&
                        w > 0 && h > 0 && q > 0)
                    {
                        if (isSheet) _sheets.Add(new SheetRow { Width = w, Height = h, Quantity = q });
                        else _orders.Add(new RectOrderRow { Width = w, Height = h, Quantity = q });
                        added++;
                    }
                }
                if (added > 0)
                    MessageBox.Show($"{added}개 항목을 붙여넣었습니다.", "붙여넣기 성공", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"붙여넣기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IntegerTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = IntegerRegex.IsMatch(e.Text);
        }

        private void DecimalTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (DecimalRegex.IsMatch(e.Text)) { e.Handled = true; return; }
            if (e.Text == "." && sender is TextBox tb && tb.Text.Contains('.')) e.Handled = true;
        }

        // ─── Build inputs / options ─────────────────────────────────

        private List<Sheet> BuildSheets()
        {
            var list = new List<Sheet>();
            foreach (var s in _sheets)
                if (s.Width > 0 && s.Height > 0 && s.Quantity > 0)
                    list.Add(new Sheet(s.Width, s.Height, s.Quantity));
            return list;
        }

        private List<RectOrder> BuildOrders()
        {
            var list = new List<RectOrder>();
            foreach (var o in _orders)
                if (o.Width > 0 && o.Height > 0 && o.Quantity > 0)
                    list.Add(new RectOrder(o.Width, o.Height, o.Quantity, o.AllowRotation));
            return list;
        }

        /// <summary>Parse 2D parameters with explicit feedback on invalid input.</summary>
        private SolverOptions2D? TryBuildOptions()
        {
            if (!int.TryParse(kerf2D.Text, out int kerf) || kerf < 0)
            {
                MessageBox.Show("Kerf 값을 올바르게 입력해주세요. (0 이상의 정수)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!int.TryParse(trim2D.Text, out int trim) || trim < 0)
            {
                MessageBox.Show("Trim 값을 올바르게 입력해주세요. (0 이상의 정수)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!float.TryParse(alphaArea2D.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha) || alpha < 0)
            {
                MessageBox.Show("α (면적 단가) 값을 올바르게 입력해주세요. (0 이상의 숫자)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!int.TryParse(timeLimit2D.Text, out int tl) || tl < 1000)
            {
                MessageBox.Show("시간 제한은 1000ms 이상이어야 합니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            return new SolverOptions2D
            {
                Kerf = kerf,
                Trim = trim,
                AlphaArea = alpha,
                AllowRotation = rotation2D.IsChecked == true,
                Stage = ((stage2D.SelectedItem as ComboBoxItem)?.Content?.ToString()?.StartsWith("3") == true) ? 3 : 2,
                TimeLimitMs = tl,
                UsageOrder = usageOrder2D.SelectedIndex == 0
                    ? StockUsageOrder.SmallToLarge
                    : StockUsageOrder.LargeToSmall,
            };
        }

        private ICuttingSolver2D GetSelectedSolver() => algoCombo2D.SelectedIndex switch
        {
            0 => new ShelfGuillotineSolver(),
            1 => new ColumnGeneration2DSolver(),
            2 => new StagedMipGuillotineSolver(),
            _ => new ShelfGuillotineSolver(),
        };

        private static ICuttingSolver2D BuildSolver(int idx) => idx switch
        {
            0 => new ShelfGuillotineSolver(),
            1 => new ColumnGeneration2DSolver(),
            2 => new StagedMipGuillotineSolver(),
            _ => new ShelfGuillotineSolver(),
        };

        // ─── Run optimization ───────────────────────────────────────

        private void SetRunningState(bool running, string label = "계산 중...")
        {
            btnCalc2D.IsEnabled = !running;
            btnComp2D.IsEnabled = !running;
            loadingOverlay2D.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            loadingBar2D.IsIndeterminate = running;
            loadingText2D.Text = label;
        }

        private async void Calculate2D_Click(object sender, RoutedEventArgs e)
        {
            var sheets = BuildSheets();
            var orders = BuildOrders();
            if (sheets.Count == 0 || orders.Count == 0)
            {
                MessageBox.Show("시트와 주문을 모두 입력하세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var options = TryBuildOptions();
            if (options == null) return;
            var solver = GetSelectedSolver();

            try
            {
                SetRunningState(true);
                report2DBox.Text = "계산 중...";
                var result = await Task.Run(() => solver.Solve(sheets, orders, options));

                if (!result.Success)
                {
                    report2DBox.Text = $"실패: {result.ErrorMessage}";
                    return;
                }
                _lastResult = result;
                _lastOptions = options;
                _lastSolver = solver;

                report2DBox.Text = result.GetDetailedReport(options);
                RenderPatterns(result, options);
                btnExport2DCsv.IsEnabled = true;
                btnExport2DExcel.IsEnabled = true;
                result2DTabs.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetRunningState(false);
            }
        }

        private async void Compare2D_Click(object sender, RoutedEventArgs e)
        {
            var sheets = BuildSheets();
            var orders = BuildOrders();
            if (sheets.Count == 0 || orders.Count == 0)
            {
                MessageBox.Show("시트와 주문을 모두 입력하세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var options = TryBuildOptions();
            if (options == null) return;

            try
            {
                SetRunningState(true, "비교 중... (0/3)");

                var rows = new List<ComparisonResult2D>();
                SolverResult2D? bestResult = null;
                ICuttingSolver2D? bestSolver = null;
                long bestCost = long.MaxValue;
                int bestSheets = int.MaxValue;
                var details = new StringBuilder();

                for (int i = 0; i < 3; i++)
                {
                    var s = BuildSolver(i);
                    loadingText2D.Text = $"비교 중... ({i + 1}/3 — {s.Name})";

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

                    if (r.Success && (r.TotalCost < bestCost ||
                        (r.TotalCost == bestCost && r.SheetsUsed < bestSheets)))
                    {
                        bestCost = r.TotalCost;
                        bestSheets = r.SheetsUsed;
                        bestResult = r;
                        bestSolver = s;
                    }
                }

                int rank = 1;
                foreach (var r in rows.Where(r => r.Success).OrderBy(r => r.TotalCost).ThenBy(r => r.SheetsUsed))
                    r.Rank = rank++;

                _lastCompareRows = rows;
                compare2DGrid.ItemsSource = rows
                    .OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank)
                    .ToList();
                compare2DBox.Text = details.ToString();
                compare2DPlaceholder.Visibility = Visibility.Collapsed;
                compare2DContent.Visibility = Visibility.Visible;
                btnExportComp2DCsv.IsEnabled = true;
                btnExportComp2DExcel.IsEnabled = true;

                // Render best solver's patterns so the user has a visualization to inspect.
                if (bestResult != null && bestSolver != null)
                {
                    _lastResult = bestResult;
                    _lastOptions = options;
                    _lastSolver = bestSolver;
                    RenderPatterns(bestResult, options);
                    btnExport2DCsv.IsEnabled = true;
                    btnExport2DExcel.IsEnabled = true;
                }

                result2DTabs.SelectedIndex = 2;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetRunningState(false);
            }
        }

        // ─── Export ────────────────────────────────────────────────

        private void Export2DToCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            {
                MessageBox.Show("먼저 2D 최적화를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                ExportService.ExportSingleResult2DToCsv(dlg.FileName, _lastSolver, _lastResult, _lastOptions);
                MessageBox.Show($"CSV 파일로 저장되었습니다.\n{dlg.FileName}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export2DToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            {
                MessageBox.Show("먼저 2D 최적화를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                ExportService.ExportSingleResult2DToExcel(dlg.FileName, _lastSolver, _lastResult, _lastOptions);
                MessageBox.Show($"Excel 파일로 저장되었습니다.\n{dlg.FileName}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportComp2DToCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_lastCompareRows.Count == 0)
            {
                MessageBox.Show("먼저 알고리즘 비교를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                ExportService.ExportComparison2DResultsToCsv(dlg.FileName, _lastCompareRows);
                MessageBox.Show($"CSV 파일로 저장되었습니다.\n{dlg.FileName}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportComp2DToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_lastCompareRows.Count == 0)
            {
                MessageBox.Show("먼저 알고리즘 비교를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                ExportService.ExportComparison2DResultsToExcel(dlg.FileName, _lastCompareRows);
                MessageBox.Show($"Excel 파일로 저장되었습니다.\n{dlg.FileName}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Render ────────────────────────────────────────────────

        private void RenderPatterns(SolverResult2D result, SolverOptions2D options)
        {
            visualization2DPanel.Children.Clear();
            if (result.Patterns.Count == 0)
            {
                visualization2DScroll.Visibility = Visibility.Collapsed;
                visualization2DPlaceholder.Visibility = Visibility.Visible;
                return;
            }
            visualization2DPlaceholder.Visibility = Visibility.Collapsed;
            visualization2DScroll.Visibility = Visibility.Visible;

            // Build color map once per render — HSL golden-angle keeps neighboring orders
            // visually distinct even with many items.
            var colorByOrder = new Dictionary<int, Brush>();

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
                    Text = $"패턴 #{patternIdx} — Sheet {pat.Sheet.Width}×{pat.Sheet.Height}mm  ×{pat.Multiplicity}  | items={pat.Placements.Count}  | eff={pat.Efficiency:F1}%",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4),
                    TextWrapping = TextWrapping.Wrap,
                });

                var canvas = new Canvas
                {
                    Width = cw,
                    Height = ch,
                    Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xF8, 0xF8)),
                };
                canvas.Children.Add(new Rectangle
                {
                    Width = cw, Height = ch,
                    Stroke = Brushes.Black, StrokeThickness = 1.2,
                    Fill = Brushes.Transparent,
                });
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
                foreach (var pl in pat.Placements)
                {
                    if (!colorByOrder.TryGetValue(pl.OrderIndex, out var brush))
                    {
                        brush = HslBrush(colorByOrder.Count);
                        colorByOrder[pl.OrderIndex] = brush;
                    }
                    var rect = new Rectangle
                    {
                        Width = pl.Width * scale,
                        Height = pl.Height * scale,
                        Fill = brush,
                        Stroke = Brushes.DimGray,
                        StrokeThickness = 0.6,
                        ToolTip = $"O{pl.OrderIndex} ({pl.Width}×{pl.Height}mm)" + (pl.Rotated ? " ↻" : ""),
                    };
                    Canvas.SetLeft(rect, pl.X * scale);
                    Canvas.SetTop(rect, pl.Y * scale);
                    canvas.Children.Add(rect);

                    // Skip labels that won't fit inside the placement to avoid visual noise.
                    if (pl.Width * scale >= 28 && pl.Height * scale >= 14)
                    {
                        var label = new TextBlock
                        {
                            Text = pl.Rotated ? $"O{pl.OrderIndex}↻" : $"O{pl.OrderIndex}",
                            FontSize = 10,
                            Foreground = IsBright(brush) ? Brushes.Black : Brushes.White,
                        };
                        Canvas.SetLeft(label, pl.X * scale + 2);
                        Canvas.SetTop(label, pl.Y * scale + 2);
                        canvas.Children.Add(label);
                    }
                }

                stack.Children.Add(canvas);
                border.Child = stack;
                visualization2DPanel.Children.Add(border);
                patternIdx++;
            }
        }

        // HSL golden-angle palette: same approach as the 1D tab, picked once per
        // distinct order index so the colour stays stable across patterns.
        private static SolidColorBrush HslBrush(int seqIndex)
        {
            double hue = (seqIndex * 137.508) % 360;
            var color = HslToRgb(hue, 0.55, 0.55);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
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
                double brightness = scb.Color.R * 0.299 + scb.Color.G * 0.587 + scb.Color.B * 0.114;
                return brightness > 128;
            }
            return true;
        }
    }
}
