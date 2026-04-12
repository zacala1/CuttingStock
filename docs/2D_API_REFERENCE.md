# 2D API Reference

네임스페이스: `CuttingStock.Core.TwoD.*`

## Quick Start

```csharp
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

var sheets = new List<Sheet>
{
    new(2440, 1220, quantity: 5),
    new(1220, 1220, quantity: 5),
};

var orders = new List<RectOrder>
{
    new(600, 400, quantity: 6),
    new(800, 300, quantity: 4),
    new(300, 300, quantity: 8),
    new(1200, 500, quantity: 2, allowRotation: true),
};

var options = new SolverOptions2D
{
    Kerf = 3,
    Trim = 5,
    AllowRotation = true,
    AlphaArea = 0.0001f,
    Stage = 2,
    TimeLimitMs = 30000,
};

ICuttingSolver2D solver = new ColumnGeneration2DSolver();
SolverResult2D result = solver.Solve(sheets, orders, options);

Console.WriteLine(result.GetDetailedReport(options));
```

## 도메인 타입

### `Sheet`
| 멤버 | 타입 | 설명 |
|---|---|---|
| `Width` | int | 시트 가로 (mm) |
| `Height` | int | 시트 세로 (mm) |
| `Quantity` | int | 가용 수량 |
| `Area` | long | `Width × Height` |

### `RectOrder`
| 멤버 | 타입 | 설명 |
|---|---|---|
| `Width` | int | 아이템 가로 (mm) |
| `Height` | int | 아이템 세로 (mm) |
| `Quantity` | int | 필요 수량 |
| `AllowRotation` | bool | 90° 회전 허용 (default true) |

### `SolverOptions2D`
| 멤버 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `Kerf` | int | 0 | 톱날 두께 (mm) |
| `Trim` | int | 0 | 시트 가장자리 폐기 (mm) |
| `AllowRotation` | bool | true | 글로벌 회전 허용 |
| `AlphaArea` | float | 1.0 | mm² 당 폐기 비용 |
| `Stage` | int | 2 | 길로틴 stage 수 (2 또는 3) |
| `TimeLimitMs` | int | 30000 | 정확/CG 솔버 시간 제한 |
| `UsageOrder` | enum | LargeToSmall | 시트 소비 순서 |

### `SolverResult2D`
| 멤버 | 타입 | 설명 |
|---|---|---|
| `AlgorithmName` | string | 알고리즘 표시명 |
| `Patterns` | List&lt;CuttingPattern2D&gt; | 패턴별 결과 |
| `TotalWasteArea` | long | 전체 폐기 면적 (mm²) |
| `TotalUsedArea` | long | 전체 사용 면적 |
| `TotalSheetArea` | long | 전체 시트 면적 |
| `SheetsUsed` | int | 사용 시트 수 |
| `MaterialEfficiency` | double | 효율 (%) |
| `TotalCost` | long | 폐기 면적 × `AlphaArea` |
| `ExecutionTimeMs` | double | 실행 시간 |
| `Success` | bool | 성공 여부 |
| `ErrorMessage` | string? | 실패 메시지 |
| `GetDetailedReport(options)` | string | 사람-가독 리포트 |

### `CuttingPattern2D`
| 멤버 | 타입 | 설명 |
|---|---|---|
| `Sheet` | Sheet | 패턴의 시트 |
| `Multiplicity` | int | 같은 패턴의 반복 횟수 |
| `Placements` | List&lt;Placement&gt; | 평면 배치 |
| `Root` | GuillotineNode? | 길로틴 트리 (옵션) |
| `UsedArea` / `WasteArea` / `Efficiency` | — | 면적 통계 |

### `Placement`
| 멤버 | 타입 | 설명 |
|---|---|---|
| `OrderIndex` | int | 입력 주문 인덱스 |
| `X`, `Y` | int | 시트 내 절대 좌표 (mm) |
| `Width`, `Height` | int | 회전 후 유효 크기 |
| `Rotated` | bool | 90° 회전 여부 |

## 솔버 인터페이스

### `ICuttingSolver2D`
```csharp
public interface ICuttingSolver2D
{
    string Name { get; }
    string Description { get; }
    string TimeComplexity { get; }
    SolverResult2D Solve(
        List<Sheet> sheets,
        List<RectOrder> orders,
        SolverOptions2D options,
        IProgress<double>? progress = null);
}
```

### 구현
- `ShelfGuillotineSolver` — 빠른 휴리스틱
- `ColumnGeneration2DSolver` — Gilmore-Gomory CG (OR-Tools GLOP + Beasley DP)
- `StagedMipGuillotineSolver` — 패턴 풀 + 정수 마스터 (OR-Tools CBC)

## 유틸리티

### `GuillotineValidator`
```csharp
bool IsGuillotineCompliant(int outerX, int outerY, int outerW, int outerH,
                          IList<(int x, int y, int w, int h)> rects);
bool IsGuillotineCompliant(CuttingPattern2D pattern, int trim = 0);
bool IsValidTree(GuillotineNode node);
```

### `GuillotineKnapsackDp`
2D 길로틴 unbounded knapsack — Beasley 1985 정규 컷 DP. 솔버에서 직접 호출 가능.

### `PatternBuilder`
flat placement list → `GuillotineNode` 트리 변환. 비-길로틴 배치는 `null` 반환.

## 1D API 와의 차이

| 1D | 2D |
|---|---|
| `RebarStock` | `Sheet` |
| `Order` | `RectOrder` |
| `SolverOptions` | `SolverOptions2D` |
| `SolverResult` | `SolverResult2D` |
| `CuttingPlan` | `CuttingPattern2D` |
| `Cut` | `Placement` |
| `ICuttingSolver` | `ICuttingSolver2D` |

1D 와 2D 는 별개 네임스페이스이며 서로 호환되지 않는다. 두 모드를 한 프로그램에서 동시에 사용하려면 입력/출력 변환을 직접 작성해야 한다.
