using CuttingStock.Core.Domain;

namespace CuttingStock.Core.Algorithms
{
    /// <summary>Canonical 1D solver list and the options each solver actually honors.</summary>
    public static class SolverCatalog
    {
        public static IReadOnlyList<SolverDescriptor> All { get; } =
        [
            new SolverDescriptor(
                Key: "greedy-knapsack",
                DisplayName: "Greedy Knapsack DP (빠름, 용접 지원)",
                Name: "Greedy Knapsack DP",
                Description: "Multi-pass sparse DP with post-processing.",
                TimeComplexity: "O(N * L * Passes)",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.Welding |
                    SolverCapability.Heuristic,
                CapabilitySummary: "Kerf, 재고 사용 순서, 용접 옵션을 실제 반영합니다.",
                AdvancedNotes: "대규모 입력에 빠른 휴리스틱입니다. 용접이 필요한 긴 주문은 이 solver만 처리합니다.",
                CreateSolver: () => new GreedyKnapsackSolver()),

            new SolverDescriptor(
                Key: "column-generation",
                DisplayName: "Column Generation LP (LP + residual)",
                Name: "Column Generation (LP)",
                Description: "CG with Simplex master + knapsack DP pricing.",
                TimeComplexity: "Poly/iter, exp worst-case",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.LinearRelaxation,
                CapabilitySummary: "Kerf와 재고 사용 순서를 반영합니다. 용접은 지원하지 않습니다.",
                AdvancedNotes: "LP master를 푼 뒤 floor-then-residual 방식으로 정수해를 구성합니다.",
                CreateSolver: () => new ColumnGenerationSolver()),

            new SolverDescriptor(
                Key: "column-generation-stabilized",
                DisplayName: "Column Generation LP (Stabilized dual)",
                Name: "Column Generation (Stabilized LP)",
                Description: "CG with dual-smoothed knapsack pricing and raw-dual fallback.",
                TimeComplexity: "Poly/iter, exp worst-case",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.LinearRelaxation,
                CapabilitySummary: "Kerf와 재고 사용 순서를 반영합니다. 용접은 지원하지 않습니다.",
                AdvancedNotes: "이전 iteration dual과 현재 dual을 0.70/0.30으로 섞어 pricing 흔들림을 줄입니다.",
                CreateSolver: () => new StabilizedColumnGenerationSolver()),

            new SolverDescriptor(
                Key: "column-generation-multicolumn",
                DisplayName: "Column Generation LP (Multi-column)",
                Name: "Column Generation (Multi-column LP)",
                Description: "CG that adds multiple improving knapsack pricing columns per iteration.",
                TimeComplexity: "Poly/iter, exp worst-case",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.LinearRelaxation,
                CapabilitySummary: "Kerf와 재고 사용 순서를 반영합니다. 용접은 지원하지 않습니다.",
                AdvancedNotes: "각 iteration에서 최고 pricing column과 주요 item 제외 column을 최대 4개까지 추가합니다.",
                CreateSolver: () => new MultiColumnGenerationSolver()),

            new SolverDescriptor(
                Key: "column-generation-integer-master",
                DisplayName: "Column Generation LP (Integer master)",
                Name: "Column Generation (Integer Master)",
                Description: "CG with a generated-column CBC integer master polish.",
                TimeComplexity: "Poly/iter + small MIP polish",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.LinearRelaxation |
                    SolverCapability.IntegerProgramming,
                CapabilitySummary: "Kerf와 재고 사용 순서를 반영합니다. 용접은 지원하지 않습니다.",
                AdvancedNotes: "생성된 column만 대상으로 CBC 정수 master를 최대 5초 풀고 실패 시 기존 라운딩으로 fallback합니다.",
                CreateSolver: () => new IntegerMasterColumnGenerationSolver()),

            new SolverDescriptor(
                Key: "global-stock-column-generation",
                DisplayName: "Global Stock CG (variable stock)",
                Name: "Global Stock Column Generation",
                Description: "Variable-stock CG with a global generated-column integer master.",
                TimeComplexity: "Poly/iter + MIP polish, exp worst-case",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.LinearRelaxation |
                    SolverCapability.IntegerProgramming,
                CapabilitySummary: "여러 stock 길이를 하나의 master에서 함께 선택합니다. 용접은 지원하지 않습니다.",
                AdvancedNotes: "stock 길이별 순차 처리 대신 전체 재고 pool의 pattern을 CBC 정수 master에서 동시에 고릅니다.",
                CreateSolver: () => new GlobalStockColumnGenerationSolver()),

            new SolverDescriptor(
                Key: "arc-flow",
                DisplayName: "Arc Flow MIP (정확, OR-Tools)",
                Name: "Arc Flow MIP (OR-Tools)",
                Description: "Exact arc flow network + SCIP MIP.",
                TimeComplexity: "Exact (MIP, 30s limit)",
                Capabilities:
                    SolverCapability.Kerf |
                    SolverCapability.StockUsageOrder |
                    SolverCapability.IntegerProgramming,
                CapabilitySummary: "Kerf와 재고 사용 순서를 반영합니다. 용접은 지원하지 않습니다.",
                AdvancedNotes: "SCIP 기반 정수계획 모델이며 내부 30초 제한을 사용합니다.",
                CreateSolver: () => new ArcFlowSolver()),
        ];

        public static SolverDescriptor GetByIndex(int index)
        {
            if (index < 0 || index >= All.Count) return All[0];
            return All[index];
        }
    }
}
