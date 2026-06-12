# Changelog

`docs/archive/` 에 흩어져 있던 PHASE 노트들을 시간순으로 압축한 변경 이력.
원본은 git history 에 보존되어 있다 (`git log -- docs/archive/`).

## 2026-06-01 — Solver 성공 결과 validator 보강

- **1D 성공 결과 공통 검증** — Greedy / ColumnGeneration / ArcFlow가
  `Success=true` 반환 전 재고 사용량, kerf-aware leftover, demand exact coverage,
  unknown cut, 용접 그룹 합계와 `Delta` 제약을 검증하도록 추가.
- **2D 성공 결과 공통 검증** — Shelf / CG2D / StagedMip가 시트 재고, exact demand,
  trim bounds, kerf-aware overlap, 회전 허용 여부, guillotine compliance를 통과한
  결과만 성공으로 반환하도록 추가.
- **validator 회귀 테스트** — 1D over-pack / over-production / welded order 합계,
  2D overlap / illegal rotation / valid kerf pattern 검증 케이스 추가.

1개 commit (`c7de45a`).

## 2026-05-31 — 알고리즘 kerf / 수요 불변식 보강

- **1D ArcFlow kerf 모델 정정** — arc 길이는 `length + kerf` 를 쓰되 stock capacity를
  `stock + kerf` 로 확장해 첫 절단 edge kerf를 과금하지 않도록 수정. `205mm`
  stock에 `100 + 5 + 100` exact-fit 회귀 테스트 추가.
- **Greedy 입력 정규화 + 용접 tail 재분배** — 동일 길이 order/stock row를 solver
  진입부에서 합산하고, 용접 잔여 tail이 `Delta` 미만일 때 직전 조각을 줄여
  `5000 + 4100 + 1000 = 10100mm` 같은 feasible split을 찾도록 보강.
- **2D guillotine DP kerf-aware normal set** — normal set에 `50 + 5 + 50 = 105`
  같은 interior-kerf extent를 포함해 가능한 kerf 패턴을 pricing에서 놓치지 않게 함.
- **2D CG/MIP exact-demand guard** — LP/MIP 패턴이 overproduce할 수 있는 경로를
  최종 materialization에서 demand 기준으로 trim하고 정확히 덮지 못하면 실패 처리.

2개 commit (`ece74b9`, `cee7d96`).

## 2026-05 (late) — 전방위 안정성 sweep + UX 마감

영역별 audit 에이전트(1D / 2D / 2D util / domain / persistence / VM / View)를
순차로 띄워 캐치한 실제 버그 + 결정된 false positive를 정리.

- **CG Simplex NaN propagation 차단** — 작은 pivot이 NaN을 tableau에 남기면
  duals가 NaN이 되고 knapsack pricing이 NaN 값을 받아 `> 1.00001` 비교가 silently
  false 처리되어 CG 조기 종료. NaN/Infinity 감지 후 fallback 정수화로 점프.
- **UserPreferences atomic write race** — `.tmp` 파일명이 모든 호출자 동일이라
  drag-drop + scenario save 동시 호출 시 경쟁. `<pid>.<guid>` suffix로 unique.
- **Scenario schema 엄격 매치 완화** — `cutting-stock-1d/v1` 정확 매치만 통과하던
  것을 `cutting-stock-1d/` 접두사 매치로 변경. 향후 minor 버전 bump가 사용자
  파일을 깨뜨리지 않음.
- **UserPreferences.Load 에러 breadcrumb** — 손상 파일 silent 폴백할 때 `crash.log`
  에 1줄 기록.
- **WasteLength int → long** — TotalCost 와 일관성, overflow 안전.
- **GuillotineNode.Children → IReadOnlyList** — 트리 구성 후 외부 mutation 차단.
- **App-wide unhandled exception 핸들러** — DispatcherUnhandledException +
  AppDomain.UnhandledException + TaskScheduler.UnobservedTaskException. silent
  crash 방지 + `crash.log` 기록.
- **WindowLeft/Top double.NaN → double?** — 첫 실행 max-then-close 시 NaN을
  `System.Text.Json` 이 throw 해 prefs 전체 손실되던 잠복 버그 해결.
- **CancellationTokenSource leak** — 매 run마다 새로 할당하되 이전 것 dispose.
- **2D ViewModel Progress runId guard** — 1D 와 동일한 stale-callback 가드.
- **UR1-4: 2D Esc, StatusText, Recent 메뉴, 검색바 탭 전환** — MVVM 1차 audit 후속.
- **R1-2: BoolToVisibilityConverter Inverse, Calculate/Compare CanExecute** —
  MVVM 라운드 직후 catch한 두 critical UX bug.

5개 commit (`cf6660f`...`4b3c1ee`).

## 2026-05 — MVVM + 강력한 테스트 + 잔여 버그 정리

- **WPF UI MVVM 전환** (`8075cc7`) — CommunityToolkit.Mvvm 도입. `MainViewModel` /
  `TwoDViewModel` + `IDialogService` + `VisualizationService` 로 분리. code-behind 2052 → 607 줄 (-70%).
- **테스트 615개로 확대** (`06df2dc`, `28519ac`) — 신규 suite: `InvariantTests1D`
  (15 seeds × 3 솔버 매트릭스), `PerformanceTests1D/2D` (wall-clock 예산),
  `StressTests1D` (`[Category("Stress")]`), `QualityComparisonTests`,
  `RobustnessTests`.
