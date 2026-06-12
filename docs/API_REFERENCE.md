# API Reference

이 문서는 `CuttingStock.Core` 의 공개 표면을 1D / 2D 두 축으로 정리한다.
UI(`CuttingStock.UI`) 측은 별도로 보충한다.

## 1D Cutting Stock

### 인터페이스

```csharp
public interface ICuttingSolver
{
    string Name { get; }
    string Description { get; }
    string TimeComplexity { get; }
    SolverResult Solve(List<RebarStock> stock,
                      List<Order> orders,
                      SolverOptions options,
                      IProgress<double>? progress = null);
}
```

### 솔버

| 클래스 | 알고리즘 | 복잡도 |
|---|---|---|
| `GreedyKnapsackSolver` | 다중 패스 sparse DP + 2-opt 후처리 + 부분 용접 호스트 | O(N × L × Passes) |
| `ColumnGenerationSolver` | Gilmore-Gomory CG + 커스텀 Simplex + knapsack 가격 매김 (kerf-aware) | Poly/iter, exp 최악 |
| `ArcFlowSolver` | DAG 네트워크 + OR-Tools SCIP MIP | Exact (30s 시간 제한) |

모든 1D 솔버는 `Success=true` 반환 전 `SolverUtils.ValidateSuccessfulResult` 를 통과한다.
따라서 성공 결과는 stock inventory, kerf-aware leftover, exact demand coverage, 용접 group
구조와 `Delta` 제약을 만족해야 한다.

### 도메인 타입

```csharp
// 입력 — 모두 immutable, 생성자에서 양의 정수 검증
public sealed class RebarStock { int Length { get; } int Quantity { get; } }
public sealed class Order      { int Length { get; } int Quantity { get; } }

// 설정 — 변경 가능 setter (validation 포함)
public class SolverOptions
{
    float Alpha;              // 자투리 1mm당 비용 (>= 0)
    float Beta;               // 용접 1회당 비용 (>= 0)
    int   Gamma;              // 재사용 가능 자투리 최소 길이 mm (>= 0)
    int   Delta;              // 용접 가능 조각 최소 길이 mm (> 0)
    int   Kerf;               // 톱날 두께 mm (>= 0)
    StockUsageOrder UsageOrder;
    bool  EnableWelding;
}

// 출력
public class SolverResult
{
    string  AlgorithmName;
    List<CuttingPlan> CuttingPlans;
    List<int> ReusableLeftovers;
    int  WasteLength, WeldCount, StockUsed;
    long TotalCost;           // long — 큰 입력에서 오버플로 방지
    double MaterialEfficiency, ExecutionTimeMs;
    bool Success; string? ErrorMessage;
}

public sealed class CuttingPlan
{
    int       StockLength { get; init; }   // init-only
    List<Cut> Cuts        { get; init; }   // init-only (참조), 리스트는 mutate 가능
    int       Leftover    { get; set; }    // 후처리에서 재계산
}

public sealed class Cut
{
    int  Length          { get; init; }
    int  OrderIndex      { get; init; }
    bool RequiresWelding { get; init; }
    int? WeldGroupId     { get; init; }    // not null ⇒ 이 cut은 용접된 조각
}
```

**불변성 원칙**
- `Order` / `RebarStock` 은 setter가 없음. `new Order(length, quantity)` 만 사용.
- `Cut` 은 전체 init-only. `Cuts.Add(new Cut { ... })` 만 가능, 사후 변경 불가.
- `CuttingPlan.StockLength` / `Cuts` 도 init-only. `Leftover` 만 후처리에서 변경됨.

### 비용 공식

```
TotalCost   = round( WasteLength × Alpha + WeldCount × Beta )    // long
WasteLength = ∑ leftover < Gamma                                  // 폐기만
WeldCount   = ∑ (그룹 cut 수 − 1)                                   // 그룹별
```

### Quick Start

```csharp
var stock   = new List<RebarStock> { new(12000, 10) };
var orders  = new List<Order>      { new(5000, 5), new(3000, 8) };
var options = new SolverOptions    { Alpha = 1f, Kerf = 3 };

ICuttingSolver solver = new GreedyKnapsackSolver();
SolverResult result   = solver.Solve(stock, orders, options);
Console.WriteLine(result.GetDetailedReport(options));
```

---

## 2D Guillotine Cutting Stock

### 인터페이스

```csharp
public interface ICuttingSolver2D
{
    string Name { get; }
    string Description { get; }
    string TimeComplexity { get; }
    SolverResult2D Solve(List<Sheet> sheets,
                        List<RectOrder> orders,
                        SolverOptions2D options,
                        IProgress<double>? progress = null);
}
```

### 솔버

| 클래스 | 알고리즘 | 복잡도 |
|---|---|---|
| `ShelfGuillotineSolver` | best-of-15 shelf 휴리스틱 (NFDH/FFDH/BFDH × 5 정렬) | O(K × N log N) |
| `ColumnGeneration2DSolver` | CG + GLOP 마스터 + Beasley 1985 DP 가격 매김 | Poly/iter, exp 최악 |
| `StagedMipGuillotineSolver` | CG 풀 + CBC 정수 마스터 + 다양화 라운드 | NP-hard, 시간 제한 |

**모든 2D 솔버는 입력 즉시 `SolverUtils2D.AggregateByDims`를 호출** — 동일 (Width, Height) 시트 행을 합산한다. `Sheet.Equals`가 구조적이므로 분산된 행은 `Dictionary<Sheet, _>` 키 충돌로 인벤토리 절반을 잃는다.

