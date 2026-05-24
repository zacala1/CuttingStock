# Cutting Stock Optimization

1D(철근/봉재) **및** 2D(시트/플레이트) Cutting Stock 문제를 해결하는 .NET 10 WPF 데스크톱 애플리케이션. 각 차원에 휴리스틱·LP·MIP 솔버 3종을 제공한다.

## 프로젝트 구조

- **CuttingStock.Core** — 알고리즘, 도메인 모델, OR-Tools 통합, JSON 시나리오 영속화 (`Persistence/`)
- **CuttingStock.UI** — WPF + MVVM(CommunityToolkit.Mvvm) UI. ViewModels / Services 분리
- **CuttingStock.Tests** — NUnit + FluentAssertions, **615 테스트** (Stress + Performance 카테고리 포함)
- **CuttingStock.Benchmarks** — BenchmarkDotNet 성능 측정 (인포메이셔널)

## 1D 솔버 (`ICuttingSolver`)

### 1. `GreedyKnapsackSolver` — 다중 패스 휴리스틱

Sparse DP + 2-opt 후처리.

- **전략**: Pass1(균등, 주문당 2cut) → Pass2(잔여, 5cut) → Pass3(채우기, 무제한) + swap/relocate
- **잔여 leftover 호스트** — 부분 용접 조각이 기존 plan의 leftover에 들어갈 수 있으면 새 재고 안 씀 (`FindHostPlanForWeld`)
- **복잡도**: O(N × L × Passes)
- **장점**: 빠르고(<1ms 소규모), 용접/kerf 자연 지원
- **단점**: 단일 길이 대량 demand 시 Pass1 캡으로 비최적 (∼ 2× 최적)

### 2. `ColumnGenerationSolver` — Gilmore-Gomory LP

커스텀 Simplex 마스터 + knapsack DP 가격 매김.

- **전략**: 초기 그리디 컬럼(kerf-aware) → LP → 가격 매김 → 100회 반복 → Floor-then-Residual 정수화
- **다중 stock**: stock 길이별 sub-problem
- **장점**: LP 최적 근접, 대규모 주문 종류에 강함
- **단점**: 커스텀 Simplex의 수치 안정성 한계

### 3. `ArcFlowSolver` — Arc Flow MIP (OR-Tools SCIP)

DAG 네트워크 + SCIP MIP.

- **전략**: 노드 = 위치, 아이템 arc = `length + kerf`, GCD 노드 압축
- **복잡도**: 정확(NP-hard, 30s 시간 제한)
- **장점**: 수학적 최적, 다중 재고 지원
- **단점**: 입력 distinct length가 많거나 kerf로 GCD 작아지면 시간 제한 도달 가능

## 2D 솔버 (`ICuttingSolver2D`)

`CuttingStock.Core.TwoD` 네임스페이스에 1D 거울 구조 3종. 산업용 패널 톱이 요구하는 **길로틴(guillotine)** 절단, 90° 회전, kerf, 트림 모두 지원. **모든 솔버는 입력 시 `SolverUtils2D.AggregateByDims`로 동일 dim 시트 행을 합산** — `Sheet.Equals`가 구조적이므로 행 분산은 인벤토리 절반 손실로 이어진다.

| 솔버 | 핵심 알고리즘 | 출처 |
|---|---|---|
| `ShelfGuillotineSolver` | NFDH/FFDH/BFDH × 5 정렬 휴리스틱 | Coffman et al. 1980; Berkey & Wang 1987 |
| `ColumnGeneration2DSolver` | Master LP(GLOP) + 2D guillotine knapsack DP | Gilmore & Gomory 1965; Beasley 1985; Cintra et al. 2008 |
| `StagedMipGuillotineSolver` | Pattern pool + 정수 마스터 (CBC) | Vance et al. 1994; Belov & Scheithauer 2006; Furini et al. 2016 |

상세는 `docs/2D_PROBLEM_DEFINITION.md`, `docs/2D_ALGORITHMS.md`, `docs/2D_API_REFERENCE.md` 참조.

### 2D 벤치마크 (i5-14600KF, .NET 10, Release)

| 규모 (∼아이템) | Shelf | CG2D | MIP (CBC) |
|---|---:|---:|---:|
| Small (∼10) | **11 μs** | 373 μs | 2.3 ms |
| Medium (∼28) | **35 μs** | 1.4 ms | 4.6 ms |
| Large (∼74) | **110 μs** | 18 ms | 35 ms |

WPF UI는 상단에 **1D 절단 / 2D 절단** 두 탭. 두 탭 모두 입력 그리드, 알고리즘 선택, 결과 텍스트, 패턴 시각화(1D는 막대 / 2D는 Canvas), 비교 데이터그리드 + LiveCharts, CSV/Excel 내보내기, JSON 시나리오 저장·열기를 제공한다.

## 파라미터

### 1D (`SolverOptions`)

