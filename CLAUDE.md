# CLAUDE.md — Cutting Stock Optimization

Project-specific notes for Claude Code agents working in this repository.
Authoritative: this file overrides general advice when there's a conflict.

## What this project is

A .NET 10 WPF desktop app that solves 1D (rebar/bar) and 2D (sheet/plate)
Cutting Stock problems. The catalogs currently expose seven 1D variants
and four 2D solvers across heuristic, LP, and MIP families.

Layout:
- `CuttingStock.Core/` — pure algorithms + domain models. No WPF.
- `CuttingStock.UI/` — WPF (net10.0-windows). MainWindow has 1D tab,
  TwoDTab.xaml has 2D tab. Services/ holds Export, Scenario, etc.
- `CuttingStock.Tests/` — NUnit + FluentAssertions. Lives in `net10.0`,
  so it cannot reference UI types (.NET-Windows). Put any logic that
  needs testing in Core.
- `CuttingStock.Benchmarks/` — BenchmarkDotNet.

## Build / test commands

```bash
# Build everything
dotnet build CuttingStock.slnx -c Release

# Full correctness test suite
dotnet test CuttingStock.slnx -c Release --nologo --no-build

# Run the LargeScale 1000-orders BenchmarkDotNet workload
dotnet run --project CuttingStock.Benchmarks -c Release -- --large

# Run a focused category
dotnet test CuttingStock.slnx --filter "Category=Welding"

# Launch the WPF app
dotnet run --project CuttingStock.UI
```

Tests currently pass 525+ in ~1m 10s. Don't merge anything that drops
the count.

## Domain conventions

**Models are immutable.** `Order`, `RebarStock`, `Sheet`, `RectOrder`
have no parameterless constructor and no setters — construct with the
validating constructor. `Cut` and `CuttingPlan.StockLength` / `.Cuts`
are `init`-only. Only `CuttingPlan.Leftover` is mutable because
post-processing recomputes it. Don't add public setters back.

**Kerf is between adjacent cuts.** Total consumed length on a bar is
`sum(cut lengths) + (cuts.Count - 1) * kerf`. The first cut does not
consume kerf at the edge. Use `SolverUtils.ComputeLeftover` everywhere
instead of inlining the formula.

**Welded plans are structural.** A plan is "welded" iff any of its
cuts carries a `WeldGroupId`. LocalSearchOptimize, RedistributeCuts
and FindHostPlanForWeld all check this invariant — keep it intact when
adding new post-processing.

**Sheets aggregate by (Width, Height).** All 2D solvers enter through
`TwoDInputPreprocessor`, which owns the aggregation implementation.
`SolverUtils2D.AggregateByDims` remains a one-way source-compatible facade
for legacy callers. `Sheet.Equals` is structural, so unaggregated
duplicate-dim rows collide as the same key in `Dictionary<Sheet, _>` and
silently hide half the inventory.

**TimeLimitMs is an absolute wall-clock deadline** from solver start,
not "time remaining after warm-start". 2D solvers compare against
`sw.ElapsedMilliseconds` directly; the warm-start / bootstrap time
counts toward the budget.

**Stage policy is descriptor-specific.** `TwoStageShelfGuillotineSolver`
enforces 2-stage shelf patterns. `ShelfGuillotineSolver`, CG2D, and
Staged MIP treat `SolverOptions2D.Stage` as advisory. No solver currently
enforces 3-stage cuts.

## Things that look broken but aren't

- `GenerateSolutionHybrid` in `ColumnGenerationSolver` is **not** the
  main entry point. The post-LP rounding goes through
  `GenerateSolutionFloorResidual`, which falls back to
  `ApplyGreedyResidual` (renamed from `GenerateSolutionHybrid` in a
  prior pass — the rename intentionally signals "fallback").
- `EstimateFutureWasteMFFDFromDict` runs MFFD+BFD, not pure FFD — the
  function is named after the entry point, not the helper inside.
- 1D `loadingOverlay` only covers the algorithm-settings GroupBox,
  not the result tabs. This is intentional so the user can read the
  previous result while the next one runs.

## Skill compatibility notes (Anthropic / community)

This repo has been audited against the `anthropics/skills` repo and
`Aaronontheweb/dotnet-skills`. We've applied the recommendations that
fit the project's scale:

- `modern-csharp-coding-standards` (sealed, records, init-only, value
  semantics) — applied to domain models.
- `type-design-performance` (sealed classes, structural equality) —
  applied; see Sheet/RectOrder/Order/RebarStock.
- `dotnet-slopwatch` (anti-pattern detection) — manual audit pass done
  in `git log`. Concrete instances: removed dead exception path,
  consolidated input validation in `SolverUtils2D.ValidateInputs`,
  killed duplicate sheet-inventory constraints in StagedMip.

**MVVM:** As of `8075cc7` the UI is MVVM (CommunityToolkit.Mvvm).
MainWindow / TwoDTab code-behind shrank from 2052 to 607 lines (-70%).
ViewModels live in `CuttingStock.UI/ViewModels/`, dialog/file-IO is
abstracted via `IDialogService`, and the 1D visualization data is built
in `VisualizationService`. The Views still own LiveCharts series
construction and the 2D placement Canvas because those touch WPF
visual types in ways that don't round-trip through bindings cleanly —
keep that split intact unless you have a concrete reason to invert it.

## When you find a bug

1. Add a regression test that fails on the unfixed code (NUnit).
2. Fix it minimally — don't refactor unrelated code in the same pass.
3. Run `dotnet test` and confirm the new test passes plus no
   pre-existing test regressed.
4. Commit with a message that names the bug and quotes the
   file:line where it was introduced.

## Changelog discipline

The repo keeps a hand-rolled `CHANGELOG.md` at the root rather than
generated release notes. After a meaningful round of work (bug fixes
landing, a feature shipping, a refactor across multiple files), add a
short bullet block at the top of `CHANGELOG.md` with the date heading
and the commit SHAs you produced. Old PHASE notes used to live as
separate files under `docs/archive/`; that approach grew stale (no one
updated them after Jan 2026) and was collapsed into `CHANGELOG.md` in
the May 2026 doc refresh. Don't reintroduce per-phase markdown files
— append to `CHANGELOG.md` instead.

## When you add a feature

1. Add it to `Core` if it has no WPF dependency, so it's testable.
2. Wire UI in MainWindow.xaml or TwoDTab.xaml as appropriate.
3. Mirror 1D ↔ 2D feature parity if it's user-facing (we worked hard
   to align the two tabs; don't let one drift again).
4. Add ToolTips on any new control that isn't self-explanatory.

## Don't do these

- Don't reintroduce `Math.Round((int)(...))` for cost calculations.
  `TotalCost` is `long` for overflow safety.
- Don't aggregate sheets only inside the IP — every solver entry must
  aggregate.
- Don't pass `IProgress<double>` from a worker thread without
  wrapping in `Progress<double>` first — `Progress<T>` marshals back
  to the UI thread.
- Don't store algorithm state on the UserControl (e.g. a static
  cache). Pass it through the solver or compute fresh.
- Don't add `async void` methods outside event handlers.
