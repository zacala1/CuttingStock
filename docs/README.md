# 📚 Cutting Stock 프로젝트 문서

`docs/` 디렉터리는 알고리즘과 API, 벤치마크에 대한 깊이 있는 자료를 모은다.
빠른 시작은 루트의 [README.md](../README.md), 에이전트 컨벤션은 [CLAUDE.md](../CLAUDE.md)를 보라.

## 문서 인덱스

| 문서 | 내용 | 분량 |
|---|---|---|
| [PROBLEM_DEFINITION.md](PROBLEM_DEFINITION.md) | 1D 절단 문제 정의 — 입력, 제약, 목적 함수 | ~10분 |
| [2D_PROBLEM_DEFINITION.md](2D_PROBLEM_DEFINITION.md) | 2D 길로틴 절단 문제 정의 — 길로틴 제약, 회전, 트림 | ~10분 |
| [ALGORITHMS.md](ALGORITHMS.md) | 1D 솔버 3종 알고리즘 요약 | ~15분 |
| [2D_ALGORITHMS.md](2D_ALGORITHMS.md) | 2D 솔버 3종(Shelf / CG2D / StagedMip) 상세 + 벤치 | ~20분 |
| [API_REFERENCE.md](API_REFERENCE.md) | 1D + 2D 공개 타입과 인터페이스 | ~20분 |
| [2D_API_REFERENCE.md](2D_API_REFERENCE.md) | 2D 전용 API 상세 + Quick Start | ~15분 |
| [BENCHMARK_REPORT.md](BENCHMARK_REPORT.md) | BenchmarkDotNet 측정치 + 테스트 커버리지 요약 | ~10분 |
| [ARCHITECTURE_GUARDRAILS.md](ARCHITECTURE_GUARDRAILS.md) | 리팩터링 중 보존해야 할 Core/UI/solver/UI/test 경계 | ~10분 |
| [CHANGELOG.md](../CHANGELOG.md) | 시간순 변경 이력 — 이전 PHASE 노트는 이리로 압축됨 | ~5분 |

## 빠른 시작 가이드

| 상황 | 읽을 문서 |
|---|---|
| "이 프로젝트가 뭐 하는 거지?" | [루트 README.md](../README.md) → [PROBLEM_DEFINITION.md](PROBLEM_DEFINITION.md) |
| "1D / 2D 솔버 차이가 뭐야?" | [ALGORITHMS.md](ALGORITHMS.md) + [2D_ALGORITHMS.md](2D_ALGORITHMS.md) |
| "어떤 솔버를 골라야 하지?" | [BENCHMARK_REPORT.md § 알고리즘 선택 가이드](BENCHMARK_REPORT.md) |
| "코드에서 어떻게 호출해?" | [API_REFERENCE.md § Quick Start](API_REFERENCE.md) |
| "에이전트로 이 프로젝트 작업해야 함" | [CLAUDE.md](../CLAUDE.md) |
| "리팩터링할 때 뭘 깨면 안 돼?" | [ARCHITECTURE_GUARDRAILS.md](ARCHITECTURE_GUARDRAILS.md) |

## 프로젝트 구조

```
CuttingStock/
├── CuttingStock.Core/                # 알고리즘 + 도메인 모델 (no WPF)
│   ├── Algorithms/                   # 1D 솔버 (Greedy / CG / ArcFlow) + Utilities
│   ├── TwoD/Algorithms/              # 2D 솔버 (Shelf / CG2D / StagedMip) + Utilities
│   ├── Domain/                       # SolverModels, ICuttingSolver, SolverOptions...
│   ├── TwoD/Domain/                  # 2D 동일 (ICuttingSolver2D, ...)
│   ├── Models/                       # Order, RebarStock, ComparisonResult (1D)
│   ├── TwoD/Models/                  # Sheet, RectOrder, ComparisonResult2D (2D)
│   └── Persistence/                  # ScenarioService (.cstock1d/2d.json)
├── CuttingStock.UI/                  # WPF (.NET 10 Windows), MVVM
│   ├── MainWindow.xaml(.cs)          # 1D 탭 View
│   ├── TwoD/TwoDTab.xaml(.cs)        # 2D 탭 View
│   ├── ViewModels/                   # MainViewModel, TwoDViewModel + row DTOs
│   └── Services/                     # DialogService, ExportService, VisualizationService
├── CuttingStock.Tests/               # NUnit + FluentAssertions (638 Core tests)
├── CuttingStock.UI.Tests/            # WPF ViewModel/service tests (40 tests)
│   ├── Algorithms/                   # Greedy/CG/ArcFlow 단위 + Quality/Robustness/Stress/Perf
│   ├── TwoD/                         # 2D 솔버 + Invariant 매트릭스 (90 fuzz runs)
│   └── Persistence/                  # ScenarioService 라운드트립
├── CuttingStock.Benchmarks/          # BenchmarkDotNet (인포메이셔널)
├── docs/                             # ← 지금 보고 있는 디렉터리
├── CLAUDE.md                         # 에이전트 컨벤션
└── README.md                         # 한국어 사용자 가이드
```

## 의존성

| 라이브러리 | 버전 | 용도 |
|---|---|---|
| Google.OrTools | 9.15.6755 | ArcFlow MIP (SCIP) + 2D CG (GLOP) + StagedMip (CBC) |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc2 | 비교 탭 막대 차트 |
| ClosedXML | 0.105.0 | Excel 임포트/내보내기 |
| CommunityToolkit.Mvvm | 8.4.2 | `[ObservableProperty]` / `[RelayCommand]` |
| NUnit | 4.4.0 | 테스트 러너 |
| FluentAssertions | 8.8.0 | 단언문 |
| BenchmarkDotNet | 0.15.5 | 성능 측정 |

## 빌드 및 실행

```bash
# 전체 빌드
dotnet build CuttingStock.slnx -c Release

# 전체 테스트 ([Explicit] LargeScale 제외, 현재 678 passed)
dotnet test CuttingStock.slnx -c Release --nologo

# 특정 카테고리
dotnet test CuttingStock.slnx -c Release --filter "Category=Welding"

# Explicit LargeScale 벤치마크
dotnet test CuttingStock.slnx -c Release --filter "FullyQualifiedName~Benchmark_LargeScale"

# WPF 앱 실행
dotnet run --project CuttingStock.UI
```

## 참고 자료

### 학술 자료
- Gilmore & Gomory, "A Linear Programming Approach to the Cutting-Stock Problem," *Operations Research* 9, 1961.
- Beasley, "Algorithms for unconstrained two-dimensional guillotine cutting," *JORS* 36(4), 1985.
- Valerio de Carvalho, "Exact solution of bin-packing problems using column generation and branch-and-bound," *Annals of OR* 86, 1999.
- Coffman, Garey, Johnson, Tarjan, "Performance bounds for level-oriented two-dimensional packing algorithms," *SIAM J. Computing* 9(4), 1980.

### 도구
- [Google OR-Tools](https://developers.google.com/optimization) — LP/MIP 솔버
- [BenchmarkDotNet](https://benchmarkdotnet.org/) — .NET 성능 측정
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — WPF MVVM source generators
