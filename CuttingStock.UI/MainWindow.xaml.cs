using CuttingStock.Core.Domain;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Models;
using CuttingStock.UI.Services;
using CuttingStock.UI.ViewModels;
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
        public ObservableCollection<StockRow> Stocks { get; set; }
        public ObservableCollection<OrderRow> Orders { get; set; }
        public ObservableCollection<ComparisonResult> ComparisonResults { get; set; }

        // 내보내기 기능을 위한 마지막 실행 결과 저장
        private SolverResult? _lastSingleResult;
        private SolverOptions? _lastParameters;
        private ICuttingSolver? _lastOptimizer;

        /// <summary>
        /// Window-wide shortcuts: Ctrl+R runs the selected algorithm, Ctrl+Shift+C runs
        /// the comparison, F1 loads the example dataset. Routed through the existing
        /// button handlers so the busy-state plumbing is shared.
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var mods = System.Windows.Input.Keyboard.Modifiers;
            bool ctrl  = (mods & System.Windows.Input.ModifierKeys.Control) != 0;
            bool shift = (mods & System.Windows.Input.ModifierKeys.Shift) != 0;

            if (e.Key == System.Windows.Input.Key.R && ctrl && !shift && btnCalculate.IsEnabled)
            {
                Calculate_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.C && ctrl && shift && btnCompare.IsEnabled)
            {
                CompareAlgorithms_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F1)
            {
                LoadExample_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // ObservableCollection 초기화
            Stocks = new ObservableCollection<StockRow>();
            Orders = new ObservableCollection<OrderRow>();
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
            Stocks.Add(new StockRow { Length = 12000, Quantity = 1 });
        }

        private void ImportStock_Click(object sender, RoutedEventArgs e)
        {
            ImportFromFile(Stocks);
        }

        private void DeleteSelectedStock_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedItems(stockGrid, Stocks);
        }

        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            Orders.Add(new OrderRow { Length = 5000, Quantity = 1 });
        }

        private void ImportOrder_Click(object sender, RoutedEventArgs e)
        {
            ImportFromFile(Orders);
        }

        private void DeleteSelectedOrder_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedItems(orderGrid, Orders);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("재고와 주문 데이터를 모두 삭제하시겠습니까?", "전체 초기화",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Stocks.Clear();
                Orders.Clear();
            }
        }

        private static void DeleteSelectedItems<T>(DataGrid grid, ObservableCollection<T> collection)
        {
            var selected = grid.SelectedItems.Cast<T>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("삭제할 항목을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Multi-row delete is destructive enough to warrant a confirmation,
            // mirroring how ClearAll prompts the user before wiping the grid.
            if (selected.Count > 1 &&
                MessageBox.Show($"{selected.Count}개 항목을 삭제하시겠습니까?", "삭제 확인",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            foreach (var item in selected)
            {
                collection.Remove(item);
            }
        }

        /// <summary>
        /// DataGrid 셀 편집 완료 시 유효성 검사
        /// 양의 정수만 허용하며, 잘못된 값은 되돌림
        /// </summary>
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;

            if (e.EditingElement is TextBox textBox)
            {
                if (!int.TryParse(textBox.Text, out int value) || value <= 0)
                {
                    MessageBox.Show("양의 정수를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    e.Cancel = true;
                }
            }
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
                bool isStock = grid.ItemsSource == Stocks;
                if (isStock) targetCollection = Stocks;
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
                            int.TryParse(columns[1].Trim(), out int quantity) &&
                            length > 0 && quantity > 0)
                        {
                            if (collection is ObservableCollection<StockRow> stockList)
                            {
                                stockList.Add(new StockRow { Length = length, Quantity = quantity });
                            }
                            else if (collection is ObservableCollection<OrderRow> orderList)
                            {
                                orderList.Add(new OrderRow { Length = length, Quantity = quantity });
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
                        // Skip first row only if it looks like a header (non-numeric)
                        var dataLines = lines.Length > 0 && !int.TryParse(lines[0].Split(',')[0].Trim(), out _)
                            ? lines.Skip(1)
                            : lines;

                        foreach (var line in dataLines)
                        {
                            var parts = line.Split(',');
                            if (parts.Length >= 2 &&
                                int.TryParse(parts[0].Trim(), out int len) &&
                                int.TryParse(parts[1].Trim(), out int qty) &&
                                len > 0 && qty > 0)
                            {
                                if (collection is ObservableCollection<StockRow> s) s.Add(new StockRow { Length = len, Quantity = qty });
                                else if (collection is ObservableCollection<OrderRow> o) o.Add(new OrderRow { Length = len, Quantity = qty });
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
                            var allRows = rangeUsed.RowsUsed().ToList();
                            // Skip first row only if it looks like a header
                            var dataRows = allRows.Count > 0 && !int.TryParse(allRows[0].Cell(1).GetValue<string>(), out _)
                                ? allRows.Skip(1)
                                : allRows;

                            foreach (var row in dataRows)
                            {
                                if (int.TryParse(row.Cell(1).GetValue<string>(), out int len) &&
                                    int.TryParse(row.Cell(2).GetValue<string>(), out int qty) &&
                                    len > 0 && qty > 0)
                                {
                                    if (collection is ObservableCollection<StockRow> s) s.Add(new StockRow { Length = len, Quantity = qty });
                                    else if (collection is ObservableCollection<OrderRow> o) o.Add(new OrderRow { Length = len, Quantity = qty });
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
            Stocks.Add(new StockRow { Length = 12000, Quantity = 20 });

            // 예제 주문: 다양한 길이
            Orders.Add(new OrderRow { Length = 5000, Quantity = 10 });
            Orders.Add(new OrderRow { Length = 4000, Quantity = 15 });
            Orders.Add(new OrderRow { Length = 3000, Quantity = 12 });
            Orders.Add(new OrderRow { Length = 2000, Quantity = 8 });

            MessageBox.Show("예제 데이터를 로드했습니다.\n재고: 12000mm × 20개\n주문: 5000mm×10, 4000mm×15, 3000mm×12, 2000mm×8",
                           "예제 로드", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region ViewModel → Domain 변환

        /// <summary>
        /// DataGrid의 StockRow 행들을 도메인 RebarStock 리스트로 변환합니다.
        /// </summary>
        private List<RebarStock> BuildStock()
        {
            return Stocks.Select(s => new RebarStock(s.Length, s.Quantity)).ToList();
        }

        /// <summary>
        /// DataGrid의 OrderRow 행들을 도메인 Order 리스트로 변환합니다.
        /// </summary>
        private List<Order> BuildOrders()
        {
            return Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
        }

        #endregion

        #region 파라미터 및 알고리즘 선택

        /// <summary>
        /// UI 파라미터를 파싱하여 SolverOptions를 반환합니다.
        /// 파싱 실패 시 null을 반환합니다.
        /// </summary>
        private SolverOptions? GetParameters()
        {
            if (!float.TryParse(alphaTextBox.Text, CultureInfo.InvariantCulture, out float alpha) || alpha < 0)
            {
                MessageBox.Show("Alpha 값을 올바르게 입력해주세요. (0 이상의 숫자)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!float.TryParse(betaTextBox.Text, CultureInfo.InvariantCulture, out float beta) || beta < 0)
            {
                MessageBox.Show("Beta 값을 올바르게 입력해주세요. (0 이상의 숫자)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!int.TryParse(gammaTextBox.Text, out int gamma) || gamma < 0)
            {
                MessageBox.Show("Gamma 값을 올바르게 입력해주세요. (0 이상의 정수)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!int.TryParse(deltaTextBox.Text, out int delta) || delta <= 0)
            {
                MessageBox.Show("Delta 값을 올바르게 입력해주세요. (1 이상의 정수)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
            if (!int.TryParse(kerfTextBox.Text, out int kerf) || kerf < 0)
            {
                MessageBox.Show("Kerf 값을 올바르게 입력해주세요. (0 이상의 정수)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return new SolverOptions
            {
                Alpha = alpha,
                Beta = beta,
                Gamma = gamma,
                Delta = delta,
                Kerf = kerf,
                UsageOrder = usageOrderComboBox.SelectedIndex == 0
                    ? StockUsageOrder.SmallToLarge
                    : StockUsageOrder.LargeToSmall,
                EnableWelding = enableWeldingCheckBox.IsChecked ?? false,
            };
        }

        private ICuttingSolver GetSelectedOptimizer()
        {
            return algorithmComboBox.SelectedIndex switch
            {
                0 => new GreedyKnapsackSolver(),
                1 => new ColumnGenerationSolver(),
                2 => new ArcFlowSolver(),
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
                        Text = "• Linear Programming 기반 전역 최적화\n• Floor-then-Residual 정수 라운딩\n• 대규모 입력 시 느릴 수 있음",
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.DarkGray
                    });
                    break;

                case 2: // Arc Flow MIP
                    advancedOptionsPanel.Children.Add(new TextBlock
                    {
                        Text = "• Arc Flow 네트워크 모델 + MIP 솔버\n• 수학적으로 증명된 최적해\n• Kerf 자연 지원\n• 30초 시간 제한",
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.DarkGray
                    });
                    break;
            }
        }

        #endregion

        #region 최적화 실행

        private void SetRunningState(bool running)
        {
            btnCalculate.IsEnabled = !running;
            btnCompare.IsEnabled = !running;
            loadingOverlay.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (Stocks.Count == 0 || Orders.Count == 0)
            {
                MessageBox.Show("재고와 주문을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parameters = GetParameters();
            if (parameters == null) return;

            SolverResult? result = null;
            ICuttingSolver? optimizer = null;
            try
            {
                SetRunningState(true);

                var stock = BuildStock();
                var orders = BuildOrders();
                optimizer = GetSelectedOptimizer();

                _lastParameters = parameters;
                _lastOptimizer = optimizer;

                var progress = new Progress<double>(percent =>
                {
                    loadingProgressBar.IsIndeterminate = false;
                    loadingProgressBar.Value = percent;
                    loadingText.Text = $"최적화 진행 중... {percent:F0}%";
                });

                loadingProgressBar.IsIndeterminate = true;
                loadingProgressBar.Value = 0;
                loadingText.Text = "최적화 준비 중...";

                result = await Task.Run(() => optimizer.Solve(stock, orders, parameters, progress));

                _lastSingleResult = result;
                mainTabControl.SelectedIndex = 0;

                resultTextBox.Text = $"═══════════════════════════════════════════════════\n" +
                                    $"  알고리즘: {optimizer.Name}\n" +
                                    $"  시간 복잡도: {optimizer.TimeComplexity}\n" +
                                    $"═══════════════════════════════════════════════════\n\n" +
                                    result.GetDetailedReport(parameters);

                if (result.Success && result.CuttingPlans.Count > 0)
                {
                    GenerateVisualizationData(result);
                    visualizationPlaceholder.Visibility = Visibility.Collapsed;
                    visualizationScrollViewer.Visibility = Visibility.Visible;
                }

                btnExportCsv.IsEnabled = true;
                btnExportExcel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetRunningState(false);
            }

            // Post-completion dialog runs after the running-state is cleared so the
            // user can interact with the UI immediately after dismissing the prompt.
            if (result == null) return;
            if (!result.Success)
            {
                MessageBox.Show($"최적화 실패: {result.ErrorMessage}\n\n부분 결과는 결과 탭에서 확인 가능합니다.",
                               "최적화 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show($"최적화가 완료되었습니다!\n\n" +
                               $"총 비용: {result.TotalCost:N0}원\n" +
                               $"재료 효율: {result.MaterialEfficiency:F2}%\n" +
                               $"실행 시간: {result.ExecutionTimeMs:F3}ms",
                               "최적화 완료", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (DecimalRegex.IsMatch(e.Text))
            {
                e.Handled = true;
                return;
            }
            // Block second decimal point
            if (e.Text == "." && sender is TextBox tb && tb.Text.Contains('.'))
            {
                e.Handled = true;
            }
        }


        private void GenerateVisualizationData(SolverResult result)
        {
            var visItems = new List<VisualizationRow>();
            var legendItems = new List<LegendItem>();
            var random = new Random(12345);
            var colorCache = new Dictionary<int, System.Windows.Media.Brush>();

            // Bar width scales to the visible viewport so the visualization stays
            // legible as the user resizes the window. Clamp at a sane minimum
            // because the ItemsControl can briefly report ActualWidth=0 during layout.
            double barTotalWidth = Math.Max(400.0, visualizationScrollViewer.ActualWidth - 60.0);

            // 패턴 그룹핑: 동일 절단 패턴끼리 묶기
            var grouped = result.CuttingPlans
                .GroupBy(p => PatternKey(p))
                .Select(g => (Plan: g.First(), Count: g.Count()))
                .ToList();

            int groupNum = 1;
            foreach (var (plan, count) in grouped)
            {
                double scale = barTotalWidth / Math.Max(1, plan.StockLength);

                // 절단 목록 문자열
                var cutLengths = string.Join(" + ", plan.Cuts.Select(c => $"{c.Length}"));
                string countLabel = count > 1 ? $"  [x {count}개]" : "";
                string effPercent = plan.StockLength > 0
                    ? $"{100.0 * plan.Cuts.Sum(c => c.Length) / plan.StockLength:F1}%"
                    : "0%";

                var row = new VisualizationRow
                {
                    InfoText = $"#{groupNum}: 재고 {plan.StockLength}mm → [{cutLengths}] 잔여 {plan.Leftover}mm (효율 {effPercent}){countLabel}"
                };

                foreach (var cut in plan.Cuts)
                {
                    EnsureColor(colorCache, legendItems, random, cut.Length);

                    double width = cut.Length * scale;
                    double pct = 100.0 * cut.Length / plan.StockLength;
                    string label = width > 45 ? $"{cut.Length}" : "";

                    row.Blocks.Add(new VisualizationBlock
                    {
                        Width = width,
                        Color = colorCache[cut.Length],
                        BorderColor = System.Windows.Media.Brushes.White,
                        ToolTip = $"{cut.Length}mm ({pct:F1}%)",
                        Text = label,
                        TextColor = IsBright(colorCache[cut.Length])
                            ? System.Windows.Media.Brushes.Black
                            : System.Windows.Media.Brushes.White
                    });
                }

                // 잔여 블록
                if (plan.Leftover > 0)
                {
                    double wWidth = plan.Leftover * scale;
                    bool isWaste = plan.Leftover < (_lastParameters?.Gamma ?? 100);
                    var bgBrush = isWaste
                        ? System.Windows.Media.Brushes.MistyRose
                        : System.Windows.Media.Brushes.LightGray;
                    var borderBrush = isWaste
                        ? System.Windows.Media.Brushes.IndianRed
                        : System.Windows.Media.Brushes.Gray;
                    string wasteLabel = isWaste ? "낭비" : "재사용";

                    row.Blocks.Add(new VisualizationBlock
                    {
                        Width = wWidth,
                        Color = bgBrush,
                        BorderColor = borderBrush,
                        ToolTip = $"잔여 {plan.Leftover}mm ({wasteLabel})",
                        Text = wWidth > 45 ? $"{plan.Leftover}" : "",
                        TextColor = isWaste
                            ? System.Windows.Media.Brushes.DarkRed
                            : System.Windows.Media.Brushes.DimGray
                    });
                }

                visItems.Add(row);
                groupNum++;
            }

            var sortedLegend = legendItems.OrderBy(i => int.Parse(i.Label.Replace("mm", ""))).ToList();

            // 잔여 범례 추가
            sortedLegend.Add(new LegendItem { Color = System.Windows.Media.Brushes.LightGray, Label = "잔여 (재사용)" });
            sortedLegend.Add(new LegendItem { Color = System.Windows.Media.Brushes.MistyRose, Label = "잔여 (낭비)" });

            visualizationItemsControl.ItemsSource = visItems;
            legendItemsControl.ItemsSource = sortedLegend;
        }

        private static string PatternKey(CuttingPlan plan)
        {
            var cuts = string.Join(",", plan.Cuts.Select(c => c.Length).OrderBy(l => l));
            return $"{plan.StockLength}|{cuts}|{plan.Leftover}";
        }

        private static void EnsureColor(Dictionary<int, System.Windows.Media.Brush> cache,
            List<LegendItem> legendItems, Random random, int length)
        {
            if (cache.ContainsKey(length)) return;

            // HSL-based colors for better distinction
            double hue = (cache.Count * 137.508) % 360; // golden angle
            var color = HslToRgb(hue, 0.55, 0.55);
            var brush = new System.Windows.Media.SolidColorBrush(color);
            brush.Freeze();
            cache[length] = brush;
            legendItems.Add(new LegendItem { Color = brush, Label = $"{length}mm" });
        }

        private static System.Windows.Media.Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r, g, b;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return System.Windows.Media.Color.FromRgb(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255));
        }

        /// <summary>
        /// Trims solver names into 2-line chart-friendly labels. Splits on parenthesis
        /// so qualifiers like "(LP)" or "(OR-Tools)" go to a second line.
        /// </summary>
        private static string AbbreviateAlgorithmName(string name)
        {
            int paren = name.IndexOf('(');
            if (paren > 0 && paren < name.Length - 1)
                return name.Substring(0, paren).TrimEnd() + Environment.NewLine + name.Substring(paren);
            return name;
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
            public List<VisualizationBlock> Blocks { get; set; } = new();
        }

        public class VisualizationBlock
        {
            public double Width { get; set; }
            public required System.Windows.Media.Brush Color { get; set; }
            public required System.Windows.Media.Brush BorderColor { get; set; }
            public required string ToolTip { get; set; }
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

            var parameters = GetParameters();
            if (parameters == null) return;

            ComparisonResult? bestAlgorithm = null;
            try
            {
                SetRunningState(true);
                loadingProgressBar.IsIndeterminate = false;
                loadingProgressBar.Value = 0;
                loadingText.Text = "알고리즘 비교 준비 중...";

                var stock = BuildStock();
                var orders = BuildOrders();
                _lastParameters = parameters;

                var optimizers = new List<ICuttingSolver>
                {
                    new GreedyKnapsackSolver(),
                    new ColumnGenerationSolver(),
                    new ArcFlowSolver()
                };

                var reports = new System.Text.StringBuilder();
                reports.AppendLine("═══════════════════════════════════════════════════");
                reports.AppendLine("  알고리즘 상세 비교");
                reports.AppendLine("═══════════════════════════════════════════════════");
                reports.AppendLine();

                var collected = new List<ComparisonResult>();

                // Run one solver at a time on the background thread so we can update the
                // progress bar between each completion — gives the user a sense of pace
                // across the 3 algorithms instead of an opaque indeterminate spinner.
                for (int i = 0; i < optimizers.Count; i++)
                {
                    var optimizer = optimizers[i];
                    loadingText.Text = $"알고리즘 비교 중... ({i + 1}/{optimizers.Count}) {optimizer.Name}";
                    loadingProgressBar.Value = i * 100.0 / optimizers.Count;

                    var ordersCopy = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                    var result = await Task.Run(() => optimizer.Solve(stock, ordersCopy, parameters));

                    collected.Add(new ComparisonResult
                    {
                        AlgorithmName = optimizer.Name,
                        TotalCost = result.TotalCost,
                        WasteLength = result.WasteLength,
                        StockUsed = result.StockUsed,
                        MaterialEfficiency = result.MaterialEfficiency,
                        ExecutionTimeMs = result.ExecutionTimeMs,
                        Success = result.Success,
                    });

                    reports.AppendLine($"┌─────────────────────────────────────────────────");
                    reports.AppendLine($"│ {optimizer.Name}");
                    reports.AppendLine($"│ 시간 복잡도: {optimizer.TimeComplexity}");
                    reports.AppendLine($"└─────────────────────────────────────────────────");
                    reports.AppendLine(result.Success ? result.GetDetailedReport(parameters) : $"실패: {result.ErrorMessage}");
                    reports.AppendLine();
                    reports.AppendLine();
                }

                loadingProgressBar.Value = 100;

                ComparisonResults.Clear();
                foreach (var cr in collected)
                    ComparisonResults.Add(cr);

                var sortedResults = ComparisonResults
                    .Where(r => r.Success)
                    .OrderBy(r => r.TotalCost)
                    .ToList();
                for (int i = 0; i < sortedResults.Count; i++)
                    sortedResults[i].Rank = i + 1;

                UpdateCharts();
                mainTabControl.SelectedIndex = 2;
                comparisonTextBox.Text = reports.ToString();
                comparisonPlaceholder.Visibility = Visibility.Collapsed;
                comparisonContent.Visibility = Visibility.Visible;
                btnExportCompCsv.IsEnabled = true;
                btnExportCompExcel.IsEnabled = true;

                bestAlgorithm = sortedResults.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetRunningState(false);
            }

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
                    Labels = successResults.Select(r => AbbreviateAlgorithmName(r.AlgorithmName)).ToArray(),
                    LabelsRotation = -15,
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
                    Labels = successResults.Select(r => AbbreviateAlgorithmName(r.AlgorithmName)).ToArray(),
                    LabelsRotation = -15,
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
                    Labels = successResults.Select(r => AbbreviateAlgorithmName(r.AlgorithmName)).ToArray(),
                    LabelsRotation = -15,
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
                    ExportService.ExportSingleResultToCsv(dialog.FileName, _lastOptimizer, _lastSingleResult, _lastParameters);
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
                    ExportService.ExportSingleResultToExcel(dialog.FileName, _lastOptimizer, _lastSingleResult, _lastParameters);
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
                    ExportService.ExportComparisonResultsToCsv(dialog.FileName, ComparisonResults);
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
                    ExportService.ExportComparisonResultsToExcel(dialog.FileName, ComparisonResults);
                    MessageBox.Show($"Excel 파일로 저장되었습니다.\n{dialog.FileName}", "저장 완료",
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
