namespace CuttingStock.Core.Domain
{
    /// <summary>UI- and test-visible metadata for a 1D solver.</summary>
    public sealed record SolverDescriptor(
        string Key,
        string DisplayName,
        string Name,
        string Description,
        string TimeComplexity,
        SolverCapability Capabilities,
        string CapabilitySummary,
        string AdvancedNotes,
        Func<ICuttingSolver> CreateSolver)
        : ISolverDescriptor<ICuttingSolver, SolverOptions>
    {
        public bool Supports(SolverCapability capability) => (Capabilities & capability) == capability;

        public string? GetUnsupportedReason(SolverOptions options)
        {
            if (options.EnableWelding && !Supports(SolverCapability.Welding))
                return "용접 옵션을 지원하지 않습니다.";

            return null;
        }
    }
}