| 파라미터 | 설명 | 기본값 |
|---------|------|--------|
| Alpha | 자투리 1mm당 비용 (원/mm) | 1.0 |
| Beta | 용접 1회당 비용 (원/회) | 500 |
| Gamma | 재사용 가능한 최소 자투리 길이 (mm) | 100 |
| Delta | 용접 가능한 최소 조각 길이 (mm) | 100 |
| Kerf | 톱날 두께 (mm). 인접 절단 사이에 소비 | 0 |
| UsageOrder | `SmallToLarge` / `LargeToSmall` | SmallToLarge |
| EnableWelding | 용접 허용 (재고 길이 초과 주문 처리) | false |

### 2D (`SolverOptions2D`)

| 파라미터 | 설명 | 기본값 |
|---------|------|--------|
| Kerf | 톱날 두께 (mm) | 0 |
| Trim | 시트 각 변 트림 (mm) | 0 |
| AlphaArea | 면적 1mm² 당 비용 (원/mm²) | 1 |
| AllowRotation | 90° 회전 허용 (글로벌) | true |
| Stage | 길로틴 단계 수 — **현재는 advisory only** | 2 |
| TimeLimitMs | CG/MIP 솔버 wall-clock 절대 deadline | 30000 |
| UsageOrder | 시트 소비 순서 | LargeToSmall |

## 주요 기능

- 1D / 2D 각 3종 알고리즘 + 비교
- Kerf(톱날 두께) 지원 — 현실 절단 손실 반영
- 1D 용접 지원 — 긴 주문을 여러 조각으로 분할, 부분 조각은 기존 plan leftover에 호스트
- 후처리 최적화 (1D) — 2-opt swap + relocate
- 결과 시각화 — 1D는 패턴 그룹핑 막대, 2D는 Canvas 배치
- 비교 시각화 — LiveCharts (1D 3차트, 2D 3차트)
- CSV / Excel 내보내기 (단일 + 비교, 1D + 2D)
- **JSON 시나리오 저장 / 불러오기** — `.cstock1d.json` / `.cstock2d.json` (입력 + 옵션 round-trip)
- 엑셀 붙여넣기(Ctrl+V) 지원
- 키보드 단축키: F1(예제), Ctrl+R(실행), Ctrl+Shift+C(비교), Ctrl+S(Excel 저장)
- 멀티 시트 dim 자동 합산 (2D 솔버 진입부)

## 테스트

| 카테고리 | 테스트 수 | 비고 |
|---|---:|---|
| 1D 도메인/모델/알고리즘 | 350+ | Greedy/CG/ArcFlow 단위 + 통합 |
| 1D 불변식 매트릭스 | 45 | 15 seeds × 3 솔버, 모든 invariant 검증 |
| 1D 견고성 (adversarial) | 16 | 큰 kerf, 극단 α/β, 빈 입력 등 |
| 1D 품질 비교 (cross-solver) | 5 | 모든 솔버 성공 + 3× 한계 + 70% 효율 |
| 1D 성능 budget | 8 | wall-clock 예산 (Greedy/CG/ArcFlow × small/med/large) |
| 1D 스트레스 (`[Category("Stress")]`) | 5 | 2000 distinct / 5000 동일 / 다중 stock |
| 2D 솔버 + 도메인 | 100+ | 솔버, 일관성, 엣지, 도메인 |
| 2D 불변식 매트릭스 | 90 | 30 seeds × 3 솔버, 8개 invariant |
| ScenarioService 라운드트립 | 4 | 1D + 2D round-trip + 스키마 가드 |
| **합계** | **615** | 모두 통과 (Stress 포함 ~4분) |

`Benchmark_LargeScale_1000_Orders`는 `[Explicit]`로 디폴트 실행에서 제외, 별도 호출 시 ~7초.

## 의존성

- **.NET 10.0** (Windows: `net10.0-windows`)
- [Google.OrTools](https://developers.google.com/optimization) 9.15.6755 — Arc Flow MIP + 2D GLOP/CBC
- [LiveChartsCore.SkiaSharpView.WPF](https://livecharts.dev/) 2.0.0-rc2 — 비교 차트
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) 0.105.0 — Excel I/O
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) 8.4.2 — `[ObservableProperty]` / `[RelayCommand]`
- NUnit 4.4.0 + FluentAssertions 8.8.0 + BenchmarkDotNet 0.15.5

## 빌드 및 실행

```bash
# 전체 빌드 (Release)
dotnet build CuttingStock.slnx -c Release

# 테스트 (Stress 제외, ~2분)
dotnet test CuttingStock.slnx -c Release --filter "TestCategory!=Stress"

# 전체 테스트 (Stress 포함, ~4분)
dotnet test CuttingStock.slnx -c Release

# WPF 앱 실행
dotnet run --project CuttingStock.UI
```

## 라이선스

이 프로젝트는 교육 및 연구 목적으로 작성되었습니다.
