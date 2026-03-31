# Cutting Stock Optimization

철근 절단 최적화 문제(Cutting Stock Problem)를 해결하는 3가지 알고리즘을 구현한 .NET 8 WPF 데스크톱 애플리케이션입니다.

## 프로젝트 구조

- **CuttingStock.Core**: 알고리즘, 도메인 모델, 유틸리티 (OR-Tools 포함)
- **CuttingStock.UI**: WPF 기반 사용자 인터페이스
- **CuttingStock.Tests**: 단위 테스트 및 통합 테스트 (254개)
- **CuttingStock.Benchmarks**: BenchmarkDotNet 성능 벤치마크

## 구현된 알고리즘

### 1. Greedy Knapsack DP (근최적, 빠름)

Multi-pass 동적 프로그래밍 기반 휴리스틱.

- **전략**: Pass1(균등 분배) -> Pass2(잔여) -> Pass3(채우기) + 후처리(swap/relocate)
- **복잡도**: O(N x L x Passes)
- **장점**: 빠른 속도, 용접 지원, kerf 지원
- **단점**: 전역 최적해 보장 없음

### 2. Column Generation LP (전역 최적화)

Gilmore-Gomory 열 생성법 기반 LP 솔버.

- **전략**: Simplex로 RMP 풀기 -> Knapsack으로 pricing -> Floor-then-Residual 정수 라운딩
- **복잡도**: LP 반복 수에 따라 가변
- **장점**: LP 최적에 근접한 정수해, 대규모 주문 유형에 강함
- **단점**: 커스텀 Simplex 구현으로 수치 안정성 제한

### 3. Arc Flow MIP (정확 최적, OR-Tools)

Arc Flow 네트워크 모델 + SCIP MIP 솔버.

- **전략**: DAG 그래프 모델링 -> GCD 노드 압축 -> MIP 최적해 -> Flow 분해
- **복잡도**: MIP (30초 시간 제한)
- **장점**: 수학적으로 증명된 최적해, kerf 자연 지원, 다중 재고 지원
- **단점**: OR-Tools 의존성, 대규모 문제에서 느릴 수 있음

## 파라미터

| 파라미터 | 설명 | 기본값 |
|---------|------|--------|
| Alpha | 자투리 1mm당 비용 (원/mm) | 1.0 |
| Beta | 용접 1회당 비용 (원/회) | 500 |
| Gamma | 재사용 가능한 최소 자투리 길이 (mm) | 100 |
| Delta | 용접 가능한 최소 조각 길이 (mm) | 100 |
| Kerf | 톱날 두께 (mm). 절단마다 소비되는 재료 손실 | 0 |

## 주요 기능

- 3가지 알고리즘 선택 및 비교
- Kerf(톱날 두께) 지원 - 현실 절단 손실 반영
- 용접 지원 - 긴 주문을 여러 조각으로 분할
- 후처리 최적화 - 2-opt swap + relocate 연산
- 결과 시각화 - 패턴 그룹핑 막대 차트
- CSV/Excel 내보내기
- 엑셀 붙여넣기(Ctrl+V) 지원

## 의존성

- .NET 8.0
- [Google.OrTools](https://developers.google.com/optimization) 9.11 (Arc Flow MIP 솔버)
- LiveChartsCore.SkiaSharpView.WPF (차트)
- ClosedXML (Excel 내보내기)
- NUnit + FluentAssertions (테스트)

## 빌드 및 실행

```bash
dotnet build
dotnet test
dotnet run --project CuttingStock.UI
```

## 라이선스

이 프로젝트는 교육 및 연구 목적으로 작성되었습니다.
