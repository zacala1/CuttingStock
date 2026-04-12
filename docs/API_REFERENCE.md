# API Reference

## 1D Cutting Stock

### Interface

```csharp
public interface ICuttingSolver
{
    string Name { get; }
    string Description { get; }
    string TimeComplexity { get; }
    SolverResult Solve(List<RebarStock> stock, List<Order> orders, SolverOptions options, IProgress<double>? progress = null);
}
```

### Solvers

| Class | Algorithm | Complexity |
|---|---|---|
| `GreedyKnapsackSolver` | Multi-pass sparse DP + 2-opt post-processing | O(N * L * Passes) |
| `ColumnGenerationSolver` | Gilmore-Gomory CG + custom Simplex + knapsack pricing | Poly/iter, exp worst |
| `ArcFlowSolver` | DAG network flow + OR-Tools SCIP MIP | Exact (30s limit) |

### Domain Types

```csharp
// Input
public sealed class RebarStock { int Length; int Quantity; }
public sealed class Order { int Length; int Quantity; }

// Configuration
public class SolverOptions {
    float Alpha;      // cost per mm waste
    float Beta;       // cost per weld
    int Gamma;        // min reusable leftover (mm)
    int Delta;        // min weldable piece (mm)
    int Kerf;         // blade width (mm)
    StockUsageOrder UsageOrder;
    bool EnableWelding;
}

// Output
public class SolverResult {
    List<CuttingPlan> CuttingPlans;
    List<int> ReusableLeftovers;
    int WasteLength, WeldCount, TotalCost;
    double MaterialEfficiency, ExecutionTimeMs;
    bool Success; string? ErrorMessage;
}

public class CuttingPlan { int StockLength; List<Cut> Cuts; int Leftover; }
public class Cut { int Length; int OrderIndex; bool RequiresWelding; int? WeldGroupId; }
```

### Quick Start

```csharp
var stock = new List<RebarStock> { new(12000, 10) };
var orders = new List<Order> { new(5000, 5), new(3000, 8) };
var options = new SolverOptions { Alpha = 1, Kerf = 3 };

ICuttingSolver solver = new GreedyKnapsackSolver();
SolverResult result = solver.Solve(stock, orders, options);
Console.WriteLine(result.GetDetailedReport(options));
```

---

## 2D Guillotine Cutting Stock

### Interface

```csharp
public interface ICuttingSolver2D
{
    string Name { get; }
    string Description { get; }
    string TimeComplexity { get; }
    SolverResult2D Solve(List<Sheet> sheets, List<RectOrder> orders, SolverOptions2D options, IProgress<double>? progress = null);
}
```

### Solvers

| Class | Algorithm | Complexity |
|---|---|---|
| `ShelfGuillotineSolver` | Best-of-15 shelf heuristic (NFDH/FFDH/BFDH x 5 sorts) | O(K * N log N) |
| `ColumnGeneration2DSolver` | CG + GLOP master + Beasley 1985 DP pricing | Poly/iter, exp worst |
| `StagedMipGuillotineSolver` | CG-enriched pattern pool + CBC integer master | NP-hard, time-bounded |

### Domain Types

```csharp
// Input
public sealed class Sheet { int Width; int Height; int Quantity; long Area; }
public sealed class RectOrder { int Width; int Height; int Quantity; bool AllowRotation; }

// Configuration
public class SolverOptions2D {
    int Kerf;           // blade width (mm)
    int Trim;           // edge trim per side (mm)
    bool AllowRotation; // global 90-degree toggle
    float AlphaArea;    // cost per mm² waste
    int Stage;          // 2 or 3 (guillotine stages)
    int TimeLimitMs;    // solver time limit
    StockUsageOrder UsageOrder;
}

// Output
public sealed class SolverResult2D {
    List<CuttingPattern2D> Patterns;
    long TotalWasteArea, TotalUsedArea, TotalSheetArea, TotalCost;
    int SheetsUsed;
    double MaterialEfficiency, ExecutionTimeMs;
    bool Success; string? ErrorMessage;
}

public sealed class CuttingPattern2D { Sheet Sheet; int Multiplicity; List<Placement> Placements; }
public sealed class Placement { int OrderIndex, X, Y, Width, Height; bool Rotated; }
```

### Quick Start

```csharp
var sheets = new List<Sheet> { new(2440, 1220, 5) };
var orders = new List<RectOrder> { new(600, 400, 6), new(800, 300, 4) };
var options = new SolverOptions2D { Kerf = 3, AllowRotation = true };

ICuttingSolver2D solver = new ShelfGuillotineSolver();
SolverResult2D result = solver.Solve(sheets, orders, options);
Console.WriteLine(result.GetDetailedReport(options));
```

---

## Utilities

| Class | Purpose |
|---|---|
| `SolverUtils` | 1D: validation, sorting, kerf calc, post-optimization |
| `SolverUtils2D` | 2D: order expansion, overlap check, sheet bounds |
| `GuillotineValidator` | Beasley 1985 recursive separator test |
| `GuillotineKnapsackDp` | Normal-cut DP for CG pricing |
| `PatternPool` | Shared CG: LP master, multi-pricing, column dedup |
| `PatternBuilder` | Flat placements to guillotine cut tree |
| `ExportService` | CSV / Excel export (ClosedXML) |
