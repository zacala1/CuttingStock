# 2D Guillotine Cutting Stock — 알고리즘

본 프로젝트는 1D 솔버 3종(Greedy KP / Column Generation / Arc Flow MIP)을 거울 삼아 2D에서도 3종 솔버를 제공한다. 모두 동일 인터페이스 `ICuttingSolver2D` 를 구현한다.

**공통 진입 규약.** 세 솔버는 입력 즉시 `SolverUtils2D.AggregateByDims`를 호출해
동일 `(Width, Height)` 시트 행을 합산한다. `Sheet.Equals`가 구조적이라 분산된 행은
`Dictionary<Sheet, _>` 키 충돌로 인벤토리 절반을 잃거나 `ArgumentException`을 던진다.

**TimeLimit 의미.** `SolverOptions2D.TimeLimitMs`는 솔버 시작 시점 기준 **절대
wall-clock deadline**. warm-start / bootstrap 시간도 예산 안에 포함된다 — 이전에는
`deadline = sw.ElapsedMilliseconds + TimeLimitMs`로 워밍업 시간을 이중 계산했으나
2026-05 수정되어 user-visible 한도와 일치한다.

**Stage 옵션.** `SolverOptions2D.Stage`는 2 또는 3 을 받지만 현재 advisory only —
어떤 솔버도 3-stage 컷을 강제하지 않고 출력은 unrestricted-stage 길로틴이다.
UI는 "2-stage" / "3-stage"로 노출하나 동작은 동일하다.

**성공 결과 검증.** 세 솔버는 `Success=true` 반환 전
`SolverUtils2D.ValidateSuccessfulResult` 를 통과해야 한다. 검증기는 sheet inventory,
pattern multiplicity, trim bounds, kerf-aware overlap, guillotine compliance, order index,
치수/회전 플래그, exact demand coverage 를 확인한다.

| Solver | 특성 | 복잡도 | 출처 |
|---|---|---|---|
| `ShelfGuillotineSolver` | 빠른 휴리스틱 | O(K · N log N) | Coffman et al. 1980; Berkey & Wang 1987 |
| `ColumnGeneration2DSolver` | LP 기반 근최적 | Polynomial/iter, exp. worst | Gilmore & Gomory 1965; Cintra et al. 2008 |
| `StagedMipGuillotineSolver` | 정수 마스터 MIP | NP-hard, 시간 제한 | Vance et al. 1994; Belov & Scheithauer 2006 |

---

## 1. ShelfGuillotineSolver — 선반 휴리스틱

**아이디어.** 각 시트를 가로 컷으로 "선반(shelf)"으로 나누고, 각 선반 안에서 세로 컷으로 아이템을 나란히 배치한다 — 자연스럽게 2-stage 길로틴이 된다.

**구현된 휴리스틱**
- **NFDH** (Next-Fit Decreasing Height): 가장 최근 선반에만 시도
- **FFDH** (First-Fit Decreasing Height): 모든 열린 선반 중 첫 번째 fit
- **BFDH** (Best-Fit Decreasing Height): 잔여 영역이 가장 작은 선반

**정렬 규칙** (5종 모두 시도)
- 높이 내림차순, 너비 내림차순, 면적 내림차순, 둘레 내림차순, 긴 변 내림차순

**회전 처리.** 글로벌 + 아이템별 옵션이 모두 활성일 때, 아이템을 사전에 "긴 변이 높이가 되도록" 회전시켜 선반 효율을 높인다. 새 선반을 열 때도 두 방향을 모두 시도하여 더 짧은 선반을 선호한다.

**선택 전략.** `5 × 3 = 15` 조합을 모두 실행하고 총 폐기 면적이 가장 작은 결과를 채택.

**성능.** 일반적으로 80–90% 효율을 빠르게(<10ms 수준) 달성. 1D `GreedyKnapsackSolver`의 2D 대응.

