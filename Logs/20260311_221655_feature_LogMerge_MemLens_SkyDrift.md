# 작업 로그: Log.Merge / Mem.Lens / Sky.Drift 신규 구현

- **일시**: 2026-03-11 22:16 ~ 23:10 KST
- **태그**: feature
- **커밋**: `79d4e6e`

---

## 작업 개요

3개 신규 프로젝트를 Playground 솔루션에 추가 구현.

---

## 1. Log.Merge — 다중 소스 로그 통합 실시간 뷰어

### 위치
`Applications/Development/Log.Merge/`

### 구현 내용
| 파일 | 설명 |
|------|------|
| `Models/LogSource.cs` | 로그 소스 정보 (파일 경로, 색상, 레이블) |
| `Models/LogEntry.cs` | 단일 로그 줄 (타임스탬프, 레벨, 코릴레이션 ID) |
| `Services/TimestampParser.cs` | 50+ 타임스탬프 포맷 자동 감지 + 레벨 파서 + UUID 추출 |
| `Services/LogSourceWatcher.cs` | FileSystemWatcher 기반 파일 tail-f 감시 |
| `Services/MergeEngine.cs` | 타임스탬프 기준 이진 삽입 병합 엔진 |
| `MainWindow.xaml/.cs` | 좌측 소스 목록 + 우측 통합 타임라인 뷰 |

### 주요 기능
- 다중 파일/폴더 동시 모니터링 (각 소스 색상 구분)
- 타임스탬프 자동 감지 (ISO 8601, syslog, Apache 등 50+ 포맷)
- 통합 타임라인 (이진 삽입으로 시간순 머지)
- 코릴레이션 ID 추적 (UUID 클릭 → 해당 ID 포함 줄 하이라이트)
- 레벨 필터 (FATAL/ERROR/WARN/INFO/DEBUG/기타 토글)
- 정규식 실시간 필터
- 드래그 앤 드롭 파일 추가
- 다크 테마 (액센트 #FF9A3C, 주황)
- VirtualizingStackPanel 대용량 대응

---

## 2. Mem.Lens — 프로세스 메모리 심층 분석기

### 위치
`Applications/System/Mem.Lens/`

### 구현 내용
| 파일 | 설명 |
|------|------|
| `Models/ProcessInfo.cs` | 프로세스 메모리 스냅샷 (Private/WS/Virtual/GC 힙) |
| `Models/MemorySnapshot.cs` | 타임라인 그래프용 시계열 샘플 |
| `Services/ProcessMemoryService.cs` | PSAPI P/Invoke + .NET GC PerformanceCounter |
| `Services/MemoryLeakDetector.cs` | 20샘플 슬라이딩 윈도우 누수 추세 감지 |
| `MainWindow.xaml/.cs` | 프로세스 목록 + 상세 패널 + 타임라인 그래프 |

### 주요 기능
- 프로세스 목록 (Private Bytes 기준 내림차순 정렬)
- Private Bytes / Working Set / Virtual 상세 표시
- .NET GC 힙 (Gen0/Gen1/Gen2/LOH) 비율 바 차트
- 메모리 타임라인 그래프 (30분, Polyline)
- 누수 감지 (5% 이상 지속 증가 → ↑ 표시)
- WorkingSet 트림 기능 (SetProcessWorkingSetSize)
- Markdown 리포트 내보내기
- 자동 새로 고침 (3/5/10/30초)
- 다크 테마 (액센트 #6EFF6E, 초록)

---

## 3. Sky.Drift — 상승기류 타기 글라이더 아케이드

### 위치
`Games/Arcade/Sky.Drift/`

### 구현 내용
| 파일 | 설명 |
|------|------|
| `Engine/GameLoop.cs` | 고정 타임스텝 루프 (16ms, Neon.Run 패턴) |
| `Engine/GliderPhysics.cs` | 날개 양력·중력·기울기 물리 (지수 스무딩) |
| `Engine/Thermal.cs` | 열상승기류 / 하강기류 (가우시안 분포 기류) |
| `Engine/Obstacle.cs` | 장애물 (새떼/폭풍구름/강풍구역) |
| `Engine/ScrollEnvironment.cs` | 절차 생성 환경 (열기류 + 장애물 스폰) |
| `MainWindow.xaml/.cs` | WPF Canvas 2D 렌더링 + 수직 스크롤 |

### 주요 기능
- 수직 무한 스크롤러 (고도 = 점수)
- 글라이더 기울기 제어 (←→ 키)
- 열상승기류: 진입 시 자동 상승 (초록 원형 시각화)
- 하강기류: 회피 필요 (빨간 원형 시각화)
- 장애물: 새떼(이동), 폭풍구름(충돌 시 게임오버), 강풍구역
- 날개 양력 물리 (수평속도 × cos(기울기))
- 역대 최고 고도 기록
- 다크 테마 (배경 #050F23, 글라이더 #64C8FF)

---

## 솔루션 통합

| 항목 | 내용 |
|------|------|
| `Playground.slnx` | 3개 프로젝트 추가 |
| `+publish.cmd` | 40(Log.Merge), 42(Mem.Lens), 63(Sky.Drift) 추가 / 번호 재조정 (1-80) |
| `+publish-all.cmd` | 알파벳 순 3개 추가 |

## 빌드 결과

```
오류 0개 / 경고 0개 (3개 신규 프로젝트)
전체 솔루션 빌드: 오류 0개 / 경고 5개 (기존 프로젝트 경고)
```
