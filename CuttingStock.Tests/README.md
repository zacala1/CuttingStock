# CuttingStock 테스트 스위트

## 개요

`CuttingStock.Tests` 는 `CuttingStock.Core` 의 1D/2D 알고리즘, 도메인 모델,
유틸리티, JSON 시나리오 persistence를 검증한다. UI 타입은 `net10.0-windows`
대상이라 이 프로젝트에서 직접 참조하지 않는다. ViewModel/service 검증은
`CuttingStock.UI.Tests` 에 둔다.

현재 Release 기준:

| 프로젝트 | 테스트 수 | 범위 |
|---|---:|---|
| `CuttingStock.Tests` | 638 | Core 알고리즘/도메인/persistence |
| `CuttingStock.UI.Tests` | 40 | WPF ViewModel command/state, dialog flow, visualization service |
| **합계** | **678** | `[Explicit]` LargeScale benchmark 1개는 기본 실행 제외 |

## 프레임워크

- NUnit 4.4.0
- FluentAssertions 8.8.0
- Microsoft.NET.Test.Sdk 18.0.0
- coverlet.collector 6.0.4
- BenchmarkDotNet 0.15.5

## 주요 테스트 영역

| 영역 | 대표 파일 |
|---|---|
| 1D solver 단위/통합 | `Algorithms/`, `AlgorithmComparisonTests.cs`, `IntegrationCorrectnessTests.cs` |
| 1D invariant fuzzing | `Algorithms/InvariantTests1D.cs` |
| 1D robustness / quality / performance / stress | `Algorithms/RobustnessTests.cs`, `GreedyKnapsackQualityTests.cs`, `PerformanceTests1D.cs`, `StressTests1D.cs` |
| 1D shared utilities | `SolverUtilsEdgeTests.cs` |
| 1D welding | `WeldingLogicTests.cs` |
| 2D solver/edge/consistency/invariant | `TwoD/Solver2D*.cs` |
| 2D guillotine DP / validator / pattern infra | `TwoD/Guillotine*.cs`, `TwoD/Pattern*.cs`, `TwoD/SolverUtils2DTests.cs` |
| Persistence | `Persistence/ScenarioServiceTests.cs`, `Persistence/UserPreferencesTests.cs` |
| Domain model equality/validation | `OptimizationModelsTests.cs`, `ModelsEqualityTests.cs`, `TwoD/Domain2D*.cs` |

## 실행

```bash
# 전체 빌드
dotnet build CuttingStock.slnx -c Release

# 전체 테스트: Explicit LargeScale benchmark 제외
dotnet test CuttingStock.slnx -c Release --nologo --no-build

# 특정 카테고리
dotnet test CuttingStock.slnx -c Release --filter "Category=Welding"
dotnet test CuttingStock.slnx -c Release --filter "Category=Performance"
dotnet test CuttingStock.slnx -c Release --filter "Category=Stress"

# Explicit LargeScale 1000-orders benchmark
dotnet test CuttingStock.slnx -c Release --filter "FullyQualifiedName~Benchmark_LargeScale"
```

## 핵심 불변식

성공한 1D 결과는 `SolverUtils.ValidateSuccessfulResult` 를 통과해야 한다.

- plan 소비 길이 = `sum(cut lengths) + (cuts.Count - 1) * kerf`
- `Leftover` 는 `SolverUtils.ComputeLeftover` 와 일치
- 비-용접 cut과 용접 group 합계가 demand를 정확히 충족
- 용접 group은 2개 이상 조각이고 각 조각 길이가 `Delta` 이상
- stock length별 사용량이 입력 inventory를 초과하지 않음

성공한 2D 결과는 `SolverUtils2D.ValidateSuccessfulResult` 를 통과해야 한다.

- sheet inventory와 pattern multiplicity가 유효
- placement가 trim 영역 안에 있고 kerf-aware overlap이 없음
- placement order index, 치수, 회전 플래그가 입력과 일치
- pattern이 `GuillotineValidator` 로 검증 가능한 길로틴 배치
- 각 order의 생산 수량이 demand와 정확히 일치

## 테스트 추가 규칙

버그를 고칠 때는 실패하는 회귀 테스트를 먼저 추가하고, 수정 범위는 원인 모듈로 제한한다.
WPF 의존성이 없는 로직은 `CuttingStock.Core` 로 내려 테스트 가능하게 유지한다.
