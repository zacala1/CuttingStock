using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using CuttingStock.Core.Persistence;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// MVVM ViewModel for the 1D Rebar tab. Owns user input state, parameter
    /// strings, command implementations, and the result/visualization output
    /// the View binds to. The View remains responsible for:
    ///   - DataGrid selection (forwarded via DeleteSelected{Stock,Order}),
    ///   - rendering the visualization rows into the ItemsControl,
    ///   - updating LiveCharts series (called after Compare completes).
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject
    {
        private readonly IDialogService _dialog;

        public MainViewModel(IDialogService dialog)
        {
            _dialog = dialog;
            Stocks = new ObservableCollection<StockRow>();
            Orders = new ObservableCollection<OrderRow>();
            ComparisonResults = new ObservableCollection<ComparisonResult>();
        }

        // ─── Input collections ───────────────────────────────────────

        public ObservableCollection<StockRow> Stocks { get; }
        public ObservableCollection<OrderRow> Orders { get; }
        public ObservableCollection<ComparisonResult> ComparisonResults { get; }

        // ─── Parameter strings (bound to TextBoxes) ──────────────────

        [ObservableProperty] private string _alphaText = "1.0";
        [ObservableProperty] private string _betaText = "500";
        [ObservableProperty] private string _gammaText = "100";
        [ObservableProperty] private string _deltaText = "100";
        [ObservableProperty] private string _kerfText = "0";

        /// <summary>0 = SmallToLarge, 1 = LargeToSmall (matches ComboBox order).</summary>
        [ObservableProperty] private int _usageOrderIndex;

        /// <summary>0 = Greedy, 1 = CG, 2 = ArcFlow (matches ComboBox order).</summary>
        [ObservableProperty] private int _algorithmIndex;

        [ObservableProperty] private bool _enableWelding;

        // ─── Result / UI feedback state ──────────────────────────────

        [ObservableProperty] private string _resultText = string.Empty;
        [ObservableProperty] private string _comparisonText = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
        [NotifyCanExecuteChangedFor(nameof(CompareAlgorithmsCommand))]
        private bool _isRunning;

        [ObservableProperty] private double _progressPercent;
        [ObservableProperty] private bool _progressIndeterminate = true;
        [ObservableProperty] private string _progressText = "계산 중...";

        /// <summary>CanExecute gate for Calculate / CompareAlgorithms — prevents
        /// concurrent solver runs from a re-entrant button click.</summary>
        private bool CanRunSolver() => !IsRunning;

        /// <summary>True after Calculate succeeds — gates Export buttons.</summary>
        [ObservableProperty] private bool _hasSingleResult;
        /// <summary>True after Compare succeeds — gates ComparisonExport buttons.</summary>
        [ObservableProperty] private bool _hasComparisonResults;

        // Visualization output the View renders into ItemsControls.
        [ObservableProperty] private List<VisualizationRow>? _visualizationRows;
        [ObservableProperty] private List<LegendItem>? _legendItems;

        // Last successful single solve — used by export.
        private SolverResult? _lastResult;
        private SolverOptions? _lastOptions;
        private ICuttingSolver? _lastSolver;

        // ─── Input commands ──────────────────────────────────────────

        [RelayCommand]
        private void AddStock() => Stocks.Add(new StockRow { Length = 12000, Quantity = 1 });

        [RelayCommand]
        private void AddOrder() => Orders.Add(new OrderRow { Length = 5000, Quantity = 1 });

        [RelayCommand]
        private void ClearAll()
        {
            if (!_dialog.Confirm("전체 초기화", "재고와 주문 데이터를 모두 삭제하시겠습니까?")) return;
            Stocks.Clear();
            Orders.Clear();
        }

        [RelayCommand]
        private void LoadExample()
        {
            Stocks.Clear();
            Orders.Clear();
            Stocks.Add(new StockRow { Length = 12000, Quantity = 20 });
            Orders.Add(new OrderRow { Length = 5000, Quantity = 10 });
            Orders.Add(new OrderRow { Length = 4000, Quantity = 15 });
            Orders.Add(new OrderRow { Length = 3000, Quantity = 12 });
            Orders.Add(new OrderRow { Length = 2000, Quantity = 8 });
            _dialog.ShowInfo("예제 로드",
                "예제 데이터를 로드했습니다.\n재고: 12000mm × 20개\n주문: 5000mm×10, 4000mm×15, 3000mm×12, 2000mm×8");
        }

        // ─── Scenario save / load ────────────────────────────────────

        [RelayCommand]
        private void SaveScenario()
        {
            var options = TryParseOptions();
            if (options == null) return;

            var path = _dialog.PromptSaveFile(
                "시나리오 저장",
                "1D 시나리오 (*.cstock1d.json)|*.cstock1d.json|JSON (*.json)|*.json",
                $"1D시나리오_{DateTime.Now:yyyyMMdd_HHmmss}.cstock1d.json");
            if (path == null) return;

            try
            {
                var scenario = new ScenarioService.Scenario1D
                {
                    Stocks = Stocks.Select(s => new ScenarioService.Stock1DDto { Length = s.Length, Quantity = s.Quantity }).ToList(),
                    Orders = Orders.Select(o => new ScenarioService.Order1DDto { Length = o.Length, Quantity = o.Quantity }).ToList(),
                    Parameters = new ScenarioService.Options1DDto
                    {
                        Alpha = options.Alpha, Beta = options.Beta,
                        Gamma = options.Gamma, Delta = options.Delta, Kerf = options.Kerf,
                        UsageOrder = options.UsageOrder, EnableWelding = options.EnableWelding,
                    },
                };
                ScenarioService.Save1D(path, scenario);
                _dialog.ShowInfo("저장 완료", $"시나리오를 저장했습니다.\n{path}");
            }
            catch (Exception ex)
            {
                _dialog.ShowError("오류", $"시나리오 저장 오류: {ex.Message}");
            }
        }

        [RelayCommand]
        private void LoadScenario()
        {
            var path = _dialog.PromptOpenFile(
                "시나리오 불러오기",
                "1D 시나리오 (*.cstock1d.json)|*.cstock1d.json|JSON (*.json)|*.json|모든 파일 (*.*)|*.*");
            if (path == null) return;

            try
            {
                var scenario = ScenarioService.Load1D(path);
                Stocks.Clear();
                foreach (var s in scenario.Stocks)
                    Stocks.Add(new StockRow { Length = s.Length, Quantity = s.Quantity });
                Orders.Clear();
                foreach (var o in scenario.Orders)
                    Orders.Add(new OrderRow { Length = o.Length, Quantity = o.Quantity });

                var p = scenario.Parameters;
                AlphaText = p.Alpha.ToString(CultureInfo.InvariantCulture);
                BetaText  = p.Beta.ToString(CultureInfo.InvariantCulture);
                GammaText = p.Gamma.ToString(CultureInfo.InvariantCulture);
                DeltaText = p.Delta.ToString(CultureInfo.InvariantCulture);
                KerfText  = p.Kerf.ToString(CultureInfo.InvariantCulture);
                UsageOrderIndex = p.UsageOrder == StockUsageOrder.SmallToLarge ? 0 : 1;
                EnableWelding = p.EnableWelding;
            }
            catch (Exception ex)
            {
                _dialog.ShowError("오류", $"시나리오 불러오기 오류: {ex.Message}");
            }
        }

        // ─── Solve / Compare commands ────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRunSolver))]
        private async Task CalculateAsync()
        {
            if (Stocks.Count == 0 || Orders.Count == 0)
            {
                _dialog.ShowWarning("입력 오류", "재고와 주문을 입력해주세요.");
                return;
            }
            var parameters = TryParseOptions();
            if (parameters == null) return;
            var stockSnapshot = Stocks.Select(s => new RebarStock(s.Length, s.Quantity)).ToList();
            var ordersSnapshot = Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
            var optimizer = BuildOptimizer();

            try
            {
                IsRunning = true;
                ProgressIndeterminate = true;
                ProgressPercent = 0;
                ProgressText = "최적화 준비 중...";

                var progress = new Progress<double>(pct =>
                {
                    ProgressIndeterminate = false;
                    ProgressPercent = pct;
                    ProgressText = $"최적화 진행 중... {pct:F0}%";
                });

                var result = await Task.Run(() => optimizer.Solve(stockSnapshot, ordersSnapshot, parameters, progress));

                _lastResult = result;
                _lastOptions = parameters;
                _lastSolver = optimizer;

                ResultText =
                    "═══════════════════════════════════════════════════\n" +
                    $"  알고리즘: {optimizer.Name}\n" +
                    $"  시간 복잡도: {optimizer.TimeComplexity}\n" +
                    "═══════════════════════════════════════════════════\n\n" +
                    result.GetDetailedReport(parameters);

                if (result.Success && result.CuttingPlans.Count > 0)
                {
                    var vis = VisualizationService.Build(result, parameters.Gamma);
                    VisualizationRows = vis.Rows;
                    LegendItems = vis.Legend;
                }

                HasSingleResult = true;

                if (!result.Success)
                {
                    _dialog.ShowWarning("최적화 실패",
                        $"최적화 실패: {result.ErrorMessage}\n\n부분 결과는 결과 탭에서 확인 가능합니다.");
                }
                else
                {
                    _dialog.ShowInfo("최적화 완료",
                        $"최적화가 완료되었습니다!\n\n" +
                        $"총 비용: {result.TotalCost:N0}원\n" +
                        $"재료 효율: {result.MaterialEfficiency:F2}%\n" +
                        $"실행 시간: {result.ExecutionTimeMs:F3}ms");
                }
            }
            catch (Exception ex)
            {
                _dialog.ShowError("오류", $"오류 발생: {ex.Message}");
            }
            finally
            {
                IsRunning = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunSolver))]
        private async Task CompareAlgorithmsAsync()
        {
            if (Stocks.Count == 0 || Orders.Count == 0)
            {
                _dialog.ShowWarning("입력 필요", "재고와 주문을 입력해주세요.");
                return;
            }
            var parameters = TryParseOptions();
            if (parameters == null) return;
            var stockSnapshot = Stocks.Select(s => new RebarStock(s.Length, s.Quantity)).ToList();
            var orders = Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
            _lastOptions = parameters;

            try
            {
                IsRunning = true;
                ProgressIndeterminate = true;
                ProgressText = "알고리즘 비교 중...";

                var optimizers = new List<ICuttingSolver>
                {
                    new GreedyKnapsackSolver(),
                    new ColumnGenerationSolver(),
                    new ArcFlowSolver(),
                };
                var rows = new List<ComparisonResult>();
                var reports = new System.Text.StringBuilder();
                reports.AppendLine("═══════════════════════════════════════════════════")
                       .AppendLine("  알고리즘 상세 비교")
                       .AppendLine("═══════════════════════════════════════════════════")
                       .AppendLine();

                for (int i = 0; i < optimizers.Count; i++)
                {
                    var optimizer = optimizers[i];
                    ProgressText = $"비교 중... ({i + 1}/{optimizers.Count} — {optimizer.Name})";
                    ProgressIndeterminate = false;
                    ProgressPercent = i * 100.0 / optimizers.Count;

                    var ordersCopy = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                    var result = await Task.Run(() => optimizer.Solve(stockSnapshot, ordersCopy, parameters));

                    rows.Add(new ComparisonResult
                    {
                        AlgorithmName = optimizer.Name,
                        TotalCost = result.TotalCost,
                        WasteLength = result.WasteLength,
                        StockUsed = result.StockUsed,
                        MaterialEfficiency = result.MaterialEfficiency,
                        ExecutionTimeMs = result.ExecutionTimeMs,
                        Success = result.Success,
                    });

                    reports.AppendLine("┌─────────────────────────────────────────────────")
                           .AppendLine($"│ {optimizer.Name}")
                           .AppendLine($"│ 시간 복잡도: {optimizer.TimeComplexity}")
                           .AppendLine("└─────────────────────────────────────────────────")
                           .AppendLine(result.Success ? result.GetDetailedReport(parameters) : $"실패: {result.ErrorMessage}")
                           .AppendLine()
                           .AppendLine();
                }

                ComparisonResults.Clear();
                foreach (var cr in rows) ComparisonResults.Add(cr);

                var sorted = ComparisonResults.Where(r => r.Success).OrderBy(r => r.TotalCost).ToList();
                for (int i = 0; i < sorted.Count; i++) sorted[i].Rank = i + 1;

                ComparisonText = reports.ToString();
                HasComparisonResults = true;

                var best = sorted.FirstOrDefault();
                if (best != null)
                {
                    _dialog.ShowInfo("비교 완료",
                        "알고리즘 비교가 완료되었습니다!\n\n" +
                        $"최고 성능: {best.AlgorithmName}\n" +
                        $"   총 비용: {best.TotalCost:N0}원\n" +
                        $"   재료 효율: {best.MaterialEfficiency:F2}%\n" +
                        $"   실행 시간: {best.ExecutionTimeMs:F3}ms");
                }
            }
            catch (Exception ex)
            {
                _dialog.ShowError("오류", $"오류 발생: {ex.Message}");
            }
            finally
            {
                IsRunning = false;
            }
        }

        // ─── Export ──────────────────────────────────────────────────

        [RelayCommand]
        private void ExportToCsv()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장",
                "CSV 파일 (*.csv)|*.csv",
                $"최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResultToCsv(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장",
                "Excel 파일 (*.xlsx)|*.xlsx",
                $"최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResultToExcel(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportComparisonToCsv()
        {
            if (!ComparisonResults.Any())
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장",
                "CSV 파일 (*.csv)|*.csv",
                $"알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportComparisonResultsToCsv(path, ComparisonResults);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportComparisonToExcel()
        {
            if (!ComparisonResults.Any())
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장",
                "Excel 파일 (*.xlsx)|*.xlsx",
                $"알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportComparisonResultsToExcel(path, ComparisonResults);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        // ─── Helpers exposed for the View (delete-selected) ──────────

        /// <summary>Removes the supplied rows from <see cref="Stocks"/>.</summary>
        public void DeleteSelectedStocks(IEnumerable<StockRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0)
            {
                _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요.");
                return;
            }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Stocks.Remove(item);
        }

        /// <summary>Removes the supplied rows from <see cref="Orders"/>.</summary>
        public void DeleteSelectedOrders(IEnumerable<OrderRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0)
            {
                _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요.");
                return;
            }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Orders.Remove(item);
        }

        /// <summary>
        /// Snapshot of the most recent successful single solve — the View uses
        /// this to render the bar visualization. Null until Calculate succeeds.
        /// </summary>
        public SolverResult? LastResult => _lastResult;

        /// <summary>True if the last single solve succeeded with at least one plan.</summary>
        public bool HasVisualization => _lastResult is { Success: true, CuttingPlans.Count: > 0 };

        // ─── Parameter parsing ───────────────────────────────────────

        private SolverOptions? TryParseOptions()
        {
            if (!float.TryParse(AlphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha) || alpha < 0)
            { _dialog.ShowWarning("입력 오류", "Alpha 값을 올바르게 입력해주세요. (0 이상의 숫자)"); return null; }
            if (!float.TryParse(BetaText, NumberStyles.Float, CultureInfo.InvariantCulture, out float beta) || beta < 0)
            { _dialog.ShowWarning("입력 오류", "Beta 값을 올바르게 입력해주세요. (0 이상의 숫자)"); return null; }
            if (!int.TryParse(GammaText, out int gamma) || gamma < 0)
            { _dialog.ShowWarning("입력 오류", "Gamma 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }
            if (!int.TryParse(DeltaText, out int delta) || delta <= 0)
            { _dialog.ShowWarning("입력 오류", "Delta 값을 올바르게 입력해주세요. (1 이상의 정수)"); return null; }
            if (!int.TryParse(KerfText, out int kerf) || kerf < 0)
            { _dialog.ShowWarning("입력 오류", "Kerf 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }

            return new SolverOptions
            {
                Alpha = alpha, Beta = beta, Gamma = gamma, Delta = delta, Kerf = kerf,
                UsageOrder = UsageOrderIndex == 0 ? StockUsageOrder.SmallToLarge : StockUsageOrder.LargeToSmall,
                EnableWelding = EnableWelding,
            };
        }

        private ICuttingSolver BuildOptimizer() => AlgorithmIndex switch
        {
            0 => new GreedyKnapsackSolver(),
            1 => new ColumnGenerationSolver(),
            2 => new ArcFlowSolver(),
            _ => new GreedyKnapsackSolver(),
        };
    }
}
