# UI 개선 문서

**작성일**: 2025-11-03
**버전**: 2.0
**상태**: ✅ 완료

---

## 📋 개요

Phase 5에서 사용자 인터페이스를 대폭 개선하여 알고리즘 선택, 파라미터 설정, 결과 비교 기능을 추가했습니다.

---

## 🎯 개선 목표

### 이전 버전의 문제점
- ❌ 알고리즘이 하드코딩됨 (Greedy만 사용)
- ❌ 파라미터가 코드에 고정됨
- ❌ 여러 알고리즘 비교 불가
- ❌ 작은 화면 (800x450)
- ❌ 예제 데이터 로드 기능 없음

### 개선 목표
- ✅ 알고리즘 선택 UI
- ✅ 파라미터 입력 UI
- ✅ 알고리즘 비교 모드
- ✅ 개선된 레이아웃 (1200x700)
- ✅ 예제 데이터 로드 기능

---

## 🎨 UI 레이아웃

### 전체 구조

```
┌─────────────────────────────────────────────────────────────┐
│                 철근 절단 최적화 프로그램                      │
├──────────────┬──────────────────────────────────────────────┤
│              │  알고리즘 설정                                 │
│  재고 입력   │  ┌─ 알고리즘 선택                              │
│  ┌────────┐  │  ├─ 파라미터 설정                             │
│  │ DataGrid │  │  └─ [최적화 실행] [알고리즘 비교]           │
│  └────────┘  │                                               │
│  [추가][예제] │  ┌────────────────────────────────┐          │
│              │  │  최적화 결과  │  알고리즘 비교  │          │
│ ─────────── │  ├────────────────────────────────┤          │
│              │  │                                 │          │
│  주문 입력   │  │   결과 표시 영역                │          │
│  ┌────────┐  │  │                                 │          │
│  │ DataGrid │  │  │                                 │          │
│  └────────┘  │  │                                 │          │
│  [추가]      │  └────────────────────────────────┘          │
└──────────────┴──────────────────────────────────────────────┘
```

### 주요 영역

#### 1. 왼쪽 패널 (400px)
- **재고 입력**
  - DataGrid (200px 높이)
  - "재고 추가" 버튼
  - "예제 로드" 버튼 (TC-007 로드)

- **주문 입력**
  - DataGrid (200px 높이)
  - "주문 추가" 버튼

#### 2. 오른쪽 패널 (800px)
- **알고리즘 설정**
  - 알고리즘 선택 드롭다운
  - 파라미터 입력 (Alpha, Beta, Gamma, Delta, UsageOrder)
  - 실행 버튼 (최적화 실행, 알고리즘 비교)

- **결과 표시**
  - TabControl (2개 탭)
    - 탭 1: 최적화 결과 (단일 알고리즘)
    - 탭 2: 알고리즘 비교 (3개 알고리즘 동시)

---

## 🔧 주요 기능

### 1. 알고리즘 선택

**UI 요소**: ComboBox

**옵션**:
1. `Greedy Knapsack DP (최고 품질, 느림)`
2. `First Fit Decreasing (최고 속도)`
3. `Best Fit Decreasing (균형 잡힌 성능/품질)`

**코드**:
```csharp
private IOptimizer GetSelectedOptimizer()
{
    return algorithmComboBox.SelectedIndex switch
    {
        0 => new GreedyKnapsackOptimizer(),
        1 => new FirstFitDecreasingOptimizer(),
        2 => new BestFitDecreasingOptimizer(),
        _ => new GreedyKnapsackOptimizer()
    };
}
```

### 2. 파라미터 설정

**파라미터 목록**:

| 파라미터 | 설명 | 기본값 | 단위 |
|---------|------|--------|------|
| **Alpha** | 자투리 1mm당 비용 | 1.0 | 원/mm |
| **Beta** | 용접 1회당 비용 | 500 | 원/회 |
| **Gamma** | 재사용 가능한 자투리 최소 길이 | 100 | mm |
| **Delta** | 용접 가능한 조각 최소 길이 | 100 | mm |
| **UsageOrder** | 재고 사용 순서 | Small to Large | - |

**UI 레이아웃**:
```
Alpha (자투리 비용): [1.0]    Beta (용접 비용): [500]
Gamma (재사용 최소): [100]    Delta (용접 최소): [100]
재고 사용 순서: [작은 것부터 ▼]
```

**코드**:
```csharp
private OptimizationParameters GetParameters()
{
    return new OptimizationParameters
    {
        Alpha = float.Parse(alphaTextBox.Text, CultureInfo.InvariantCulture),
        Beta = float.Parse(betaTextBox.Text, CultureInfo.InvariantCulture),
        Gamma = int.Parse(gammaTextBox.Text),
        Delta = int.Parse(deltaTextBox.Text),
        UsageOrder = usageOrderComboBox.SelectedIndex == 0
            ? StockUsageOrder.SmallToLarge
            : StockUsageOrder.LargeToSmall
    };
}
```

