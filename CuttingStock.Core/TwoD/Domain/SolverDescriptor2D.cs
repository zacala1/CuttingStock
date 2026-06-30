using CuttingStock.Core.Domain;

namespace CuttingStock.Core.TwoD.Domain
{
    /// <summary>UI- and test-visible metadata for a 2D solver.</summary>
    public sealed record SolverDescriptor2D(
        string Key,
        string DisplayName,
        string Name,
        string Description,
        string TimeComplexity,
        SolverCapability Capabilities,
        string CapabilitySummary,
        string AdvancedNotes,
        IReadOnlyList<int> SupportedStages,
        Func<ICuttingSolver2D> CreateSolver)
        : ISolverDescriptor<ICuttingSolver2D, SolverOptions2D>
    {
        public bool Supports(SolverCapability capability) => (Capabilities & capability) == capability;

        public string? GetUnsupportedReason(SolverOptions2D options)
        {
            if (Supports(SolverCapability.EnforcedStage) && !SupportedStages.Contains(options.Stage))
                return $"{options.Stage}-stage 옵션을 지원하지 않습니다.";

            return null;
        }
    }
}
