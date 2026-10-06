# Changelog

`docs/archive/` 에 흩어져 있던 PHASE 노트와 구현 이력을 주제별로 압축한 변경 이력.
`main` 은 아래 주요 단계와 일치하는 curated milestone commit으로 구성한다.

## 2026-10-05 — 전수 재분석 후 solver 계약/품질 수정

재분석(실측 probe 기반)에서 발견한 5건을 순서대로 수정했다. 항목마다 회귀 테스트를 추가했고, 수정 전 코드에서의
실패는 1·2·3번에서 확인했다.

- **Integer-master CG / Global-stock CG 품질** — 정수 master가 등호 수요 행 + waste 목적함수로
  모델링돼, 생성된 column 풀에 정확 cover가 없으면 단일 cut identity column으로 채워
  LP 반올림 해보다 약 2배 bar를 썼다(12m 평균 12.2 vs 7.2 bars). ≥ 수요 행 + 재료(stock
  길이) 목적, 초과 생산 trim, "LP 반올림 해보다 나쁘면 채택하지 않는" 품질 가드로 교체
  (평균 6.6 bars, 다중 재고 효율 88.5% → 93.9%). `IntegerMasterQualityTests` 92건
  (수정 전 59건 실패) (`ccdd680`).
- **`StockUsageOrder` capability 정직화** — ArcFlow, Global-stock CG, CG2D, Staged MIP는
  옵션을 읽지 않으면서 지원한다고 선언했다. 선언을 제거하고 UI의 "재고 사용 순서" 컨트롤을
  solver 선택에 따라 비활성화(`CanConfigureUsageOrder`). `StockUsageOrderContractTests`가
  선언과 실제 동작을 양방향 검증한다 (`a699f80`).
- **2D `TimeLimitMs` 준수** — `GuillotineKnapsackDp`에 재귀 중 협력적 취소(`TrySolve`)를
  추가하고 pricing/diversification에서 deadline을 전달. 대형 시트 + 고유 치수 40종에서
  3s 한도가 CG2D 7.5s, Staged MIP 31s로 늘어지던 것이 한도 내로 복귀 (`002f7aa`).
- **ArcFlow 실패/주장** — SCIP가 해를 못 찾으면(`NOT_SOLVED`) 예외로 실패하던 것을
  CG/Greedy 중 재료가 적은 결과로 대체하고, 한도에 걸린 해는 `(time limit, best found)`로
  표시하며 휴리스틱보다 나쁘면 휴리스틱 결과로 대체. kerf=3 probe에서 SCIP 해가 Greedy보다
  최대 1.8배 bar를 썼고(11 vs 6), 다중 재고에서는 30초 후 실패하던 것이 성공으로 바뀐다.
  `SolverResult.StockMaterial` 추가, MIP 시간 제한은 테스트용 internal 생성자로 주입 (`a699f80`).
- **1D 비교 순위 tie-break** — 비용만으로 순위를 매겨 동률(여유 자투리 ≥ Gamma는 무료)이
  catalog 순서로 정해졌다. `(TotalCost, 높은 재료 효율)`로 정렬하고, 비교 행에 solver가 낸
  결과 이름(fallback/time limit 표시)을 유지한다 (`dca5667`).

알려진 후속 과제: ArcFlow는 kerf > 0에서 unit-step 손실 arc 때문에 노드가 capacity/GCD
개수까지 늘어 사실상 항상 30초 한도에 도달한다(손실 arc를 종단으로 모으는 축소 그래프 필요).
다중 재고 ArcFlow의 목적함수는 bar 수(Σz)이며 재료 길이 합이 아니다.

## 2026-07-24 — Architecture and pattern hardening

- **아키텍처 계약과 검증 하네스** — Core/UI/benchmark 경계, 1D/2D solver
  생명주기, 2D placement/stage 계약을 문서화하고 카탈로그 기반 공통 계약
  테스트를 추가 (`5b926bf`).
- **공통 생명주기와 정책 소유권** — 1D 결과 finalization, 2D 전처리/후처리,
  절대 deadline, 2D utility facade를 단일 경로로 정리하고 검토에서 발견된
  invariant 및 one-way dependency 위반을 수정 (`5b926bf`).
- **변형과 패턴 서비스 구성** — Column Generation 변형을 profile로 모델링하고
  2D pattern pool 생성/정규화/중복 제거 책임을 분리 (`5b926bf`).
- **UI shell과 projection 경계** — 창 shell workflow, plain projection DTO,
  비교/내보내기 workflow를 서비스로 추출하고 1D/2D 사용자 기능 parity 정책을
  고정했다. 활성 작업공간 기준 단축키 라우팅을 회귀 테스트로 보호하고 상태
  표시도 선택된 탭을 따르도록 수정했다 (`bef5f62`, `edfaae8`).
- **빌드와 검증 거버넌스** — 공통 MSBuild/package 설정, 정합성 테스트와
  BenchmarkDotNet 분리, Windows .NET 10 CI 및 테스트 수 단일 출처 정책을 추가
  (`5efddbf`).
- **최종 계약 하드닝** — 취소된 worker가 종료되기 전 재진입을 차단하고,
  비교 차트 stale state를 제거했다. 2D 절대 deadline을 확장·정렬·선반 탐색까지
  전달하고 OR-Tools 수명을 명시했으며, 1D 재사용 잔재의 출처·재고·효율 계약과
  benchmark 품질 비교 출력을 회귀 테스트로 고정했다 (`edfaae8`).

