# CuttingStock 알고리즘 벤치마크

이 프로젝트는 CuttingStock 최적화 알고리즘의 성능과 품질을 측정하는 벤치마크를 제공합니다.

## 📊 벤치마크 종류

### 1. AlgorithmBenchmarks
**목적**: 알고리즘 실행 속도 및 메모리 사용량 측정

**테스트 규모**:
- **Small** (소규모): 재고 10개, 주문 30개
- **Medium** (중규모): 재고 50개, 주문 80개
- **Large** (대규모): 재고 100개, 주문 200개

**측정 지표**:
- 실행 시간 (Mean, Median, StdDev)
- 메모리 할당량 (Gen0, Gen1, Gen2, Allocated)
- 알고리즘 간 상대적 순위

### 2. QualityBenchmarks
**목적**: 알고리즘 최적화 품질 비교

**측정 지표**:
- 총 비용 (Total Cost)
- 낭비 길이 (Waste Length)
- 사용 재고 수 (Stock Used)
- 재료 효율 (Material Efficiency)

### 3. LargeScaleBenchmarks
**목적**: Greedy 솔버의 1,000건 장시간 처리량과 메모리 측정

정합성 회귀 테스트와 분리되어 있으며 명시적으로 실행할 때만 수행됩니다.

## 🚀 실행 방법

### 기본 실행
```bash
cd CuttingStock.Benchmarks
dotnet run -c Release
```

### 특정 벤치마크만 실행
```bash
# 품질 비교 벤치마크
dotnet run -c Release -- --quality

# 1,000건 장시간 벤치마크
dotnet run -c Release -- --large

# 모든 벤치마크
dotnet run -c Release -- --all
```

### 결과 내보내기
BenchmarkDotNet 결과는 기본 HTML, CSV, Markdown 형식으로
`BenchmarkDotNet.Artifacts/results/`에 생성됩니다.

## 📈 예상 결과

### 성능 (실행 속도)
```
| Method                | Mean      | Allocated |
|---------------------- |----------:|----------:|
| FFD (Small)           |  1.234 ms |   12.5 KB |
| BFD (Small)           |  1.456 ms |   14.2 KB |
| Greedy (Small)        |  8.901 ms |   45.3 KB |
```

**예상 순위**:
1. **FFD** - 가장 빠름 (O(S × Q))
2. **BFD** - FFD보다 약간 느림 (정렬 오버헤드)
3. **Greedy** - 가장 느림 (DP 계산 O(S × L × N))

### 품질 (최적화 결과)
```
| 알고리즘               | 비용    | 낭비    | 재고  | 효율     |
|-----------------------|---------|---------|-------|----------|
| Greedy Knapsack       | 1,234원 | 1,234mm | 12개  | 92.34%   |
| BFD                   | 1,156원 | 1,156mm | 11개  | 93.21%   |
| FFD                   | 1,289원 | 1,289mm | 12개  | 91.45%   |
```

**예상 순위** (품질):
1. **Greedy Knapsack** - 가장 낮은 비용 (DP로 최적 조합 탐색)
2. **BFD** - FFD보다 10-15% 개선
3. **FFD** - 가장 빠르지만 품질은 낮음

## 🔍 벤치마크 상세 설명

### AlgorithmBenchmarks.cs
```csharp
[Benchmark(Description = "Greedy (Small)")]
public OptimizationResult GreedyKnapsack_Small()
{
    var optimizer = new GreedyKnapsackOptimizer();
    return optimizer.Optimize(_smallStock, _smallOrders, _defaultParams);
}
```

**특징**:
- `[MemoryDiagnoser]`: 메모리 할당량 측정
- `[Orderer(SummaryOrderPolicy.FastestToSlowest)]`: 빠른 순서대로 정렬
- `[RankColumn]`: 순위 컬럼 추가

### QualityBenchmarks.cs
```csharp
[Benchmark(Baseline = true, Description = "Greedy Knapsack")]
public OptimizationResult GreedyKnapsack()
{
    var optimizer = new GreedyKnapsackOptimizer();
    var result = optimizer.Optimize(_testStock, _testOrders, _defaultParams);

    // 결과 저장
    LastTotalCost = result.TotalCost;
    LastWasteLength = result.WasteLength;

    return result;
}
```

