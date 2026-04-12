# 개선 로드맵

## 📋 Executive Summary

### 현재 상태
- ✅ 3가지 알고리즘 구현 (Current, Origin, FFD)
- ❌ **모두 치명적 결함 보유**
- ⚠️ 목적 함수 불일치 (비용 최소화 vs 공간 최대화)
- ❌ 용접 로직 미완성 또는 오류

### 핵심 문제
1. **Current (메인)**: 비용 최적화 안함 → 잘못된 목적 함수
2. **Origin**: 너무 느림 (지수적) → 실용성 없음
3. **FFD**: 용접 미구현 → 기능 미완성

### 권장 조치
**즉시**: Current 알고리즘 비용 함수 수정 (2-3시간)
**단기**: 공통 아키텍처 + 새 알고리즘 (1-2주)
**장기**: Column Generation (선택적)

---

## Phase 1: 분석 및 문서화 ✅ (완료)

### 완료 항목
- [x] 알고리즘 3개 상세 분석
- [x] 문제 정의서 작성 (`PROBLEM_DEFINITION.md`)
- [x] 테스트 케이스 설계 (`TEST_CASES.md`)
- [x] 테스트 데이터 생성 (JSON 4개)
- [x] 테스트 자동화 코드 (`AlgorithmTester.cs`)
- [x] 상세 분석 리포트 (`ALGORITHM_ANALYSIS.md`)

### 주요 발견사항

#### 1. 목적 함수 불일치
```
문제 정의: 총 비용 최소화 = (자투리 × α) + (용접 × β)
Current 구현: 채운 길이 최대화 + 조각 수 최소화
```
→ **근본적 설계 오류**

#### 2. 성능 vs 정확성 트레이드오프
| 알고리즘 | 속도 | 기능 완성도 | 비용 최적화 |
|---------|------|-------------|------------|
| Current | ⭐⭐⭐ | ⭐⭐ | ❌ |
| Origin | ❌ | ⭐⭐⭐ | ⭐ |
| FFD | ⭐⭐⭐ | ⭐ | ❌ |

→ **완벽한 알고리즘 없음**

#### 3. 구조적 문제
- 공통 인터페이스 부재
- 테스트 부재
- 문서 부재
- 비용 계산 로직 중복/오류

---

## Phase 2: 긴급 수정 (2-3일 예상)

### 목표
현재 메인 알고리즘(Current)을 실용적으로 사용 가능하게 만들기

### 작업 항목

#### 2.1 비용 함수 수정 ⚠️ HIGH PRIORITY

**파일**: `Domain/CuttingStockOptimizer.cs`

**현재 코드** (라인 98):
```csharp
if (newCuts.Sum() > dp[i].Sum() ||
    newCuts.Sum() == dp[i].Sum() && newCuts.Count < dp[i].Count)
{
    dp[i] = newCuts;
}
```

**수정 방안 1 - 간단 (비용 기반)**:
```csharp
// 비용 = 자투리 × alpha
var newWaste = stockLength - newCuts.Sum();
var oldWaste = stockLength - dp[i].Sum();

if (newWaste < oldWaste ||
    (newWaste == oldWaste && newCuts.Count < dp[i].Count))
{
    dp[i] = newCuts;
}
```

**수정 방안 2 - 완전 (비용 + 용접)**:
```csharp
// DP 상태를 (비용, 절단 리스트)로 변경
var dp = new (int Cost, List<int> Cuts)[stockLength + 1];

// 비용 계산 (자투리 + 용접 추정)
var newCost = (stockLength - newCuts.Sum()) * alpha;
if (newCost < dp[i].Cost)
{
    dp[i] = (newCost, newCuts);
}
```

**예상 효과**:
- ✅ 목적 함수 일치
- ✅ 비용 최적화 시작
- ⚠️ 용접은 여전히 미완성

#### 2.2 용접 로직 수정

**파일**: `Domain/CuttingStockOptimizer.cs`

**현재 오류** (라인 49):
```csharp
totalCuts += bestCuts.Count - 1; // 절단 횟수 (용접 아님!)
```

**수정**:
```csharp
// 용접 추적은 전체 최적화 단계에서 수행
// 개별 재고에서는 절단만 추적
```

**실제 용접 로직은 Phase 3에서 구현** (복잡함)

#### 2.3 테스트 실행 및 검증