**참고**
- Coffman, Garey, Johnson, Tarjan, "Performance bounds for level-oriented two-dimensional packing algorithms," *SIAM J. Computing* 9(4), 1980.
- Berkey, Wang, "Two-dimensional finite bin-packing algorithms," *JORS* 38(5), 1987.
- Lodi, Martello, Vigo, "Recent advances on two-dimensional bin packing problems," *DAM* 123, 2002.

---

## 2. ColumnGeneration2DSolver — Gilmore-Gomory 칼럼 생성

**문제 분해**
```
Master LP:   min  Σ_p s_p · x_p
             s.t. Σ_p a_pi · x_p ≥ d_i      ∀ i
                  x_p ≥ 0

Pricing:     max  Σ_i π_i · a_i
             s.t. (a_i)는 시트에 길로틴 packing 가능
```

여기서 `s_p` 는 패턴 `p` 에서 사용한 시트의 면적, `a_pi` 는 패턴이 담은 아이템 `i` 의 개수, `π_i` 는 마스터 LP 의 듀얼.

**Pricing 의 핵심 — 2D 길로틴 knapsack DP** (`GuillotineKnapsackDp`)

Beasley 1985 의 정규(normal) 컷 DP:
```
F(W, H) = max {
    v_i                              ∀ 아이템 i 가 (W,H) 에 들어가면
    F(x, H) + F(W − x − kerf, H)     ∀ x ∈ X \ {0, W}    (수직 컷)
    F(W, y) + F(W, H − y − kerf)     ∀ y ∈ Y \ {0, H}    (수평 컷)
}
```

**정규 집합(normal set)** `X`, `Y` — 아이템 너비/높이 합으로 도달 가능한 좌표만 모은 집합.
kerf가 있을 때는 `50 + 5 + 50 = 105` 같은 interior-kerf extent도 포함한다.
`O(W · H)` 를 `O(|X| · |Y|)` 로 축소 (Christofides & Whitlock 1977; Beasley 1985).

회전이 허용된 아이템은 두 방향(원본 + 회전)을 별개 "아이템"으로 등록한다.

**Master LP 풀이.** OR-Tools GLOP (open-source revised simplex). LP 듀얼이 각 주문의 잠재가격 `π_i` 를 제공한다.

**컬럼 생성 루프**
1. Shelf 휴리스틱 결과를 워밍 패턴으로 마스터 채움
2. 마스터 LP 풀고 듀얼 `π` 획득
3. 각 시트 종류에 대해 pricing DP 풀고 reduced cost `s_p − Σ π_i · a_i^new < 0` 면 컬럼 추가
4. 개선 컬럼 없으면 종료 (또는 시간 제한)

**정수화.** LP 해를 floor 한 뒤 잔여 demand 는 ShelfGuillotineSolver 로 채운다
(1D `ColumnGenerationSolver` 와 동일 패턴). materialization 마지막에는
`SolverUtils2D.TrimToDemand` 로 과생산 배치를 제거하고, 그래도 exact coverage가 아니면
공통 validator가 실패로 전환한다.

**참고**
- Gilmore, Gomory, "Multistage cutting stock problems of two and more dimensions," *Operations Research* 13(1), 1965.
- Beasley, "Algorithms for unconstrained two-dimensional guillotine cutting," *JORS* 36(4), 1985.
- Cintra, Miyazawa, Wakabayashi, Xavier, "Algorithms for two-dimensional cutting stock and strip packing problems using DP and column generation," *EJOR* 191, 2008.

---

## 3. StagedMipGuillotineSolver — Pattern Pool + Integer Master (CBC)

칼럼 생성으로 패턴 풀을 만든 뒤 **정수 마스터** 를 OR-Tools CBC 로 직접 풀어 길로틴 cutting stock 의 정수 최적해(또는 시간 제한 내 베스트)를 찾는다.