### 3. 예제 데이터 로드

**기능**: TC-007 테스트 케이스를 자동으로 로드

**데이터**:
- 재고: 12000mm × 20개
- 주문:
  - 5000mm × 10개
  - 4000mm × 15개
  - 3000mm × 12개
  - 2000mm × 8개

**코드**:
```csharp
private void LoadExample_Click(object sender, RoutedEventArgs e)
{
    Stocks.Clear();
    Orders.Clear();

    Stocks.Add(new RebarStock(12000, 20));

    Orders.Add(new Order(5000, 10));
    Orders.Add(new Order(4000, 15));
    Orders.Add(new Order(3000, 12));
    Orders.Add(new Order(2000, 8));

    MessageBox.Show("예제 데이터를 로드했습니다.", "예제 로드",
                    MessageBoxButton.OK, MessageBoxImage.Information);
}
```

### 4. 최적화 실행

**기능**: 선택한 알고리즘으로 최적화 실행

**프로세스**:
1. 입력 검증 (재고, 주문)
2. 파라미터 읽기
3. 알고리즘 선택
4. 최적화 실행
5. 결과 표시
6. 성공 메시지 표시

**결과 형식**:
```
═══════════════════════════════════════════════════
  알고리즘: Greedy Knapsack DP
  시간 복잡도: O(S × L × N)
═══════════════════════════════════════════════════

[상세 리포트]
```

### 5. 알고리즘 비교 모드 ⭐

**기능**: 3가지 알고리즘을 동시에 실행하여 비교

**비교 테이블**:

| 알고리즘 | 총 비용 | 낭비(mm) | 재고 사용 | 효율(%) | 시간(ms) | 순위 |
|---------|---------|----------|-----------|---------|----------|------|
| Greedy Knapsack DP | 1,234 | 1,234 | 12 | 92.34 | 45.123 | 1 |
| BFD | 1,156 | 1,156 | 11 | 93.21 | 7.234 | 2 |
| FFD | 1,289 | 1,289 | 12 | 91.45 | 5.678 | 3 |

**비교 결과 클래스**:
```csharp
public class ComparisonResult
{
    public string AlgorithmName { get; set; }
    public int TotalCost { get; set; }
    public int WasteLength { get; set; }
    public int StockUsed { get; set; }
    public double MaterialEfficiency { get; set; }
    public double ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
    public int Rank { get; set; }  // 1 = 최고
}
```

**순위 계산 로직**:
```csharp
// 총 비용 기준 오름차순 정렬 (낮을수록 좋음)
var sortedResults = ComparisonResults
    .Where(r => r.Success)
    .OrderBy(r => r.TotalCost)
    .ToList();

for (int i = 0; i < sortedResults.Count; i++)
{
    sortedResults[i].Rank = i + 1;
}
```

**프로세스**:
1. 입력 검증
2. 파라미터 읽기
3. 3가지 알고리즘 모두 실행
4. 결과 수집
5. 총 비용 기준으로 순위 계산
6. DataGrid에 요약 표시
7. TextBox에 상세 결과 표시
8. 비교 탭으로 자동 전환
9. 최고 성능 알고리즘 메시지 표시

---

## 📊 UI 개선 전후 비교

### 이전 버전 (v1.0)

```
┌─────────────────────────┐
│  재고 [DataGrid]         │
│  [재고 추가]             │
├─────────────────────────┤
│  주문 [DataGrid]         │
│  [주문 추가]             │
├─────────────────────────┤
│  [계산]                  │
├─────────────────────────┤
│  [결과 TextBox]          │
└─────────────────────────┘
```

**특징**:
- 800x450 크기
- 알고리즘 고정 (Greedy만)
- 파라미터 코드에 하드코딩
- 단일 결과만 표시

### 개선 버전 (v2.0)

```
┌──────────────┬─────────────────────────────┐
│ 재고 입력    │  알고리즘 설정               │
│ [DataGrid]   │  [Greedy/FFD/BFD ▼]         │
│ [추가][예제] │  Alpha: [1.0]  Beta: [500]  │
│              │  Gamma: [100]  Delta: [100] │
│ 주문 입력    │  [최적화 실행] [알고리즘 비교]│
│ [DataGrid]   │                              │
│ [추가]       │  ┌─────────────────────────┐ │
│              │  │ 최적화 결과 │ 비교 결과  │ │
│              │  ├─────────────────────────┤ │
│              │  │   [결과 표시 영역]       │ │
│              │  └─────────────────────────┘ │
└──────────────┴─────────────────────────────┘
```

**특징**:
- 1200x700 크기 (50% 증가)
- 알고리즘 선택 가능 (3가지)
- 파라미터 UI로 입력
- 비교 모드 지원 (DataGrid + 상세 결과)
- 예제 로드 기능
- TabControl로 결과 구분

