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
| `Kerf` | int | 0 | 톱날 두께 (mm). 인접 컷 사이에 소비 |
| `Trim` | int | 0 | 시트 각 변의 트림 폐기 (mm) |
| `AllowRotation` | bool | true | 글로벌 90° 회전 토글 (per-`RectOrder` 플래그도 활성이어야 함) |
| `AlphaArea` | float | 1.0 | mm² 당 폐기 비용 |
| `Stage` | int | 2 | 길로틴 stage 수 (2 또는 3). `TwoStageShelfGuillotineSolver`는 2-stage를 강제하고 나머지 솔버에서는 advisory다. 어떤 솔버도 3-stage를 강제하지 않는다 |
| `TimeLimitMs` | int | 30000 | CG/MIP 솔버 **절대 wall-clock deadline**. 솔버 시작 시점부터 카운트되며 warm-start / bootstrap 시간도 포함 |
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
| `Success` | bool | 성공 여부. `true` 이면 공통 validator가 sheet inventory, trim/kerf/회전, 길로틴, exact demand 불변식을 확인한 상태 |
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
- `TwoStageShelfGuillotineSolver` — shelf 결과의 2-stage 구조를 강제 검증
- `ColumnGeneration2DSolver` — Gilmore-Gomory CG (OR-Tools GLOP + Beasley DP)
- `StagedMipGuillotineSolver` — 패턴 풀 + 정수 마스터 (OR-Tools CBC)

**모든 솔버는 `TwoDInputPreprocessor` 진입 경로에서
`SolverUtils2D.AggregateByDims(sheets)` 호환 파사드를 호출** —
`Sheet.Equals`가 구조적이라 동일 dim 행이 여러 개 있으면 `Dictionary<Sheet, _>`에서
키가 충돌해 인벤토리가 절반으로 잘리거나 `ArgumentException`이 난다. 외부에서 시트
리스트를 만들 때 미리 합쳐도 되고, 그대로 넘겨도 솔버 안에서 안전하게 합산된다.

또한 모든 솔버는 성공 조기 반환을 포함해 `Success=true` 반환 전
`TwoDResultFinalizer.FinalizeAndValidate` 를 통과한다.
CG2D / Staged MIP는 정수화 또는 MIP materialization 뒤 `TrimToDemand` 로 과생산 배치를
제거한 다음 validator를 실행한다.

## 유틸리티

### `SolverUtils2D.AggregateByDims`
```csharp
public static List<Sheet> AggregateByDims(List<Sheet> sheets);
```
동일 `(Width, Height)` 행을 한 `Sheet` 로 합쳐 `Quantity` 를 더한다. 모든 2D 솔버의
진입부에서 호출하므로 일반적인 사용자가 직접 호출할 필요는 없으나, 외부 코드가
LP 마스터 같은 하위 컴포넌트에 직접 접근하는 경우 같이 호출해야 한다.

### `SolverUtils2D.TrimToDemand`
```csharp
public static List<CuttingPattern2D> TrimToDemand(
    List<CuttingPattern2D> patterns,
    int[] demand,
    out int[] produced);
```
패턴 multiplicity를 개별 copy로 펼치면서 demand를 초과하는 placement를 제거한다.
CG2D / Staged MIP의 최종 materialization에서 과생산을 제거하는 안전장치다.

### `SolverUtils2D.ValidateSuccessfulResult`
```csharp
public static string? ValidateSuccessfulResult(
    List<Sheet> sheets,
    List<RectOrder> orders,
    SolverOptions2D options,
    SolverResult2D result);
```
성공 결과의 sheet inventory, multiplicity, trim bounds, kerf-aware overlap,
guillotine compliance, order index, 치수/회전 플래그, exact demand coverage를 검증한다.
문제가 없으면 `null`, 문제가 있으면 사용자에게 노출 가능한 오류 문자열을 반환한다.

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
