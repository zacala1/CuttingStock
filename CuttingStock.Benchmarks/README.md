# CuttingStock 벤치마크

이 프로젝트는 BenchmarkDotNet으로 1D/2D 솔버의 실행 시간과 메모리
할당을 측정합니다. 벤치마크 결과는 정합성 테스트가 아니며, 같은 머신과
환경에서 얻은 상대값으로 비교해야 합니다.

## 벤치마크 구성

- `AlgorithmBenchmarks`: 1D Greedy 솔버의 Small/Medium/Large 입력
- `TwoDBenchmarks`: 2D 솔버의 대표 입력
- `QualityBenchmarks`: 동일한 1D 입력에서 Greedy, Column Generation,
  Arc Flow의 시간·메모리 비교
- `LargeScaleBenchmarks`: Greedy 1,000건 장시간 처리량 측정

`QualityBenchmarks`의 `LastTotalCost`, `LastWasteLength`, `LastStockUsed`,
`LastMaterialEfficiency`는 각 실행이 만든 결과를 확인하기 위한 보조
지표입니다. BenchmarkDotNet 요약표의 주 측정값은 시간과 할당량입니다.
`--quality` 실행이 끝나면 `DetailedQualityComparison`이 비용, 낭비 길이,
원재고 사용량, 효율, 실행 시간을 별도 콘솔 표로 출력합니다.

## 실행

저장소 루트에서 Release 모드로 실행합니다.

```powershell
# 기본: 1D + 2D 속도/메모리
dotnet run --project CuttingStock.Benchmarks -c Release -- --default

# 1D 솔버 품질 비교
dotnet run --project CuttingStock.Benchmarks -c Release -- --quality

# Greedy 1,000건
dotnet run --project CuttingStock.Benchmarks -c Release -- --large

# 전체
dotnet run --project CuttingStock.Benchmarks -c Release -- --all

# 지원 모드 확인
dotnet run --project CuttingStock.Benchmarks -c Release -- --help
```

`Program.cs`는 위 모드만 해석합니다. BenchmarkDotNet의 임의 CLI 옵션은
현재 래퍼를 통해 전달되지 않습니다.

## 결과

기본 결과는 다음 경로에 생성됩니다.

```text
BenchmarkDotNet.Artifacts/
├── results/
│   ├── *-report.html
│   ├── *-report.csv
│   └── *-report-github.md
└── CuttingStock.Benchmarks.*-<timestamp>.log
```

실행 중인 백그라운드 작업, 전원 정책, CPU 온도와 런타임 버전이 결과에
영향을 줍니다. 비교 전에는 같은 커밋, 같은 머신, 같은 전원 상태를
유지하십시오.

## 커스텀 벤치마크

프로젝트가 실제로 노출하는 계약인 `ICuttingSolver.Solve`와
`SolverResult`를 사용합니다.

```csharp
[Benchmark]
public SolverResult CustomCase()
{
    var stock = new List<RebarStock> { new(12000, 20) };
    var orders = new List<Order> { new(5000, 10), new(3000, 12) };
    var options = new SolverOptions { Gamma = 100 };

    return new GreedyKnapsackSolver().Solve(stock, orders, options);
}
```

입력 축을 비교하려면 BenchmarkDotNet의 `[Params]`를 사용합니다.

```csharp
[Params(100, 500, 1000)]
public int Gamma { get; set; }
```

## CI 예시

일반 CI에서는 벤치마크 프로젝트를 빌드만 하고, 노이즈가 적은 전용
러너나 예약 작업에서 실행하는 편이 적절합니다.

```yaml
- name: Build benchmarks
  run: dotnet build CuttingStock.Benchmarks/CuttingStock.Benchmarks.csproj -c Release

- name: Run benchmark suite
  run: dotnet run --project CuttingStock.Benchmarks -c Release -- --default

- name: Upload benchmark results
  uses: actions/upload-artifact@v7
  with:
    name: benchmark-results
    path: BenchmarkDotNet.Artifacts/results/
```

## 참고

- [BenchmarkDotNet 공식 문서](https://benchmarkdotnet.org/)
- [알고리즘 분석](../docs/ALGORITHMS.md)
- [아키텍처 가드레일](../docs/ARCHITECTURE_GUARDRAILS.md)
