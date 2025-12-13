# 📚 철근 절단 최적화 프로젝트 문서

## 개요

이 디렉토리는 Cutting Stock Problem (철근 절단 최적화) 프로젝트의 분석 및 개선 문서를 포함합니다.

## 📄 문서 목록

### 1. [PROBLEM_DEFINITION.md](./PROBLEM_DEFINITION.md) 🎯
**문제 정의서** - 가장 먼저 읽어야 할 문서

**내용**:
- 비즈니스 배경 및 도메인 설명
- 입력 데이터 형식 (재고, 주문, 파라미터)
- 제약 조건 (물리적, 용접, 자투리)
- 목적 함수 (총 비용 = 자투리 + 용접)
- 현재 구현 상태 개요

**읽는 시간**: 10분

---

### 2. [ALGORITHM_ANALYSIS.md](./ALGORITHM_ANALYSIS.md) 🔬
**알고리즘 상세 분석 리포트**

**내용**:
- 현재 구현된 3가지 알고리즘 깊이 분석
  - **Current (DP)**: 시간 복잡도, 장단점, 버그
  - **Origin (Brute Force)**: 왜 느린지, 어떻게 고칠지
  - **FFD**: 무엇이 빠졌는지
- 코드 레벨 상세 분석 (라인별)
- 종합 비교표
- 버그 목록 및 수정 방안

**읽는 시간**: 20-30분

**추천 독자**: 개발자, 알고리즘 개선 담당자

---

### 3. [TEST_CASES.md](./TEST_CASES.md) 🧪
**테스트 케이스 설계서**

**내용**:
- 15개 테스트 케이스 상세 정의
  - 기본 케이스 (TC-001~004)
  - 용접 케이스 (TC-005~006)
  - 복잡한 케이스 (TC-007~008)
  - 경계 케이스 (TC-009~012)
  - 성능 테스트 (TC-013~015)
- 검증 체크리스트
- 테스트 실행 계획

**읽는 시간**: 15분

**관련 파일**: `../Tests/TestData/*.json`, `../Tests/AlgorithmTester.cs`

---

### 4. [IMPROVEMENT_ROADMAP.md](./IMPROVEMENT_ROADMAP.md) 🗺️
**개선 로드맵** - 다음에 무엇을 할지

**내용**:
- Phase별 작업 계획 (6단계)
- 우선순위 매트릭스
- 구체적 구현 방안 (코드 예제 포함)
- 예상 소요 시간
- 성공 기준

**읽는 시간**: 25분

**추천 독자**: 프로젝트 관리자, 개발 리더

---

## 🚀 빠른 시작 가이드

### 상황 1: "현재 코드에 무슨 문제가 있는지 알고 싶어요"
→ **[ALGORITHM_ANALYSIS.md](./ALGORITHM_ANALYSIS.md) 섹션 3, 5 읽기**
- 3. 종합 비교표
- 5. 버그 및 개선사항 목록

### 상황 2: "비용 계산이 이상해요"
→ **[PROBLEM_DEFINITION.md](./PROBLEM_DEFINITION.md) 섹션 4 + [ALGORITHM_ANALYSIS.md](./ALGORITHM_ANALYSIS.md) A 섹션**
- 목적 함수 정의 확인
- Current 알고리즘의 치명적 단점 1번 읽기

### 상황 3: "빨리 고치고 싶어요"
→ **[IMPROVEMENT_ROADMAP.md](./IMPROVEMENT_ROADMAP.md) Phase 2**
- 긴급 수정 가이드 (2-3일 소요)
- 코드 예제 포함

### 상황 4: "제대로 다시 만들고 싶어요"
→ **[IMPROVEMENT_ROADMAP.md](./IMPROVEMENT_ROADMAP.md) 전체 읽기**
- Phase 2~4 순차 진행
- 예상 시간: 1-2주

### 상황 5: "테스트부터 돌려보고 싶어요"
→ **[TEST_CASES.md](./TEST_CASES.md) + `../Tests/AlgorithmTester.cs`**
```bash
# .NET 환경에서
cd Tests
dotnet run AlgorithmTester.cs ./TestData
```

---

## 📊 핵심 발견사항 요약

### ❌ 치명적 문제 3가지

1. **비용 최적화 안함** (Current)
   ```
   목표: 비용 최소화
   실제: 공간 최대화
   → 목적 함수 불일치
   ```

2. **실행 불가능한 속도** (Origin)
   ```
   주문 10개: ~5초
   주문 20개: ~10분+
   → 메모이제이션 부재
   ```

3. **용접 미구현** (FFD)
   ```
   재고 < 주문 길이: 처리 불가
   → 기능 미완성
   ```