**체크리스트**:
- [ ] 수정된 Current 알고리즘으로 TC-001~003 실행
- [ ] 비용 계산 정확성 확인
- [ ] 성능 저하 확인 (50ms → ? )
- [ ] 회귀 테스트 (기존 기능 유지)

**예상 시간**: 1일

---

## Phase 3: 아키텍처 재설계 (3-5일 예상)

### 목표
확장 가능하고 유지보수 쉬운 구조 구축

### 3.1 공통 인터페이스 설계

**파일**: `Domain/IOptimizer.cs` (신규)

```csharp
namespace CuttingStock.Domain
{
    /// <summary>
    /// 절단 최적화 알고리즘 공통 인터페이스
    /// </summary>
    public interface IOptimizer
    {
        /// <summary>
        /// 알고리즘 이름
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 알고리즘 설명
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 최적화 실행
        /// </summary>
        OptimizationResult Optimize(
            List<RebarStock> stock,
            List<Order> orders,
            OptimizationParameters parameters);
    }

    /// <summary>
    /// 최적화 파라미터
    /// </summary>
    public class OptimizationParameters
    {
        public float Alpha { get; set; } = 1.0f;      // 자투리 비용
        public float Beta { get; set; } = 500.0f;      // 용접 비용
        public int Gamma { get; set; } = 1000;         // 재사용 자투리 최소 길이
        public int Delta { get; set; } = 1000;         // 용접 가능 최소 길이
        public StockUsageOrder UsageOrder { get; set; } = StockUsageOrder.SmallToLarge;
    }

    /// <summary>
    /// 최적화 결과
    /// </summary>
    public class OptimizationResult
    {
        // 절단 계획
        public List<CuttingPlan> CuttingPlans { get; set; } = new();

        // 성능 지표
        public int TotalCost { get; set; }
        public int StockUsed { get; set; }
        public int WeldCount { get; set; }
        public int WasteLength { get; set; }
        public List<int> ReusableLeftovers { get; set; } = new();

        // 실행 정보
        public TimeSpan ExecutionTime { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        // 상세 정보
        public double MaterialEfficiency => /* 계산 */;
        public string DetailedReport => /* 생성 */;
    }

    /// <summary>
    /// 개별 절단 계획
    /// </summary>
    public class CuttingPlan
    {
        public int StockLength { get; set; }
        public List<Cut> Cuts { get; set; } = new();
        public int Leftover { get; set; }
    }

    public class Cut
    {
        public int Length { get; set; }
        public int OrderIndex { get; set; }  // 어떤 주문인지
        public bool IsWelded { get; set; }    // 용접 여부
    }

    public enum StockUsageOrder
    {
        SmallToLarge,
        LargeToSmall
    }
}
```

### 3.2 기존 알고리즘 리팩토링

**구조**:
```
Domain/
├── IOptimizer.cs (신규)
├── OptimizationModels.cs (신규 - 공통 타입)
├── Algorithms/
│   ├── DPOptimizer.cs (Current 리팩토링)
│   ├── FFDOptimizer.cs (FFD 리팩토링)
│   ├── BFDOptimizer.cs (신규)
│   └── BranchBoundOptimizer.cs (선택적)
└── Legacy/
    ├── CuttingStockOptimizer.cs (백업)
    ├── CuttingStockOptimizer_Origin.cs
    └── CuttingStockOptimizer_FFD.cs
```

### 3.3 비용 계산 로직 통일

**파일**: `Domain/CostCalculator.cs` (신규)

```csharp
public static class CostCalculator
{
    /// <summary>
    /// 총 비용 계산
    /// </summary>
    public static int CalculateTotalCost(
        OptimizationResult result,
        OptimizationParameters parameters)
    {
        return (int)(result.WasteLength * parameters.Alpha +
                    result.WeldCount * parameters.Beta);
    }

    /// <summary>
    /// 자투리 분류
    /// </summary>
    public static (List<int> Reusable, int WasteTotal) ClassifyLeftovers(
        List<int> leftovers,
        int gamma)
    {
        var reusable = leftovers.Where(l => l >= gamma).ToList();
        var waste = leftovers.Where(l => l < gamma).Sum();
        return (reusable, waste);
    }

    /// <summary>
    /// 재료 효율 계산
    /// </summary>
    public static double CalculateMaterialEfficiency(
        List<Order> orders,
        OptimizationResult result)
    {
        var requiredLength = orders.Sum(o => o.Length * o.Quantity);
        var usedLength = result.StockUsed * /* 평균 재고 길이 */;
        return 100.0 * requiredLength / usedLength;
    }
}
```