이 작업은 기존 공개 solver 인터페이스와 호환 클래스 이름을 유지하는
source-compatible 리팩터링이다.

## 2026-06-30 — Solver lifecycle architecture 정리

- **Solver descriptor 공통 계약** — 1D/2D solver catalog가 같은 descriptor
  인터페이스로 지원 기능과 미지원 사유를 노출하도록 정리.
- **실행 생명주기 표준화** — solver run 시작, 취소, progress, stale callback 차단,
  CTS dispose를 `SolverRunLifecycle`과 공통 Workspace ViewModel로 통합.
- **UI 관리 구조 분리** — 1D/2D ViewModel과 ExportService를 partial 파일로 역할별
  분리하고, 창/탭 종료 시 ViewModel dispose 경로를 명시.
- **회귀 테스트** — descriptor 계약, run lifecycle, workspace 공통 실행 흐름,
  scenario 저장/로드 fixture를 보강.

Lifecycle architecture milestone (`2fb7d72`).

## 2026-06-13 — Solver capability catalog + 선택형 CG 강화 variants

- **Solver capability catalog** — 1D/2D solver별 실제 지원 옵션을 Core catalog로
  명시하고, UI가 catalog를 기준으로 알고리즘 목록과 옵션 활성화 상태를 결정하도록 변경.
- **1D Column Generation variants** — stabilized dual pricing, multi-column pricing,
  generated-column integer master, global variable-stock master solver를 별도 선택형
  알고리즘으로 추가.
- **2D enforced 2-stage solver** — shelf heuristic을 명시적 2-stage guillotine solver로
  노출하고 결과 패턴의 shelf-stage 형태를 검증.
- **회귀 테스트** — solver catalog 계약, CG variant smoke/quality, global stock 선택,
  2D solver matrix에 TwoStage solver를 추가.

Solver contract milestone (`bbb9413`).

## 2026-06-01 — Solver 성공 결과 validator 보강

- **1D 성공 결과 공통 검증** — Greedy / ColumnGeneration / ArcFlow가
  `Success=true` 반환 전 재고 사용량, kerf-aware leftover, demand exact coverage,
  unknown cut, 용접 그룹 합계와 `Delta` 제약을 검증하도록 추가.
- **2D 성공 결과 공통 검증** — Shelf / CG2D / StagedMip가 시트 재고, exact demand,
  trim bounds, kerf-aware overlap, 회전 허용 여부, guillotine compliance를 통과한
  결과만 성공으로 반환하도록 추가.
- **validator 회귀 테스트** — 1D over-pack / over-production / welded order 합계,
  2D overlap / illegal rotation / valid kerf pattern 검증 케이스 추가.

Solver contract milestone (`bbb9413`).

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

Solver contract milestone (`bbb9413`).

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

Desktop stability milestone (`edde40d`).

## 2026-05 — MVVM + 강력한 테스트 + 잔여 버그 정리

아래 변경은 MVVM milestone (`83bab19`)에 통합되어 있다.

- **WPF UI MVVM 전환** — CommunityToolkit.Mvvm 도입. `MainViewModel` /
  `TwoDViewModel` + `IDialogService` + `VisualizationService` 로 분리. code-behind 2052 → 607 줄 (-70%).
- **테스트 615개로 확대** — 신규 suite: `InvariantTests1D`
  (15 seeds × 3 솔버 매트릭스), `PerformanceTests1D/2D` (wall-clock 예산),
  `StressTests1D` (`[Category("Stress")]`), `QualityComparisonTests`,
  `RobustnessTests`.
- **CG 버그 fix 2건**
  - 초기 그리디 컬럼이 kerf 무시 — `5×1000mm` 패턴이 5000mm bar에 들어가 over-pack.
    초기 컬럼 생성을 `weight = len + (cutsSoFar>0 ? kerf : 0)` 으로 수정.
  - `SolveSingleStock`이 order > stock 인 demand에도 identity 패턴을 생성해
    `Success=true` 와 invalid 패턴을 반환. demand를 `length ≤ stockLength`
    로 필터링하도록 수정.
- **시트 인벤토리 합산** — 동일 dim 시트 행이 두 개면 `Dictionary<Sheet,_>`
  키 충돌로 인벤토리 절반 손실 / `ArgumentException`. `SolverUtils2D.AggregateByDims`
  추가, 모든 2D 솔버 진입부에서 호출.
- **2D 진행률 + 비교 차트 + JSON 시나리오** — 2D 탭이 1D 패리티 도달.
  `ScenarioService` (Core/Persistence) — `.cstock1d/2d.json` round-trip + 스키마 가드.
- **UI/UX 전면 정비** — Excel import 타입 체크 수정, ExportComparison
  rank 정렬 (실패 행 마지막), placeholder 추가, ToolTip 전면 도입, 키보드 단축키
  (F1/Ctrl+R/Ctrl+Shift+C/Ctrl+S), HSL 팔레트 적용, 차트 라벨 회전.
- **알고리즘 정확성 + 도메인 불변성** — `Order`/`RebarStock` 완전
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

이력 작성 규칙: 새 변경은 위쪽에 추가하고 해당 milestone commit SHA를 기록한다.
한 라운드의 구현·검증·문서화는 하나의 의미 있는 변경 묶음으로 유지한다.
