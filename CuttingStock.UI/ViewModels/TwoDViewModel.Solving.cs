using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class TwoDViewModel
    {
        [RelayCommand(CanExecute = nameof(CanRunSolver))]
        private async Task CalculateAsync()
        {
            if (Sheets.Count == 0 || Orders.Count == 0)
            { _dialog.ShowWarning("입력 필요", "시트와 주문을 모두 입력하세요."); return; }
            var options = TryParseOptions();
            if (options == null) return;
            var descriptor = SelectedSolverDescriptor;
            var unsupportedReason = descriptor.GetUnsupportedReason(options);
            if (unsupportedReason != null)
            {
                _dialog.ShowWarning("지원하지 않는 옵션", $"{descriptor.Name}: {unsupportedReason}");
                return;
            }
            var sheetSnapshot = BuildSheets();
            var orderSnapshot = BuildOrders();
            if (sheetSnapshot.Count == 0 || orderSnapshot.Count == 0)
            { _dialog.ShowWarning("입력 오류", "유효한 시트/주문이 없습니다."); return; }
            var solver = descriptor.CreateSolver();

            await RunSolverAsync(
                initialProgressText: "계산 중...",
                executeAsync: async run =>
                {
                    var progress = CreateProgress(run, pct =>
                    {
                        ProgressIndeterminate = false;
                        double v = pct <= 1.0 ? pct * 100.0 : pct;
                        v = Math.Clamp(v, 0, 100);
                        ProgressPercent = v;
                        ProgressText = $"계산 중... {v:F0}%";
                    });

                    var result = await Task.Run(() => solver.Solve(sheetSnapshot, orderSnapshot, options, progress));
                    if (!IsCurrent(run)) return;

                    _lastResult = result;
                    _lastOptions = options;
                    _lastSolver = solver;
                    RenderProjection = TwoDProjectionService.BuildRender(
                        solver.Name,
                        result,
                        options);

                    ReportText = result.Success
                        ? result.GetDetailedReport(options)
                        : $"실패: {result.ErrorMessage}";
                    HasSingleResult = result.Success;
                    if (result.Success)
                        StatusText = $"완료: {solver.Name} · {result.SheetsUsed} 시트 · 효율 {result.MaterialEfficiency:F1}% · {result.ExecutionTimeMs:F0}ms";
                    else
                        StatusText = $"실패: {result.ErrorMessage}";
                },
                onError: ex =>
                {
                    _dialog.ShowError("오류", $"오류: {ex.Message}");
                });
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

            await RunSolverAsync(
                initialProgressText: "비교 중...",
                executeAsync: async run =>
                {
                    var comparison = await CompareSolversAsync<SolverDescriptor2D, ICuttingSolver2D, SolverOptions2D, SolverResult2D, ComparisonResult2D>(
                        scope: run,
                        descriptors: SolverDescriptors,
                        options: options,
                        progressTextPrefix: "비교 중...",
                        solveAsync: (solver, progress) =>
                            Task.Run(() => solver.Solve(sheetSnapshot, orderSnapshot, options, progress)),
                        createSkippedRow: descriptor => new ComparisonResult2D
                        {
                            AlgorithmName = descriptor.Name,
                            Success = false,
                        },
                        createResultRow: (solver, result) => new ComparisonResult2D
                        {
                            AlgorithmName = solver.Name,
                            TotalCost = result.TotalCost,
                            WasteArea = result.TotalWasteArea,
                            SheetsUsed = result.SheetsUsed,
                            MaterialEfficiency = result.MaterialEfficiency,
                            ExecutionTimeMs = result.ExecutionTimeMs,
                            Success = result.Success,
                        },
                        getSolverName: solver => solver.Name,
                        getReport: result => result.Success
                            ? result.GetDetailedReport(options)
                            : $"실패: {result.ErrorMessage}");
                    if (!comparison.Completed) return;

                    var summary = ComparisonWorkflow.Complete(
                        comparison,
                        row => row.Success,
                        row => new ComparisonRankKey(row.TotalCost, row.SheetsUsed),
                        (row, rank) => row.Rank = rank,
                        string.Empty,
                        outcome =>
                            $"=== {outcome.AlgorithmName} ==={Environment.NewLine}" +
                            $"{outcome.Detail}{Environment.NewLine}{Environment.NewLine}");

                    CompareRows.Clear();
                    foreach (var outcome in comparison.Outcomes
                                 .OrderBy(o => o.Row.Rank == 0 ? int.MaxValue : o.Row.Rank))
                        CompareRows.Add(outcome.Row);
                    CompareText = summary.Report;

                    var bestOutcome = summary.BestOutcome;
                    var bestRow = bestOutcome?.Row;
                    if (bestOutcome is { Result: not null, Solver: not null })
                    {
                        _lastResult = bestOutcome.Result;
                        _lastOptions = options;
                        _lastSolver = bestOutcome.Solver;
                        HasSingleResult = true;
                        ReportText = bestOutcome.Detail;
                        RenderProjection = TwoDProjectionService.BuildRender(
                            bestOutcome.AlgorithmName,
                            bestOutcome.Result,
                            options);
                    }
                    else
                    {
                        _lastResult = null;
                        _lastOptions = null;
                        _lastSolver = null;
                        ReportText = string.Empty;
                        HasSingleResult = false;
                        RenderProjection = null;
                    }

                    ChartProjection = TwoDProjectionService.BuildChart(CompareRows);
                    HasComparisonResults = true;
                    if (bestRow != null)
                        StatusText = $"비교 완료 · 최고: {bestRow.AlgorithmName} · 효율 {bestRow.MaterialEfficiency:F1}%";
                    else
                        StatusText = "비교 완료 (모두 실패)";
                },
                onError: ex =>
                {
                    _dialog.ShowError("오류", $"오류: {ex.Message}");
                });
        }
    }
}
