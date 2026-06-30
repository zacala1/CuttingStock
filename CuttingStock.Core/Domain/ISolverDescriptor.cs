using System;

namespace CuttingStock.Core.Domain
{
    /// <summary>Common metadata and option contract for solver selection surfaces.</summary>
    public interface ISolverDescriptor<out TSolver, in TOptions>
    {
        string Key { get; }
        string DisplayName { get; }
        string Name { get; }
        string Description { get; }
        string TimeComplexity { get; }
        SolverCapability Capabilities { get; }
        string CapabilitySummary { get; }
        string AdvancedNotes { get; }
        Func<TSolver> CreateSolver { get; }

        bool Supports(SolverCapability capability);
        string? GetUnsupportedReason(TOptions options);
    }
}
