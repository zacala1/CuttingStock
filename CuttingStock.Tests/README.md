# CuttingStock 테스트 스위트

## 개요

이 테스트 스위트는 철근 절단 최적화 알고리즘의 정확성, 성능, 안정성을 검증합니다.

## 테스트 프레임워크

- **NUnit 4.2.2**: 테스트 프레임워크
- **FluentAssertions 6.12.1**: 가독성 높은 Assertion
- **Coverlet**: 코드 커버리지 측정

## 테스트 구조

```
CuttingStock.Tests/
├── Algorithms/
│   ├── GreedyKnapsackOptimizerTests.cs      # GreedyKnapsack 알고리즘 테스트 (70개 테스트)
│   └── FirstFitDecreasingOptimizerTests.cs  # FFD 알고리즘 테스트 (35개 테스트)
├── AlgorithmComparisonTests.cs              # 알고리즘 비교 테스트 (15개 테스트)
├── OptimizationModelsTests.cs               # 모델 테스트 (15개 테스트)
└── TestData/
    ├── TC-001.json
    ├── TC-002.json
    ├── TC-003.json
    └── TC-007.json
```

**총 테스트 수**: 135개 이상

## 테스트 카테고리

### 1. Basic (기본 기능)
기본적인 알고리즘 동작 검증
- 완벽 매칭
- 자투리 처리
- 복수 재고 사용

### 2. Complex (복잡한 케이스)
실제 사용 사례 검증
- 혼합 주문
- 다양한 길이 조합
- TC-007 (문서화된 테스트 케이스)

### 3. Error (에러 처리)
예외 상황 처리 검증
- Null 입력
- 빈 입력
- 재고 부족

### 4. Performance (성능)
실행 시간 검증
- 소규모: < 100ms
- 중규모: < 1000ms
- 대규모: 알고리즘별 상이

### 5. Parameters (파라미터)
다양한 파라미터 조합 검증
- StockUsageOrder
- Gamma 값 변경

### 6. Comparison (비교)
알고리즘 간 비교
- 정확성 비교
- 성능 비교
- 품질 비교

### 7. Meta (메타)
알고리즘 메타 정보 검증
- Name, Description, TimeComplexity
- 리포트 생성

### 8. Quality (품질)
결과 품질 검증
- 재료 효율
- 자투리 최소화

## 실행 방법

### 전체 테스트 실행
```bash
dotnet test
```

### 카테고리별 실행
```bash
# 기본 테스트만
dotnet test --filter Category=Basic

# 성능 테스트만
dotnet test --filter Category=Performance

# 알고리즘 비교
dotnet test --filter Category=Comparison
```

### 특정 클래스 실행
```bash
dotnet test --filter ClassName~GreedyKnapsack
dotnet test --filter ClassName~FirstFitDecreasing
```

### 상세 출력
```bash
dotnet test --logger "console;verbosity=detailed"
```

### 코드 커버리지
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 테스트 예제

### GreedyKnapsackOptimizerTests

#### TC-001: 완벽 매칭
```csharp
[Test]
public void Optimize_PerfectMatch_ShouldHaveNoWaste()
{
    var stock = new List<RebarStock> { new RebarStock(10000, 1) };
    var orders = new List<Order> { new Order(5000, 2) };

    var result = _optimizer.Optimize(stock, orders, _defaultParams);

    result.Success.Should().BeTrue();
    result.WasteLength.Should().Be(0);
    result.TotalCost.Should().Be(0);
}
```

#### TC-007: 복잡한 케이스
```csharp
[Test]
public void Optimize_MixedOrders_ShouldMinimizeWaste()
{
    var stock = new List<RebarStock> { new RebarStock(12000, 10) };
    var orders = new List<Order>
    {
        new Order(5000, 5),
        new Order(3000, 8),
        new Order(2000, 6)
    };

    var result = _optimizer.Optimize(stock, orders, _defaultParams);

    result.Success.Should().BeTrue();
    result.StockUsed.Should().BeInRange(6, 8);
    result.MaterialEfficiency.Should().BeGreaterThan(80.0);
}
```

### AlgorithmComparisonTests

