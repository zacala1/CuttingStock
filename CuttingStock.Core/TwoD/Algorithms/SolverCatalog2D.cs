using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Domain;

namespace CuttingStock.Core.TwoD.Algorithms
{
    /// <summary>Canonical 2D solver list and the options each solver actually honors.</summary>
    public static class SolverCatalog2D
    {
        public static IReadOnlyList<SolverDescriptor2D> All { get; } =
        [
            new SolverDescriptor2D(
                Key: "shelf-guillotine",
                DisplayName: "Shelf Guillotine (빠름)",
                Name: "Shelf Guillotine (NFDH/FFDH/BFDH)",
                Description: "Best-of-15 shelf heuristic with rotation.",
                TimeComplexity: "O(K * N log N)",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.Trim |
                    SolverCapability.Rotation |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.AdvisoryStage |
                    SolverCapability.Heuristic,
                CapabilitySummary: "Kerf, Trim, 회전, 재고 사용 순서를 반영합니다.",
                AdvancedNotes: "Stage 값은 현재 결과를 제한하지 않는 advisory 값입니다.",
                SupportedStages: [2, 3],
                CreateSolver: () => new ShelfGuillotineSolver()),

            new SolverDescriptor2D(
                Key: "two-stage-shelf-guillotine",
                DisplayName: "Two-Stage Shelf Guillotine (강제 2-stage)",
                Name: "Two-Stage Shelf Guillotine",
                Description: "Shelf heuristic locked to two-stage guillotine patterns.",
                TimeComplexity: "O(K * N log N)",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.Trim |
                    SolverCapability.Rotation |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.EnforcedStage |
                    SolverCapability.Heuristic,
                CapabilitySummary: "Kerf, Trim, 회전, 재고 사용 순서를 반영하며 2-stage shelf 패턴만 허용합니다.",
                AdvancedNotes: "첫 절단은 shelf strip, 두 번째 절단은 shelf 내부 item 절단으로 제한됩니다.",
                SupportedStages: [2],
                CreateSolver: () => new TwoStageShelfGuillotineSolver()),

            new SolverDescriptor2D(
                Key: "column-generation-2d",
                DisplayName: "Column Generation 2D (LP + DP pricing)",
                Name: "Column Generation 2D (Gilmore-Gomory)",
                Description: "CG with GLOP master + Beasley DP pricing, LP-rounded.",
                TimeComplexity: "Poly/iter, exp worst-case",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.Trim |
                    SolverCapability.Rotation |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.TimeLimit |
                    SolverCapability.AdvisoryStage |
                    SolverCapability.LinearRelaxation,
                CapabilitySummary: "Kerf, Trim, 회전, 시간 제한, 재고 사용 순서를 반영합니다.",
                AdvancedNotes: "Stage 값은 현재 결과를 제한하지 않는 advisory 값입니다.",
                SupportedStages: [2, 3],
                CreateSolver: () => new ColumnGeneration2DSolver()),

            new SolverDescriptor2D(
                Key: "staged-mip-guillotine",
                DisplayName: "Staged MIP Guillotine (CBC)",
                Name: "Staged Guillotine MIP (CBC)",
                Description: "CG-enriched pattern pool + CBC integer master.",
                TimeComplexity: "NP-hard, bounded by TimeLimitMs",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.Trim |
                    SolverCapability.Rotation |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.TimeLimit |
                    SolverCapability.AdvisoryStage |
                    SolverCapability.IntegerProgramming,
                CapabilitySummary: "Kerf, Trim, 회전, 시간 제한, 재고 사용 순서를 반영합니다.",
                AdvancedNotes: "이름은 staged지만 현재 Stage=2/3을 강제하지는 않습니다. 패턴은 unrestricted guillotine입니다.",
                SupportedStages: [2, 3],
                CreateSolver: () => new StagedMipGuillotineSolver()),
        ];

        public static SolverDescriptor2D GetByIndex(int index)
        {
            if (index < 0 || index >= All.Count) return All[0];
            return All[index];
        }
    }
}
