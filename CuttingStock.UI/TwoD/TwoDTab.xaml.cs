using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.UI.Services;
using CuttingStock.UI.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CuttingStock.UI.TwoD
{
    /// <summary>
    /// 2D 탭. MVVM 전환 후 thin view 역할만 한다 — DataContext는 TwoDViewModel이고
    /// View는 Canvas 직접 렌더링, DataGrid SelectedItems 위임, LiveCharts 시리즈
    /// 갱신, 클립보드 paste만 처리한다.
    /// </summary>
    public partial class TwoDTab : UserControl
    {
        private readonly TwoDViewModel _vm;
        private static readonly Regex IntegerRegex = new("[^0-9]+", RegexOptions.Compiled);
        private static readonly Regex DecimalRegex = new("[^0-9.]+", RegexOptions.Compiled);

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
            _vm = new TwoDViewModel(new DialogService());
            DataContext = _vm;
            _vm.SingleResultReady  += (_, _) => Dispatcher.Invoke(() => RenderPatterns(_vm.LastResult!, _vm.LastOptions!));
            _vm.CompareResultReady += (_, _) => Dispatcher.Invoke(() => { UpdateCompareCharts(); if (_vm.LastResult != null) RenderPatterns(_vm.LastResult, _vm.LastOptions!); });
        }

        // ─── DataGrid: selection delete / paste / validation ─────────

        private void DeleteSheet_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedSheets(sheetGrid.SelectedItems.Cast<SheetRow>());

        private void DeleteRectOrder_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedOrders(rectOrderGrid.SelectedItems.Cast<RectOrderRow>());

        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            if (sender is not DataGrid grid) return;

            try
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                int added = 0;
                foreach (var row in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cols = row.Split(new[] { '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (grid.ItemsSource == _vm.Sheets)
                    {
                        if (cols.Length < 3) continue;
                        if (!int.TryParse(cols[0].Trim(), out int w) ||
                            !int.TryParse(cols[1].Trim(), out int h) ||
                            !int.TryParse(cols[2].Trim(), out int q) ||
                            w <= 0 || h <= 0 || q <= 0) continue;
                        _vm.Sheets.Add(new SheetRow { Width = w, Height = h, Quantity = q });
                        added++;
                    }
                    else if (grid.ItemsSource == _vm.Orders)
                    {
                        if (cols.Length < 3) continue;
                        if (!int.TryParse(cols[0].Trim(), out int w) ||
                            !int.TryParse(cols[1].Trim(), out int h) ||
                            !int.TryParse(cols[2].Trim(), out int q) ||
                            w <= 0 || h <= 0 || q <= 0) continue;
                        bool rot = cols.Length < 4 || ParseBool(cols[3].Trim(), defaultValue: true);
                        _vm.Orders.Add(new RectOrderRow { Width = w, Height = h, Quantity = q, AllowRotation = rot });
                        added++;
                    }
                }
                if (added > 0)
                    MessageBox.Show($"{added}개의 항목을 붙여넣었습니다.", "붙여넣기 성공",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"붙여넣기 중 오류가 발생했습니다: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool ParseBool(string s, bool defaultValue)
        {
            if (bool.TryParse(s, out var b)) return b;
            if (s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase) || s.Equals("y", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("no", StringComparison.OrdinalIgnoreCase) || s.Equals("n", StringComparison.OrdinalIgnoreCase)) return false;
            return defaultValue;
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (sender is not DataGrid grid) return;
            if (e.EditingElement is not TextBox tb) return;
            string header = e.Column?.Header?.ToString() ?? string.Empty;
            // 회전 컬럼은 CheckBox이므로 여기 도달하지 않음.
            if (!int.TryParse(tb.Text, out int v) || v <= 0)
            {
                MessageBox.Show($"'{header}'에는 양의 정수만 입력 가능합니다.", "입력 오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
            }
        }

        // ─── Numeric input filtering ─────────────────────────────────

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = IntegerRegex.IsMatch(e.Text);

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DecimalRegex.IsMatch(e.Text)) { e.Handled = true; return; }
            if (e.Text == "." && sender is TextBox tb && tb.Text.Contains('.')) e.Handled = true;
        }

        // ─── Charts (LiveCharts series construction) ─────────────────

        private void UpdateCompareCharts()
        {
            var ok = _vm.CompareRows.Where(r => r.Success).ToList();
            if (ok.Count == 0)
            {
                sheetsChart2D.Series = Array.Empty<ISeries>();
                effChart2D.Series = Array.Empty<ISeries>();
                timeChart2D.Series = Array.Empty<ISeries>();
                return;
            }

            string[] labels = ok.Select(r => AbbreviateName(r.AlgorithmName)).ToArray();

            sheetsChart2D.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = ok.Select(r => (double)r.SheetsUsed).ToArray(),
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                },
            };
            sheetsChart2D.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            sheetsChart2D.YAxes = new[] { new Axis { Name = "시트 사용 (개)" } };

            effChart2D.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = ok.Select(r => r.MaterialEfficiency).ToArray(),
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}%",
                },
            };
            effChart2D.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            effChart2D.YAxes = new[] { new Axis { Name = "효율 (%)", MinLimit = 0, MaxLimit = 100 } };

            timeChart2D.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = ok.Select(r => r.ExecutionTimeMs).ToArray(),
                    Fill = new SolidColorPaint(SKColors.Coral),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}ms",
                },
            };
            timeChart2D.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            timeChart2D.YAxes = new[] { new Axis { Name = "시간 (ms)" } };
        }

        private static string AbbreviateName(string name)
        {
            int paren = name.IndexOf('(');
            if (paren > 0 && paren < name.Length - 1)
                return name[..paren].TrimEnd() + Environment.NewLine + name[paren..];
            return name;
        }

        // ─── Pattern canvas rendering ────────────────────────────────

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
                    Width = cw, Height = ch,
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
                    var brush = Palette[pl.OrderIndex % Palette.Length];
                    var rect = new Rectangle
                    {
                        Width = pl.Width * scale, Height = pl.Height * scale,
                        Fill = brush, Stroke = Brushes.DimGray, StrokeThickness = 0.6,
                        ToolTip = $"O{pl.OrderIndex} ({pl.Width}×{pl.Height})" + (pl.Rotated ? " ↻" : ""),
                    };
                    Canvas.SetLeft(rect, pl.X * scale);
                    Canvas.SetTop(rect, pl.Y * scale);
                    canvas.Children.Add(rect);

                    var label = new TextBlock
                    {
                        Text = pl.Rotated ? $"O{pl.OrderIndex}↻" : $"O{pl.OrderIndex}",
                        FontSize = 10, Foreground = Brushes.Black,
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