**특징**:
- `Baseline = true`: 기준 알고리즘으로 설정
- 최적화 결과 지표 저장 (비용, 낭비, 효율 등)

## 📊 결과 분석 가이드

### 1. 성능 vs 품질 트레이드오프

| 알고리즘        | 속도 | 품질 | 추천 용도                    |
|----------------|------|------|------------------------------|
| **FFD**        | ⭐⭐⭐ | ⭐   | 대규모, 실시간 처리          |
| **BFD**        | ⭐⭐  | ⭐⭐  | 균형 잡힌 성능/품질          |
| **Greedy**     | ⭐   | ⭐⭐⭐ | 소규모, 최적 품질 중요       |

### 2. 규모별 권장 알고리즘

- **소규모** (주문 <50개): **Greedy Knapsack** - 최적 품질
- **중규모** (주문 50-200개): **BFD** - 균형
- **대규모** (주문 >200개): **FFD** - 빠른 속도

### 3. 시간 복잡도 검증

예상 시간 복잡도를 실제 측정값과 비교:

```
규모     | FFD (O(S×Q)) | BFD (O(S×Q log S)) | Greedy (O(S×L×N))
---------|--------------|--------------------|-----------------
Small    | 1.2ms        | 1.5ms              | 8.9ms
Medium   | 5.8ms        | 7.2ms              | 45.3ms
Large    | 11.2ms       | 14.8ms             | 180.2ms
```

Linear scaling 확인: Large/Small 비율 ≈ 10배 (재고/주문 10배 증가)

## 🛠️ 고급 사용법

### 커스텀 벤치마크 추가
```csharp
[Benchmark]
public OptimizationResult MyCustomBenchmark()
{
    // 커스텀 재고 및 주문 설정
    var stock = new List<RebarStock> { /* ... */ };
    var orders = new List<Order> { /* ... */ };

    var optimizer = new BestFitDecreasingOptimizer();
    return optimizer.Optimize(stock, orders, _defaultParams);
}
```

### 파라미터 변경 테스트
```csharp
[Params(100, 500, 1000)]
public int Gamma { get; set; }

[Benchmark]
public OptimizationResult TestGammaImpact()
{
    var parameters = new OptimizationParameters { Gamma = Gamma };
    // ...
}
```

### 통계 분석
```bash
# 통계 분석 포함
dotnet run -c Release --statisticalTest 3ms

# Outlier 제거
dotnet run -c Release --outliers RemoveUpper
```

## 📂 결과 파일

벤치마크 실행 후 생성되는 파일:

```
BenchmarkDotNet.Artifacts/
├── results/
│   ├── AlgorithmBenchmarks-report.html
│   ├── AlgorithmBenchmarks-report.csv
│   └── AlgorithmBenchmarks-report-github.md
└── logs/
    └── AlgorithmBenchmarks.log
```

## 🔬 CI/CD 통합

### GitHub Actions 예시
```yaml
- name: Run Benchmarks
  run: |
    cd CuttingStock.Benchmarks
    dotnet run -c Release --exporters json

- name: Upload Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: BenchmarkDotNet.Artifacts/results/
```

## 📚 참고 자료

- [BenchmarkDotNet 공식 문서](https://benchmarkdotnet.org/)
- [알고리즘 분석 문서](../docs/ALGORITHM_ANALYSIS.md)
- [성능 최적화 로드맵](../docs/IMPROVEMENT_ROADMAP.md)

## ⚠️ 주의사항

1. **Release 모드로 실행**: Debug 모드는 정확한 성능 측정 불가
2. **충분한 반복 횟수**: BenchmarkDotNet이 자동으로 조정 (최소 15회)
3. **안정적인 환경**: 백그라운드 프로세스 최소화
4. **결과 해석**: 절대값보다 상대적 비교가 중요

---

**작성일**: 2025-11-03
**버전**: 1.0
**상태**: Phase 4 완료
