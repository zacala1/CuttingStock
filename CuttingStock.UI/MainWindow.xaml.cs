using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClosedXML.Excel;
using CuttingStock.Core.Models;
using CuttingStock.UI.Services;
using CuttingStock.UI.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CuttingStock
{
    /// <summary>
    /// 1D 메인 윈도우. MVVM 전환 후 thin view 역할만 한다:
    ///   - DataContext에 MainViewModel을 바인딩.
    ///   - DataGrid SelectedItems / 파일 다이얼로그 / 클립보드 / 키 단축키 등
    ///     ViewModel이 직접 처리하기 어려운 WPF 정합성 코드만 남김.
    ///   - LiveCharts 시리즈는 코드에서 직접 갱신하고, ComparisonResults가
    ///     채워질 때 ViewModel.PropertyChanged 이벤트로 트리거됨.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(new DialogService());
            DataContext = _vm;
            _vm.PropertyChanged += Vm_PropertyChanged;
            UpdateAdvancedOptions();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // LiveCharts series can't be built declaratively in XAML (the
            // ColumnSeries<double> generic needs WPF runtime types), so we
            // refresh them whenever the comparison results change.
            if (e.PropertyName == nameof(MainViewModel.HasComparisonResults) && _vm.HasComparisonResults)
                UpdateCharts();
        }

        // ─── DataGrid: selection / paste / cell validation ───────────

        private void DeleteSelectedStock_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedStocks(stockGrid.SelectedItems.Cast<StockRow>());

        private void DeleteSelectedOrder_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedOrders(orderGrid.SelectedItems.Cast<OrderRow>());

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (e.EditingElement is TextBox tb && (!int.TryParse(tb.Text, out int v) || v <= 0))
            {
                MessageBox.Show("양의 정수를 입력해주세요.", "입력 오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
            }
        }

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
                    if (cols.Length < 2) continue;
                    if (!int.TryParse(cols[0].Trim(), out int len) ||
                        !int.TryParse(cols[1].Trim(), out int qty) ||
                        len <= 0 || qty <= 0) continue;

                    if (grid.ItemsSource == _vm.Stocks) _vm.Stocks.Add(new StockRow { Length = len, Quantity = qty });
                    else if (grid.ItemsSource == _vm.Orders) _vm.Orders.Add(new OrderRow { Length = len, Quantity = qty });
                    else continue;
                    added++;
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

        // ─── File import (CSV / XLSX → ObservableCollection) ─────────

        private void ImportStock_Click(object sender, RoutedEventArgs e)
        {
            int added = ImportFromFile(
                addStock:  (l, q) => _vm.Stocks.Add(new StockRow { Length = l, Quantity = q }),
                addOrder:  null);
            if (added >= 0)
                MessageBox.Show($"{added}개의 데이터를 불러왔습니다.", "완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportOrder_Click(object sender, RoutedEventArgs e)
        {
            int added = ImportFromFile(
                addStock: null,
                addOrder: (l, q) => _vm.Orders.Add(new OrderRow { Length = l, Quantity = q }));
            if (added >= 0)
                MessageBox.Show($"{added}개의 데이터를 불러왔습니다.", "완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private int ImportFromFile(Action<int, int>? addStock, Action<int, int>? addOrder)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel/CSV 파일 (*.xlsx;*.csv)|*.xlsx;*.csv|모든 파일 (*.*)|*.*",
                Title = "데이터 불러오기",
            };
            if (dlg.ShowDialog() != true) return -1;

            try
            {
                string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                int added = 0;
                void Add(int len, int qty)
                {
                    addStock?.Invoke(len, qty);
                    addOrder?.Invoke(len, qty);
                    added++;
                }

                if (ext == ".csv")
                {
                    var lines = File.ReadAllLines(dlg.FileName);
                    var data = lines.Length > 0 &&
                               !int.TryParse(lines[0].Split(',')[0].Trim(), out _)
                        ? lines.Skip(1) : lines;
                    foreach (var line in data)
                    {
                        var p = line.Split(',');
                        if (p.Length >= 2 &&
                            int.TryParse(p[0].Trim(), out int len) &&
                            int.TryParse(p[1].Trim(), out int qty) &&
                            len > 0 && qty > 0)
                            Add(len, qty);
                    }
                }
                else if (ext == ".xlsx")
                {
                    using var wb = new XLWorkbook(dlg.FileName);
                    var ws = wb.Worksheets.First();
                    var rangeUsed = ws.RangeUsed();
                    if (rangeUsed == null) return 0;

                    var allRows = rangeUsed.RowsUsed().ToList();
                    var dataRows = allRows.Count > 0 &&
                                   !int.TryParse(allRows[0].Cell(1).GetValue<string>(), out _)
                        ? allRows.Skip(1) : allRows;

                    foreach (var row in dataRows)
                    {
                        if (int.TryParse(row.Cell(1).GetValue<string>(), out int len) &&
                            int.TryParse(row.Cell(2).GetValue<string>(), out int qty) &&
                            len > 0 && qty > 0)
                            Add(len, qty);
                    }
                }
                return added;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 읽기 실패: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return -1;
            }
        }

        // ─── Numeric input filtering ─────────────────────────────────

        private static readonly Regex IntegerRegex = new("[^0-9]+", RegexOptions.Compiled);
        private static readonly Regex DecimalRegex = new("[^0-9.]+", RegexOptions.Compiled);

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = IntegerRegex.IsMatch(e.Text);

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DecimalRegex.IsMatch(e.Text)) { e.Handled = true; return; }
            if (e.Text == "." && sender is TextBox tb && tb.Text.Contains('.')) e.Handled = true;
        }

        // ─── Algorithm advanced-options description ──────────────────

        private void AlgorithmComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateAdvancedOptions();

        private void UpdateAdvancedOptions()
        {
            if (advancedOptionsPanel == null) return;
            advancedOptionsPanel.Children.Clear();
            string text = algorithmComboBox?.SelectedIndex switch
            {
                0 => "• DP 기반 최적 조합 탐색 (Multi-Pass)\n• 자투리 최소화 우선\n• 용접 지원",
                1 => "• Linear Programming 기반 전역 최적화\n• Floor-then-Residual 정수 라운딩\n• 대규모 입력 시 느릴 수 있음",
                2 => "• Arc Flow 네트워크 모델 + MIP 솔버\n• 수학적으로 증명된 최적해\n• Kerf 자연 지원\n• 30초 시간 제한",
                _ => string.Empty,
            };
            advancedOptionsPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.DarkGray,
            });
        }

        // ─── Keyboard shortcuts ──────────────────────────────────────

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift)   == ModifierKeys.Shift;

            if (e.Key == Key.F1)
            { _vm.LoadExampleCommand.Execute(null); e.Handled = true; return; }
            if (ctrl && !shift && e.Key == Key.R)
            { _vm.CalculateCommand.Execute(null); e.Handled = true; return; }
            if (ctrl && shift && e.Key == Key.C)
            { _vm.CompareAlgorithmsCommand.Execute(null); e.Handled = true; return; }
            if (ctrl && !shift && e.Key == Key.S && _vm.HasSingleResult)
            { _vm.ExportToExcelCommand.Execute(null); e.Handled = true; }
        }

        // ─── LiveCharts series refresh (called from PropertyChanged) ─

        private void UpdateCharts()
        {
            var successResults = _vm.ComparisonResults.Where(r => r.Success).ToList();
            if (successResults.Count == 0) return;

            string[] labels = successResults.Select(r => AbbreviateName(r.AlgorithmName)).ToArray();

            costChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => (double)r.TotalCost).ToArray(),
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                },
            };
            costChart.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            costChart.YAxes = new[] { new Axis { Name = "비용 (원)" } };

            efficiencyChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => r.MaterialEfficiency).ToArray(),
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}%",
                },
            };
            efficiencyChart.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            efficiencyChart.YAxes = new[] { new Axis { Name = "효율 (%)", MinLimit = 0, MaxLimit = 100 } };

            timeChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => r.ExecutionTimeMs).ToArray(),
                    Fill = new SolidColorPaint(SKColors.Coral),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}ms",
                },
            };
            timeChart.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            timeChart.YAxes = new[] { new Axis { Name = "시간 (ms)" } };
        }

        private static string AbbreviateName(string name)
        {
            int paren = name.IndexOf('(');
            if (paren > 0 && paren < name.Length - 1)
                return name[..paren].TrimEnd() + Environment.NewLine + name[paren..];
            return name;
        }
    }
}