**파이프라인**
1. **Bootstrap.** Shelf 휴리스틱으로 패턴 풀 초기화
2. **Column Generation Warm-up.** 시간 예산의 절반 동안 LP 마스터-pricing 사이클을 돌려 풀에 좋은 패턴을 추가
3. **Diversification.** 듀얼 외에도 demand × area 기반 가중치에 jitter 를 더해 6 라운드 추가 pricing — 풀의 다양성 증가
4. **Integer Master.**
   ```
   min   Σ_p s_p · x_p + M · Σ_i o_i
   s.t.  Σ_p a_pi · x_p − o_i = d_i     ∀ i        (정확 충족)
         Σ_{p: sheet=s} x_p ≤ Q_s        ∀ s        (시트 재고)
         x_p ∈ Z_+,  o_i ∈ Z_+
   ```
   `o_i` 는 과생산 슬랙(빅-M 페널티). 풀에 정확 커버 조합이 없을 때만 사용된다.

**시간 제한.** 전체 `TimeLimitMs` 를 CG 워밍업과 IP 풀이로 분할(기본 절반/절반). CBC 가 시간 안에 최적해를 못 찾아도 베스트 feasible 을 반환한다.

**정확 수요 materialization.** 정수 마스터는 과생산 slack에 빅-M 페널티를 두지만,
풀 조합상 과생산 배치가 남을 수 있다. 출력 직전 `TrimToDemand` 로 demand 밖 배치를
제거하고 `ValidateSuccessfulResult` 로 다시 검증한다.

**참고**
- Vance, Barnhart, Johnson, Nemhauser, "Solving binary cutting stock problems by column generation and branch-and-bound," *Comp. Optim. Appl.* 3, 1994.
- Belov, Scheithauer, "A branch-and-cut-and-price algorithm for one-dimensional stock cutting and two-dimensional two-stage cutting," *EJOR* 171, 2006.
- Furini, Malaguti, Thomopulos, "Modeling Two-Dimensional Guillotine Cutting Problems via Integer Programming," *INFORMS J. on Computing* 28(4), 2016.

---

## 길로틴 검증

모든 솔버 출력은 `GuillotineValidator.IsGuillotineCompliant` 를 통과해야 한다.
이 검증은 `SolverUtils2D.ValidateSuccessfulResult` 안에서도 수행된다. 검증기는
Beasley 1985 의 재귀 분리 테스트를 구현 — 부모 직사각형에서 어떤 직사각형도
가로지르지 않는 가로/세로 직선을 찾고, 양쪽 부분에 재귀 적용한다. 핀휠(pinwheel)
같은 비-길로틴 패턴은 즉시 거부된다 (테스트 케이스 참조).

## 벤치마크 결과 (i5-14600KF, .NET 10, BenchmarkDotNet v0.15.5)

### 실행 속도

| 시나리오 (아이템 수) | Shelf | CG2D | Staged MIP |
|---|---:|---:|---:|
| Small (~10) | **11 us** | 373 us | 2,339 us |
| Medium (~28) | **35 us** | 1,409 us | 4,636 us |
| Large (~74) | **110 us** | 18,260 us | 35,085 us |

### 메모리 (managed, per-operation)

| 시나리오 | Shelf | CG2D | Staged MIP |
|---|---:|---:|---:|
| Small | 45 KB | 193 KB | 214 KB |
| Medium | 101 KB | 464 KB | 527 KB |
| Large | 236 KB | 2,673 KB | 2,902 KB |

### 주요 병목
- **Shelf**: 15 조합 순회 — 순수 메모리 연산, sub-ms
- **CG2D**: Beasley normal-cut DP (`|X|·|Y|` 행렬). Multi-pricing 으로 CG iteration 2~3× 단축
- **Staged MIP**: CG warm-up(60~70%) + CBC 정수 최적화(30~40%). Per-column upper bound 타이트닝 + column dedup 로 branching tree 축소

## 트레이드오프 정리

| | Shelf | CG2D | Staged MIP |
|---|---|---|---|
| 속도 | ★★★ (11~110 us) | ★★ (0.4~18 ms) | ★ (2~35 ms) |
| 품질 | ★★ (~85%) | ★★★ (~95%) | ★★★ (~97%) |
| 결정성 | 결정적 | 결정적 | 시간 의존 |
| 멀티 시트 | ★★★ | ★★★ | ★★★ |
| 외부 의존 | 없음 | OR-Tools(GLOP) | OR-Tools(GLOP+CBC) |
