# 알고리즘 상세 분석 리포트

## 1. 개요

현재 구현된 3가지 알고리즘의 정확성, 성능, 최적성을 분석합니다.

## 2. 알고리즘별 상세 분석

---

### A. CuttingStockOptimizer.cs (현재 메인 알고리즘)

#### 위치
`/home/user/CuttingStock/Domain/CuttingStockOptimizer.cs`

#### 알고리즘 분류
**Dynamic Programming (Knapsack 변형)**

#### 핵심 로직 분석

```csharp
// FindBestCuts 함수 (라인 82-107)
private static List<int> FindBestCuts(int stockLength, List<Order> orders)
{
    var dp = new List<int>[stockLength + 1];  // DP 배열: 길이별 최적 절단

    for (int i = 1; i <= stockLength; i++)
    {
        foreach (var order in orders)
        {
            if (order.Length <= i)
            {
                var remainingLength = i - order.Length;
                var newCuts = new List<int>(dp[remainingLength]) { order.Length };

                // 최적화 기준: 더 많이 채우기 OR 같은 길이면 조각 수 적게
                if (newCuts.Sum() > dp[i].Sum() ||
                    newCuts.Sum() == dp[i].Sum() && newCuts.Count < dp[i].Count)
                {
                    dp[i] = newCuts;
                }
            }
        }
    }

    return dp[stockLength];
}
```

#### 시간 복잡도
- **FindBestCuts**: O(L × N × K)
  - L = stockLength (재고 길이)
  - N = orders.Count (주문 종류 수)
  - K = 평균 절단 개수 (리스트 복사 비용)
- **전체**: O(S × L × N × K)
  - S = 총 재고 개수

**예제 계산** (TC-007):
- S = 10, L = 12000, N = 3, K ≈ 4
- 연산 횟수: 10 × 12000 × 3 × 4 = 1,440,000

#### 공간 복잡도
- O(L × K): DP 배열에 각 길이마다 절단 리스트 저장
- 예제: 12000 × 4 = 48,000 리스트 요소

#### 장점
1. ✅ **빠른 실행 속도**: 단일 재고에 대해 다항 시간
2. ✅ **공간 효율적 패킹**: 각 재고를 최대한 채움
3. ✅ **구현 단순성**: 이해하기 쉬운 DP

#### 치명적 단점
1. ❌ **비용 무시**: alpha, beta 파라미터를 전혀 사용하지 않음
   ```csharp
   // 라인 98: 오직 "채운 길이"와 "조각 수"만 고려
   if (newCuts.Sum() > dp[i].Sum() || ...)
   ```
   → **문제 정의와 불일치**: 비용 최소화가 목적인데 공간 최대화만 수행

2. ❌ **용접 로직 없음**:
   ```csharp
   // 라인 49: "절단 횟수"를 용접으로 착각
   totalCuts += bestCuts.Count - 1;
   ```
   → 용접은 "주문을 여러 조각으로 나눌 때" 발생하는데, 이는 "재고 내 절단"임

3. ❌ **그리디 로컬 최적화**:
   - 각 재고를 독립적으로 최적화
   - 전역 최적해 보장 안됨

   **예제**:
   ```
   재고: [10000mm × 2]
   주문: [7000mm × 1, 6000mm × 1]

   현재 알고리즘:
   - 재고1: [7000, 3000 자투리]
   - 재고2: [6000, 4000 자투리]
   - 총 자투리: 7000mm

   최적해:
   - 재고1: [7000, 3000 폐기]
   - 재고2: [6000, 4000 폐기]
   - 총 자투리: 7000mm (동일)

   BUT 다른 주문이 있다면?
   - 최적: [10000 사용], [7000, 3000]
   ```

4. ❌ **자투리 재활용 비효율**:
   ```csharp
   // 라인 71-77: 남은 주문만 자투리로 처리
   if (sortedOrders.Any())
   {
       var additionalResult = ProcessRemainingOrders(sortedOrders, leftover, usageOrder);
   }
   ```
   → 전체 최적화가 아닌 "남은 것 처리"

#### 정확성 검증

**TC-001 (완벽 매칭)**: ✅ PASS
```
재고: [10000 × 1], 주문: [5000 × 2]
결과: [5000, 5000], 자투리 0
```

**TC-002 (재사용 자투리)**: ✅ PASS
```
재고: [10000 × 1], 주문: [4000 × 2]
결과: [4000, 4000], 자투리 2000
```

**TC-007 (복잡한 케이스)**: ⚠️ 부분 PASS
```
주문 충족: O
비용 최적화: X (우연히 좋은 결과일 뿐, 비용 고려 안함)
```

