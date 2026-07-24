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
            int resourceUsed)
        {
            var row = new TestRow(success, cost, resourceUsed);
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
            public TestRow(bool success, long cost, int resourceUsed)
            {
                Success = success;
                Cost = cost;
                ResourceUsed = resourceUsed;
            }

            public bool Success { get; }
            public long Cost { get; }
            public int ResourceUsed { get; }
            public int Rank { get; set; }
        }
    }
}