### 3.4 UI 개선

**파일**: `MainWindow.xaml.cs`

**추가 기능**:
1. 알고리즘 선택 드롭다운
2. 파라미터 입력 UI (alpha, beta, gamma, delta)
3. 결과 비교 모드 (여러 알고리즘 동시 실행)
4. 결과 시각화 (막대 그래프)

**예상 시간**: 2일

---

## Phase 4: 새 알고리즘 구현 (3-5일 예상)

### 4.1 Best Fit Decreasing (BFD) ⭐ 권장

**개념**:
FFD와 유사하지만, "첫 번째로 들어가는 재고" 대신 "가장 적합한 재고" 선택

**장점**:
- FFD보다 평균 10-15% 더 좋은 결과
- 시간 복잡도 동일 O(S × Q log S)
- 구현 난이도 낮음

**핵심 로직**:
```csharp
// 주문을 큰 것부터 정렬
var sortedOrders = orders.OrderByDescending(o => o.Length);

// 각 재고의 남은 공간 추적
var stockCapacities = stock.Select(s => s.Length).ToList();

foreach (var order in sortedOrders)
{
    // Best Fit: 남은 공간이 order.Length 이상이면서 가장 작은 재고 선택
    var bestIdx = -1;
    var bestRemaining = int.MaxValue;

    for (int i = 0; i < stockCapacities.Count; i++)
    {
        var remaining = stockCapacities[i] - order.Length;
        if (remaining >= 0 && remaining < bestRemaining)
        {
            bestIdx = i;
            bestRemaining = remaining;
        }
    }

    if (bestIdx >= 0)
    {
        // 재고에 주문 배치
        stockCapacities[bestIdx] -= order.Length;
        // ... 절단 계획 추가
    }
}
```

**구현 파일**: `Domain/Algorithms/BFDOptimizer.cs`

**예상 시간**: 1-2일

### 4.2 Column Generation (선택적) 🎓

**개념**:
Linear Programming (LP) 기반 정석 알고리즘
- 이론적으로 최적해 보장 (정수 완화 후)
- Branch & Price로 정수 최적해

**장점**:
- 학술적으로 인정받은 방법
- 대규모 문제에서도 좋은 성능
- 최적성 증명 가능

**단점**:
- 구현 복잡도 매우 높음 (500+ 줄)
- LP 솔버 필요 (Google OR-Tools, CPLEX 등)
- 개발 시간 오래 걸림 (1-2주)

**구현 난이도**: ⭐⭐⭐⭐⭐

**권장 사항**: Phase 5 이후로 연기

### 4.3 Branch & Bound (중간 선택지)

**개념**:
Origin의 재귀 탐색 + 가지치기 + 메모이제이션

**장점**:
- Origin보다 100-1000배 빠름
- 중소 규모에서 최적해 보장
- 이해하기 쉬움

**핵심 개선**:
```csharp
// 메모이제이션 추가
private Dictionary<State, (List<int>, int)> memo = new();

private (List<int>, int) FindBestCutMemo(State state)
{
    if (memo.ContainsKey(state))
        return memo[state];

    var result = FindBestCutRecursive(state);
    memo[state] = result;
    return result;
}

// 상태 정의
public struct State : IEquatable<State>
{
    public int RemainingLength;
    public Dictionary<int, int> RemainingOrders; // Length → Quantity
    // GetHashCode, Equals 구현
}

// 가지치기 (Bounding)
private bool ShouldPrune(State state, int currentBestCost)
{
    var lowerBound = CalculateLowerBound(state);
    return lowerBound >= currentBestCost; // 더 나빠질 것 같으면 가지치기
}
```

**예상 시간**: 2-3일

---

## Phase 5: 성능 최적화 (2-3일 예상)

### 5.1 벤치마크 시스템

**파일**: `Tests/Benchmark.cs`

**기능**:
- 규모별 성능 측정 (Small/Medium/Large)
- 알고리즘 간 비교
- 메모리 사용량 측정
- 결과 CSV/차트 출력

**도구**: BenchmarkDotNet (NuGet)

### 5.2 프로파일링

**목표**:
- 병목 지점 파악
- 불필요한 복사 제거
- 자료구조 최적화

