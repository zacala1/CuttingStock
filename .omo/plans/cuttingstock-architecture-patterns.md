# CuttingStock Architecture And Pattern Hardening Plan

## TL;DR
> **Summary**: Harden CuttingStock by turning implicit conventions into tested contracts, then reduce duplication through profiles, focused services, and UI projection seams without changing solver behavior first.
> **Deliverables**:
> - Core 1D and 2D solver contract tests and finalization/validation pipelines
> - Explicit 1D CG profiles, 2D deadline/stage/pattern policy, and focused utility services
> - Thinner WPF shell with testable services and bindable projection DTOs
> - Build/test/benchmark/docs governance that prevents drift
> **Effort**: XL
> **Parallel**: YES - 5 waves
> **Critical Path**: Task 1 -> Tasks 2-3 -> Tasks 4-8 -> Tasks 9-13 -> Final Verification

## Context
### Original Request
The user asked for a thorough architecture and pattern analysis, with subagent delegation, and a complete solution for what must be done to make the repository excellent from an architecture/pattern standpoint.

### Investigation Summary
- Current structure is healthy: `CuttingStock.Core` is WPF-free, `CuttingStock.UI` owns WPF, `CuttingStock.Tests` targets `net10.0`, `CuttingStock.UI.Tests` targets `net10.0-windows`, and `CuttingStock.Benchmarks` is separate.
- Correctness tests are strong, but several important architecture rules are still conventions rather than enforced contracts.
- 1D risk: mutable solver result surfaces and distributed welding/post-processing invariants.
- 2D risk: broad `SolverUtils2D`, duplicated guillotine pattern representations, call-site aggregation/deadline conventions, and mixed stage semantics.
- UI risk: MVVM exists, but code-behind still owns shell workflow, import/search/MRU/chart/canvas triggers, and event/dispatcher glue.
- Repo risk: benchmarks leak into the correctness test project, docs have test-count drift, and there is no visible central build/package/CI governance.

### Metis Review Resolutions
- Artifact decision: this file is the executable architecture backlog; ADR-style decisions are embedded in each task.
- Public API compatibility: no breaking change in the first implementation pass. Add internal wrappers/facades and preserve existing solver interfaces/classes unless a later task explicitly adds a compatibility adapter.
- 2D pattern truth: flat `Placements` remain the public source of truth. `Root` stays optional/derived until every solver can populate it without breaking UI/export behavior.
- Stage behavior: do not change solver output semantics in this plan. Clarify and test existing policy: only `TwoStageShelfGuillotineSolver` enforces 2-stage; other 2D solvers treat `Stage` as advisory.
- UI visual boundary: LiveCharts series and WPF Canvas elements remain in views per project guidance. New services may produce DTO/projection data only.
- Governance scope: include build props, package centralization, CI, benchmark separation, and docs count policy as separate tasks so they can be merged independently.

## Work Objectives
### Core Objective
Make architecture rules explicit, tested, and maintainable without changing optimization behavior or violating the Core/UI boundary.

### Deliverables
- 1D solver contract tests and result validation/finalization pipeline.
- 2D solver pre/post pipeline, deadline helper, stage policy tests, and pattern contract tests.
- CG and 2D pattern-pool strategy seams that expose variant behavior through profiles/services rather than hidden constructor or utility policy.
- UI shell services and projection DTOs that reduce code-behind while keeping WPF-only rendering in views.
- Repository governance: central props/packages, CI, benchmark separation, and single-source docs count policy.

### Definition Of Done
- `dotnet build CuttingStock.slnx -c Release` succeeds.
- `dotnet test CuttingStock.slnx -c Release --nologo --no-build` succeeds with no test-count drop after intentional test additions.
- Architecture guardrail tests cover 1D invariants, 2D entry invariants, UI shell service behavior, benchmark separation, and catalog/stage contracts.
- No WPF reference is introduced into `CuttingStock.Core`.
- No per-phase markdown files are introduced; meaningful completed work is summarized in `CHANGELOG.md`.

### Must Have
- Preserve kerf semantics: 1D kerf is between adjacent cuts and leftover uses `SolverUtils.ComputeLeftover`.
- Preserve welded-plan structural invariant: a plan is welded iff any cut has `WeldGroupId`.
- Preserve 2D sheet aggregation by `(Width, Height)` for every solver entry.
- Preserve 2D absolute wall-clock `TimeLimitMs` from solver start, including warm-start/bootstrap time.
- Preserve current UI behavior unless a task explicitly adds parity behavior with tests.

### Must NOT Have
- No Core dependency on WPF, LiveCharts, ClosedXML UI types, or view models.
- No source code refactor that changes solver quality/cost/runtime without regression tests first.
- No public API breaking change in the first pass.
- No staging/committing unrelated untracked files such as the current untracked `AGENTS.md` unless the user explicitly asks.

## Verification Strategy
> ZERO HUMAN INTERVENTION - all verification is agent-executed.
- Test decision: tests-first for every behavior guardrail; tests-after only for mechanical build governance changes.
- Frameworks: NUnit 4, FluentAssertions, existing test projects.
- Commands:
  - `dotnet build CuttingStock.slnx -c Release`
  - `dotnet test CuttingStock.slnx -c Release --nologo --no-build`
  - Focused categories/filters named in each task.
- Evidence: `.omo/evidence/task-{N}-{slug}.txt` or `.omo/evidence/task-{N}-{slug}.md`.