---

## 🎯 사용자 시나리오

### 시나리오 1: 단일 알고리즘 실행

1. "예제 로드" 버튼 클릭
2. 알고리즘 선택: "Best Fit Decreasing"
3. 파라미터 조정 (선택사항)
4. "최적화 실행" 버튼 클릭
5. 성공 메시지 확인
6. "최적화 결과" 탭에서 결과 확인

### 시나리오 2: 알고리즘 비교

1. "예제 로드" 버튼 클릭
2. 파라미터 조정 (선택사항)
3. "알고리즘 비교" 버튼 클릭
4. "알고리즘 비교" 탭으로 자동 전환
5. DataGrid에서 요약 비교
6. TextBox에서 상세 결과 확인
7. 최고 성능 알고리즘 메시지 확인

### 시나리오 3: 파라미터 영향 분석

1. "예제 로드" 버튼 클릭
2. Gamma = 100으로 설정
3. "알고리즘 비교" 실행 → 결과 확인
4. Gamma = 500으로 변경
5. "알고리즘 비교" 재실행
6. 두 결과 비교 (재사용 자투리 개수 변화)

---

## 🔍 기술적 세부사항

### XAML 구조

**Grid 레이아웃**:
```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="400"/>     <!-- 입력 -->
        <ColumnDefinition Width="*"/>       <!-- 설정 및 결과 -->
    </Grid.ColumnDefinitions>
</Grid>
```

**TabControl**:
```xml
<TabControl>
    <TabItem Header="최적화 결과">
        <TextBox x:Name="resultTextBox" FontFamily="Consolas" />
    </TabItem>
    <TabItem Header="알고리즘 비교">
        <Grid>
            <DataGrid x:Name="comparisonGrid" />  <!-- 요약 -->
            <TextBox x:Name="comparisonTextBox" />  <!-- 상세 -->
        </Grid>
    </TabItem>
</TabControl>
```

### 코드 비하인드

**ObservableCollection 사용**:
```csharp
public ObservableCollection<RebarStock> Stocks { get; set; }
public ObservableCollection<Order> Orders { get; set; }
public ObservableCollection<ComparisonResult> ComparisonResults { get; set; }
```

**데이터 바인딩**:
```csharp
stockGrid.ItemsSource = Stocks;
orderGrid.ItemsSource = Orders;
comparisonGrid.ItemsSource = ComparisonResults;
```

**에러 처리**:
```csharp
try
{
    var parameters = GetParameters();
    // ... 최적화 실행
}
catch (Exception ex)
{
    MessageBox.Show($"오류 발생: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
}
```

---

## 📈 개선 효과

### 사용성
- ✅ 알고리즘 선택 자유도 증가 (1개 → 3개)
- ✅ 파라미터 실시간 조정 가능
- ✅ 예제 데이터로 빠른 테스트
- ✅ 비교 모드로 최적 알고리즘 발견

### 정보 제공
- ✅ 요약 테이블 (DataGrid)
- ✅ 상세 리포트 (TextBox)
- ✅ 순위 자동 계산
- ✅ 성능 지표 (시간, 효율)

### 화면 공간
- ✅ 800x450 → 1200x700 (50% 증가)
- ✅ 2열 레이아웃으로 효율적 활용
- ✅ TabControl로 결과 구분

---

## 🚀 향후 개선 방향

### Phase 6 옵션

#### 1. 시각화 추가
- 막대 그래프 (비용, 효율 비교)
- 절단 계획 다이어그램
- 재고 사용 현황 차트

#### 2. 결과 내보내기
- CSV 파일 저장
- Excel 리포트 생성
- PDF 출력

#### 3. 히스토리 기능
- 과거 실행 결과 저장
- 결과 비교
- 통계 분석

#### 4. 고급 설정
- 알고리즘별 세부 옵션
- 제약 조건 추가
- 목적 함수 커스터마이징

---

## 📝 변경된 파일

### 수정된 파일
- `MainWindow.xaml` (45줄 → 180줄, 300% 증가)
- `MainWindow.xaml.cs` (76줄 → 240줄, 316% 증가)

### 백업 파일
- `MainWindow.xaml.bak` (이전 버전 백업)

---

## ✅ 검증 체크리스트

### 기능 검증
- [x] 알고리즘 선택 동작
- [x] 파라미터 입력 및 적용
- [x] 예제 로드 기능
- [x] 단일 최적화 실행
- [x] 알고리즘 비교 모드
- [x] 순위 계산 정확성
- [x] TabControl 전환
- [x] 에러 처리

### UI 검증
- [x] 레이아웃 반응형
- [x] 폰트 (Consolas) 적용
- [x] DataGrid 정렬
- [x] TextBox 스크롤
- [x] 버튼 배치
- [x] 메시지 박스

---

**문서 버전**: 1.0
**작성자**: Claude (AI Assistant)
**상태**: UI 개선 완료
