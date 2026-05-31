# 1D Cutting Stock Algorithms

세 솔버 모두 `ICuttingSolver` 를 구현하고 동일한 `SolverOptions` 를 받는다.

## Solvers

### 1. `GreedyKnapsackSolver`

동일 길이 입력을 합산한 뒤 다중 패스 bounded knapsack DP + 2-opt 후처리를 수행한다.

- **입력 정규화**: 같은 길이의 `RebarStock` / `Order` row는 solver 진입부에서 quantity를 합산
- **Pass 1** (balanced): 주문당 최대 2 cut, scarcity-first 정렬
- **Pass 2** (residual): 주문당 최대 5 cut
- **Pass 3** (fill): 제한 없음
- **Post-processing**: 2-opt swap + relocate (계획 간 cut 이동으로 폐기 감소)
- **Kerf**: 인접 cut 사이에 소비. `ComputeLeftover` 공식 일관 적용
- **Welding**: 활성화 시 stock 길이 초과 주문을 여러 조각으로 분할
- **부분 조각 호스트 (`FindHostPlanForWeld`)** — 용접 그룹의 마지막 partial 조각은
  기존 비-용접 plan의 leftover에 들어갈 수 있으면 새 재고를 안 쓴다. 가장 작은
  여유 plan을 선택해 큰 leftover를 보존한다.
- **짧은 tail 재분배** — 마지막 partial 조각이 `Delta` 미만이면 직전 조각을 줄여
  `5000 + 4100 + 1000 = 10100mm` 같은 유효한 용접 split을 찾는다.

복잡도: O(N × L × Passes). 일반적 입력에서 빠름 (밀리초 단위).

**알려진 한계**: 단일 길이 대량 demand(예: 1000× 동일 cut) 시 Pass1의 maxPerOrder=2 캡 때문에
최적의 2~3배 bar를 쓰는 경향. CG/ArcFlow는 같은 케이스에서 최적에 근접.

### 2. `ColumnGenerationSolver`

Gilmore-Gomory 칼럼 생성 + 커스텀 Simplex 마스터.

- **Master LP**: `min Σ x_p` s.t. demand 충족, `x_p ≥ 0`
- **초기 컬럼**: identity (각 주문 1개) + **kerf-aware 그리디 패턴**
  (이전에는 kerf를 무시해 over-pack 가능했음 — 2026-05 수정)
- **Pricing**: 1D bounded knapsack DP (kerf 포함 weight = `length + kerf`, expanded capacity = `stock + kerf`)
- **Rounding**: floor LP → 잔여 demand 그리디 (`ApplyGreedyResidual`)
- **Multi-stock**: stock 길이별 sub-problem
- **단일-stock 안전장치**: order length > stock length 인 주문은 LP 진입 전 필터 —
  이전에는 invalid 패턴이 LP에 들어가 false `Success=true` 를 반환할 수 있었음 (2026-05 수정)

복잡도: iteration당 polynomial, 최악 지수. Ref: Gilmore & Gomory 1961.

### 3. `ArcFlowSolver`

Arc Flow 네트워크 + OR-Tools SCIP MIP.

- **그래프**: nodes 0..`stockLength + kerf`, item arc 의 폭 = `length + kerf`.
  capacity 를 `stock + kerf` 로 확장해 첫 cut 가장자리 kerf를 잘못 과금하지 않는다.
- **GCD 압축**: stockLength, itemLength, kerf의 GCD로 노드 수 축소
- **Multi-stock**: stock 길이별 sub-graph, 단일 MIP
- **Time limit**: 30s 내부 한도 (`MipTimeLimitMs` 상수)
- **Trim**: 결과 추출 시 demand 초과 cut을 잘라냄

복잡도: exact (NP-hard, 시간 제한 bounded). Ref: Valerio de Carvalho 1999.

distinct length가 많거나 kerf 때문에 GCD가 작아지면 MIP 그래프가 폭증해 30s 한도에 도달할 수 있다.
실무 입력(고정된 표준 길이 팔레트, 큰 quantity)에서는 빠르게 최적해를 찾는다.

## 성공 결과 검증

세 솔버는 `Success=true` 를 반환하기 직전 `SolverUtils.ValidateSuccessfulResult` 를 통과해야 한다.
검증 항목은 다음과 같다.

- 각 plan의 소비 길이 = `Σ cuts + (cuts.Count - 1) * kerf` 가 stock 길이를 넘지 않음
- `Leftover` 가 `SolverUtils.ComputeLeftover` 결과와 일치
- 비-용접 cut과 용접 group 합계가 입력 demand를 정확히 충족
- 용접 group은 2개 이상 조각이고 각 조각 길이가 `Delta` 이상
- 실제 stock length별 사용량이 입력 inventory를 초과하지 않음

검증 실패 시 해당 solver 결과는 실패로 바뀌며 `ErrorMessage` 에 원인이 들어간다.

## 파라미터

| 이름 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| Alpha | float | 1.0 | mm 폐기당 비용 (>= 0) |
| Beta | float | 500 | 용접당 비용 (>= 0) |
| Gamma | int | 100 | 재사용 가능 자투리 최소 길이 mm (>= 0) |
| Delta | int | 100 | 용접 가능 조각 최소 길이 mm (> 0) |
| Kerf | int | 0 | 톱날 두께 mm (>= 0) |
| UsageOrder | enum | SmallToLarge | stock 소비 순서 |
| EnableWelding | bool | false | stock 초과 주문 분할 허용 |

## 비용 공식

```
TotalCost   = round( WasteLength × Alpha + WeldCount × Beta )    // long
WasteLength = Σ leftovers < Gamma
WeldCount   = Σ (group_size − 1) per weld group
```

## Kerf 규약

`N` 개 cut이 한 bar에 들어갈 때 총 소비 = `Σ lengths + (N − 1) × kerf`.
첫 cut은 가장자리에서 kerf를 소비하지 않는다. 모든 솔버와 검증 코드는
`SolverUtils.ComputeLeftover` 헬퍼로 동일 공식을 쓴다.

## 솔버 선택 가이드

| 우선순위 | 솔버 |
|---|---|
| 속도 (실시간 미리보기) | `GreedyKnapsackSolver` |
| 품질 (LP 최적 근접) | `ColumnGenerationSolver` |
| 증명된 최적 (작은~중간 입력) | `ArcFlowSolver` |
| 용접 필요 | `GreedyKnapsackSolver` (다른 두 솔버는 미지원) |
| 대량 동일 cut | `ColumnGenerationSolver` / `ArcFlowSolver` (Greedy는 비최적) |

2D 알고리즘은 `docs/2D_ALGORITHMS.md` 참조.
