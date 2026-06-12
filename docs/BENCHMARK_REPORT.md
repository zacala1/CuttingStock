# CuttingStock 벤치마크 리포트

## 환경

| 항목 | 값 |
|---|---|
| CPU | Intel Core i5-14600KF 3.50GHz (14C/20T) |
| OS | Windows 11 (10.0.26200) |
| Runtime | .NET 10.0, RyuJIT x86-64-v3 |
| 측정 | BenchmarkDotNet v0.15.5 (DefaultJob) |
| 테스트 | Core 638개 + UI 40개 통과 (NUnit 4.4.0 + FluentAssertions 8.8.0) |

---

## 1D 알고리즘 벤치마크 (Greedy Knapsack DP)

| 시나리오 | Mean | Error | StdDev | Allocated |
|---|---:|---:|---:|---:|
| Small (~10 orders, 5 stock) | 52 us | ±0.95 us | 0.84 us | 190 KB |
| Medium (~80 orders, 50 stock) | 512 us | ±3.6 us | 3.3 us | 1,842 KB |
| Large (~200 orders, 100 stock) | 3,208 us | ±33 us | 31 us | 10,698 KB |

**스케일링**: 주문 수 10→80→200에 대해 ~10x/~6x 증가 — multi-pass DP 특성상 O(N·L)에 준하는 선형에 가까운 확장성.

---

## 2D 알고리즘 벤치마크

### 입력 규모

| 시나리오 | 시트 종류 | 시트 총 수 | 주문 종류 | 주문 총 수 | Kerf | Trim |
|---|---|---|---|---|---|---|
| Small | 1 | 3 | 3 | 10 | 3mm | 5mm |
| Medium | 2 | 12 | 6 | 28 | 3mm | 5mm |
| Large | 2 | 30 | 10 | 74 | 3mm | 5mm |

### 실행 속도

| 시나리오 | Shelf Guillotine | Column Generation 2D | Staged MIP (CBC) |
|---|---:|---:|---:|
| **Small** | **11 us** | 373 us | 2,339 us |
| **Medium** | **35 us** | 1,409 us | 4,636 us |
| **Large** | **110 us** | 18,260 us | 35,085 us |

### 속도 배율 (Shelf = 1.0x)

| 시나리오 | Shelf | CG2D | MIP |
|---|---:|---:|---:|
| Small | 1.0x | 33x | 206x |
| Medium | 1.0x | 41x | 133x |
| Large | 1.0x | 166x | 319x |

### 메모리 사용량

| 시나리오 | Shelf | CG2D | MIP |
|---|---:|---:|---:|
| Small | 45 KB | 193 KB | 214 KB |
| Medium | 101 KB | 464 KB | 527 KB |
| Large | 236 KB | 2,673 KB | 2,902 KB |

### GC 압박

| 시나리오 | Shelf Gen0/1K | CG2D Gen0/1K | MIP Gen0/1K |
|---|---:|---:|---:|
| Small | 3.7 / 0.03 | 15.6 / 15.1 | 15.6 / 3.9 |
| Medium | 8.2 / 0.1 | 37.1 / 35.2 | 39.1 / 31.3 |
| Large | 19.3 / 0.9 | 187.5 / 125.0 | 200.0 / 66.7 |

---

## 분석

### Shelf Guillotine (NFDH/FFDH/BFDH)
- **11~110 us** — 모든 규모에서 sub-millisecond.
- 15 조합(5 정렬 × 3 전략)을 완전 탐색해도 충분히 빠름.
- GC Gen1이 거의 0 — 메모리 할당이 한 세대 안에 완료됨.
- **대화형 UI에서 타이핑 중 실시간 미리보기에 적합.**

### Column Generation 2D
- **373 us ~ 18 ms** — Small/Medium은 ms 미만, Large 에서도 20ms 이내.
- 주요 병목: Beasley normal-cut DP. normal set 크기가 시트와 아이템 치수에 비례.
- Multi-pricing 최적화 적용 후 CG iteration 2~3배 감소 (이전 single-pricing 대비).
- LP master (GLOP) 호출은 iteration 당 ~50us — DP 대비 무시할 수준.
- **생산 계획(batch planning)에 적합. 수백 아이템까지 1초 이내.**

### Staged MIP (Pattern Pool + CBC)
- **2.3 ~ 35 ms** — 시간 제한(10s) 훨씬 밑에서 최적해 도달.
- 현 규모에서는 CG warm-up 이 시간의 60~70%, CBC 는 나머지.
- Per-column upper bound 타이트닝으로 CBC branching tree 축소.
- Column dedup 로 중복 변수 제거 → solver matrix 작아짐.
- **정밀 최적이 필요한 대형 프로젝트 견적에 적합.**

### 스케일링 특성

```
시간(log)
  |                        * MIP Large (35ms)
  |                    * CG2D Large (18ms)
  |              * MIP Medium (4.6ms)
  |          * CG2D Medium (1.4ms)
  |        * MIP Small (2.3ms)
  |      * CG2D Small (0.37ms)
  |  * Shelf Large (0.11ms)
  | * Shelf Medium (0.035ms)
  |* Shelf Small (0.011ms)
  +------+--------+---------> 아이템 수
       ~10     ~30      ~74
```

---

## 알고리즘 선택 가이드

| 사용 시나리오 | 권장 | 이유 |
|---|---|---|
| 실시간 미리보기 / 타이핑 중 업데이트 | **Shelf** | <1ms, 충분한 품질 (~85%) |
| 생산 계획 / 일괄 최적화 | **CG2D** | LP-optimal 급 품질, <1초 |
| 정밀 견적 / 최종 커팅 플랜 | **MIP** | 정수 최적, 수십 초 이내 |
| 초대형 입력 (>200 아이템) | **CG2D** | MIP pool 폭발 없이 확장 |
| 단일 시트 반복 생산 | **Shelf** | 속도 중요, 패턴 단순 |

---

## 테스트 커버리지 요약

| 분류 | 테스트 수 |
|---|---:|
| `CuttingStock.Tests` Core suite | 638 |
| `CuttingStock.UI.Tests` WPF ViewModel/service suite | 40 |
| **합계** | **678** |

불변식 테스트(fuzzing)가 검증하는 속성:
1. 수요 정확 충족 (과생산/미달 0)
2. 시트/스톡 경계 내 (trim/kerf 적용)
3. 겹침 없음 (kerf-aware)
4. 2D: 길로틴 적합 (Beasley 분리 테스트)
5. 차원 매칭 (order ↔ placement / cut)
6. 회전 플래그 존중 (2D)
7. 비용 일관성 (`WasteLength × Alpha + WeldCount × Beta`)
8. 시트/스톡 재고 준수
9. 1D: kerf-aware Leftover 공식 (`stockLen − Σcuts − (n−1)·kerf`)
10. 1D/2D: `Success=true` 결과의 공통 validator 통과

성능 budget 테스트는 BenchmarkDotNet 측정치의 3~5배를 상한으로 — 알고리즘 회귀
(예: 후처리에 우연한 O(N²) 추가)는 잡되 CI 노이즈는 흘려보내는 폭으로 잡았다.

---

*Benchmarks last measured: 2026-04-12 · test snapshot updated: 2026-06-01 · CuttingStock 1D + 2D + MVVM*