### ✅ 즉시 조치 사항

| 우선순위 | 작업 | 파일 | 시간 |
|---------|------|------|------|
| 🔴 1 | 비용 함수 수정 | `CuttingStockOptimizer.cs:98` | 4시간 |
| 🟡 2 | 테스트 실행 | `AlgorithmTester.cs` | 2시간 |
| 🟢 3 | 문서 업데이트 | `README.md` (프로젝트 루트) | 1시간 |

---

## 📁 프로젝트 구조

```
CuttingStock/
├── Domain/               # 비즈니스 로직
│   ├── CuttingStockOptimizer.cs          # 현재 메인 (DP)
│   ├── CuttingStockOptimizer_Origin.cs   # 원본 (Brute Force)
│   └── CuttingStockOptimizer_FFD.cs      # FFD
├── Models/              # 데이터 모델
│   ├── Order.cs
│   └── RebarStock.cs
├── Tests/               # 테스트 코드
│   ├── AlgorithmTester.cs
│   └── TestData/
│       ├── TC-001.json
│       ├── TC-002.json
│       ├── TC-003.json
│       └── TC-007.json
├── docs/                # 📚 이 디렉토리
│   ├── README.md                    # ← 지금 읽고 있는 파일
│   ├── PROBLEM_DEFINITION.md
│   ├── ALGORITHM_ANALYSIS.md
│   ├── TEST_CASES.md
│   └── IMPROVEMENT_ROADMAP.md
└── MainWindow.xaml[.cs] # WPF UI
```

---

## 🔧 권장 작업 흐름

### 단기 (이번 주)
```
Day 1: Phase 1 완료 (✅ 완료!)
Day 2-3: Phase 2 - 긴급 수정
  - [ ] 비용 함수 수정
  - [ ] 테스트 실행
  - [ ] 회귀 확인

Day 4-5: 문서화 및 배포
  - [ ] 프로젝트 README 업데이트
  - [ ] 커밋 및 푸시
```

### 중기 (다음 주)
```
Week 2: Phase 3 - 아키텍처 재설계
  - [ ] IOptimizer 인터페이스
  - [ ] 기존 알고리즘 리팩토링
  - [ ] UI 개선
```

### 장기 (이번 달)
```
Week 3-4: Phase 4 - 새 알고리즘
  - [ ] BFD 구현
  - [ ] Branch & Bound (선택)
  - [ ] 성능 벤치마크
```

---

## 📈 예상 성과

### 현재 상태
- ⚠️ 비용 최적화: 작동 안함
- ⚠️ 용접 지원: 미완성
- ✅ 실행 속도: 빠름 (50ms)

### Phase 2 완료 후
- ✅ 비용 최적화: 작동
- ⚠️ 용접 지원: 부분 작동
- ✅ 실행 속도: 유지 (50-100ms)
- **비용 10-20% 개선 예상**

### Phase 4 완료 후
- ✅ 비용 최적화: 완전 작동
- ✅ 용접 지원: 완전 작동
- ✅ 알고리즘 선택: 3가지 이상
- **비용 30-50% 개선 예상**

---

## 🎓 참고 자료

### 학술 자료
1. **Cutting Stock Problem**: Gilmore & Gomory (1961)
2. **Column Generation**: Dantzig-Wolfe 분해
3. **FFD 알고리즘**: Johnson (1973) - 근사 비율 11/9

### 온라인 자료
- [Wikipedia: Cutting Stock Problem](https://en.wikipedia.org/wiki/Cutting_stock_problem)
- [OR-Tools: Bin Packing](https://developers.google.com/optimization/bin/bin_packing)

### 도구
- **Google OR-Tools**: LP/MIP 솔버
- **BenchmarkDotNet**: .NET 성능 측정
- **xUnit**: 단위 테스트

---

## 📞 문의 및 기여

### 버그 리포트
`ALGORITHM_ANALYSIS.md` 섹션 5에 알려진 버그 목록 있음

### 기여 방법
1. `IMPROVEMENT_ROADMAP.md`에서 작업 선택
2. 테스트 작성 (`TEST_CASES.md` 참고)
3. 구현
4. Pull Request

---

## 📝 버전 히스토리

| 버전 | 날짜 | 변경 사항 |
|------|------|-----------|
| 1.0 | 2025-11-02 | Phase 1 완료 - 초기 분석 및 문서화 |

---

**다음 단계**: [IMPROVEMENT_ROADMAP.md](./IMPROVEMENT_ROADMAP.md) Phase 2 시작

**작성자**: Claude (알고리즘 분석 및 리팩토링)
**문서 위치**: `/docs/README.md`