## Execution Strategy
### Parallel Execution Waves
Wave 1: Tasks 1-3 establish contract tests and baseline evidence.
Wave 2: Tasks 4-10 harden Core contracts and strategy seams.
Wave 3: Tasks 11-14 thin UI shell and remove 1D/2D workflow drift.
Wave 4: Tasks 15-17 add repository governance and docs consistency.
Wave 5: Task 18 and Final Verification integrate, document, and audit.

### Dependency Matrix
| Task | Depends On | Blocks |
| --- | --- | --- |
| 1 | None | 2,3,18 |
| 2 | 1 | 4,9,18 |
| 3 | 1 | 5,6,7,8,18 |
| 4 | 2 | 9,18 |
| 5 | 3 | 6,7,8,18 |
| 6 | 3,5 | 18 |
| 7 | 3,5 | 8,18 |
| 8 | 3,5,7 | 18 |
| 9 | 2,4 | 18 |
| 10 | 5,7,8 | 18 |
| 11 | 1 | 13,14,18 |
| 12 | 11 | 13,14,18 |
| 13 | 11,12 | 18 |
| 14 | 11,12 | 18 |
| 15 | 1 | 17,18 |
| 16 | 1 | 17,18 |
| 17 | 15,16 | 18 |
| 18 | All previous | Final Verification |

## TODOs

- [x] 1. Capture Architecture Baseline And Guardrail Map

  **What to do**: Create a concise architecture map that names every enforced rule before refactoring. Include Core/UI dependency boundaries, solver invariants, UI shell responsibilities, benchmark/test separation, and current exceptions. Put it in `docs/ARCHITECTURE_GUARDRAILS.md` or a clearly named docs file.
  **Must NOT do**: Do not create per-phase docs under `docs/archive/`; do not edit source behavior.

  **Parallelization**: Can Parallel: YES | Wave 1 | Blocks: 2,3,18 | Blocked By: None

  **References**:
  - Pattern: `AGENTS.md` - authoritative repo constraints.
  - Pattern: `CuttingStock.Core/CuttingStock.Core.csproj` - Core must stay WPF-free.
  - Pattern: `CuttingStock.UI/CuttingStock.csproj` - WPF/ClosedXML/LiveCharts live in UI.
  - Pattern: `CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj` - WPF-targeted tests.

  **Acceptance Criteria**:
  - [x] Guardrail document exists and lists 1D kerf, welding, 2D aggregation, 2D deadline, stage policy, Core/UI boundary, UI visual boundary, benchmark/test boundary.
  - [x] `dotnet build CuttingStock.slnx -c Release` succeeds.

  **QA Scenarios**:
  ```text
  Scenario: Guardrail document covers known invariants
    Tool: powershell
    Steps: rg -n "kerf|WeldGroupId|AggregateByDims|TimeLimitMs|Stage|WPF|Benchmark" docs
    Expected: Each invariant appears in the new architecture guardrail document.
    Evidence: .omo/evidence/task-1-guardrails.txt

  Scenario: No source behavior changed
    Tool: powershell
    Steps: git diff --stat -- CuttingStock.Core CuttingStock.UI CuttingStock.Tests CuttingStock.UI.Tests
    Expected: Empty or docs-only changes for this task.
    Evidence: .omo/evidence/task-1-no-source-diff.txt
  ```

  **Commit**: YES | Message: `docs(architecture): capture cutting stock guardrails` | Files: `docs/ARCHITECTURE_GUARDRAILS.md`

- [x] 2. Add 1D Solver Contract Test Harness

  **What to do**: Add parameterized tests driven by `SolverCatalog.All`. Tests must instantiate each descriptor, run small deterministic inputs, and assert common contracts: success/failure model, demand coverage, `ComputeLeftover` consistency, stock inventory limits, cost formula, and welded-plan structural invariant where supported.
  **Must NOT do**: Do not duplicate solver-specific behavior tests; this is a catalog-wide contract suite.

  **Parallelization**: Can Parallel: YES | Wave 1 | Blocks: 4,9,18 | Blocked By: 1

  **References**:
  - API: `CuttingStock.Core/Algorithms/SolverCatalog.cs:8` - descriptor list.
  - API: `CuttingStock.Core/Domain/ICuttingSolver.cs:13` - current solver entry contract.
  - Utility: `CuttingStock.Core/Algorithms/Utilities/SolverUtils.cs:93` - canonical leftover formula.
  - Test pattern: `CuttingStock.Tests/Algorithms/InvariantTests1D.cs` - existing invariant style.

  **Acceptance Criteria**:
  - [x] New tests enumerate `SolverCatalog.All`, not hard-coded solver arrays.
  - [x] Tests cover unsupported welding behavior through `SolverDescriptor.GetUnsupportedReason`.
  - [x] `dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Category=Architecture"` succeeds.

  **QA Scenarios**:
  ```text
  Scenario: Every catalog solver satisfies common 1D contracts
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Category=Architecture"
    Expected: All new 1D architecture contract tests pass.
    Evidence: .omo/evidence/task-2-1d-contracts.txt

  Scenario: Contract tests fail if a solver skips leftover recomputation
    Tool: powershell
    Steps: Temporarily alter one test fixture result in test-only code or use a crafted invalid SolverResult against the validator.
    Expected: Test reports a clear failure mentioning leftover mismatch.
    Evidence: .omo/evidence/task-2-1d-leftover-negative.txt
  ```

  **Commit**: YES | Message: `test(core): add 1d solver contract harness` | Files: `CuttingStock.Tests/**`

