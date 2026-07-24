using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class MainViewModel
    {
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
            var descriptor = SelectedSolverDescriptor;
            var unsupportedReason = descriptor.GetUnsupportedReason(parameters);
            if (unsupportedReason != null)
            {
                _dialog.ShowWarning("지원하지 않는 옵션", $"{descriptor.Name}: {unsupportedReason}");
                return;
            }
            var stockSnapshot = Stocks.Select(s => new RebarStock(s.Length, s.Quantity)).ToList();
            var ordersSnapshot = Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
            var optimizer = descriptor.CreateSolver();

            await RunSolverAsync(
                initialProgressText: "최적화 준비 중...",
                executeAsync: async run =>
                {
                    var progress = CreateProgress(run, pct =>
                    {
                        ProgressIndeterminate = false;
                        ProgressPercent = pct;
                        ProgressText = $"최적화 진행 중... {pct:F0}%";
                    });

                    var result = await Task.Run(() => optimizer.Solve(stockSnapshot, ordersSnapshot, parameters, progress));

                    if (!IsCurrent(run)) return;  // cancelled or superseded — discard

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
                        StatusText = $"완료: {optimizer.Name} · {result.StockUsed}개 사용 · 효율 {result.MaterialEfficiency:F1}% · {result.ExecutionTimeMs:F0}ms";
                        _dialog.ShowInfo("최적화 완료",
                            $"최적화가 완료되었습니다!\n\n" +
                            $"총 비용: {result.TotalCost:N0}원\n" +
                            $"재료 효율: {result.MaterialEfficiency:F2}%\n" +
                            $"실행 시간: {result.ExecutionTimeMs:F3}ms");
                    }
                },
                onError: ex =>
                {
                    StatusText = $"오류: {ex.Message}";
                    _dialog.ShowError("오류", $"오류 발생: {ex.Message}");
                });
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

            await RunSolverAsync(
                initialProgressText: "알고리즘 비교 중...",
                executeAsync: async run =>
                {
                    var comparison = await CompareSolversAsync<SolverDescriptor, ICuttingSolver, SolverOptions, SolverResult, ComparisonResult>(
                        scope: run,
                        descriptors: SolverDescriptors,
                        options: parameters,
                        progressTextPrefix: "비교 중...",
                        solveAsync: (optimizer, progress) =>
                        {
                            var ordersCopy = orders.Select(o => new Order(o.Length, o.Quantity)).ToList();
                            return Task.Run(() => optimizer.Solve(stockSnapshot, ordersCopy, parameters, progress));
                        },
                        createSkippedRow: descriptor => new ComparisonResult
                        {
                            AlgorithmName = descriptor.Name,
                            Success = false,
                        },
                        createResultRow: (optimizer, result) => new ComparisonResult
                        {
                            AlgorithmName = optimizer.Name,
                            TotalCost = result.TotalCost,
                            WasteLength = result.WasteLength,
                            StockUsed = result.StockUsed,
                            MaterialEfficiency = result.MaterialEfficiency,
                            ExecutionTimeMs = result.ExecutionTimeMs,
                            Success = result.Success,
                        },
                        getSolverName: optimizer => optimizer.Name,
                        getReport: result => result.Success
                            ? result.GetDetailedReport(parameters)
                            : $"실패: {result.ErrorMessage}");
                    if (!comparison.Completed) return;

                    var summary = ComparisonWorkflow.Complete(
                        comparison,
                        row => row.Success,
                        row => new ComparisonRankKey(row.TotalCost),
                        (row, rank) => row.Rank = rank,
                        "═══════════════════════════════════════════════════" + Environment.NewLine +
                        "  알고리즘 상세 비교" + Environment.NewLine +
                        "═══════════════════════════════════════════════════" + Environment.NewLine +
                        Environment.NewLine,
                        FormatComparisonOutcome);

                    ComparisonResults.Clear();
                    foreach (var outcome in comparison.Outcomes)
                        ComparisonResults.Add(outcome.Row);

                    ComparisonText = summary.Report;
                    HasComparisonResults = true;
                    OnPropertyChanged(nameof(ComparisonResults));

                    var best = summary.BestOutcome?.Row;
                    if (best != null)
                    {
                        StatusText = $"비교 완료 · 최고: {best.AlgorithmName} · 효율 {best.MaterialEfficiency:F1}%";
                        _dialog.ShowInfo("비교 완료",
                            "알고리즘 비교가 완료되었습니다!\n\n" +
                            $"최고 성능: {best.AlgorithmName}\n" +
                            $"   총 비용: {best.TotalCost:N0}원\n" +
                            $"   재료 효율: {best.MaterialEfficiency:F2}%\n" +
                            $"   실행 시간: {best.ExecutionTimeMs:F3}ms");
                    }
                },
                onError: ex =>
                {
                    _dialog.ShowError("오류", $"오류 발생: {ex.Message}");
                });
        }

        private static string FormatComparisonOutcome(
            SolverComparisonOutcome<ICuttingSolver, SolverResult, ComparisonResult> outcome)
        {
            var report = new StringBuilder();
            report.AppendLine("┌─────────────────────────────────────────────────")
                  .AppendLine($"│ {outcome.AlgorithmName}");
            if (outcome.Solver != null)
                report.AppendLine($"│ 시간 복잡도: {outcome.Solver.TimeComplexity}");
            report.AppendLine("└─────────────────────────────────────────────────")
                  .AppendLine(outcome.Detail)
                  .AppendLine()
                  .AppendLine();
            return report.ToString();
        }
    }
}