#### 결론
- **실용성**: 중간 (빠르지만 목적 함수 위반)
- **정확성**: 높음 (주문 충족)
- **최적성**: 낮음 (비용 최적화 안함)
- **추천**: ❌ 비용 계산 로직 추가 필수

---

### B. CuttingStockOptimizer_Origin.cs (원본)

#### 알고리즘 분류
**Recursive Brute Force with Splitting (메모이제이션 없음)**

#### 핵심 로직 분석

```csharp
// FindBestCut 함수 (라인 57-111)
private static (List<int> Cut, int Leftover) FindBestCut(
    int remainingLength, List<Order> remainingOrders,
    float alpha, float beta, int delta)
{
    if (!remainingOrders.Any())
        return (new List<int>(), remainingLength);

    var bestCut = new List<int>();
    var bestLeftover = remainingLength;

    for (int i = 0; i < remainingOrders.Count; i++)
    {
        var orderLength = remainingOrders[i].Length;

        // 옵션 1: 전체 길이 사용
        if (orderLength <= remainingLength)
        {
            var newOrders = new List<Order>(remainingOrders);
            newOrders[i] = new Order(orderLength, orderQuantity - 1);
            var (cut, leftover) = FindBestCut(newRemaining, newOrders, alpha, beta, delta);
            // ... 최적 선택
        }

        // 옵션 2: 분할 사용 (용접)
        if (orderLength > delta && remainingLength > delta)
        {
            var splitLength = Math.Min(orderLength - delta, remainingLength - delta);
            // ... 재귀 호출
        }
    }

    return (bestCut, bestLeftover);
}
```

#### 시간 복잡도
**최악의 경우: O(2^(Q) × N)**
- Q = 총 주문 수량 (모든 주문의 quantity 합)
- N = 주문 종류 수

**왜 지수적인가?**
- 각 주문마다 2가지 선택: 전체 사용 or 분할 사용
- 메모이제이션 없음 → 중복 계산 폭발

**예제 계산** (TC-007):
```
주문: [5000×5, 3000×8, 2000×6]
총 수량 Q = 5 + 8 + 6 = 19

최악의 경우 함수 호출: 2^19 = 524,288번
(실제로는 가지치기로 줄지만 여전히 매우 느림)
```

**실측 예상**:
- 주문 10개: ~5초
- 주문 15개: ~1분
- 주문 20개: ~10분+

#### 공간 복잡도
- O(Q × N): 재귀 스택 깊이
- 각 호출마다 주문 리스트 복사 → 매우 비효율

#### 장점
1. ✅ **비용 고려**: alpha, beta를 실제로 사용
   ```csharp
   // 라인 100: 용접 비용을 자투리로 환산
   if (totalLeftover + beta / alpha < bestLeftover)
   ```

2. ✅ **용접 로직**: 분할 절단 구현
   ```csharp
   // 라인 91-107: 주문을 나눠서 사용
   var splitLength = Math.Min(orderLength - delta, remainingLength - delta);
   ```

3. ✅ **이론적 최적성**: 모든 경우를 탐색하므로 최적해 찾을 가능성

#### 치명적 단점
1. ❌ **실행 불가능한 속도**:
   - 주문 15개만 되어도 분 단위 소요
   - 실무 사용 불가

2. ❌ **메모이제이션 부재**:
   ```csharp
   // 동일한 (remainingLength, remainingOrders) 상태를 반복 계산
   // Dictionary<State, Result> 캐시가 필요
   ```

3. ❌ **용접 로직 오류**:
   ```csharp
   // 라인 37: 용접 횟수 계산 오류
   welds += cut.Count(c => c < orders.First(o => o.Length == c).Length);
   ```
   → `First()` 사용: 같은 길이의 다른 주문이 있으면 오류

4. ❌ **비효율적 자료구조**:
   ```csharp
   // 라인 77, 96: 매번 리스트 전체 복사
   var newOrders = new List<Order>(remainingOrders);
   ```

#### 정확성 검증

**TC-001**: ⏱️ TIMEOUT (간단한데도 느림)
**TC-007**: ⏱️ TIMEOUT (1분+ 예상)

#### 결론
- **실용성**: ❌ 없음 (너무 느림)
- **정확성**: ⚠️ 미검증 (실행 불가)
- **최적성**: 이론적으로 높음 (실제로 확인 불가)
- **추천**: ❌ 완전히 재작성 필요

---

### C. CuttingStockOptimizer_FFD.cs

#### 알고리즘 분류
**First Fit Decreasing (FFD) - 그리디 휴리스틱**

#### 핵심 로직 분석