- [ ] 3. Add 2D Solver Entry And Policy Contract Tests

  **What to do**: Add parameterized tests driven by `SolverCatalog2D.All`. Assert duplicate same-dimension sheets are aggregated by every solver, time-limit metadata matches behavior for CG/MIP, stage policy matches descriptors, every successful output validates through `SolverUtils2D.ValidateSuccessfulResult`, and only `TwoStageShelfGuillotineSolver` advertises enforced stage semantics.
  **Must NOT do**: Do not enforce 3-stage behavior; current policy is advisory for non-two-stage solvers.

  **Parallelization**: Can Parallel: YES | Wave 1 | Blocks: 5,6,7,8,18 | Blocked By: 1

  **References**:
  - API: `CuttingStock.Core/TwoD/Algorithms/SolverCatalog2D.cs:9` - 2D descriptor list.
  - Utility: `CuttingStock.Core/TwoD/Algorithms/Utilities/SolverUtils2D.cs:63` - aggregation helper.
  - Utility: `CuttingStock.Core/TwoD/Algorithms/Utilities/SolverUtils2D.cs:76` - result validator.
  - Policy: `CuttingStock.Core/TwoD/Domain/SolverOptions2D.cs:39` - advisory stage note.

  **Acceptance Criteria**:
  - [ ] New tests are catalog-driven.
  - [ ] Duplicate-dim inventory regression is covered for every 2D solver descriptor.
  - [ ] Stage descriptor tests distinguish `AdvisoryStage` from `EnforcedStage`.
  - [ ] `dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Category=Architecture"` succeeds.

  **QA Scenarios**:
  ```text
  Scenario: Every 2D solver preserves duplicate sheet inventory
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "DuplicateSheetDims|Architecture"
    Expected: Each solver covers demand using the full aggregated inventory.
    Evidence: .omo/evidence/task-3-2d-aggregation.txt

  Scenario: Stage policy cannot drift silently
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Stage"
    Expected: Only Two-Stage Shelf advertises `EnforcedStage`; CG/MIP remain advisory.
    Evidence: .omo/evidence/task-3-stage-policy.txt
  ```

  **Commit**: YES | Message: `test(core): add 2d solver entry policy contracts` | Files: `CuttingStock.Tests/**`

- [ ] 4. Introduce 1D Result Finalizer And Validator Facade

  **What to do**: Extract 1D result finalization and validation into explicit Core services while preserving `SolverUtils` compatibility. The facade must own reusable leftovers, waste, weld count, total cost, leftover recomputation, demand coverage checks, and welded-plan structural validation. Route solvers through the facade after existing post-processing.
  **Must NOT do**: Do not change `ICuttingSolver` signature or make `SolverResult` immutable in this pass.

  **Parallelization**: Can Parallel: NO | Wave 2 | Blocks: 9,18 | Blocked By: 2

  **References**:
  - Current finalizer: `CuttingStock.Core/Algorithms/Utilities/SolverUtils.cs:100`
  - Current validator: `CuttingStock.Core/Algorithms/Utilities/SolverUtils.cs:128`
  - Post-process: `CuttingStock.Core/Algorithms/Utilities/SolverUtils.cs:212`
  - Models: `CuttingStock.Core/Domain/SolverModels.cs:64`

  **Acceptance Criteria**:
  - [ ] Public solver behavior and tests remain unchanged.
  - [ ] Existing `SolverUtils` methods either delegate to the new service or remain as compatibility wrappers.
  - [ ] Every 1D solver calls the same finalization/validation path.
  - [ ] `dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Category=Architecture|Invariant|Welding"` succeeds.

  **QA Scenarios**:
  ```text
  Scenario: Shared 1D finalizer computes metrics for all solvers
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Category=Architecture"
    Expected: Every catalog solver passes cost, waste, leftover, and demand assertions.
    Evidence: .omo/evidence/task-4-1d-finalizer.txt

  Scenario: Welded plans are never post-processed as normal plans
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "Welding|Invariant"
    Expected: Weld group structure and count expectations remain unchanged.
    Evidence: .omo/evidence/task-4-welding-invariants.txt
  ```

  **Commit**: YES | Message: `refactor(core): centralize 1d result finalization` | Files: `CuttingStock.Core/**`, `CuttingStock.Tests/**`

- [ ] 5. Introduce 2D Solver Pre/Post Pipeline

  **What to do**: Add focused Core services for 2D solver entry and exit: input preprocessing, result finalization, result validation, and placement math. Start by routing existing `SolverUtils2D` methods through these services to keep compatibility, then route all 2D solvers through the same pre/post path.
  **Must NOT do**: Do not remove `SolverUtils2D` immediately; keep it as a stable facade until callers are migrated.

  **Parallelization**: Can Parallel: NO | Wave 2 | Blocks: 6,7,8,18 | Blocked By: 3

  **References**:
  - Current utility: `CuttingStock.Core/TwoD/Algorithms/Utilities/SolverUtils2D.cs:10`
  - Aggregation: `CuttingStock.Core/TwoD/Algorithms/Utilities/SolverUtils2D.cs:63`
  - Validation: `CuttingStock.Core/TwoD/Algorithms/Utilities/SolverUtils2D.cs:76`
  - Solver entries: `ShelfGuillotineSolver.cs`, `ColumnGeneration2DSolver.cs`, `StagedMipGuillotineSolver.cs`

  **Acceptance Criteria**:
  - [ ] New service names make responsibilities explicit, for example `TwoDInputPreprocessor`, `TwoDResultValidator`, `TwoDResultFinalizer`, `TwoDPlacementMath`.
  - [ ] Every 2D solver entry uses the same preprocessing path before dictionary/pattern work.
  - [ ] Every successful 2D result uses the same validation/finalization path.
  - [ ] Existing tests plus Task 3 architecture tests pass.

  **QA Scenarios**:
  ```text
  Scenario: Duplicate-dim aggregation cannot be bypassed
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "DuplicateSheetDims|Architecture"
    Expected: Every 2D solver uses full inventory.
    Evidence: .omo/evidence/task-5-2d-preprocessor.txt

  Scenario: Invalid pattern validation is still caught
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "SolverUtils2D|GuillotineValidator"
    Expected: Existing invalid overlap/out-of-bounds/rotation tests pass.
    Evidence: .omo/evidence/task-5-2d-validator.txt
  ```

  **Commit**: YES | Message: `refactor(core): add 2d solver prepost pipeline` | Files: `CuttingStock.Core/TwoD/**`, `CuttingStock.Tests/TwoD/**`