**예상 개선**:
- FFD: O(S×Q²) → O(S×Q log S)
- DP: 메모리 50% 절감

### 5.3 병렬화 (선택적)

**개념**:
재고별 독립 계산 → 병렬 처리

```csharp
Parallel.ForEach(stock, stockItem =>
{
    var bestCuts = FindBestCuts(stockItem.Length, orders);
    // Thread-safe 결과 저장
});
```

**예상 효과**: 2-4배 속도 향상 (멀티코어)

---

## Phase 6: 고도화 (장기)

### 6.1 ML 기반 휴리스틱 학습

**개념**:
과거 최적 해를 학습하여 빠른 초기 해 생성

**기술 스택**:
- ML.NET
- 강화 학습 (DQN, PPO)

### 6.2 웹 서비스화

**아키텍처**:
```
Frontend (Blazor/React)
    ↓
API (ASP.NET Core)
    ↓
CuttingStock.Domain (현재 코드)
```

### 6.3 3D 시각화

**도구**: Three.js, Unity

---

## 우선순위 매트릭스

| Phase | 중요도 | 긴급도 | 난이도 | 예상 시간 | 권장 순서 |
|-------|--------|--------|--------|----------|-----------|
| **Phase 2** (긴급 수정) | ⭐⭐⭐ | ⭐⭐⭐ | ⭐ | 2-3일 | 1 |
| **Phase 3** (아키텍처) | ⭐⭐⭐ | ⭐⭐ | ⭐⭐ | 3-5일 | 2 |
| **Phase 4.1** (BFD) | ⭐⭐ | ⭐⭐ | ⭐ | 1-2일 | 3 |
| **Phase 4.3** (B&B) | ⭐⭐ | ⭐ | ⭐⭐⭐ | 2-3일 | 4 |
| **Phase 5** (성능) | ⭐ | ⭐ | ⭐⭐ | 2-3일 | 5 |
| **Phase 4.2** (Column Gen) | ⭐ | - | ⭐⭐⭐⭐⭐ | 1-2주 | (보류) |

---

## 구체적 다음 단계

### 옵션 A: 빠른 수정 (2-3일)
```
1. Current 알고리즘 비용 함수 수정 (4시간)
2. 테스트 실행 및 검증 (2시간)
3. UI 파라미터 입력 추가 (4시간)
4. 문서 업데이트 (2시간)
```
**추천 대상**: 빠르게 사용 가능한 버전 필요

### 옵션 B: 제대로 된 리팩토링 (1-2주)
```
Week 1:
  Mon-Tue: 공통 인터페이스 설계
  Wed-Thu: 기존 알고리즘 리팩토링
  Fri: BFD 구현

Week 2:
  Mon-Tue: Branch & Bound 구현
  Wed: 테스트 자동화
  Thu: UI 개선
  Fri: 문서화 및 배포
```
**추천 대상**: 장기적으로 유지보수할 시스템

### 옵션 C: 학습 중심 (2-3주)
```
Week 1-2: 옵션 B와 동일
Week 3: Column Generation 구현 (도전 과제)
```
**추천 대상**: 학술적 완성도 중요

---

## 성공 기준

### Phase 2 완료 시
- [ ] TC-001~007 모두 PASS
- [ ] 비용 계산 정확 (±5% 오차)
- [ ] 성능 저하 없음 (<100ms)

### Phase 3 완료 시
- [ ] 최소 3개 알고리즘 인터페이스 구현
- [ ] UI에서 알고리즘 선택 가능
- [ ] 단위 테스트 커버리지 >80%

### Phase 4 완료 시
- [ ] BFD가 FFD보다 10% 이상 개선
- [ ] Branch & Bound가 소규모에서 최적해 보장

---

## 리스크 및 대응

### 리스크 1: 용접 로직 복잡도
**영향**: 높음
**대응**: Origin 코드 참고, 단순 케이스부터 구현

### 리스크 2: 성능 저하
**영향**: 중간
**대응**: 벤치마크 먼저 수행, 최적화 단계 분리

### 리스크 3: 프로젝트 범위 확대
**영향**: 중간
**대응**: MVP 먼저 완성, 추가 기능은 Phase 구분

---

**문서 버전**: 1.0
**작성일**: 2025-11-02
**상태**: Phase 1 완료, Phase 2 대기 중