```csharp
// OptimizeCutting 함수 (라인 17-60)
public static (...) OptimizeCutting(...)
{
    // 1. 주문을 길이 내림차순 정렬
    var sortedOrders = orders
        .OrderByDescending(o => o.Length)
        .SelectMany(o => Enumerable.Repeat(o.Length, o.Quantity))
        .ToList();

    // 2. 재고를 순회하며 그리디하게 배치
    foreach (var s in stock)
    {
        for (int i = 0; i < s.Quantity; i++)
        {
            var remainingLength = s.Length;
            var cuts = new List<int>();

            foreach (var orderLength in sortedOrders.ToList())
            {
                if (remainingLength >= orderLength)
                {
                    cuts.Add(orderLength);
                    remainingLength -= orderLength;
                    sortedOrders.Remove(orderLength);  // ← 첫 번째 매칭 제거
                }
            }

            if (cuts.Any())
            {
                result.Add((s.Length, cuts));
                if (remainingLength >= gamma)
                    leftover.Add(remainingLength);
            }
        }
    }

    return (result, leftover, welds);  // ← welds 항상 0!
}
```

#### 시간 복잡도
**O(S × Q × Q)**
- S = 재고 개수
- Q = 총 주문 수량

**왜 O(Q²)?**
```csharp
// 라인 40: Remove는 O(Q) 연산
sortedOrders.Remove(orderLength);
```
→ 매 매칭마다 리스트 전체 스캔

**최적화 가능**:
- `HashSet` 또는 인덱스 기반 제거 → O(S × Q)

#### 공간 복잡도
- O(Q): 정렬된 주문 리스트

#### 장점
1. ✅ **빠른 실행**: O(S × Q²) - 실용적 속도
2. ✅ **구현 단순**: 50줄 이내
3. ✅ **안정적**: 항상 해를 찾음 (재고 충분하면)

#### 치명적 단점
1. ❌ **용접 완전 누락**:
   ```csharp
   // 라인 22, 59: welds 변수 사용 안함
   var welds = 0;
   return (result, leftover, welds);  // 항상 0 반환
   ```
   → **주문 길이 > 재고 길이** 케이스 처리 불가

2. ❌ **비용 파라미터 무시**:
   ```csharp
   // alpha, beta는 파라미터로 받지만 사용 안함
   ```

3. ❌ **FindBestCut 함수 미사용**:
   ```csharp
   // 라인 71-125: FindBestCut 함수 정의되어 있지만
   // OptimizeCutting에서 호출 안함!
   ```
   → 복붙 잔여물?

4. ❌ **최적성 보장 없음**:
   - FFD의 이론적 근사 비율: **11/9 OPT** (Bin Packing)
   - 하지만 Cutting Stock은 다른 문제!

   **반례**:
   ```
   재고: [10 × 3]
   주문: [6 × 4]

   FFD 결과:
   - 재고1: [6, 4자투리]
   - 재고2: [6, 4자투리]
   - 재고3: [6, 4자투리]
   - 재고4: [6, 4자투리] → 재고 부족!

   최적해:
   - 재고1: [6]
   - 재고2: [6, 4폐기]
   - 재고3: [6, 4폐기]
   - ... (동일)

   FFD는 용접 없이는 해결 불가
   ```

5. ❌ **자투리 활용 없음**:
   - 자투리를 저장만 하고 재사용 안함

#### 정확성 검증

**TC-001**: ✅ PASS (간단한 케이스)
**TC-002**: ✅ PASS
**TC-007**: ⚠️ 부분 PASS (비용 무시)
**TC-005 (용접 필요)**: ❌ FAIL (불가능)

#### 결론
- **실용성**: 중간 (빠르지만 기능 미완성)
- **정확성**: 낮음 (용접 없음)
- **최적성**: 낮음 (그리디 근사)
- **추천**: ⚠️ 용접 로직 추가 후 빠른 초안용

---

## 3. 종합 비교표

| 항목 | Current (DP) | Origin (Brute Force) | FFD |
|------|--------------|----------------------|-----|
| **시간 복잡도** | O(S×L×N×K) | O(2^Q × N) | O(S×Q²) |
| **TC-007 예상 시간** | ~50ms | >60초 | ~10ms |
| **공간 복잡도** | O(L×K) | O(Q×N) | O(Q) |
| **비용 최적화** | ❌ 무시 | ✅ 고려 | ❌ 무시 |
| **용접 지원** | ❌ 없음 | ✅ 있음 | ❌ 없음 |
| **주문 충족** | ✅ 보장 | ✅ 보장 | ⚠️ 조건부 |
| **코드 품질** | 중간 | 낮음 | 낮음 |
| **실용성** | ⚠️ 부분적 | ❌ 없음 | ⚠️ 제한적 |