- [ ] 6. Centralize 2D Deadline Policy

  **What to do**: Add a small helper that models absolute wall-clock deadlines from solver start. Use it in `ColumnGeneration2DSolver` and `StagedMipGuillotineSolver` while preserving their existing warm-start accounting and pricing/master split behavior.
  **Must NOT do**: Do not reset the deadline after warm-start; do not make `TimeLimitMs` mean remaining time.

  **Parallelization**: Can Parallel: YES | Wave 2 | Blocks: 18 | Blocked By: 3,5

  **References**:
  - Current CG deadline: `CuttingStock.Core/TwoD/Algorithms/ColumnGeneration2DSolver.cs:72`
  - Current MIP deadline: `CuttingStock.Core/TwoD/Algorithms/StagedMipGuillotineSolver.cs:73`
  - Policy doc: `AGENTS.md` TimeLimitMs section.

  **Acceptance Criteria**:
  - [ ] Helper exposes elapsed, remaining, and stop checks without hiding `Stopwatch` semantics.
  - [ ] CG and MIP continue to count warm-start/bootstrap time toward the same total budget.
  - [ ] Deadline tests use a deterministic/fake clock where practical; otherwise use small bounded cases.

  **QA Scenarios**:
  ```text
  Scenario: Warm-start time counts toward total deadline
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "TimeLimit|Deadline|Architecture"
    Expected: Tests assert no solver treats warm-start as free time.
    Evidence: .omo/evidence/task-6-deadline.txt

  Scenario: Existing performance ceilings remain acceptable
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "PerformanceTests2D"
    Expected: 2D performance tests pass or are explicitly excluded if already category-gated.
    Evidence: .omo/evidence/task-6-performance.txt
  ```

  **Commit**: YES | Message: `refactor(core): centralize 2d deadline policy` | Files: `CuttingStock.Core/TwoD/**`, `CuttingStock.Tests/TwoD/**`

- [ ] 7. Normalize 2D Pattern Contract Without Breaking Placements

  **What to do**: Document and test that `CuttingPattern2D.Placements` are the canonical public output for now. Keep `Root` optional and derived. Add compatibility tests proving `PatternBuilder` can reconstruct supported guillotine trees from flat placements and `GuillotineValidator` remains the acceptance gate.
  **Must NOT do**: Do not make `Root` mandatory in this pass; that would be a breaking contract change for current solvers.

  **Parallelization**: Can Parallel: YES | Wave 2 | Blocks: 8,10,18 | Blocked By: 3,5

  **References**:
  - Model: `CuttingStock.Core/TwoD/Domain/CuttingPattern2D.cs:10`
  - Optional root: `CuttingStock.Core/TwoD/Domain/CuttingPattern2D.cs:18`
  - Builder: `CuttingStock.Core/TwoD/Algorithms/Utilities/PatternBuilder.cs:11`
  - Validator: `CuttingStock.Core/TwoD/Algorithms/Utilities/GuillotineValidator.cs:9`

  **Acceptance Criteria**:
  - [ ] Pattern contract is documented in Core XML docs and architecture guardrails.
  - [ ] Tests cover flat-placement validation, optional root compatibility, and reconstruction for representative patterns.
  - [ ] UI/export still consume placements successfully.

  **QA Scenarios**:
  ```text
  Scenario: Flat placements remain canonical
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "PatternBuilder|GuillotineValidator|Solver2D"
    Expected: Existing pattern/solver tests pass with `Root` optional.
    Evidence: .omo/evidence/task-7-pattern-contract.txt

  Scenario: Invalid guillotine layout fails validation
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "GuillotineValidatorEdge"
    Expected: Invalid non-guillotine cases fail as before.
    Evidence: .omo/evidence/task-7-pattern-negative.txt
  ```

  **Commit**: YES | Message: `docs(core): define 2d pattern contract` | Files: `CuttingStock.Core/TwoD/**`, `CuttingStock.Tests/TwoD/**`, `docs/**`

