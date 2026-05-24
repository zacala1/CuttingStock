using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Persistence;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// MVVM ViewModel for the 2D Sheet tab. Owns user input collections,
    /// parameter strings, command implementations, and the result/comparison
    /// state the View binds to. The View remains responsible for:
    ///   - DataGrid selection (forwarded via DeleteSelected{Sheets,Orders}),
    ///   - rendering the placement Canvas patterns,
    ///   - updating LiveCharts series after Compare completes,
    /// because those touch WPF visual types directly.
    /// </summary>
    public sealed partial class TwoDViewModel : ObservableObject
    {
        private readonly IDialogService _dialog;

        public TwoDViewModel(IDialogService dialog)
        {
            _dialog = dialog;
            Sheets = new ObservableCollection<SheetRow>();
            Orders = new ObservableCollection<RectOrderRow>();
            CompareRows = new ObservableCollection<ComparisonResult2D>();
        }

        // ─── Input collections ───────────────────────────────────────

        public ObservableCollection<SheetRow> Sheets { get; }
        public ObservableCollection<RectOrderRow> Orders { get; }
        public ObservableCollection<ComparisonResult2D> CompareRows { get; }

        // ─── Parameter strings ───────────────────────────────────────

        [ObservableProperty] private string _kerfText = "0";
        [ObservableProperty] private string _trimText = "0";
        [ObservableProperty] private string _alphaAreaText = "1";
        [ObservableProperty] private string _timeLimitText = "30000";
        [ObservableProperty] private bool _allowRotation = true;
        /// <summary>0 = 2-stage, 1 = 3-stage.</summary>
        [ObservableProperty] private int _stageIndex;
        /// <summary>0 = Small→Large, 1 = Large→Small. Default Large→Small (index 1).</summary>
        [ObservableProperty] private int _usageOrderIndex = 1;
        /// <summary>0 = Shelf, 1 = CG2D, 2 = StagedMip.</summary>
        [ObservableProperty] private int _algorithmIndex;

        // ─── UI feedback / result state ──────────────────────────────

        [ObservableProperty] private string _reportText = string.Empty;
        [ObservableProperty] private string _compareText = string.Empty;

        [ObservableProperty] private string _statusText = "준비됨";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
        [NotifyCanExecuteChangedFor(nameof(CompareCommand))]
        private bool _isRunning;

        [ObservableProperty] private double _progressPercent;
        [ObservableProperty] private bool _progressIndeterminate = true;
        [ObservableProperty] private string _progressText = "계산 중...";

        /// <summary>CanExecute gate for Calculate / Compare — re-entrancy guard.</summary>
        private bool CanRunSolver() => !IsRunning;

        private bool CanCancelSolver() => CanCancel;

        [RelayCommand(CanExecute = nameof(CanCancelSolver))]
        private void Cancel()
        {
            _currentRunId++;
            try { _currentCts?.Cancel(); } catch { }
            IsRunning = false;
            CanCancel = false;
            ProgressText = "취소됨";
            StatusText = "취소됨";
        }

        [ObservableProperty] private bool _hasSingleResult;
        [ObservableProperty] private bool _hasComparisonResults;

        private SolverResult2D? _lastResult;
        private SolverOptions2D? _lastOptions;
        private ICuttingSolver2D? _lastSolver;

        private int _currentRunId;
        private System.Threading.CancellationTokenSource? _currentCts;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        private bool _canCancel;

        /// <summary>
        /// Last successful single solve. View reads this to render the placement
        /// canvas; bound for visualization only, not exposed as a property to
        /// avoid coupling WPF types to the ViewModel surface.
        /// </summary>
        public SolverResult2D? LastResult => _lastResult;
        public SolverOptions2D? LastOptions => _lastOptions;

        /// <summary>Fires after Calculate succeeds — view re-renders the pattern canvas.</summary>
        public event EventHandler? SingleResultReady;

        /// <summary>Fires after Compare succeeds — view re-renders charts and patterns.</summary>
        public event EventHandler? CompareResultReady;

        // ─── Input commands ──────────────────────────────────────────

        [RelayCommand] private void AddSheet() => Sheets.Add(new SheetRow());
        [RelayCommand] private void AddRectOrder() => Orders.Add(new RectOrderRow());

        [RelayCommand]
        private void ClearAll()
        {
            if (!_dialog.Confirm("전체 초기화", "시트와 주문 데이터를 모두 삭제하시겠습니까?")) return;
            Sheets.Clear();
            Orders.Clear();
            ReportText = string.Empty;
            CompareText = string.Empty;
            CompareRows.Clear();
            HasSingleResult = false;
            HasComparisonResults = false;
            _lastResult = null;
        }

        [RelayCommand]
        private void LoadExample()
        {
            Sheets.Clear();
            Orders.Clear();
            Sheets.Add(new SheetRow { Width = 2440, Height = 1220, Quantity = 5 });
            Sheets.Add(new SheetRow { Width = 1220, Height = 1220, Quantity = 5 });
            Orders.Add(new RectOrderRow { Width = 600,  Height = 400, Quantity = 6 });
            Orders.Add(new RectOrderRow { Width = 800,  Height = 300, Quantity = 4 });
            Orders.Add(new RectOrderRow { Width = 300,  Height = 300, Quantity = 8 });
            Orders.Add(new RectOrderRow { Width = 1200, Height = 500, Quantity = 2 });
            StatusText = "2D 예제 데이터 로드됨";
        }

        // ─── Scenario save / load ────────────────────────────────────

        [RelayCommand]
        private void SaveScenario()
        {
            var options = TryParseOptions();
            if (options == null) return;
            var path = _dialog.PromptSaveFile(
                "시나리오 저장",
                "2D 시나리오 (*.cstock2d.json)|*.cstock2d.json|JSON (*.json)|*.json",
                $"2D시나리오_{DateTime.Now:yyyyMMdd_HHmmss}.cstock2d.json");
            if (path == null) return;
            try
            {
                var scenario = new ScenarioService.Scenario2D
                {
                    Sheets = Sheets.Select(s => new ScenarioService.Sheet2DDto
                    {
                        Width = s.Width, Height = s.Height, Quantity = s.Quantity,
                    }).ToList(),
                    Orders = Orders.Select(o => new ScenarioService.Order2DDto
                    {
                        Width = o.Width, Height = o.Height, Quantity = o.Quantity, AllowRotation = o.AllowRotation,
                    }).ToList(),
                    Options = new ScenarioService.Options2DDto
                    {
                        Kerf = options.Kerf, Trim = options.Trim, AlphaArea = options.AlphaArea,
                        AllowRotation = options.AllowRotation, Stage = options.Stage,
                        TimeLimitMs = options.TimeLimitMs, UsageOrder = options.UsageOrder,
                    },
                };
                ScenarioService.Save2D(path, scenario);
                StatusText = $"저장됨: {System.IO.Path.GetFileName(path)}";
                _dialog.ShowInfo("저장 완료", $"시나리오를 저장했습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"시나리오 저장 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void LoadScenario()
        {
            var path = _dialog.PromptOpenFile(
                "시나리오 불러오기",
                "2D 시나리오 (*.cstock2d.json)|*.cstock2d.json|JSON (*.json)|*.json|모든 파일 (*.*)|*.*");
            if (path == null) return;
            try
            {
                var scenario = ScenarioService.Load2D(path);
                Sheets.Clear();
                foreach (var s in scenario.Sheets)
                    Sheets.Add(new SheetRow { Width = s.Width, Height = s.Height, Quantity = s.Quantity });
                Orders.Clear();
                foreach (var o in scenario.Orders)
                    Orders.Add(new RectOrderRow { Width = o.Width, Height = o.Height, Quantity = o.Quantity, AllowRotation = o.AllowRotation });

                var o2 = scenario.Options;
                KerfText = o2.Kerf.ToString(CultureInfo.InvariantCulture);
                TrimText = o2.Trim.ToString(CultureInfo.InvariantCulture);
                AlphaAreaText = o2.AlphaArea.ToString(CultureInfo.InvariantCulture);
                TimeLimitText = o2.TimeLimitMs.ToString(CultureInfo.InvariantCulture);
                AllowRotation = o2.AllowRotation;
                StageIndex = o2.Stage == 3 ? 1 : 0;
                UsageOrderIndex = o2.UsageOrder == StockUsageOrder.SmallToLarge ? 0 : 1;
                StatusText = $"불러옴: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"시나리오 불러오기 오류: {ex.Message}"); }
        }

        // ─── Solve / Compare ─────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRunSolver))]
        private async Task CalculateAsync()
        {
            if (Sheets.Count == 0 || Orders.Count == 0)
            { _dialog.ShowWarning("입력 필요", "시트와 주문을 모두 입력하세요."); return; }
            var options = TryParseOptions();
            if (options == null) return;
            var sheetSnapshot = BuildSheets();
            var orderSnapshot = BuildOrders();
            if (sheetSnapshot.Count == 0 || orderSnapshot.Count == 0)
            { _dialog.ShowWarning("입력 오류", "유효한 시트/주문이 없습니다."); return; }
            var solver = BuildSolver(AlgorithmIndex);

            int runId = ++_currentRunId;
            _currentCts = new System.Threading.CancellationTokenSource();

            try
            {
                IsRunning = true;
                CanCancel = true;
                ProgressIndeterminate = true;
                ProgressPercent = 0;
                ProgressText = "계산 중...";

                var progress = new Progress<double>(pct =>
                {
                    ProgressIndeterminate = false;
                    double v = pct <= 1.0 ? pct * 100.0 : pct;
                    v = Math.Clamp(v, 0, 100);
                    ProgressPercent = v;
                    ProgressText = $"계산 중... {v:F0}%";
                });

                var result = await Task.Run(() => solver.Solve(sheetSnapshot, orderSnapshot, options, progress));
                if (runId != _currentRunId) return;

                _lastResult = result;
                _lastOptions = options;
                _lastSolver = solver;

                ReportText = result.Success
                    ? result.GetDetailedReport(options)
                    : $"실패: {result.ErrorMessage}";
                HasSingleResult = result.Success;
                if (result.Success)
                    StatusText = $"완료: {solver.Name} · {result.SheetsUsed} 시트 · 효율 {result.MaterialEfficiency:F1}% · {result.ExecutionTimeMs:F0}ms";
                else
                    StatusText = $"실패: {result.ErrorMessage}";
                SingleResultReady?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                if (runId == _currentRunId) _dialog.ShowError("오류", $"오류: {ex.Message}");
            }
            finally
            {
                if (runId == _currentRunId) { IsRunning = false; CanCancel = false; }
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunSolver))]
        private async Task CompareAsync()
        {
            if (Sheets.Count == 0 || Orders.Count == 0)
            { _dialog.ShowWarning("입력 필요", "시트와 주문을 모두 입력하세요."); return; }
            var options = TryParseOptions();
            if (options == null) return;
            var sheetSnapshot = BuildSheets();
            var orderSnapshot = BuildOrders();
            if (sheetSnapshot.Count == 0 || orderSnapshot.Count == 0)
            { _dialog.ShowWarning("입력 오류", "유효한 시트/주문이 없습니다."); return; }

            int runId = ++_currentRunId;
            _currentCts = new System.Threading.CancellationTokenSource();

            try
            {
                IsRunning = true;
                CanCancel = true;
                ProgressIndeterminate = true;
                ProgressText = "비교 중...";

                ICuttingSolver2D[] solvers =
                {
                    new ShelfGuillotineSolver(),
                    new ColumnGeneration2DSolver(),
                    new StagedMipGuillotineSolver(),
                };

                var rows = new List<ComparisonResult2D>();
                var details = new StringBuilder();
                SolverResult2D? bestResult = null;
                ICuttingSolver2D? bestSolver = null;
                long bestSheets = long.MaxValue;

                for (int i = 0; i < solvers.Length; i++)
                {
                    if (runId != _currentRunId) return;
                    var s = solvers[i];
                    int solverIdx = i;
                    ProgressText = $"비교 중... ({i + 1}/{solvers.Length} — {s.Name})";
                    ProgressIndeterminate = false;

                    var sliceProgress = new Progress<double>(pct =>
                    {
                        double frac = pct <= 1.0 ? pct : pct / 100.0;
                        double overall = (solverIdx + Math.Clamp(frac, 0, 1)) / solvers.Length * 100.0;
                        ProgressPercent = Math.Clamp(overall, 0, 100);
                    });

                    var r = await Task.Run(() => s.Solve(sheetSnapshot, orderSnapshot, options, sliceProgress));
                    if (runId != _currentRunId) return;
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
                    details.AppendLine($"=== {s.Name} ===")
                           .AppendLine(r.Success ? r.GetDetailedReport(options) : $"실패: {r.ErrorMessage}")
                           .AppendLine();

                    if (r.Success && r.SheetsUsed < bestSheets)
                    {
                        bestResult = r;
                        bestSolver = s;
                        bestSheets = r.SheetsUsed;
                    }
                }

                int rank = 1;
                foreach (var r in rows.OrderBy(r => r.TotalCost).ThenBy(r => r.SheetsUsed))
                    r.Rank = rank++;

                CompareRows.Clear();
                foreach (var r in rows.OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank))
                    CompareRows.Add(r);
                CompareText = details.ToString();

                if (bestResult != null && bestSolver != null)
                {
                    _lastResult = bestResult;
                    _lastOptions = options;
                    _lastSolver = bestSolver;
                    HasSingleResult = true;
                }
                HasComparisonResults = true;
                var bestRow = CompareRows.FirstOrDefault(r => r.Success);
                if (bestRow != null)
                    StatusText = $"비교 완료 · 최고: {bestRow.AlgorithmName} · 효율 {bestRow.MaterialEfficiency:F1}%";
                else
                    StatusText = "비교 완료 (모두 실패)";
                CompareResultReady?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                if (runId == _currentRunId) _dialog.ShowError("오류", $"오류: {ex.Message}");
            }
            finally
            {
                if (runId == _currentRunId) { IsRunning = false; CanCancel = false; }
            }
        }

        // ─── Export ──────────────────────────────────────────────────

        [RelayCommand]
        private void ExportToCsv()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장", "CSV 파일 (*.csv)|*.csv",
                $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResult2DToCsv(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장", "Excel 파일 (*.xlsx)|*.xlsx",
                $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResult2DToExcel(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportCompareToCsv()
        {
            if (CompareRows.Count == 0)
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장", "CSV 파일 (*.csv)|*.csv",
                $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportComparison2DResultsToCsv(path, CompareRows);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportCompareToExcel()
        {
            if (CompareRows.Count == 0)
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장", "Excel 파일 (*.xlsx)|*.xlsx",
                $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportComparison2DResultsToExcel(path, CompareRows);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        // ─── Helpers exposed to the View ─────────────────────────────

        public void DeleteSelectedSheets(IEnumerable<SheetRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0) { _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요."); return; }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Sheets.Remove(item);
        }

        public void DeleteSelectedOrders(IEnumerable<RectOrderRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0) { _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요."); return; }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Orders.Remove(item);
        }

        // ─── Internals ───────────────────────────────────────────────

        private List<Sheet> BuildSheets()
        {
            var list = new List<Sheet>();
            foreach (var s in Sheets)
                if (s.Width > 0 && s.Height > 0 && s.Quantity > 0)
                    list.Add(new Sheet(s.Width, s.Height, s.Quantity));
            return list;
        }

        private List<RectOrder> BuildOrders()
        {
            var list = new List<RectOrder>();
            foreach (var o in Orders)
                if (o.Width > 0 && o.Height > 0 && o.Quantity > 0)
                    list.Add(new RectOrder(o.Width, o.Height, o.Quantity, o.AllowRotation));
            return list;
        }

        private SolverOptions2D? TryParseOptions()
        {
            if (!int.TryParse(KerfText, out int kerf) || kerf < 0)
            { _dialog.ShowWarning("입력 오류", "Kerf 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }
            if (!int.TryParse(TrimText, out int trim) || trim < 0)
            { _dialog.ShowWarning("입력 오류", "Trim 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }
            if (!double.TryParse(AlphaAreaText, NumberStyles.Float, CultureInfo.InvariantCulture, out double alpha) || alpha < 0)
            { _dialog.ShowWarning("입력 오류", "AlphaArea 값을 올바르게 입력해주세요. (0 이상의 숫자)"); return null; }
            if (!int.TryParse(TimeLimitText, out int tl) || tl <= 0)
            { _dialog.ShowWarning("입력 오류", "시간 제한 값을 올바르게 입력해주세요. (1ms 이상의 정수)"); return null; }

            return new SolverOptions2D
            {
                Kerf = kerf, Trim = trim, AlphaArea = (float)alpha,
                AllowRotation = AllowRotation,
                Stage = StageIndex == 1 ? 3 : 2,
                TimeLimitMs = Math.Max(1000, tl),
                UsageOrder = UsageOrderIndex == 0 ? StockUsageOrder.SmallToLarge : StockUsageOrder.LargeToSmall,
            };
        }

        private static ICuttingSolver2D BuildSolver(int idx) => idx switch
        {
            0 => new ShelfGuillotineSolver(),
            1 => new ColumnGeneration2DSolver(),
            2 => new StagedMipGuillotineSolver(),
            _ => new ShelfGuillotineSolver(),
        };
    }
}
