using System;
using CuttingStock.Core.Persistence;

namespace CuttingStock.UI.Services
{
    public enum ScenarioRoute
    {
        OneD,
        TwoD,
    }

    /// <summary>Classifies drag/drop scenario files without depending on WPF data objects.</summary>
    public static class ScenarioFileRouteService
    {
        public static bool IsCandidate(string? path) =>
            !string.IsNullOrWhiteSpace(path) &&
            path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        public static ScenarioRoute DetectRoute(string path) =>
            ScenarioService.DetectKind(path) switch
            {
                ScenarioKind.OneD => ScenarioRoute.OneD,
                ScenarioKind.TwoD => ScenarioRoute.TwoD,
                _ => throw new InvalidOperationException("Unknown scenario kind."),
            };
    }
}