- [ ] 8. Split SolverUtils2D Into Focused Services

  **What to do**: After Task 5 stabilizes compatibility, move implementation out of `SolverUtils2D` into focused classes. Leave `SolverUtils2D` as a thin facade for existing call sites, then migrate internal solvers/tests to the focused names where clearer.
  **Must NOT do**: Do not create abstractions that introduce state or dependency injection into Core algorithms unless needed; prefer static/pure services consistent with current Core style.

  **Parallelization**: Can Parallel: NO | Wave 2 | Blocks: 10,18 | Blocked By: 5,7

  **References**:
  - Current broad utility: `CuttingStock.Core/TwoD/Algorithms/Utilities/SolverUtils2D.cs:10`
  - Tests: `CuttingStock.Tests/TwoD/SolverUtils2DTests.cs`

  **Acceptance Criteria**:
  - [ ] Each new class has a single responsibility.
  - [ ] `SolverUtils2D` remains source-compatible.
  - [ ] Existing tests are either kept or moved to the new class names with equivalent coverage.

  **QA Scenarios**:
  ```text
  Scenario: Compatibility facade still works
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "SolverUtils2D"
    Expected: Existing SolverUtils2D tests pass.
    Evidence: .omo/evidence/task-8-solverutils2d-facade.txt

  Scenario: Focused services own behavior
    Tool: powershell
    Steps: rg -n "class TwoD(InputPreprocessor|ResultValidator|ResultFinalizer|PlacementMath)" CuttingStock.Core
    Expected: Focused classes exist and `SolverUtils2D` delegates to them.
    Evidence: .omo/evidence/task-8-focused-services.txt
  ```

  **Commit**: YES | Message: `refactor(core): split 2d solver utilities` | Files: `CuttingStock.Core/TwoD/**`, `CuttingStock.Tests/TwoD/**`

- [ ] 9. Replace 1D CG Thin Subclasses With Profiles Internally

  **What to do**: Introduce a `ColumnGenerationProfile` or equivalent immutable configuration object that names the variant behavior: display name, dual stabilization, smoothing factor if exposed, max columns per iteration, integer-master use, and integer-master time limit. Keep current subclasses as compatibility wrappers that pass predefined profiles. Update `SolverCatalog` to reference profile-backed constructors.
  **Must NOT do**: Do not remove public solver classes in this pass.

  **Parallelization**: Can Parallel: NO | Wave 2 | Blocks: 18 | Blocked By: 2,4

  **References**:
  - Base solver knobs: `CuttingStock.Core/Algorithms/ColumnGenerationSolver.cs:23`
  - Thin subclasses: `MultiColumnGenerationSolver.cs`, `StabilizedColumnGenerationSolver.cs`, `IntegerMasterColumnGenerationSolver.cs`
  - Catalog: `CuttingStock.Core/Algorithms/SolverCatalog.cs:25`

  **Acceptance Criteria**:
  - [ ] Constructor boolean/long knobs are replaced or wrapped by a named profile internally.
  - [ ] Existing solver names and catalog ordering remain unchanged.
  - [ ] Column generation tests pass with no quality regression.

  **QA Scenarios**:
  ```text
  Scenario: Catalog behavior is unchanged
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "SolverCatalog|ColumnGeneration"
    Expected: Catalog order, names, capability tests, and CG tests pass.
    Evidence: .omo/evidence/task-9-cg-profile.txt

  Scenario: Profiles make variant behavior discoverable
    Tool: powershell
    Steps: rg -n "ColumnGenerationProfile|UseDualStabilization|UseIntegerMaster|IntegerMasterTimeLimit" CuttingStock.Core/Algorithms
    Expected: Variant behavior is expressed through named profile properties.
    Evidence: .omo/evidence/task-9-profile-discovery.txt
  ```

  **Commit**: YES | Message: `refactor(core): model column generation variants as profiles` | Files: `CuttingStock.Core/Algorithms/**`, `CuttingStock.Tests/**`

- [ ] 10. Split 2D PatternPool Into Column, Master, And Pricing Services

  **What to do**: Refactor `PatternPool` into explicit pieces: pure pattern-column model/signature, LP master solve, DP pricing, and materialization helpers. Keep current behavior and thresholds unchanged. Route CG2D and StagedMip through the new names.
  **Must NOT do**: Do not tune reduced-cost thresholds, objective policy, or pattern generation quality in this task.

  **Parallelization**: Can Parallel: NO | Wave 2 | Blocks: 18 | Blocked By: 7,8

  **References**:
  - Current utility: `CuttingStock.Core/TwoD/Algorithms/Utilities/PatternPool.cs:14`
  - Consumers: `ColumnGeneration2DSolver.cs`, `StagedMipGuillotineSolver.cs`

  **Acceptance Criteria**:
  - [ ] Pattern column, master solve, and pricing responsibilities are separated.
  - [ ] CG2D and StagedMip outputs remain valid on existing tests.
  - [ ] New tests cover signature dedup and pricing/master behavior at service level.

  **QA Scenarios**:
  ```text
  Scenario: 2D CG and MIP behavior survives service split
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "ColumnGeneration2D|StagedMip|PatternPool|Solver2D"
    Expected: Existing solver and pattern-pool tests pass.
    Evidence: .omo/evidence/task-10-patternpool-split.txt

  Scenario: Dedup policy remains unchanged
    Tool: powershell
    Steps: dotnet test CuttingStock.Tests/CuttingStock.Tests.csproj -c Release --filter "PatternPool"
    Expected: Duplicate pattern signatures are rejected exactly as before.
    Evidence: .omo/evidence/task-10-dedup-policy.txt
  ```

  **Commit**: YES | Message: `refactor(core): split 2d pattern pool services` | Files: `CuttingStock.Core/TwoD/**`, `CuttingStock.Tests/TwoD/**`

