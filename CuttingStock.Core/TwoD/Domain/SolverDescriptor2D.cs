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
    {
        public bool Supports(SolverCapability capability) => (Capabilities & capability) == capability;
    }
}