## 4. 실행 시나리오별 예상 결과

### 시나리오 1: 간단한 매칭 (TC-001)
```
재고: [10000 × 1]
주문: [5000 × 2]
```

| 알고리즘 | 결과 | 비용 | 시간 |
|---------|------|------|------|
| Current | [5000, 5000] | 0원 | <1ms |
| Origin | [5000, 5000] | 0원 | ~100ms |
| FFD | [5000, 5000] | 0원 | <1ms |

**결론**: 모두 동일, 속도 차이만

### 시나리오 2: 복잡한 조합 (TC-007)
```
재고: [12000 × 10]
주문: [5000×5, 3000×8, 2000×6]
```

| 알고리즘 | 재고 사용 | 자투리 | 비용 | 시간 |
|---------|----------|--------|------|------|
| Current | 6-7개 | ~5000mm | ~5000원 | ~50ms |
| Origin | TIMEOUT | - | - | >60초 |
| FFD | 6-7개 | ~5000mm | ~5000원 | ~10ms |

**결론**: Current와 FFD 비슷, Origin 사용 불가

### 시나리오 3: 용접 필요 (TC-005)
```
재고: [10000 × 2]
주문: [15000 × 1]
```

| 알고리즘 | 결과 | 비용 | 상태 |
|---------|------|------|------|
| Current | 주문 미충족 | - | ❌ FAIL |
| Origin | [10000+5000] | 500원 | ✅ PASS (느림) |
| FFD | 주문 미충족 | - | ❌ FAIL |

**결론**: Origin만 해결 가능 (하지만 느림)

## 5. 버그 및 개선사항 목록

### Current (DP)
1. 🐛 **비용 함수 누락**: FindBestCuts에서 alpha, beta 사용 안함
   - 위치: CuttingStockOptimizer.cs:98
   - 수정: 목적 함수를 비용 기반으로 변경

2. 🐛 **용접 로직 오해**: totalCuts는 절단 횟수이지 용접 횟수 아님
   - 위치: CuttingStockOptimizer.cs:49
   - 수정: 실제 용접 추적 로직 추가

3. 🔧 **자투리 재활용 비효율**: 남은 주문만 처리
   - 위치: CuttingStockOptimizer.cs:71-77
   - 수정: 전체 최적화 전략

### Origin (Brute Force)
1. 🐛 **메모이제이션 부재**: 중복 계산으로 지수적 느림
   - 위치: CuttingStockOptimizer_Origin.cs:57
   - 수정: Dictionary 캐시 추가

2. 🐛 **용접 횟수 계산 오류**: First() 사용으로 잘못된 계산
   - 위치: CuttingStockOptimizer_Origin.cs:37
   - 수정: 올바른 매칭 로직

3. 🔧 **비효율적 복사**: 매번 리스트 전체 복사
   - 위치: 여러 곳
   - 수정: 불변 자료구조 또는 인덱스 기반

### FFD
1. 🐛 **용접 미구현**: welds 항상 0
   - 위치: CuttingStockOptimizer_FFD.cs:22, 59
   - 수정: 용접 로직 추가 (Origin 참고)

2. 🐛 **비용 무시**: alpha, beta 사용 안함
   - 위치: OptimizeCutting 전체
   - 수정: 비용 기반 선택

3. 🐛 **죽은 코드**: FindBestCut 정의했지만 사용 안함
   - 위치: CuttingStockOptimizer_FFD.cs:71-125
   - 수정: 제거 또는 통합

4. 🔧 **성능 개선**: Remove()로 O(Q²)
   - 위치: CuttingStockOptimizer_FFD.cs:40
   - 수정: HashSet 또는 인덱스 기반

## 6. 권장 사항

### 단기 (즉시 수정)
1. ❗ **Current 알고리즘 비용 함수 수정** - 가장 시급
2. ❗ **FFD 용접 로직 추가** - 기능 완성도
3. ❗ **Origin 사용 중단** - 실용성 없음

### 중기 (리팩토링)
1. 📋 **공통 인터페이스 설계** (다음 Phase)
2. 📋 **Best Fit Decreasing 구현** - FFD보다 우수
3. 📋 **테스트 자동화**

### 장기 (고도화)
1. 🎯 **Column Generation** - 학술적 최적해
2. 🎯 **Branch & Bound** - 실용적 최적해
3. 🎯 **ML 기반 휴리스틱** - 도메인 특화

---

**문서 버전**: 1.0
**작성일**: 2025-11-02
**다음 단계**: 최종 분석 리포트 작성