- [ ] 11. Extract UI Shell Services For Non-Visual Workflow

  **What to do**: Move non-visual shell workflow out of `MainWindow.xaml.cs` and `TwoDTab.xaml.cs` into testable services. Start with MRU/recent scenarios, file import parsing, clipboard import parsing, and text search. Keep actual `MessageBox`, `Clipboard`, `OpenFileDialog`, and visual tree interaction in the view or `DialogService`.
  **Must NOT do**: Do not move LiveCharts series or Canvas element creation into ViewModel/Core.

  **Parallelization**: Can Parallel: YES | Wave 3 | Blocks: 12,13,14,18 | Blocked By: 1

  **References**:
  - 1D shell logic: `CuttingStock.UI/MainWindow.xaml.cs:101`, `:184`, `:255`, `:341`, `:423`
  - 2D shell logic: `CuttingStock.UI/TwoD/TwoDTab.xaml.cs:57`, `:65`
  - Dialog seam: `CuttingStock.UI/Services/IDialogService.cs:8`

  **Acceptance Criteria**:
  - [ ] Parsing/search/history logic is unit-testable without constructing WPF windows.
  - [ ] Views still own WPF objects and call services with plain data.
  - [ ] UI tests cover MRU update, CSV/XLSX parse path where feasible, clipboard text parse, and search indexing.

  **QA Scenarios**:
  ```text
  Scenario: Shell services parse clipboard rows without WPF
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release --filter "Clipboard|Import|Shell"
    Expected: Service tests pass for valid and invalid pasted data.
    Evidence: .omo/evidence/task-11-shell-services.txt

  Scenario: View code-behind shrinks without behavior loss
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release
    Expected: Existing VM/lifecycle tests and new shell service tests pass.
    Evidence: .omo/evidence/task-11-ui-tests.txt
  ```

  **Commit**: YES | Message: `refactor(ui): extract shell workflow services` | Files: `CuttingStock.UI/**`, `CuttingStock.UI.Tests/**`

- [ ] 12. Replace View Events With Bindable Projection State

  **What to do**: Remove or reduce `SingleResultReady`/`CompareResultReady` event coupling by exposing projection state from `TwoDViewModel` and, where useful, `MainViewModel`. Create DTOs for chart data and 2D pattern render inputs. The view may still translate DTOs into LiveCharts series and Canvas shapes.
  **Must NOT do**: Do not bind WPF `Shape`, `Brush`, `ISeries`, or `Axis` objects from Core. Avoid pushing WPF visual types into Core.

  **Parallelization**: Can Parallel: NO | Wave 3 | Blocks: 13,14,18 | Blocked By: 11

  **References**:
  - Event coupling: `CuttingStock.UI/TwoD/TwoDTab.xaml.cs:48`
  - 2D render method: `CuttingStock.UI/TwoD/TwoDTab.xaml.cs:230`
  - 1D projection pattern: `CuttingStock.UI/Services/VisualizationService.cs:16`

  **Acceptance Criteria**:
  - [ ] `TwoDViewModel` exposes plain projection data for last result render/compare chart triggers.
  - [ ] View code no longer needs dispatcher event subscriptions for normal result rendering.
  - [ ] UI tests assert projection state changes after calculate/compare.

  **QA Scenarios**:
  ```text
  Scenario: 2D calculate produces bindable projection
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release --filter "TwoDViewModel"
    Expected: Tests assert render projection is populated after successful solve.
    Evidence: .omo/evidence/task-12-2d-projection.txt

  Scenario: Compare chart data is independent of WPF chart objects
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release --filter "Chart|Projection"
    Expected: Projection tests pass without creating LiveCharts controls.
    Evidence: .omo/evidence/task-12-chart-projection.txt
  ```

  **Commit**: YES | Message: `refactor(ui): expose bindable solver projections` | Files: `CuttingStock.UI/**`, `CuttingStock.UI.Tests/**`

- [ ] 13. Consolidate 1D And 2D Workspace Workflow Duplication

  **What to do**: Extract shared ViewModel workflow helpers for calculate/compare/export/scenario patterns where the type parameters make behavior clearer. Prefer composition or a narrow template method over a large inheritance hierarchy. Keep dimension-specific parsing, DTO mapping, and rendering projection explicit.
  **Must NOT do**: Do not collapse 1D and 2D ViewModels into one generic class if readability suffers.

  **Parallelization**: Can Parallel: NO | Wave 3 | Blocks: 18 | Blocked By: 11,12

  **References**:
  - Shared base today: `CuttingStock.UI/ViewModels/SolverWorkspaceViewModel.cs:98`
  - 1D solve/compare: `MainViewModel.Solving.cs`
  - 2D solve/compare: `TwoDViewModel.Solving.cs`
  - Export duplication: `MainViewModel.Export.cs`, `TwoDViewModel.Export.cs`

  **Acceptance Criteria**:
  - [ ] Compare/rank/report flow has one tested shared implementation or helper where practical.
  - [ ] Export command boilerplate is reduced through a helper without hiding dimension-specific export calls.
  - [ ] Scenario save/load mapping remains explicit and tested.

  **QA Scenarios**:
  ```text
  Scenario: 1D and 2D compare commands still rank successful rows
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release --filter "Compare"
    Expected: Existing compare tests and new shared-helper tests pass.
    Evidence: .omo/evidence/task-13-compare-shared.txt

  Scenario: Failed/unsupported descriptors still produce skipped rows
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release --filter "SolverWorkspaceViewModel"
    Expected: Unsupported descriptor behavior is unchanged.
    Evidence: .omo/evidence/task-13-unsupported-descriptor.txt
  ```

  **Commit**: YES | Message: `refactor(ui): consolidate solver workspace workflows` | Files: `CuttingStock.UI/**`, `CuttingStock.UI.Tests/**`

