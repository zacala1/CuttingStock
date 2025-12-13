# CuttingStock API Reference

## 목차

1. [개요](#개요)
2. [설치](#설치)
3. [빠른 시작](#빠른-시작)
4. [네임스페이스](#네임스페이스)
5. [주요 클래스](#주요-클래스)
6. [알고리즘](#알고리즘)
7. [모델 클래스](#모델-클래스)
8. [사용 예제](#사용-예제)
9. [FAQ](#faq)

---

## 개요

CuttingStock은 철근 절단 최적화(Cutting Stock Problem)를 해결하기 위한 .NET 라이브러리입니다.

**주요 기능:**
- 3가지 최적화 알고리즘 제공 (Greedy Knapsack DP, FFD, BFD)
- 용접 로직 지원
- 자투리 재사용 최적화
- 비용 최적화 (자투리 비용 + 용접 비용)

---

## 설치

```bash
# NuGet (예정)
dotnet add package CuttingStock.Core

# 또는 프로젝트 참조
dotnet add reference ../CuttingStock.Core/CuttingStock.Core.csproj
```

---

## 빠른 시작

```csharp
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

// 1. 알고리즘 선택
IOptimizer optimizer = new GreedyKnapsackOptimizer();

// 2. 재고 정의 (12000mm 철근 10개)
var stock = new List<RebarStock>
{
    new RebarStock(12000, 10)
};

// 3. 주문 정의
var orders = new List<Order>
{
    new Order(5000, 5),   // 5000mm × 5개
    new Order(3000, 8),   // 3000mm × 8개
    new Order(2000, 6)    // 2000mm × 6개
};

// 4. 파라미터 설정
var parameters = new OptimizationParameters
{
    Alpha = 1.0f,         // 자투리 비용 계수
    Beta = 500.0f,        // 용접 비용
    Gamma = 100,          // 최소 자투리 길이
    EnableWelding = false // 용접 비활성화
};

// 5. 최적화 실행
var result = optimizer.Optimize(stock, orders, parameters);

// 6. 결과 확인
Console.WriteLine($"성공: {result.Success}");
Console.WriteLine($"효율: {result.MaterialEfficiency:F2}%");
Console.WriteLine($"총 비용: {result.TotalCost}원");
```

---

## 네임스페이스

| 네임스페이스 | 설명 |
|------------|------|
| `CuttingStock.Core.Algorithms` | 최적화 알고리즘 구현 |
| `CuttingStock.Core.Domain` | 인터페이스 및 도메인 모델 |
| `CuttingStock.Core.Models` | 데이터 모델 클래스 |

---

## 주요 클래스

### IOptimizer (Interface)

모든 최적화 알고리즘의 공통 인터페이스입니다.

```csharp
public interface IOptimizer
{
    string Name { get; }
    string Description { get; }
    string TimeComplexity { get; }

    OptimizationResult Optimize(
        List<RebarStock> stock,
        List<Order> orders,
        OptimizationParameters parameters);
}
```

**속성:**

| 속성 | 타입 | 설명 |
|-----|------|------|
| `Name` | `string` | 알고리즘 이름 |
| `Description` | `string` | 알고리즘 설명 |
| `TimeComplexity` | `string` | 시간 복잡도 (Big-O) |

**메서드:**

| 메서드 | 반환 타입 | 설명 |
|-------|---------|------|
| `Optimize()` | `OptimizationResult` | 최적화 실행 |

---

### OptimizationParameters

최적화 파라미터를 정의합니다.

```csharp
public class OptimizationParameters
{
    public float Alpha { get; set; } = 1.0f;
    public float Beta { get; set; } = 500.0f;
    public int Gamma { get; set; } = 100;
    public int Delta { get; set; } = 100;
    public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.SmallToLarge;
    public bool EnableWelding { get; set; } = false;
}
```

**속성:**

| 속성 | 타입 | 기본값 | 설명 |
|-----|------|-------|------|
| `Alpha` | `float` | 1.0 | 자투리 1mm당 비용 (원/mm) |
| `Beta` | `float` | 500.0 | 용접 1회당 비용 (원/회) |
| `Gamma` | `int` | 100 | 재사용 가능한 자투리의 최소 길이 (mm) |
| `Delta` | `int` | 100 | 용접 가능한 조각의 최소 길이 (mm) |
| `UsageOrder` | `StockUsageOrder` | SmallToLarge | 재고 사용 순서 |
| `EnableWelding` | `bool` | false | 용접 활성화 여부 |

---

### OptimizationResult

최적화 결과를 담는 클래스입니다.

```csharp
public class OptimizationResult
{
    public string AlgorithmName { get; set; }
    public List<CuttingPlan> CuttingPlans { get; set; }
    public List<int> ReusableLeftovers { get; set; }
    public int WasteLength { get; set; }
    public int WeldCount { get; set; }
    public int TotalCost { get; set; }
    public double ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // 계산된 속성
    public int StockUsed { get; }
    public double MaterialEfficiency { get; }

    // 메서드
    public string GetDetailedReport(OptimizationParameters parameters);
}
```

**주요 속성:**

| 속성 | 타입 | 설명 |
|-----|------|------|
| `Success` | `bool` | 최적화 성공 여부 |
| `StockUsed` | `int` | 사용된 재고 개수 |
| `MaterialEfficiency` | `double` | 재료 효율 (%) |
| `TotalCost` | `int` | 총 비용 (원) |
| `WasteLength` | `int` | 폐기 자투리 (mm) |
| `WeldCount` | `int` | 용접 횟수 |
| `CuttingPlans` | `List<CuttingPlan>` | 절단 계획 목록 |

---

### CuttingPlan

개별 재고의 절단 계획입니다.

```csharp
public class CuttingPlan
{
    public int StockLength { get; set; }
    public List<Cut> Cuts { get; set; }
    public int Leftover { get; set; }
}
```

**속성:**

| 속성 | 타입 | 설명 |
|-----|------|------|
| `StockLength` | `int` | 원래 재고 길이 (mm) |
| `Cuts` | `List<Cut>` | 절단 조각 목록 |
| `Leftover` | `int` | 남은 자투리 (mm) |

---

### Cut

개별 절단 조각입니다.

```csharp
public class Cut
{
    public int Length { get; set; }
    public int OrderIndex { get; set; }
    public bool RequiresWelding { get; set; }
    public int? WeldGroupId { get; set; }
}
```

**속성:**

| 속성 | 타입 | 설명 |
|-----|------|------|
| `Length` | `int` | 절단 길이 (mm) |
| `OrderIndex` | `int` | 해당 주문 인덱스 |
| `RequiresWelding` | `bool` | 용접 필요 여부 |
| `WeldGroupId` | `int?` | 용접 그룹 ID |

---

## 알고리즘

### GreedyKnapsackOptimizer

동적 계획법 기반 Knapsack 최적화 알고리즘입니다.

```csharp
var optimizer = new GreedyKnapsackOptimizer();
```

**특징:**
- 희소 DP로 메모리 90% 절감
- 다중 패스 최적화 (균등분배 → 잔여최적화 → 마무리)
- 희소성 기반 정렬 (수량 적은 주문 우선)
- 후처리 최적화 (재고 간 주문 재분배)

**시간 복잡도:** O(S × L × N)

**권장 사용:**
- 소~중규모 데이터 (주문 100개 이하)
- 최적화 품질이 중요한 경우
- 용접이 필요한 경우

---

### FirstFitDecreasingOptimizer

FFD (First Fit Decreasing) 휴리스틱 알고리즘입니다.

```csharp
var optimizer = new FirstFitDecreasingOptimizer();
```

**특징:**
- 큰 주문부터 정렬
- 첫 번째로 들어가는 재고에 배치
- 자투리 활용 (v2.0)
- 후처리 최적화 (v2.0)

**시간 복잡도:** O(S × Q log Q)

**권장 사용:**
- 대규모 데이터 (주문 1000개 이상)
- 빠른 응답이 필요한 경우

---

### BestFitDecreasingOptimizer

BFD (Best Fit Decreasing) 휴리스틱 알고리즘입니다.

```csharp
var optimizer = new BestFitDecreasingOptimizer();
```

**특징:**
- 큰 주문부터 정렬
- 남은 공간이 가장 작은 재고에 배치
- FFD보다 10-15% 더 효율적
- 자투리 활용 (v2.0)

**시간 복잡도:** O(S × Q log S)

**권장 사용:**
- 대규모 데이터
- 재료 효율이 중요한 경우

---

## 모델 클래스

### RebarStock

재고 철근을 나타냅니다.

```csharp
public class RebarStock
{
    public int Length { get; set; }    // 길이 (mm)
    public int Quantity { get; set; }  // 수량 (개)
}
```

**생성자:**

```csharp
// 기본 생성자
var stock1 = new RebarStock();

// 파라미터 생성자
var stock2 = new RebarStock(12000, 10);  // 12000mm × 10개
```

---

### Order

주문 철근을 나타냅니다.

```csharp
public class Order
{
    public int Length { get; set; }    // 길이 (mm)
    public int Quantity { get; set; }  // 수량 (개)
}
```

**생성자:**

```csharp
// 기본 생성자
var order1 = new Order();

// 파라미터 생성자
var order2 = new Order(5000, 5);  // 5000mm × 5개
```

---

### StockUsageOrder (Enum)

재고 사용 순서를 정의합니다.

```csharp
public enum StockUsageOrder
{
    SmallToLarge,  // 작은 것부터 사용
    LargeToSmall   // 큰 것부터 사용
}
```

---

## 사용 예제

### 예제 1: 기본 최적화

```csharp
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

var optimizer = new BestFitDecreasingOptimizer();

var stock = new List<RebarStock>
{
    new RebarStock(12000, 5),
    new RebarStock(9000, 3)
};

var orders = new List<Order>
{
    new Order(4000, 5),
    new Order(3000, 5),
    new Order(2000, 5)
};

var result = optimizer.Optimize(stock, orders, new OptimizationParameters());

Console.WriteLine($"성공: {result.Success}");
Console.WriteLine($"사용 재고: {result.StockUsed}개");
Console.WriteLine($"효율: {result.MaterialEfficiency:F2}%");
```

### 예제 2: 용접 활성화

```csharp
var parameters = new OptimizationParameters
{
    EnableWelding = true,
    Beta = 1000.0f,  // 용접 비용 높게 설정
    Delta = 500      // 최소 조각 크기
};

var result = optimizer.Optimize(stock, orders, parameters);

// 용접 정보 출력
foreach (var plan in result.CuttingPlans)
{
    foreach (var cut in plan.Cuts.Where(c => c.RequiresWelding))
    {
        Console.WriteLine($"용접 필요: {cut.Length}mm (그룹 {cut.WeldGroupId})");
    }
}
```

### 예제 3: 알고리즘 비교

```csharp
var algorithms = new IOptimizer[]
{
    new GreedyKnapsackOptimizer(),
    new FirstFitDecreasingOptimizer(),
    new BestFitDecreasingOptimizer()
};

foreach (var algo in algorithms)
{
    var result = algo.Optimize(stock, orders, new OptimizationParameters());

    Console.WriteLine($"{algo.Name}:");
    Console.WriteLine($"  효율: {result.MaterialEfficiency:F2}%");
    Console.WriteLine($"  시간: {result.ExecutionTimeMs:F2}ms");
    Console.WriteLine($"  비용: {result.TotalCost}원");
}
```

### 예제 4: 상세 리포트 생성

```csharp
var result = optimizer.Optimize(stock, orders, parameters);

if (result.Success)
{
    var report = result.GetDetailedReport(parameters);
    Console.WriteLine(report);
}
else
{
    Console.WriteLine($"실패: {result.ErrorMessage}");
}
```

---

## FAQ

### Q1: 어떤 알고리즘을 선택해야 하나요?

| 상황 | 권장 알고리즘 |
|-----|-------------|
| 빠른 응답 필요 | BFD 또는 FFD |
| 최적화 품질 중요 | Greedy Knapsack |
| 용접 필요 | Greedy Knapsack |
| 대규모 데이터 | BFD |

### Q2: 용접은 언제 사용하나요?

주문 길이가 재고 길이보다 긴 경우 용접을 활성화하면 여러 조각으로 분할하여 처리합니다.

```csharp
parameters.EnableWelding = true;
parameters.Delta = 100;  // 최소 조각 크기
```

### Q3: 자투리 재사용은 어떻게 하나요?

`Gamma` 이상의 자투리는 자동으로 재사용 가능한 것으로 분류됩니다.

```csharp
parameters.Gamma = 500;  // 500mm 이상만 재사용 가능
```

### Q4: 비용 계산 공식은?

```
총 비용 = (폐기 자투리 × Alpha) + (용접 횟수 × Beta)
```

---

## 버전 기록

| 버전 | 날짜 | 변경 사항 |
|-----|-----|---------|
| 2.0 | 2025-11-28 | FFD/BFD 개선, 자투리 활용, 후처리 최적화 |
| 1.0 | 2025-11-27 | 초기 버전 |

---

*Generated: 2025-11-28*
*Project: CuttingStock v2.0*
