using CuttingStock.UI.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class ComparisonWorkflowTests
    {
        [Test]
        public void Complete_RanksSuccessfulRowsSelectsExactWinnerAndBuildsReport()
        {
            var first = CreateOutcome("First", success: true, cost: 100, resourceUsed: 2);
            var skipped = CreateOutcome("Skipped", success: false, cost: 0, resourceUsed: 0);
            skipped.Row.Rank = 9;
            var best = CreateOutcome("Best", success: true, cost: 100, resourceUsed: 1);
            var batch = new SolverComparisonBatch<TestSolver, TestResult, TestRow>(
                true,
                [first, skipped, best]);

            var summary = ComparisonWorkflow.Complete(
                batch,
                row => row.Success,
                row => new ComparisonRankKey(row.Cost, row.ResourceUsed),
                (row, rank) => row.Rank = rank,
                "HEADER\n",
                outcome => $"{outcome.AlgorithmName}:{outcome.Detail}\n");

            summary.BestOutcome.Should().BeSameAs(best);
            first.Row.Rank.Should().Be(2);
            skipped.Row.Rank.Should().Be(0);
            best.Row.Rank.Should().Be(1);
            summary.Report.Should().Be(
                "HEADER\nFirst:detail-First\nSkipped:detail-Skipped\nBest:detail-Best\n");
        }

        [Test]
        public void CostThenEfficiency_EqualCostRanksByHigherEfficiencyThenCatalogOrder()
        {
            // 1D leftovers >= Gamma are free, so these all cost 0: efficiency must decide.
            var greedy = CreateOutcome("Greedy", success: true, cost: 0, resourceUsed: 0, efficiency: 88.0);
            var arcFlow = CreateOutcome("ArcFlow", success: true, cost: 0, resourceUsed: 0, efficiency: 95.5);
            var tiedWithArc = CreateOutcome("TiedWithArc", success: true, cost: 0, resourceUsed: 0, efficiency: 95.5);
            var costly = CreateOutcome("Costly", success: true, cost: 40, resourceUsed: 0, efficiency: 99.0);
            var batch = new SolverComparisonBatch<TestSolver, TestResult, TestRow>(
                true,
                [greedy, arcFlow, tiedWithArc, costly]);

            var summary = ComparisonWorkflow.Complete(
                batch,
                row => row.Success,
                row => ComparisonRankKey.CostThenEfficiency(row.Cost, row.Efficiency),
                (row, rank) => row.Rank = rank,
                string.Empty,
                outcome => outcome.Detail);

            summary.BestOutcome.Should().BeSameAs(arcFlow);
            arcFlow.Row.Rank.Should().Be(1);
            tiedWithArc.Row.Rank.Should().Be(2, "exact ties keep catalog order");
            greedy.Row.Rank.Should().Be(3);
            costly.Row.Rank.Should().Be(4, "cost still outranks efficiency");
        }

        [Test]
        public void CostThenEfficiency_SubPermilleDifferencesCollapseToTies()
        {
            ComparisonRankKey.CostThenEfficiency(5, 90.0001)
                .Should().Be(ComparisonRankKey.CostThenEfficiency(5, 90.0004));
            ComparisonRankKey.CostThenEfficiency(5, 91.0)
                .Secondary.Should().BeLessThan(ComparisonRankKey.CostThenEfficiency(5, 90.0).Secondary);
        }

        [Test]
        public void Complete_NoSuccessfulRowsReturnsNoWinnerAndClearsStaleRanks()
        {
            var failed = CreateOutcome("Failed", success: false, cost: 1, resourceUsed: 1);
            failed.Row.Rank = 3;
            var batch = new SolverComparisonBatch<TestSolver, TestResult, TestRow>(
                true,
                [failed]);

            var summary = ComparisonWorkflow.Complete(
                batch,
                row => row.Success,
                row => new ComparisonRankKey(row.Cost, row.ResourceUsed),
                (row, rank) => row.Rank = rank,
                string.Empty,
                outcome => outcome.Detail);

            summary.BestOutcome.Should().BeNull();
            failed.Row.Rank.Should().Be(0);
            summary.Report.Should().Be("detail-Failed");
        }

        [Test]
        public void Complete_InterruptedBatchIsRejected()
        {
            var batch = new SolverComparisonBatch<TestSolver, TestResult, TestRow>(
                false,
                []);

            Action complete = () => ComparisonWorkflow.Complete(
                batch,
                row => row.Success,
                row => new ComparisonRankKey(row.Cost, row.ResourceUsed),
                (row, rank) => row.Rank = rank,
                string.Empty,
                outcome => outcome.Detail);

            complete.Should().Throw<InvalidOperationException>()
                .WithMessage("*interrupted comparison*");
        }

        private static SolverComparisonOutcome<TestSolver, TestResult, TestRow> CreateOutcome(
            string name,
            bool success,
            long cost,
            int resourceUsed,
            double efficiency = 0)
        {
            var row = new TestRow(success, cost, resourceUsed, efficiency);
            return new SolverComparisonOutcome<TestSolver, TestResult, TestRow>(
                name,
                row,
                success ? new TestSolver(name) : null,
                success ? new TestResult() : null,
                $"detail-{name}");
        }

        private sealed record TestSolver(string Name);
        private sealed record TestResult;

        private sealed class TestRow
        {
            public TestRow(bool success, long cost, int resourceUsed, double efficiency = 0)
            {
                Success = success;
                Cost = cost;
                ResourceUsed = resourceUsed;
                Efficiency = efficiency;
            }

            public bool Success { get; }
            public long Cost { get; }
            public int ResourceUsed { get; }
            public double Efficiency { get; }
            public int Rank { get; set; }
        }
    }
}