성공 결과는 `SolverUtils2D.ValidateSuccessfulResult` 로 재검증한다. 검증 항목은 sheet
inventory, pattern multiplicity, trim bounds, kerf-aware overlap, guillotine compliance,
order index, 치수/회전 플래그, exact demand coverage다. CG2D / Staged MIP는 결과
materialization 뒤 `TrimToDemand` 로 과생산 배치를 제거한 다음 이 validator를 통과해야 한다.

### 도메인 타입

```csharp
// 입력 — 모두 immutable
public sealed class Sheet     { int Width, Height, Quantity { get; } long Area { get; } }
public sealed class RectOrder { int Width, Height, Quantity; bool AllowRotation { get; } }

// 설정 — 변경 가능 (validation 포함)
public class SolverOptions2D
{
    int   Kerf;           // 톱날 두께 mm (>= 0)
    int   Trim;           // 시트 각 변 트림 mm (>= 0)
    bool  AllowRotation;  // 글로벌 90° 회전 토글
    float AlphaArea;      // mm² 당 비용 (>= 0)
    int   Stage;          // 2 또는 3 — *advisory only* (현 솔버는 강제 안함)
    int   TimeLimitMs;    // 절대 wall-clock 시간 제한 (> 0). 솔버 시작부터 카운트
    StockUsageOrder UsageOrder;
}

// 출력
public sealed class SolverResult2D
{
    string AlgorithmName;
    List<CuttingPattern2D> Patterns;
    long  TotalWasteArea, TotalUsedArea, TotalSheetArea;
    long  TotalCost;       // long
    int   SheetsUsed;
    double MaterialEfficiency, ExecutionTimeMs;
    bool  Success; string? ErrorMessage;
}

public sealed class CuttingPattern2D
{
    Sheet              Sheet        { get; init; }
    int                Multiplicity { get; init; }
    List<Placement>    Placements   { get; init; }
    GuillotineNode?    Root         { get; init; }   // 옵션: 길로틴 컷 트리
}

public sealed class Placement
{
    int  OrderIndex, X, Y, Width, Height { get; init; }
    bool Rotated                          { get; init; }
}
```

### Quick Start

```csharp
var sheets  = new List<Sheet> { new(2440, 1220, 5) };
var orders  = new List<RectOrder> { new(600, 400, 6), new(800, 300, 4) };
var options = new SolverOptions2D { Kerf = 3, AllowRotation = true };

ICuttingSolver2D solver = new ShelfGuillotineSolver();
SolverResult2D result   = solver.Solve(sheets, orders, options);
Console.WriteLine(result.GetDetailedReport(options));
```

---

## 영속화 (`CuttingStock.Core.Persistence`)

### `ScenarioService`

JSON 직렬화로 입력+옵션 스냅샷을 round-trip. 1D와 2D 별도 스키마, 스키마 태그로 혼용 차단.

```csharp
public static class ScenarioService
{
    // 1D
    public sealed class Scenario1D { Stocks, Orders, Parameters }
    public static void        Save1D(string path, Scenario1D scenario);
    public static Scenario1D  Load1D(string path);   // throws InvalidDataException on schema mismatch

    // 2D
    public sealed class Scenario2D { Sheets, Orders, Options }
    public static void        Save2D(string path, Scenario2D scenario);
    public static Scenario2D  Load2D(string path);
}
```

파일 확장자 권장: `.cstock1d.json` / `.cstock2d.json`. 포맷은 `System.Text.Json` camelCase + indented.

---

## 유틸리티

| 클래스 | 용도 |
|---|---|
| `SolverUtils` | 1D 입력 검증, 동일 길이 stock/order 합산, 정렬, `ComputeLeftover`, 성공 결과 validator, 2-opt 후처리, 용접 그룹 카운트 |
| `SolverUtils2D` | 2D 입력 검증, sheet 정렬, **`AggregateByDims`**, item 확장, `TrimToDemand`, 성공 결과 validator, 겹침/경계 체크 |
| `GuillotineValidator` | Beasley 1985 재귀 분리 테스트 + 트리 구조 검증 |
| `GuillotineKnapsackDp` | 2D 정규 컷 DP — CG 가격 매김 sub-problem |
| `PatternPool` | CG 인프라: 컬럼 dedup, LP 마스터, multi-pricing |
| `PatternBuilder` | flat placement → 길로틴 컷 트리 (1-rect 비-corner 케이스 포함) |

---

## UI 측 (`CuttingStock.UI`)

WPF + MVVM(CommunityToolkit.Mvvm). 핵심 표면:

| 타입 | 역할 |
|---|---|
| `MainViewModel`, `TwoDViewModel` | `[ObservableProperty]` 상태 + `[RelayCommand]` 액션 |
| `StockRow`, `OrderRow`, `SheetRow`, `RectOrderRow` | DataGrid binding용 `ObservableObject` row |
| `IDialogService` / `DialogService` | MessageBox + OpenFile/SaveFile 추상화 |
| `ExportService` | CSV / Excel 출력 (1D + 2D, 단일 + 비교) |
| `VisualizationService` | 1D `SolverResult` → `VisualizationRow`/`Block`/`LegendItem` 변환 |
| `VisualizationRow`, `VisualizationBlock`, `LegendItem` | 막대 시각화용 presentation model (WPF `Brush` 포함) |

View는 LiveCharts 시리즈 빌딩과 2D Canvas 렌더링만 남겨두고, 나머지는 모두 ViewModel + Service로 위임된다.