#### 성능 벤치마크
```csharp
[Test]
public void AllAlgorithms_PerformanceBenchmark_SmallScale()
{
    foreach (var optimizer in _allOptimizers)
    {
        var result = optimizer.Optimize(stock, orders, _defaultParams);
        result.ExecutionTimeMs.Should().BeLessThan(100);
    }
}
```

## FluentAssertions 사용법

### 기본 Assertion
```csharp
result.Success.Should().BeTrue();
result.WasteLength.Should().Be(1000);
result.TotalCost.Should().BeGreaterThan(0);
```

### 컬렉션 Assertion
```csharp
result.CuttingPlans.Should().HaveCount(5);
result.ReusableLeftovers.Should().ContainSingle().Which.Should().Be(2000);
result.CuttingPlans.Should().NotBeEmpty();
```

### 범위 Assertion
```csharp
result.StockUsed.Should().BeInRange(6, 8);
result.MaterialEfficiency.Should().BeGreaterThan(80.0);
```

### 문자열 Assertion
```csharp
result.ErrorMessage.Should().Contain("재고");
report.Should().Contain("=== 절단 결과 ===");
```

## 테스트 결과 예제

```
=== Algorithm Comparison ===
Greedy Knapsack DP             Cost:   4500원  Stock:  6개  Efficiency:  91.2%  Time:  45.23ms
First Fit Decreasing (FFD)     Cost:   4800원  Stock:  6개  Efficiency:  90.5%  Time:  12.34ms

=== Performance (Small Scale) ===
First Fit Decreasing (FFD)     12.34ms
Greedy Knapsack DP             45.23ms

=== Material Efficiency ===
Greedy Knapsack DP             91.20%
First Fit Decreasing (FFD)     90.50%

=== Waste & Cost ===
Greedy Knapsack DP             Waste:  4500mm  Cost:   4500원
First Fit Decreasing (FFD)     Waste:  4800mm  Cost:   4800원
```

## 테스트 커버리지 목표

| 항목 | 목표 | 현재 |
|------|------|------|
| **Line Coverage** | > 90% | TBD |
| **Branch Coverage** | > 85% | TBD |
| **Method Coverage** | > 95% | TBD |

## 알려진 제약사항

### 용접 로직 미구현
현재 모든 알고리즘에서 `WeldCount = 0`입니다.
- GreedyKnapsackOptimizer: 용접 미지원
- FirstFitDecreasingOptimizer: 용접 미지원

용접 로직 구현 후 관련 테스트 추가 예정.

### 성능 테스트 시간
CI/CD 환경에서 성능 테스트는 하드웨어에 따라 결과가 다를 수 있습니다.
- 로컬 개발: 엄격한 시간 제한
- CI/CD: 여유 있는 시간 제한 권장

## 테스트 추가 가이드

### 1. 새 테스트 작성
```csharp
[Test]
[Category("YourCategory")]
public void Optimize_YourScenario_ShouldBehavior()
{
    // Arrange
    var stock = ...;
    var orders = ...;

    // Act
    var result = _optimizer.Optimize(stock, orders, _defaultParams);

    // Assert
    result.Success.Should().BeTrue();
    // more assertions...
}
```

### 2. 테스트 데이터 추가
`TestData/TC-XXX.json` 파일 추가:
```json
{
  "testCase": "TC-XXX",
  "description": "설명",
  "stock": [...],
  "orders": [...],
  "parameters": {...},
  "expected": {...}
}
```

### 3. 카테고리 정의
- `[Category("Basic")]`: 기본 기능
- `[Category("Complex")]`: 복잡한 케이스
- `[Category("Error")]`: 에러 처리
- `[Category("Performance")]`: 성능
- `[Category("Comparison")]`: 알고리즘 비교

## CI/CD 통합

### GitHub Actions 예제
```yaml
- name: Run Tests
  run: dotnet test --logger "trx;LogFileName=test-results.trx"

- name: Code Coverage
  run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## 문의

테스트 관련 문의사항은 이슈로 등록해주세요.

---

**테스트 버전**: 1.0
**마지막 업데이트**: 2025-11-02
