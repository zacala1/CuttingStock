# 2D Guillotine Cutting Stock Problem

## 정의

2차원 길로틴 절단 문제(2D Guillotine Cutting Stock Problem, 2D-GCSP)는 1차원 절단 문제의 자연스러운 확장이다.

**입력**
- 시트 집합 `S = { (W_s, H_s, Q_s) }` — 가용 시트 종류, 각각 가로·세로·재고 수량.
- 주문 집합 `O = { (w_i, h_i, d_i, r_i) }` — 직사각형 아이템, 각각 가로·세로·필요 수량·90° 회전 허용 플래그.
- 파라미터: `kerf` (톱날 두께), `trim` (가장자리 폐기), `α` (mm² 단가).

**제약**
1. 모든 주문 수량 `d_i`가 정확히 충족되어야 한다.
2. 시트 종류 `s`는 `Q_s`매 이하 사용.
3. 모든 절단은 **길로틴(guillotine) 절단** — 즉 직선이 부모 직사각형을 끝에서 끝까지 가로지른다.
4. 인접한 두 컷 사이에는 `kerf` 만큼의 폐기가 발생한다.
5. 각 시트는 가장자리에서 `trim` 만큼 잘라낸 후 사용한다.
6. 회전이 허용된 아이템(`r_i = true` 이고 옵션 `AllowRotation` 활성화)은 90° 회전 배치 가능.

**목표**
사용된 총 시트 면적을 최소화 — 동치로 폐기 면적 `Σ_s |s| − Σ_i d_i · w_i · h_i` 최소화.

비용:
```
TotalCost = TotalWasteArea × α
```

## 길로틴 제약의 의미

산업용 패널 톱(panel saw)은 한 번에 한 직선을 시트 끝까지 자른다 — 자유로운 위치의 부분 컷은 불가능하다. 이 제약은:
- **2-stage**: 시트를 가로 컷으로 strip 으로 나누고, 각 strip 을 세로 컷으로 아이템 으로 나눈다.
- **3-stage**: 위 + strip 양 끝의 트림(trim).

본 프로젝트는 2-stage 또는 3-stage 길로틴을 옵션으로 지원하며, **재귀 분리 가능성** (recursive separability)을 모든 솔버 출력에 강제한다 (`GuillotineValidator` 참조 — Beasley 1985 분리 테스트).

## 1D vs 2D 비교

| 측면 | 1D | 2D |
|---|---|---|
| 아이템 | 길이만 (`length`) | 너비 + 높이 |
| 회전 | 해당 없음 | 90° 회전 (선택) |
| 패턴 | 1차원 컷 시퀀스 | 길로틴 트리 |
| 폐기 | 길이 (mm) | 면적 (mm²) |
| Knapsack | 1D DP | 2D DP (정규 컷) |
| LP 마스터 | Σ a_pi x_p ≥ d_i | 동일 |
| Pricing | 1D bounded knapsack | **2D 길로틴 knapsack** |

## 평가 지표

- **재료 효율(Material Efficiency)**: `사용된 아이템 면적 / 사용된 시트 면적 × 100%`
- **시트 사용 수(Sheets Used)**: 사용된 시트 인스턴스 수
- **총 비용(Total Cost)**: `폐기 면적 × α`
- **실행 시간(Execution Time)**: 솔버 wall-time

## 참고

- Lodi, Martello, Vigo, "Recent advances on two-dimensional bin packing problems," *Discrete Applied Math* 123, 2002.
- Wäscher, Haußner, Schumann, "An improved typology of cutting and packing problems," *EJOR* 183, 2007.
