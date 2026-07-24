# Architecture Guardrails

This document records the architecture rules that must remain true while
CuttingStock is refactored. Treat it as a contributor-facing checklist: if a
change violates one of these rules, either redesign the change or add a
deliberate, reviewed migration plan before merging.

## Project Boundaries

| Area | Owns | Must not own |
|---|---|---|
| `CuttingStock.Core` | Domain models, solver interfaces, 1D/2D algorithms, scenario persistence, result validation | WPF types, ViewModels, LiveCharts, UI controls |
| `CuttingStock.UI` | WPF views, ViewModels, dialog/file prompts, export files, charts, canvas rendering | Solver business logic that can live in Core |
| `CuttingStock.Tests` | Core algorithm/domain/persistence tests under `net10.0` | WPF-only types |
| `CuttingStock.UI.Tests` | ViewModel and UI service tests under `net10.0-windows` | Algorithm correctness suites |
| `CuttingStock.Benchmarks` | Informational BenchmarkDotNet workloads | Required correctness gates |

`CuttingStock.Core` must continue to build without `net10.0-windows`.
Anything that can be tested without WPF belongs in Core or in a UI service
that exposes plain data.

## 1D Solver Guardrails

### Kerf

Kerf is consumed only between adjacent cuts on the same stock bar:

```text
consumed = sum(cut lengths) + max(0, cuts.Count - 1) * kerf
leftover = max(0, stock length - consumed)
```

Use `SolverUtils.ComputeLeftover` instead of inlining this formula. The first
cut does not consume edge kerf.

### Welding

A plan is structurally welded when any cut in the plan has `WeldGroupId`.
Post-processing must preserve that invariant:

- Do not relocate or swap welded cuts as ordinary cuts.
- Keep `WeldGroupId` assignments stable when copying cuts.
- Validate that every weld group contains at least two pieces and satisfies
  the configured minimum weldable piece length.
- Recompute `Leftover` after any post-processing change through the shared
  leftover path.

### Results

Successful 1D results must satisfy all of these conditions:

- Produced demand matches input demand, including weld-group totals.
- Stock usage by length does not exceed inventory.
- Every plan `Leftover` matches `SolverUtils.ComputeLeftover`.
- `ReusableLeftovers`, `WasteLength`, `WeldCount`, and `TotalCost` are
  finalized through the shared result path.
- `TotalCost` remains a `long`; do not reintroduce integer-truncated
  `Math.Round((int)(...))` calculations.

## 2D Solver Guardrails

### Sheet Aggregation

Every 2D solver entry must aggregate sheets by `(Width, Height)` before any
dictionary or stock-capacity logic:

```csharp
var input = TwoDInputPreprocessor.Preprocess(sheets, orders, result);
```

`TwoDInputPreprocessor` owns the entry policy and aggregation implementation.
`SolverUtils2D.AggregateByDims` is a one-way source-compatible facade that
delegates to the preprocessor for legacy callers.

This is required because `Sheet.Equals` is structural. Duplicate same-dimension
rows can otherwise collide as dictionary keys and hide inventory.

### Deadline Semantics

`SolverOptions2D.TimeLimitMs` is an absolute wall-clock budget from solver
start. Warm-start and bootstrap time count toward the same budget. Do not reset
the clock after `ShelfGuillotineSolver` warm-starts or pattern-pool bootstrap.

### Stage Semantics

`SolverOptions2D.Stage` accepts `2` or `3`, but current production solvers do
not enforce 3-stage cuts. Current policy:

- `TwoStageShelfGuillotineSolver` enforces a 2-stage shelf pattern.
- `ShelfGuillotineSolver`, `ColumnGeneration2DSolver`, and
  `StagedMipGuillotineSolver` treat `Stage` as advisory metadata.
- Do not claim 3-stage enforcement until a solver has tests proving that
  restriction.

### Pattern Contract

`CuttingPattern2D.Placements` are the public source of truth for now.
`CuttingPattern2D.Root` is optional derived data. A successful 2D result must
validate through placement geometry and guillotine compliance:

- Placements remain within the trimmed sheet.
- Placements do not overlap under kerf-aware checks.
- Placement dimensions and rotation flags match the source `RectOrder`.
- Multiplicity respects sheet inventory.
- Pattern geometry is accepted by `GuillotineValidator`.
- UI, export, and tests must consume `Placements`; do not require `Root` to be
  non-null in this architecture pass.
- If a tree is needed, derive it from `Placements` with `PatternBuilder` and
  keep `GuillotineValidator` as the acceptance gate.

## UI Guardrails

The WPF views may own visual object construction when the types do not
round-trip cleanly through bindings:

- LiveCharts `ISeries`, `Axis`, and paint objects stay in the views or WPF-only
  projection adapters.
- The 2D placement `Canvas` and `Shape` objects stay in `TwoDTab.xaml.cs` or a
  WPF-only view helper.
- ViewModels should expose plain state and projection DTOs where practical.

Non-visual workflow should be extracted behind testable seams over time:

- scenario history and recent-file policy
- CSV/XLSX/clipboard parse logic
- result text search
- chart data projection before WPF object construction

Progress from worker-thread solver runs must be wrapped in `Progress<double>`
before reaching UI state so updates marshal back to the UI thread.

## User-Facing Parity

When a user-facing workflow is added to one dimension, record whether it also
belongs to the other dimension:

| Workflow | 1D | 2D | Policy |
|---|---:|---:|---|
| Add/delete rows | yes | yes | Keep parity |
| Example data | yes | yes | Keep parity |
| Scenario save/load | yes | yes | Keep parity |
| Calculate and compare | yes | yes | Keep parity |
| Cancel in-flight run | yes | yes | Keep parity |
| CSV/Excel export | yes | yes | Keep parity |
| Result visualization | bar groups | placement canvas | Dimension-specific |
| LiveCharts comparison | yes | yes | Keep parity |
| Recent scenario MRU | yes | no | Intentional until Task 14 resolves parity |
| Drag/drop scenario import | yes | no | Intentional until Task 14 resolves parity |
| Result text search | yes | no | Intentional until Task 14 resolves parity |

If a mismatch remains intentional, document why. If it is not intentional, add
the feature and tests in the same change.

## Benchmark And Test Boundaries

Correctness tests should be deterministic and run in the default test command.
Long-running or informational performance work belongs in
`CuttingStock.Benchmarks` unless a test is deliberately marked as an explicit
performance gate.

Default verification remains:

```bash
dotnet build CuttingStock.slnx -c Release
dotnet test CuttingStock.slnx -c Release --nologo --no-build
```

Explicit benchmarks must not become required for ordinary correctness merges.

## Adding A Solver Safely

Before adding a solver to a catalog:

1. Add the solver implementation under Core.
2. Add a catalog descriptor with truthful capabilities.
3. Add catalog-driven contract coverage for shared invariants.
4. Add solver-specific tests only for behavior that is unique to that solver.
5. Wire UI selection through existing ViewModel descriptor surfaces.
6. Keep export, comparison, and visualization behavior compatible with existing
   result models.

For 2D solvers, the entry path must aggregate sheets, final successful results
must validate, and `TimeLimitMs` must remain absolute from solver start.
