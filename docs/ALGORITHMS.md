# 1D Cutting Stock Algorithms

## Solvers

### 1. GreedyKnapsackSolver

Multi-pass bounded knapsack DP with post-processing.

- **Pass 1** (balanced): max 2 cuts per order per stock, scarcity-first ordering
- **Pass 2** (residual): max 5 cuts per order
- **Pass 3** (fill): unlimited cuts
- **Post-processing**: 2-opt swap + relocate between plans to reduce waste
- **Kerf**: consumed between adjacent cuts
- **Welding**: optional splitting of long orders across stocks

Complexity: O(N * L * Passes), fast for practical inputs.

### 2. ColumnGenerationSolver

Gilmore-Gomory column generation with custom Simplex LP.

- **Master LP**: min total stock used, s.t. demand coverage
- **Pricing**: 1D bounded knapsack DP to find improving patterns
- **Rounding**: floor LP solution, cover residual with greedy
- **Multi-stock**: per-stock-length pricing sub-problems

Complexity: polynomial per iteration, exponential worst-case. Ref: Gilmore & Gomory 1961.

### 3. ArcFlowSolver

Arc flow network model solved by OR-Tools SCIP MIP.

- **Graph**: DAG with nodes 0..stockLength, item arcs (width = length + kerf)
- **GCD optimization**: reduces node count when item lengths share common factors
- **Multi-stock**: separate flow network per stock length, unified MIP
- **Time limit**: 30s (configurable via `MipTimeLimitMs` constant)

Complexity: exact (NP-hard, bounded by timeout). Ref: Valerio de Carvalho 1999.

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| Alpha | float | 1.0 | Cost per mm waste |
| Beta | float | 500 | Cost per weld |
| Gamma | int | 100 | Min reusable leftover (mm) |
| Delta | int | 100 | Min weldable piece (mm) |
| Kerf | int | 0 | Blade width (mm) |
| UsageOrder | enum | SmallToLarge | Stock consumption order |
| EnableWelding | bool | false | Allow order splitting across stocks |

## Cost Formula

```
TotalCost = WasteLength * Alpha + WeldCount * Beta
WasteLength = sum of leftovers < Gamma
WeldCount = sum of (group_size - 1) per weld group
```

## Selection Guide

| Priority | Solver |
|---|---|
| Speed | GreedyKnapsackSolver |
| Quality (LP-optimal) | ColumnGenerationSolver |
| Proven optimality | ArcFlowSolver |
| Welding needed | GreedyKnapsackSolver |

For 2D algorithms, see `docs/2D_ALGORITHMS.md`.