- **CG 버그 fix 2건** (`06df2dc`)
  - 초기 그리디 컬럼이 kerf 무시 — `5×1000mm` 패턴이 5000mm bar에 들어가 over-pack.
    초기 컬럼 생성을 `weight = len + (cutsSoFar>0 ? kerf : 0)` 으로 수정.
  - `SolveSingleStock`이 order > stock 인 demand에도 identity 패턴을 생성해
    `Success=true` 와 invalid 패턴을 반환. demand를 `length ≤ stockLength`
    로 필터링하도록 수정.
- **시트 인벤토리 합산** (`28519ac`) — 동일 dim 시트 행이 두 개면 `Dictionary<Sheet,_>`
  키 충돌로 인벤토리 절반 손실 / `ArgumentException`. `SolverUtils2D.AggregateByDims`
  추가, 모든 2D 솔버 진입부에서 호출.
- **2D 진행률 + 비교 차트 + JSON 시나리오** (`d10f882`) — 2D 탭이 1D 패리티 도달.
  `ScenarioService` (Core/Persistence) — `.cstock1d/2d.json` round-trip + 스키마 가드.
- **UI/UX 전면 정비** (`a061993`) — Excel import 타입 체크 수정, ExportComparison
  rank 정렬 (실패 행 마지막), placeholder 추가, ToolTip 전면 도입, 키보드 단축키
  (F1/Ctrl+R/Ctrl+Shift+C/Ctrl+S), HSL 팔레트 적용, 차트 라벨 회전.
- **알고리즘 정확성 + 도메인 불변성** (`c85b60e`) — `Order`/`RebarStock` 완전
  immutable, `Cut`/`CuttingPlan` init-only, `TotalCost` int → long, ArcFlow
  flow decomposition safety counter, `PatternBuilder` 단일-rect non-corner
  케이스 처리, Greedy `FindHostPlanForWeld` 도입 (용접 부분 조각을 기존 plan에
  호스트), `FindTopKCutsSparse` DP dedup 정정, 2D `TimeLimitMs` 이중 카운트
  버그 수정.

## 2026-01 — Phase 7: 알고리즘 고도화

- **MFFD** (Modified First-Fit Decreasing) — 아이템을 large/medium/small/tiny로
  분류 후 BFD. Greedy 후처리에서 future-waste 추정에 사용.
- **2-opt 스타일 Local Search** (`OptimizePostProcess`) — 두 plan 간 cut swap +
  relocate. waste 감소 시에만 적용, welding-aware (용접 plan 보호).
- **Pattern Reduction** — Column Generation 에서 RMP 컬럼 풀 dedup + frequency
  tracking. 중복 패턴 제거로 simplex 매트릭스 축소.
- **Multi Stock Length Column Generation** — 여러 stock 길이에 대해 stock-별
  sub-problem 으로 분해 후 통합.
- 학술 자료 참고: Coffman et al. 1980, Berkey & Wang 1987, Cintra et al. 2008.

## 2025-12 — Greedy 한계 문서화

`docs/archive/ALGORITHM_LIMITATIONS.md` 에서 Greedy Knapsack DP 의 알려진 한계
공식화 — Pass1 maxPerOrder=2 캡으로 단일-길이 대량 demand 에서 비최적, bounded
knapsack 자체의 인스턴스 제약. 사용자 가이드: 용접 활성화 / 적절 재고 크기 /
ColumnGeneration 또는 ArcFlow 선택.

## 2025-11 — Phase 2~6: 토대 구축

- **Phase 2** (`2025-11-02`) — .NET 8 업그레이드, `FindBestCuts` / `OptimizeCutting`
  / `ProcessRemainingOrders` 함수 리팩토링, 테스트 프로젝트 추가.
- **Phase 3** (`2025-11-02`) — 알고리즘 이름 정리 (Current/Origin/FFD → Greedy/CG/FFD),
  `IOptimizer` 공통 인터페이스 도입, FFD 의 명확한 버그 수정.
- **Phase 4** (`2025-11-03`) — Best-Fit Decreasing 추가, BenchmarkDotNet
  프로젝트 신설, `AlgorithmComparisonTests` 확장.
- **Phase 6** (`2025-11-03`) — LiveCharts2 시각화, ClosedXML CSV/Excel
  내보내기, 알고리즘별 advanced options UI, MainWindow 800×600 → 1000×750.
- **INSPECTION_REPORT** (`2025-11-03`) — 자가 점검: Greedy 자투리 중복 처리
  Critical 버그 적발 및 수정, BFD 미사용 변수 제거.

## 2025-10 이전 — 초기 분석

- `ALGORITHM_ANALYSIS.md` — 세 알고리즘(Current DP / Origin Brute Force / FFD)
  분석. 치명적 문제 3건 식별: 비용 함수 불일치, 실행 불가능한 속도, 용접 미구현.
- `IMPROVEMENT_ROADMAP.md` — 6 phase 작업 계획. 우선순위 매트릭스. (Phase 2~4
  완료, Phase 5는 흡수, Phase 6 완료. Phase 7+ 는 별도 진행.)
- `TEST_CASES.md` — 15개 시드 케이스 (TC-001~TC-015). 이후 실제 테스트는
  `CuttingStock.Tests` 의 NUnit 매트릭스로 흡수됨.
- `UI_IMPROVEMENTS.md` — v1.0 → v2.0 UI 개선 (단일 화면 → 알고리즘 선택 +
  파라미터 + 비교 모드).

---

이력 작성 규칙: 새 변경은 위쪽에 추가, 커밋 SHA 와 함께. 한 라운드 = 한 묶음.
원본 PHASE 노트(2025-11~2026-01)는 git history에서 `git show 7259d84^:docs/archive/PHASE3_CHANGES.md`
같은 형식으로 복원 가능.
