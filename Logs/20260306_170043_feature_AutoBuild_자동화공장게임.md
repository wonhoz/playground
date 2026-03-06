# Auto.Build — 자동화 공장 미니 퍼즐 게임 구현

- **날짜**: 2026-03-06
- **태그**: feature
- **경로**: `Games/Puzzle/Auto.Build/`

## 목표
Factorio를 단일 화면 10분 퍼즐로 압축한 자동화 공장 퍼즐 게임.
컨베이어 벨트·가공 기계·분류기·합성기를 배치해 목표 아이템을 생산.

## 기술 스택
- WPF Canvas (그리드 스냅, 틱 기반 시뮬레이션)
- net10.0-windows, 외부 NuGet 없음

## 게임 요소
| 기계 | 기능 |
|------|------|
| Spawner | 원료 아이템 주기적 생성 (고정 배치) |
| Belt | 4방향 아이템 이송 컨베이어 |
| Processor | 원료 → 가공품 변환 (2틱 소요) |
| Sorter | 지정 색상 → 한쪽, 나머지 → 직진 분기 |
| Merger | 두 입력 → 하나로 합성 |
| Collector | 목표 아이템 수집 (고정 배치) |

## 스테이지 구성
1. 기초 이송: Spawner → Collector (벨트만)
2. 가공: Red 원료 → Processor → Yellow 수집
3. 분류: 혼합 원료 → Sorter → 각각 수집
4. 합성: 2종 원료 → Merger → 합성품 수집
5. 종합: 복합 파이프라인

## 작업 내역
- [ ] CreateIcon.ps1 + app.ico (기어/공장 테마)
- [ ] Auto.Build.csproj
- [ ] App.xaml + App.xaml.cs
- [ ] MainWindow.xaml (팔레트 + Canvas + 패널)
- [ ] MainWindow.xaml.cs (게임 로직)
- [ ] 빌드 확인
- [ ] 솔루션/publish 등록
- [ ] 커밋