- [ ] 14. Define And Enforce 1D/2D UI Parity Matrix

  **What to do**: Create a parity matrix for user-facing tab features: example load, add/delete, scenario save/load, recent scenarios, drag/drop, search, calculate, compare, cancel, export, visualization, and progress. For each mismatch, choose one of two outcomes: implement parity with tests or document intentional difference in architecture guardrails/tooltips/docs.
  **Must NOT do**: Do not silently add large UI features without matching tests.

  **Parallelization**: Can Parallel: YES | Wave 3 | Blocks: 18 | Blocked By: 11,12

  **References**:
  - 1D MRU/search/drag-drop: `MainWindow.xaml.cs:101`, `:184`, `:423`
  - 2D shell: `TwoDTab.xaml.cs`
  - Feature parity rule: `AGENTS.md` "When you add a feature".

  **Acceptance Criteria**:
  - [ ] Parity matrix exists in docs or architecture guardrails.
  - [ ] Every intentional mismatch is documented with rationale.
  - [ ] Every implemented parity improvement has UI tests or shell service tests.

  **QA Scenarios**:
  ```text
  Scenario: Parity matrix covers both tabs
    Tool: powershell
    Steps: rg -n "recent|drag|drop|search|scenario|export|compare|cancel|visual" docs
    Expected: Matrix entries cover listed user-facing features.
    Evidence: .omo/evidence/task-14-parity-doc.txt

  Scenario: Implemented parity behaviors are tested
    Tool: powershell
    Steps: dotnet test CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj -c Release --filter "Parity|Shell|Scenario|Search"
    Expected: Tests pass for implemented parity decisions.
    Evidence: .omo/evidence/task-14-parity-tests.txt
  ```

  **Commit**: YES | Message: `docs(ui): define tab parity matrix` | Files: `docs/**`, `CuttingStock.UI/**`, `CuttingStock.UI.Tests/**`

- [ ] 15. Add Solution-Level Build And Package Governance

  **What to do**: Add `Directory.Build.props` for shared project settings and, if adopted, `Directory.Packages.props` for centralized package versions. Preserve project-specific settings such as `UseWPF`, output type, package metadata, and test project markers.
  **Must NOT do**: Do not accidentally change target frameworks or package versions while centralizing.

  **Parallelization**: Can Parallel: YES | Wave 4 | Blocks: 17,18 | Blocked By: 1

  **References**:
  - Project settings duplicated across `*.csproj`.
  - UI project target: `CuttingStock.UI/CuttingStock.csproj`
  - Test target split: `CuttingStock.Tests/CuttingStock.Tests.csproj`, `CuttingStock.UI.Tests/CuttingStock.UI.Tests.csproj`

  **Acceptance Criteria**:
  - [ ] Shared properties are centralized without changing effective TFM/WPF/test metadata.
  - [ ] Build and full tests pass.
  - [ ] Package centralization does not duplicate version declarations.

  **QA Scenarios**:
  ```text
  Scenario: Central props preserve build
    Tool: powershell
    Steps: dotnet build CuttingStock.slnx -c Release
    Expected: Build succeeds.
    Evidence: .omo/evidence/task-15-build-props.txt

  Scenario: Package versions are centralized or intentionally local
    Tool: powershell
    Steps: rg -n "PackageReference|PackageVersion" Directory.Packages.props *.csproj CuttingStock.*/*.csproj
    Expected: Version placement matches the chosen governance style with no duplicates.
    Evidence: .omo/evidence/task-15-package-governance.txt
  ```

  **Commit**: YES | Message: `build: centralize solution project settings` | Files: `Directory.Build.props`, `Directory.Packages.props`, `*.csproj`

- [ ] 16. Separate Benchmarks From Correctness Tests

  **What to do**: Move benchmark-only code out of `CuttingStock.Tests` or explicitly reclassify it as a deterministic performance gate. Preferred target: move `LargeScaleBenchmarks.cs` into `CuttingStock.Benchmarks` and remove `BenchmarkDotNet` from `CuttingStock.Tests.csproj`. Keep correctness tests deterministic and fast by default.
  **Must NOT do**: Do not drop the large-scale coverage without relocating or replacing it.

  **Parallelization**: Can Parallel: YES | Wave 4 | Blocks: 17,18 | Blocked By: 1

  **References**:
  - Current leakage: `CuttingStock.Tests/CuttingStock.Tests.csproj:26`
  - Explicit benchmark: `CuttingStock.Tests/LargeScaleBenchmarks.cs:18`
  - Benchmark project: `CuttingStock.Benchmarks/CuttingStock.Benchmarks.csproj:13`

  **Acceptance Criteria**:
  - [ ] `CuttingStock.Tests` no longer references `BenchmarkDotNet` unless an explicit rationale is documented.
  - [ ] Large-scale performance scenario exists in `CuttingStock.Benchmarks` or a deliberately named perf gate.
  - [ ] Default `dotnet test` remains correctness-focused.

  **QA Scenarios**:
  ```text
  Scenario: Correctness tests do not depend on BenchmarkDotNet
    Tool: powershell
    Steps: rg -n "BenchmarkDotNet" CuttingStock.Tests
    Expected: No match, or a documented exception outside project package refs.
    Evidence: .omo/evidence/task-16-no-benchmarkdotnet-in-tests.txt

  Scenario: Benchmark project still runs benchmark entry point
    Tool: powershell
    Steps: dotnet build CuttingStock.Benchmarks/CuttingStock.Benchmarks.csproj -c Release
    Expected: Benchmark project builds.
    Evidence: .omo/evidence/task-16-benchmark-build.txt
  ```

  **Commit**: YES | Message: `build: separate benchmarks from correctness tests` | Files: `CuttingStock.Tests/**`, `CuttingStock.Benchmarks/**`, `README.md`, `docs/**`

