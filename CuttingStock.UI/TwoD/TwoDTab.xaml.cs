using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
    public partial class TwoDTab : UserControl, IDisposable
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
            _vm.PropertyChanged += Vm_PropertyChanged;
        }

        public event EventHandler<string>? ScenarioSaved
        {
            add => _vm.ScenarioSaved += value;
            remove => _vm.ScenarioSaved -= value;
        }

        public event EventHandler<string>? ScenarioLoaded
        {
            add => _vm.ScenarioLoaded += value;
            remove => _vm.ScenarioLoaded -= value;
        }

        public event EventHandler? RecentScenariosRequested;

        public FrameworkElement RecentMenuPlacementTarget => btnRecent2D;

        public int AlgorithmIndex
        {
            get => _vm.AlgorithmIndex;
            set => _vm.AlgorithmIndex = value;
        }

        public int SolverCount => _vm.SolverDescriptors.Count;

        public bool LoadScenarioFromPath(string path) => _vm.LoadScenarioFromPath(path);

        public void Dispose()
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm.Dispose();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TwoDViewModel.RenderProjection))
            {
                if (_vm.RenderProjection is { } renderProjection)
                    RenderPatterns(renderProjection);
                else
                    visualization2DPanel.Children.Clear();
            }
            else if (e.PropertyName == nameof(TwoDViewModel.ChartProjection))
            {
                UpdateCompareCharts(_vm.ChartProjection);
            }
        }

        // ─── DataGrid: selection delete / paste / validation ─────────

        private void DeleteSheet_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedSheets(sheetGrid.SelectedItems.Cast<SheetRow>());

        private void DeleteRectOrder_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedOrders(rectOrderGrid.SelectedItems.Cast<RectOrderRow>());

        private void Recent2D_Click(object sender, RoutedEventArgs e) =>
            RecentScenariosRequested?.Invoke(this, EventArgs.Empty);

        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            if (sender is not DataGrid grid) return;

            try
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                int added;
                if (grid.ItemsSource == _vm.Sheets)
                {
                    var rows = ClipboardRowParser.ParseSheetRows(text);
                    foreach (var row in rows)
                    {
                        _vm.Sheets.Add(new SheetRow
                        {
                            Width = row.Width,
                            Height = row.Height,
                            Quantity = row.Quantity,
                        });
                    }
                    added = rows.Count;
                }
                else if (grid.ItemsSource == _vm.Orders)
                {
                    var rows = ClipboardRowParser.ParseRectOrderRows(text);
                    foreach (var row in rows)
                    {
                        _vm.Orders.Add(new RectOrderRow
                        {
                            Width = row.Width,
                            Height = row.Height,
                            Quantity = row.Quantity,
                            AllowRotation = row.AllowRotation,
                        });
                    }
                    added = rows.Count;
                }
                else return;

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

        // ─── Keyboard shortcuts ──────────────────────────────────────

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Esc cancels an in-flight 2D solve. MainWindow's window-level handler
            // only knows about the 1D ViewModel, so we route Esc locally here.
            if (e.Key == Key.Escape && _vm.CanCancel)
            {
                _vm.CancelCommand.Execute(null);
                e.Handled = true;
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

        private void UpdateCompareCharts(TwoDChartProjection projection)
        {
            if (projection.Labels.Count == 0)
            {
                sheetsChart2D.Series = Array.Empty<ISeries>();
                effChart2D.Series = Array.Empty<ISeries>();
                timeChart2D.Series = Array.Empty<ISeries>();
                return;
            }

            sheetsChart2D.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = projection.SheetsUsed,
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                },
            };
            sheetsChart2D.XAxes = new[] { new Axis { Labels = projection.Labels, LabelsRotation = -15 } };
            sheetsChart2D.YAxes = new[] { new Axis { Name = "시트 사용 (개)" } };

            effChart2D.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = projection.MaterialEfficiency,
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}%",
                },
            };
            effChart2D.XAxes = new[] { new Axis { Labels = projection.Labels, LabelsRotation = -15 } };
            effChart2D.YAxes = new[] { new Axis { Name = "효율 (%)", MinLimit = 0, MaxLimit = 100 } };

            timeChart2D.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = projection.ExecutionTimeMs,
                    Fill = new SolidColorPaint(SKColors.Coral),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}ms",
                },
            };
            timeChart2D.XAxes = new[] { new Axis { Labels = projection.Labels, LabelsRotation = -15 } };
            timeChart2D.YAxes = new[] { new Axis { Name = "시간 (ms)" } };
        }

        // ─── Pattern canvas rendering ────────────────────────────────

        private void RenderPatterns(TwoDRenderProjection projection)
        {
            visualization2DPanel.Children.Clear();
            const double targetMaxDim = 700.0;

            int patternIdx = 1;
            foreach (var pat in projection.Patterns)
            {
                double scale = targetMaxDim / Math.Max(pat.SheetWidth, pat.SheetHeight);
                double cw = pat.SheetWidth * scale;
                double ch = pat.SheetHeight * scale;

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
                    Text = $"패턴 #{patternIdx} — Sheet {pat.SheetWidth}×{pat.SheetHeight}  ×{pat.Multiplicity}  | items={pat.Placements.Count}  | eff={pat.Efficiency:F1}%",
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
                if (projection.Trim > 0)
                {
                    double tx = projection.Trim * scale;
                    var trimRect = new Rectangle
                    {
                        Width = (pat.SheetWidth - 2 * projection.Trim) * scale,
                        Height = (pat.SheetHeight - 2 * projection.Trim) * scale,
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
