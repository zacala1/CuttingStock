using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

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

                    ReportText = result.Success
                        ? result.GetDetailedReport(options)
                        : $"실패: {result.ErrorMessage}";
                    HasSingleResult = result.Success;
                    if (result.Success)
                        StatusText = $"완료: {solver.Name} · {result.SheetsUsed} 시트 · 효율 {result.MaterialEfficiency:F1}% · {result.ExecutionTimeMs:F0}ms";
                    else
                        StatusText = $"실패: {result.ErrorMessage}";
                    SingleResultReady?.Invoke(this, EventArgs.Empty);
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

                    var details = new StringBuilder();
                    foreach (var outcome in comparison.Outcomes)
                    {
                        details.AppendLine($"=== {outcome.AlgorithmName} ===")
                               .AppendLine(outcome.Detail)
                               .AppendLine();
                    }

                    int rank = 1;
                    foreach (var row in comparison.Outcomes.Select(o => o.Row)
                                 .Where(r => r.Success)
                                 .OrderBy(r => r.TotalCost)
                                 .ThenBy(r => r.SheetsUsed))
                        row.Rank = rank++;

                    CompareRows.Clear();
                    foreach (var outcome in comparison.Outcomes
                                 .OrderBy(o => o.Row.Rank == 0 ? int.MaxValue : o.Row.Rank))
                        CompareRows.Add(outcome.Row);
                    CompareText = details.ToString();

                    var bestOutcome = comparison.Outcomes
                        .Where(o => o.Result?.Success == true && o.Solver != null)
                        .OrderBy(o => o.Result!.SheetsUsed)
                        .FirstOrDefault();
                    if (bestOutcome is { Result: not null, Solver: not null })
                    {
                        _lastResult = bestOutcome.Result;
                        _lastOptions = options;
                        _lastSolver = bestOutcome.Solver;
                        HasSingleResult = true;
                    }
                    HasComparisonResults = true;
                    var bestRow = CompareRows.FirstOrDefault(r => r.Success);
                    if (bestRow != null)
                        StatusText = $"비교 완료 · 최고: {bestRow.AlgorithmName} · 효율 {bestRow.MaterialEfficiency:F1}%";
                    else
                        StatusText = "비교 완료 (모두 실패)";
                    CompareResultReady?.Invoke(this, EventArgs.Empty);
                },
                onError: ex =>
                {
                    _dialog.ShowError("오류", $"오류: {ex.Message}");
                });
        }
    }
}