- [ ] 17. Add CI And Single Test Count Source Policy

  **What to do**: Add a CI workflow for build/test on Windows because WPF projects target `net10.0-windows`. Include Release build, full non-explicit tests, and optional benchmark smoke build. Establish one authoritative source for test counts; remove hard-coded stale counts from docs or mark them generated/snapshot with update instructions.
  **Must NOT do**: Do not make CI run long explicit benchmarks by default.

  **Parallelization**: Can Parallel: NO | Wave 4 | Blocks: 18 | Blocked By: 15,16

  **References**:
  - No visible `.github` directory found.
  - Test-count drift: `README.md:112`, `docs/README.md:75`, `CLAUDE.md:40`
  - Commands from `AGENTS.md`.

  **Acceptance Criteria**:
  - [ ] CI workflow exists under `.github/workflows/` or the chosen provider path.
  - [ ] CI uses `dotnet build CuttingStock.slnx -c Release`.
  - [ ] CI uses `dotnet test CuttingStock.slnx -c Release --nologo --no-build`.
  - [ ] Docs no longer contradict each other on current test count.

  **QA Scenarios**:
  ```text
  Scenario: CI workflow contains required commands
    Tool: powershell
    Steps: rg -n "dotnet build CuttingStock.slnx -c Release|dotnet test CuttingStock.slnx -c Release" .github
    Expected: Required build/test commands are present.
    Evidence: .omo/evidence/task-17-ci-commands.txt

  Scenario: Test count docs do not drift
    Tool: powershell
    Steps: rg -n "525\\+|678|current [0-9]+|현재 [0-9]+" README.md docs CLAUDE.md
    Expected: No contradictory hard-coded counts remain, or all counts point to one maintained source.
    Evidence: .omo/evidence/task-17-doc-counts.txt
  ```

  **Commit**: YES | Message: `ci: add build test governance` | Files: `.github/workflows/**`, `README.md`, `docs/**`, `CLAUDE.md`

- [ ] 18. Final Architecture Documentation And Changelog Update

  **What to do**: Update `CHANGELOG.md` with a dated block and produced commit SHAs. Update architecture guardrails and README/docs to reflect final decisions: no API break, placements canonical for 2D, stage policy, UI visual boundary, benchmark separation, CI policy, and how to add a new solver safely.
  **Must NOT do**: Do not introduce per-phase markdown files.

  **Parallelization**: Can Parallel: NO | Wave 5 | Blocks: Final Verification | Blocked By: All prior tasks

  **References**:
  - Changelog rule: `AGENTS.md` Changelog discipline.
  - Root changelog: `CHANGELOG.md`
  - Docs index: `docs/README.md`

  **Acceptance Criteria**:
  - [ ] `CHANGELOG.md` has a new top entry with date and commit SHAs.
  - [ ] Docs describe how to add 1D and 2D solvers without skipping contract tests.
  - [ ] Docs describe UI shell/service boundaries.
  - [ ] Full build and tests pass.

  **QA Scenarios**:
  ```text
  Scenario: Changelog includes produced commits
    Tool: powershell
    Steps: git log --oneline -n 20; Get-Content CHANGELOG.md -TotalCount 80
    Expected: Latest changelog block references the commits produced by this work.
    Evidence: .omo/evidence/task-18-changelog.txt

  Scenario: Full repository verification passes
    Tool: powershell
    Steps: dotnet build CuttingStock.slnx -c Release; dotnet test CuttingStock.slnx -c Release --nologo --no-build
    Expected: Both commands pass.
    Evidence: .omo/evidence/task-18-full-verify.txt
  ```

  **Commit**: YES | Message: `docs: record architecture hardening work` | Files: `CHANGELOG.md`, `README.md`, `docs/**`

## Final Verification Wave
> ALL must APPROVE. Present consolidated results to user and get explicit "okay" before completing.

- [ ] F1. Plan Compliance Audit
  - Verify every task acceptance criterion has evidence in `.omo/evidence/`.
  - Verify no task violated Core/UI boundary or public API compatibility.

- [ ] F2. Code Quality Review
  - Review changed Core abstractions for unnecessary inheritance, stateful services, and hidden behavior changes.
  - Review UI changes for WPF visual types leaking into Core or ViewModels beyond current project patterns.

- [ ] F3. Real Manual QA
  - Launch WPF app with `dotnet run --project CuttingStock.UI`.
  - Exercise 1D and 2D examples, calculate, compare, cancel, export prompt open/cancel, scenario save/load, and any parity changes.
  - Capture screenshots or notes under `.omo/evidence/f3-manual-qa.md`.

- [ ] F4. Scope Fidelity Check
  - Confirm no solver quality/cost behavior changed except through documented refactors with passing regression tests.
  - Confirm no unrelated untracked files were staged.
  - Confirm docs and changelog are updated exactly once.

## Commit Strategy
- Use one commit per task or tightly related task pair.
- Preferred messages are listed per task.
- Do not stage the current untracked `AGENTS.md` unless the user explicitly says it is intended to be committed.
- If `.omo/evidence/` is local-only in this repository, keep evidence untracked unless the user requests otherwise.

## Success Criteria
- Architecture rules are discoverable in docs and enforced by tests.
- Every solver entry/finalization rule has one named owner.
- 1D CG variants are expressed through profiles while compatibility classes remain.
- 2D pattern/stage/deadline semantics are explicit and tested.
- UI shell behavior is thinner, more testable, and parity decisions are documented.
- Build/package/test/benchmark/docs governance prevents the drift observed in the current repository.
