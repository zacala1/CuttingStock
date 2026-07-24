using System;
using System.Linq;
using System.Text;

namespace CuttingStock.UI.ViewModels
{
    public readonly record struct ComparisonRankKey(long Primary, long Secondary = 0);

    public sealed record CompletedComparison<TSolver, TResult, TRow>(
        SolverComparisonOutcome<TSolver, TResult, TRow>? BestOutcome,
        string Report)
        where TSolver : class
        where TResult : class
        where TRow : class;

    /// <summary>Completes dimension-neutral ranking and report assembly for a solver batch.</summary>
    public static class ComparisonWorkflow
    {
        public static CompletedComparison<TSolver, TResult, TRow> Complete<TSolver, TResult, TRow>(
            SolverComparisonBatch<TSolver, TResult, TRow> batch,
            Func<TRow, bool> isSuccessful,
            Func<TRow, ComparisonRankKey> getRankKey,
            Action<TRow, int> setRank,
            string reportHeader,
            Func<SolverComparisonOutcome<TSolver, TResult, TRow>, string> formatOutcome)
            where TSolver : class
            where TResult : class
            where TRow : class
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(isSuccessful);
            ArgumentNullException.ThrowIfNull(getRankKey);
            ArgumentNullException.ThrowIfNull(setRank);
            ArgumentNullException.ThrowIfNull(reportHeader);
            ArgumentNullException.ThrowIfNull(formatOutcome);
            if (!batch.Completed)
                throw new InvalidOperationException("Cannot complete an interrupted comparison batch.");

            foreach (var outcome in batch.Outcomes)
                setRank(outcome.Row, 0);

            var ranked = batch.Outcomes
                .Where(outcome => isSuccessful(outcome.Row))
                .Select(outcome => (Outcome: outcome, Key: getRankKey(outcome.Row)))
                .OrderBy(item => item.Key.Primary)
                .ThenBy(item => item.Key.Secondary)
                .Select(item => item.Outcome)
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
                setRank(ranked[i].Row, i + 1);

            var report = new StringBuilder(reportHeader);
            foreach (var outcome in batch.Outcomes)
                report.Append(formatOutcome(outcome));

            return new CompletedComparison<TSolver, TResult, TRow>(
                ranked.FirstOrDefault(),
                report.ToString());
        }
    }
}
