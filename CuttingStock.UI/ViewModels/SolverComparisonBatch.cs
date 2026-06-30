namespace CuttingStock.UI.ViewModels
{
    public sealed record SolverComparisonOutcome<TSolver, TResult, TRow>(
        string AlgorithmName,
        TRow Row,
        TSolver? Solver,
        TResult? Result,
        string Detail)
        where TSolver : class
        where TResult : class
        where TRow : class;

    public sealed record SolverComparisonBatch<TSolver, TResult, TRow>(
        bool Completed,
        IReadOnlyList<SolverComparisonOutcome<TSolver, TResult, TRow>> Outcomes)
        where TSolver : class
        where TResult : class
        where TRow : class;
}
