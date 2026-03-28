using CuttingStock.Core.Domain;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace CuttingStock
{
    /// <summary>
    /// 철근 절단 최적화 메인 윈도우
    ///
    /// 주요 기능:
    /// - 재고 및 주문 입력/관리
    /// - 3가지 알고리즘 중 선택하여 최적화 실행
    /// - 알고리즘 성능 비교
    /// - 결과 시각화 (차트)
    /// - CSV/Excel 내보내기
    /// </summary>
    public partial class MainWindow : Window
    {
        // 데이터 바인딩을 위한 ObservableCollection
        public ObservableCollection<RebarStock> Stocks { get; set; }
        public ObservableCollection<Order> Orders { get; set; }
        public ObservableCollection<ComparisonResult> ComparisonResults { get; set; }

        // 내보내기 기능을 위한 마지막 실행 결과 저장
        private SolverResult? _lastSingleResult;
        private SolverOptions? _lastParameters;
        private ICuttingSolver? _lastOptimizer;

        public MainWindow()
        {
            InitializeComponent();

            // ObservableCollection 초기화
            Stocks = new ObservableCollection<RebarStock>();
            Orders = new ObservableCollection<Order>();
            ComparisonResults = new ObservableCollection<ComparisonResult>();

            // DataGrid 바인딩
            stockGrid.ItemsSource = Stocks;
            orderGrid.ItemsSource = Orders;
            comparisonGrid.ItemsSource = ComparisonResults;

            DataContext = this;

            // 초기 고급 옵션 패널 업데이트
            UpdateAdvancedOptions();
        }

        #region 기본 입력 기능 (추가/삭제/불러오기/붙여넣기)

        /// <summary>
        /// 재고 추가 버튼 클릭 이벤트
        /// 기본값: 12000mm × 1개
        /// </summary>
        private void AddStock_Click(object sender, RoutedEventArgs e)
        {
            Stocks.Add(new RebarStock(12000, 1));
        }

        private void ImportStock_Click(object sender, RoutedEventArgs e)
        {
            ImportFromFile(Stocks);
        }

        /// <summary>
        /// 주문 추가 버튼 클릭 이벤트
        /// 기본값: 5000mm × 1개
        /// </summary>
        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            Orders.Add(new Order(5000, 1));
        }

        private void ImportOrder_Click(object sender, RoutedEventArgs e)
        {
            ImportFromFile(Orders);
        }

        /// <summary>
        /// DataGrid 복사/붙여넣기 처리 (Ctrl+V)
        /// 엑셀이나 텍스트 파일에서 복사한 데이터를 그리드에 붙여넣습니다.
        /// </summary>
        private void DataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.V &&
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                var grid = sender as DataGrid;
                if (grid == null) return;

                // 붙여넣기 대상 컬렉션 확인
                System.Collections.IList? targetCollection = null;
                if (grid.ItemsSource == Stocks) targetCollection = Stocks;
                else if (grid.ItemsSource == Orders) targetCollection = Orders;

                if (targetCollection != null)
                {
                    PasteFromClipboard(targetCollection);
                    e.Handled = true;
                }
            }
        }

        private void PasteFromClipboard(System.Collections.IList collection)
        {
            try
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                var rows = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int addedCount = 0;

                foreach (var row in rows)
                {
                    var columns = row.Split(new[] { '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length >= 2)
                    {
                        if (int.TryParse(columns[0].Trim(), out int length) &&
                            int.TryParse(columns[1].Trim(), out int quantity))
                        {
                            if (collection is ObservableCollection<RebarStock> stockList)
                            {
                                stockList.Add(new RebarStock(length, quantity));
                            }
                            else if (collection is ObservableCollection<Order> orderList)
                            {
                                orderList.Add(new Order(length, quantity));
                            }
                            addedCount++;
                        }
                    }
                }

                if (addedCount > 0)
                {
                    MessageBox.Show($"{addedCount}개의 항목을 붙여넣었습니다.", "붙여넣기 성공", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"붙여넣기 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFromFile(System.Collections.IList collection)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel/CSV 파일 (*.xlsx;*.csv)|*.xlsx;*.csv|모든 파일 (*.*)|*.*",
                Title = "데이터 불러오기"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(dialog.FileName).ToLower();
                    int addedCount = 0;

                    if (ext == ".csv")
                    {
                        var lines = File.ReadAllLines(dialog.FileName);
                        foreach (var line in lines.Skip(1)) // Skip header assumption
                        {
                            var parts = line.Split(',');
                            if (parts.Length >= 2 &&
                                int.TryParse(parts[0], out int len) &&
                                int.TryParse(parts[1], out int qty))
                            {
                                if (collection is ObservableCollection<RebarStock> s) s.Add(new RebarStock(len, qty));
                                else if (collection is ObservableCollection<Order> o) o.Add(new Order(len, qty));
                                addedCount++;
                            }
                        }
                    }
                    else if (ext == ".xlsx")
                    {
                        using var wb = new XLWorkbook(dialog.FileName);
                        var ws = wb.Worksheets.First();
                        var rangeUsed = ws.RangeUsed();
                        if (rangeUsed != null)
                        {
                            var rows = rangeUsed.RowsUsed().Skip(1); // Skip header

                            foreach (var row in rows)
                            {
                                if (int.TryParse(row.Cell(1).GetValue<string>(), out int len) &&
                                    int.TryParse(row.Cell(2).GetValue<string>(), out int qty))
                                {
                                    if (collection is ObservableCollection<RebarStock> s) s.Add(new RebarStock(len, qty));
                                    else if (collection is ObservableCollection<Order> o) o.Add(new Order(len, qty));
                                    addedCount++;
                                }
                            }
                        }
                    }

                    MessageBox.Show($"{addedCount}개의 데이터를 불러왔습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"파일 읽기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 예제 데이터 로드 버튼 클릭 이벤트
        /// 알고리즘 테스트를 위한 샘플 데이터를 로드합니다.
        /// </summary>
        private void LoadExample_Click(object sender, RoutedEventArgs e)
        {
            Stocks.Clear();
            Orders.Clear();

            // 예제 재고: 12m 철근 20개
            Stocks.Add(new RebarStock(12000, 20));

            // 예제 주문: 다양한 길이
            Orders.Add(new Order(5000, 10));
            Orders.Add(new Order(4000, 15));
            Orders.Add(new Order(3000, 12));
            Orders.Add(new Order(2000, 8));

            MessageBox.Show("예제 데이터를 로드했습니다.\n재고: 12000mm × 20개\n주문: 5000mm×10, 4000mm×15, 3000mm×12, 2000mm×8",
                           "예제 로드", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 파라미터 및 알고리즘 선택

        /// <summary>
        /// UI에서 입력된 최적화 파라미터를 읽어와 객체로 변환
        ///
        /// 파라미터:
        /// - Alpha: 자투리 1mm당 비용 (원/mm)
        /// - Beta: 용접 1회당 비용 (원/회)
        /// - Gamma: 재사용 가능한 자투리 최소 길이 (mm)
        /// - Delta: 용접 가능한 조각 최소 길이 (mm)
        /// - UsageOrder: 재고 사용 순서 (작은 것부터 / 큰 것부터)
        /// </summary>
        private SolverOptions GetParameters()
        {
            try
            {
                return new SolverOptions
                {
                    Alpha = float.Parse(alphaTextBox.Text, CultureInfo.InvariantCulture),
                    Beta = float.Parse(betaTextBox.Text, CultureInfo.InvariantCulture),
                    Gamma = int.Parse(gammaTextBox.Text),
                    Delta = int.Parse(deltaTextBox.Text),
                    UsageOrder = usageOrderComboBox.SelectedIndex == 0
                        ? StockUsageOrder.SmallToLarge
                        : StockUsageOrder.LargeToSmall,
                    EnableWelding = enableWeldingCheckBox.IsChecked ?? false,
                    EnablePatternReduction = enablePatternReductionCheckBox.IsChecked ?? false,
                    MaxPatternCount = int.TryParse(maxPatternCountTextBox.Text, out int maxPattern) ? maxPattern : 0
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파라미터 입력 오류: {ex.Message}\n올바른 숫자를 입력해주세요.",
                               "입력 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// 사용자가 선택한 알고리즘의 인스턴스 생성
        ///
        /// 지원 알고리즘:
        /// 0: Greedy Knapsack DP - DP 기반 로컬 최적화
        /// 1: First Fit Decreasing (FFD) - 빠른 휴리스틱
        /// 2: Best Fit Decreasing (BFD) - 개선된 휴리스틱
        /// </summary>
        private ICuttingSolver GetSelectedOptimizer()
        {
            return algorithmComboBox.SelectedIndex switch
            {
                0 => new GreedyKnapsackSolver(),
                1 => new ColumnGenerationSolver(),
                _ => new GreedyKnapsackSolver()
            };
        }

        /// <summary>
        /// 알고리즘 선택 변경 이벤트
        /// 선택된 알고리즘에 맞는 고급 옵션 패널을 동적으로 업데이트
        /// </summary>
        private void AlgorithmComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAdvancedOptions();
        }

        /// <summary>
        /// 고급 옵션 패널 동적 업데이트
        /// 선택된 알고리즘에 따라 설명 텍스트를 표시
        /// </summary>
        private void UpdateAdvancedOptions()
        {
            if (advancedOptionsPanel == null) return;

            advancedOptionsPanel.Children.Clear();

            switch (algorithmComboBox.SelectedIndex)
            {
                case 0: // Greedy Knapsack
                    advancedOptionsPanel.Children.Add(new TextBlock
                    {
                        Text = "• DP 기반 최적 조합 탐색 (Multi-Pass)\n• 자투리 최소화 우선\n• 용접 지원",
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.DarkGray
                    });
                    break;

                case 1: // Column Generation
                    advancedOptionsPanel.Children.Add(new TextBlock
                    {
                        Text = "• Linear Programming 기반 전역 최적화\n• 이론적으로 가장 최적에 가까움\n• 대규모 입력 시 느릴 수 있음",
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.DarkGray
                    });
                    break;
            }
        }

        #endregion

        #region 최적화 실행

        private async void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (Stocks.Count == 0 || Orders.Count == 0)
            {
                MessageBox.Show("재고와 주문을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;

                // UI 스레드에서 필요한 데이터 캡처
                var stock = Stocks.ToList();
                var orders = Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                var parameters = GetParameters();
                var optimizer = GetSelectedOptimizer();

                _lastParameters = parameters;
                _lastOptimizer = optimizer;

                // 진행률 핸들러
                var progress = new Progress<double>(percent =>
                {
                    loadingProgressBar.IsIndeterminate = false;
                    loadingProgressBar.Value = percent;
                    loadingText.Text = $"최적화 진행 중... {percent:F0}%";
                });

                // 초기 상태 설정
                loadingProgressBar.IsIndeterminate = true;
                loadingProgressBar.Value = 0;
                loadingText.Text = "최적화 준비 중...";

                // 비동기 실행
                var result = await Task.Run(() =>
                {
                    return optimizer.Solve(stock, orders, parameters, progress);
                });

                _lastSingleResult = result;

                if (!result.Success)
                {
                    loadingOverlay.Visibility = Visibility.Collapsed;
                    MessageBox.Show($"최적화 실패: {result.ErrorMessage}",
                                   "최적화 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                mainTabControl.SelectedIndex = 0;

                resultTextBox.Text = $"═══════════════════════════════════════════════════\n" +
                                    $"  알고리즘: {optimizer.Name}\n" +
                                    $"  시간 복잡도: {optimizer.TimeComplexity}\n" +
                                    $"═══════════════════════════════════════════════════\n\n" +
                                    result.GetDetailedReport(parameters);

                // 시각화 데이터 생성
                GenerateVisualizationData(result);

                loadingOverlay.Visibility = Visibility.Collapsed;

                MessageBox.Show($"최적화가 완료되었습니다!\n\n" +
                               $"총 비용: {result.TotalCost:N0}원\n" +
                               $"재료 효율: {result.MaterialEfficiency:F2}%\n" +
                               $"실행 시간: {result.ExecutionTimeMs:F3}ms",
                               "최적화 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private static readonly Regex IntegerRegex = new("[^0-9]+", RegexOptions.Compiled);
        private static readonly Regex DecimalRegex = new("[^0-9.]+", RegexOptions.Compiled);

        private void IntegerTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = IntegerRegex.IsMatch(e.Text);
        }

        private void DecimalTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = DecimalRegex.IsMatch(e.Text);
        }

        private void PatternReductionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (maxPatternCountTextBox != null)
            {
                maxPatternCountTextBox.IsEnabled = true;
            }
        }

        private void PatternReductionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (maxPatternCountTextBox != null)
            {
                maxPatternCountTextBox.IsEnabled = false;
            }
        }

        private void GenerateVisualizationData(SolverResult result)
        {
            var visItems = new System.Collections.ObjectModel.ObservableCollection<VisualizationRow>();
            var legendItems = new System.Collections.ObjectModel.ObservableCollection<LegendItem>();
            var random = new Random(12345);
            var colorCache = new Dictionary<int, System.Windows.Media.Brush>();

            // 스케일링 팩터 계산 (화면에 맞추기 위함)
            // 최대 길이를 기준으로 800px에 맞춤 (스크롤뷰어 내부라 더 커도 되지만 일단 핏하게)
            double maxStockLength = result.CuttingPlans.Any() ? result.CuttingPlans.Max(p => p.StockLength) : 10000;
            double scale = 800.0 / Math.Max(1, maxStockLength);

            foreach (var plan in result.CuttingPlans)
            {
                var row = new VisualizationRow
                {
                    InfoText = $"Stock: {plan.StockLength}mm (Leftover: {plan.Leftover}mm)",
                    TotalWidth = plan.StockLength * scale
                };

                double currentX = 0;
                foreach (var cut in plan.Cuts)
                {
                    if (!colorCache.ContainsKey(cut.Length))
                    {
                        var color = System.Windows.Media.Color.FromRgb(
                            (byte)random.Next(50, 255),
                            (byte)random.Next(50, 255),
                            (byte)random.Next(50, 255));
                        var brush = new System.Windows.Media.SolidColorBrush(color);
                        colorCache[cut.Length] = brush;

                        // 범례 추가
                        legendItems.Add(new LegendItem { Color = brush, Label = $"{cut.Length}mm" });
                    }

                    double width = cut.Length * scale;

                    // 텍스트 표시 여부 결정 (가로 공간이 충분할 때만)
                    string toolTip = $"Length: {cut.Length}mm";
                    string label = width > 40 ? $"{cut.Length}" : ""; // 40px 이상일 때만 텍스트 표시

                    row.Blocks.Add(new VisualizationBlock
                    {
                        Width = width,
                        X = currentX,
                        Color = colorCache[cut.Length],
                        ToolTip = toolTip,
                        Text = label,
                        TextColor = IsBright(colorCache[cut.Length]) ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White
                    });
                    currentX += width;
                }

                // 자투리 표시 (회색)
                if (plan.Leftover > 0)
                {
                    double wWidth = plan.Leftover * scale;
                    row.Blocks.Add(new VisualizationBlock
                    {
                        Width = wWidth,
                        X = currentX,
                        Color = System.Windows.Media.Brushes.LightGray,
                        ToolTip = $"Leftover: {plan.Leftover}mm",
                        Text = wWidth > 40 ? $"{plan.Leftover}" : "",
                        TextColor = System.Windows.Media.Brushes.Black
                    });
                }

                visItems.Add(row);
            }

            // 범례는 길이 순 정렬
            var sortedLegend = new System.Collections.ObjectModel.ObservableCollection<LegendItem>(legendItems.OrderBy(i => int.Parse(i.Label.Replace("mm", ""))));

            visualizationItemsControl.ItemsSource = visItems;
            legendItemsControl.ItemsSource = sortedLegend;
        }

        private bool IsBright(System.Windows.Media.Brush brush)
        {
            if (brush is System.Windows.Media.SolidColorBrush scb)
            {
                // 간단한 밝기 계산 ((R*299 + G*587 + B*114) / 1000)
                double brightness = (scb.Color.R * 0.299 + scb.Color.G * 0.587 + scb.Color.B * 0.114);
                return brightness > 128; // 밝으면 검은 글씨, 어두우면 흰 글씨
            }
            return true;
        }

        public class VisualizationRow
        {
            public required string InfoText { get; set; }
            public double TotalWidth { get; set; }
            public System.Collections.ObjectModel.ObservableCollection<VisualizationBlock> Blocks { get; set; } = new System.Collections.ObjectModel.ObservableCollection<VisualizationBlock>();
        }

        public class VisualizationBlock
        {
            public double Width { get; set; }
            public double X { get; set; }
            public required System.Windows.Media.Brush Color { get; set; }
            public required string ToolTip { get; set; }

            // 텍스트 표시용 속성 추가
            public required string Text { get; set; }
            public required System.Windows.Media.Brush TextColor { get; set; }
        }

        public class LegendItem
        {
            public required System.Windows.Media.Brush Color { get; set; }
            public required string Label { get; set; }
        }

        #endregion

        #region 알고리즘 비교

        private async void CompareAlgorithms_Click(object sender, RoutedEventArgs e)
        {
            if (Stocks.Count == 0 || Orders.Count == 0)
            {
                MessageBox.Show("재고와 주문을 입력해주세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                loadingOverlay.Visibility = Visibility.Visible;
                loadingProgressBar.IsIndeterminate = true;
                loadingText.Text = "알고리즘 비교 중...";

                var stock = Stocks.ToList();
                var orders = Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                var parameters = GetParameters();

                _lastParameters = parameters;

                var optimizers = new List<ICuttingSolver>
                {
                    new GreedyKnapsackSolver(),
                    new ColumnGenerationSolver()
                };

                // Run comparison on background thread
                var (comparisonResults, detailedReport) = await Task.Run(() =>
                {
                    var results = new List<ComparisonResult>();
                    var reports = new System.Text.StringBuilder();
                    reports.AppendLine("═══════════════════════════════════════════════════");
                    reports.AppendLine("  알고리즘 상세 비교");
                    reports.AppendLine("═══════════════════════════════════════════════════");
                    reports.AppendLine();

                    foreach (var optimizer in optimizers)
                    {
                        var ordersCopy = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                        var result = optimizer.Solve(stock, ordersCopy, parameters);

                        results.Add(new ComparisonResult
                        {
                            AlgorithmName = optimizer.Name,
                            TotalCost = result.TotalCost,
                            WasteLength = result.WasteLength,
                            StockUsed = result.StockUsed,
                            MaterialEfficiency = result.MaterialEfficiency,
                            ExecutionTimeMs = result.ExecutionTimeMs,
                            Success = result.Success
                        });

                        reports.AppendLine($"┌─────────────────────────────────────────────────");
                        reports.AppendLine($"│ {optimizer.Name}");
                        reports.AppendLine($"│ 시간 복잡도: {optimizer.TimeComplexity}");
                        reports.AppendLine($"└─────────────────────────────────────────────────");
                        if (result.Success)
                        {
                            reports.AppendLine(result.GetDetailedReport(parameters));
                        }
                        else
                        {
                            reports.AppendLine($"실패: {result.ErrorMessage}");
                        }
                        reports.AppendLine();
                        reports.AppendLine();
                    }

                    return (results, reports.ToString());
                });

                ComparisonResults.Clear();
                foreach (var cr in comparisonResults)
                {
                    ComparisonResults.Add(cr);
                }

                var sortedResults = ComparisonResults
                    .Where(r => r.Success)
                    .OrderBy(r => r.TotalCost)
                    .ToList();

                for (int i = 0; i < sortedResults.Count; i++)
                {
                    sortedResults[i].Rank = i + 1;
                }

                UpdateCharts();

                mainTabControl.SelectedIndex = 2;

                comparisonTextBox.Text = detailedReport;

                loadingOverlay.Visibility = Visibility.Collapsed;

                var bestAlgorithm = sortedResults.FirstOrDefault();
                if (bestAlgorithm != null)
                {
                    MessageBox.Show($"알고리즘 비교가 완료되었습니다!\n\n" +
                                   $"최고 성능: {bestAlgorithm.AlgorithmName}\n" +
                                   $"   총 비용: {bestAlgorithm.TotalCost:N0}원\n" +
                                   $"   재료 효율: {bestAlgorithm.MaterialEfficiency:F2}%\n" +
                                   $"   실행 시간: {bestAlgorithm.ExecutionTimeMs:F3}ms",
                                   "비교 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 비교 결과를 LiveCharts로 시각화
        ///
        /// 3개의 차트를 생성:
        /// 1. 총 비용 차트 (CornflowerBlue)
        /// 2. 재료 효율 차트 (MediumSeaGreen)
        /// 3. 실행 시간 차트 (Coral)
        ///
        /// 각 차트에는 데이터 라벨이 자동으로 표시됩니다.
        /// </summary>
        private void UpdateCharts()
        {
            if (!ComparisonResults.Any()) return;

            var successResults = ComparisonResults.Where(r => r.Success).ToList();
            if (!successResults.Any()) return;

            // ─────────────────────────────────────────────────────────
            // 차트 1: 총 비용 차트 (단위: 원)
            // ─────────────────────────────────────────────────────────
            costChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => (double)r.TotalCost).ToArray(),
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };

            costChart.XAxes = new[]
            {
                new Axis
                {
                    Labels = successResults.Select(r => r.AlgorithmName.Replace(" ", "\n")).ToArray(),
                    LabelsRotation = 0
                }
            };

            costChart.YAxes = new[]
            {
                new Axis
                {
                    Name = "비용 (원)"
                }
            };

            // ─────────────────────────────────────────────────────────
            // 차트 2: 재료 효율 차트 (단위: %)
            // ─────────────────────────────────────────────────────────
            efficiencyChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => r.MaterialEfficiency).ToArray(),
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:F1}%"
                }
            };

            efficiencyChart.XAxes = new[]
            {
                new Axis
                {
                    Labels = successResults.Select(r => r.AlgorithmName.Replace(" ", "\n")).ToArray(),
                    LabelsRotation = 0
                }
            };

            efficiencyChart.YAxes = new[]
            {
                new Axis
                {
                    Name = "효율 (%)",
                    MinLimit = 0,
                    MaxLimit = 100
                }
            };

            // ─────────────────────────────────────────────────────────
            // 차트 3: 실행 시간 차트 (단위: ms)
            // ─────────────────────────────────────────────────────────
            timeChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => r.ExecutionTimeMs).ToArray(),
                    Fill = new SolidColorPaint(SKColors.Coral),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:F1}ms"
                }
            };

            timeChart.XAxes = new[]
            {
                new Axis
                {
                    Labels = successResults.Select(r => r.AlgorithmName.Replace(" ", "\n")).ToArray(),
                    LabelsRotation = 0
                }
            };

            timeChart.YAxes = new[]
            {
                new Axis
                {
                    Name = "시간 (ms)"
                }
            };
        }

        #endregion

        #region 내보내기 기능

        /// <summary>
        /// 단일 최적화 결과를 CSV로 내보내기
        /// UTF-8 인코딩으로 한글을 올바르게 저장합니다.
        /// </summary>
        private void ExportToCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSingleResult == null || _lastOptimizer == null || _lastParameters == null)
            {
                MessageBox.Show("먼저 최적화를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv",
                    FileName = $"최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExportSingleResultToCsv(dialog.FileName, _lastOptimizer, _lastSingleResult, _lastParameters);
                    MessageBox.Show($"CSV 파일로 저장되었습니다.\n{dialog.FileName}", "저장 완료",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSingleResult == null || _lastOptimizer == null || _lastParameters == null)
            {
                MessageBox.Show("먼저 최적화를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = $"최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExportSingleResultToExcel(dialog.FileName, _lastOptimizer, _lastSingleResult, _lastParameters);
                    MessageBox.Show($"Excel 파일로 저장되었습니다.\n{dialog.FileName}", "저장 완료",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportComparisonToCsv_Click(object sender, RoutedEventArgs e)
        {
            if (!ComparisonResults.Any())
            {
                MessageBox.Show("먼저 알고리즘 비교를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv",
                    FileName = $"알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExportComparisonResultsToCsv(dialog.FileName);
                    MessageBox.Show($"CSV 파일로 저장되었습니다.\n{dialog.FileName}", "저장 완료",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportComparisonToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (!ComparisonResults.Any())
            {
                MessageBox.Show("먼저 알고리즘 비교를 실행해주세요.", "내보내기 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = $"알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExportComparisonResultsToExcel(dialog.FileName);
                    MessageBox.Show($"Excel 파일로 저장되었습니다.\n{dialog.FileName}", "저장 완료",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportSingleResultToCsv(string filename, ICuttingSolver optimizer, SolverResult result, SolverOptions parameters)
        {
            using var writer = new StreamWriter(filename, false, System.Text.Encoding.UTF8);

            writer.WriteLine("철근 절단 최적화 결과");
            writer.WriteLine($"날짜,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"알고리즘,{optimizer.Name}");
            writer.WriteLine($"시간 복잡도,{optimizer.TimeComplexity}");
            writer.WriteLine();

            writer.WriteLine("파라미터");
            writer.WriteLine($"Alpha (자투리 비용),{parameters.Alpha}");
            writer.WriteLine($"Beta (용접 비용),{parameters.Beta}");
            writer.WriteLine($"Gamma (재사용 최소),{parameters.Gamma}");
            writer.WriteLine($"Delta (용접 최소),{parameters.Delta}");
            writer.WriteLine($"재고 사용 순서,{parameters.UsageOrder}");
            writer.WriteLine();

            writer.WriteLine("결과 요약");
            writer.WriteLine($"총 비용,{result.TotalCost}원");
            writer.WriteLine($"낭비 길이,{result.WasteLength}mm");
            writer.WriteLine($"재고 사용,{result.StockUsed}개");
            writer.WriteLine($"재료 효율,{result.MaterialEfficiency:F2}%");
            writer.WriteLine($"실행 시간,{result.ExecutionTimeMs:F3}ms");
            writer.WriteLine();

            writer.WriteLine("절단 계획");
            writer.WriteLine("번호,재고 길이,절단 개수,자투리");
            for (int i = 0; i < result.CuttingPlans.Count; i++)
            {
                var plan = result.CuttingPlans[i];
                writer.WriteLine($"{i + 1},{plan.StockLength},{plan.Cuts.Count},{plan.Leftover}");
            }
        }

        private void ExportSingleResultToExcel(string filename, ICuttingSolver optimizer, SolverResult result, SolverOptions parameters)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("최적화 결과");

            int row = 1;

            worksheet.Cell(row, 1).Value = "철근 절단 최적화 결과";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "날짜:";
            worksheet.Cell(row++, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 1).Value = "알고리즘:";
            worksheet.Cell(row++, 2).Value = optimizer.Name;
            worksheet.Cell(row, 1).Value = "시간 복잡도:";
            worksheet.Cell(row++, 2).Value = optimizer.TimeComplexity;
            row++;

            worksheet.Cell(row, 1).Value = "파라미터";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "Alpha (자투리 비용):";
            worksheet.Cell(row++, 2).Value = parameters.Alpha;
            worksheet.Cell(row, 1).Value = "Beta (용접 비용):";
            worksheet.Cell(row++, 2).Value = parameters.Beta;
            worksheet.Cell(row, 1).Value = "Gamma (재사용 최소):";
            worksheet.Cell(row++, 2).Value = parameters.Gamma;
            worksheet.Cell(row, 1).Value = "Delta (용접 최소):";
            worksheet.Cell(row++, 2).Value = parameters.Delta;
            worksheet.Cell(row, 1).Value = "재고 사용 순서:";
            worksheet.Cell(row++, 2).Value = parameters.UsageOrder.ToString();
            row++;

            worksheet.Cell(row, 1).Value = "결과 요약";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "총 비용:";
            worksheet.Cell(row++, 2).Value = $"{result.TotalCost}원";
            worksheet.Cell(row, 1).Value = "낭비 길이:";
            worksheet.Cell(row++, 2).Value = $"{result.WasteLength}mm";
            worksheet.Cell(row, 1).Value = "재고 사용:";
            worksheet.Cell(row++, 2).Value = $"{result.StockUsed}개";
            worksheet.Cell(row, 1).Value = "재료 효율:";
            worksheet.Cell(row++, 2).Value = $"{result.MaterialEfficiency:F2}%";
            worksheet.Cell(row, 1).Value = "실행 시간:";
            worksheet.Cell(row++, 2).Value = $"{result.ExecutionTimeMs:F3}ms";
            row++;

            worksheet.Cell(row, 1).Value = "절단 계획";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "번호";
            worksheet.Cell(row, 2).Value = "재고 길이";
            worksheet.Cell(row, 3).Value = "절단 개수";
            worksheet.Cell(row, 4).Value = "자투리";
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;

            for (int i = 0; i < result.CuttingPlans.Count; i++)
            {
                var plan = result.CuttingPlans[i];
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = plan.StockLength;
                worksheet.Cell(row, 3).Value = plan.Cuts.Count;
                worksheet.Cell(row, 4).Value = plan.Leftover;
                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filename);
        }

        private void ExportComparisonResultsToCsv(string filename)
        {
            using var writer = new StreamWriter(filename, false, System.Text.Encoding.UTF8);

            writer.WriteLine("알고리즘 비교 결과");
            writer.WriteLine($"날짜,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();

            writer.WriteLine("알고리즘,총 비용,낭비(mm),재고 사용,효율(%),실행 시간(ms),순위");

            foreach (var result in ComparisonResults.OrderBy(r => r.Rank))
            {
                writer.WriteLine($"{result.AlgorithmName},{result.TotalCost},{result.WasteLength}," +
                               $"{result.StockUsed},{result.MaterialEfficiency:F2},{result.ExecutionTimeMs:F3},{result.Rank}");
            }
        }

        private void ExportComparisonResultsToExcel(string filename)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("알고리즘 비교");

            int row = 1;

            worksheet.Cell(row, 1).Value = "알고리즘 비교 결과";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "날짜:";
            worksheet.Cell(row++, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            row++;

            worksheet.Cell(row, 1).Value = "알고리즘";
            worksheet.Cell(row, 2).Value = "총 비용";
            worksheet.Cell(row, 3).Value = "낭비(mm)";
            worksheet.Cell(row, 4).Value = "재고 사용";
            worksheet.Cell(row, 5).Value = "효율(%)";
            worksheet.Cell(row, 6).Value = "실행 시간(ms)";
            worksheet.Cell(row, 7).Value = "순위";
            worksheet.Range(row, 1, row, 7).Style.Font.Bold = true;
            row++;

            foreach (var result in ComparisonResults.OrderBy(r => r.Rank))
            {
                worksheet.Cell(row, 1).Value = result.AlgorithmName;
                worksheet.Cell(row, 2).Value = result.TotalCost;
                worksheet.Cell(row, 3).Value = result.WasteLength;
                worksheet.Cell(row, 4).Value = result.StockUsed;
                worksheet.Cell(row, 5).Value = result.MaterialEfficiency;
                worksheet.Cell(row, 6).Value = result.ExecutionTimeMs;
                worksheet.Cell(row, 7).Value = result.Rank;

                if (result.Rank == 1)
                {
                    worksheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filename);
        }

        #endregion
    }
}
