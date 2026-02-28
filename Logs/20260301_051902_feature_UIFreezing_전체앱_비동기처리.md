# [feature] Applications 전체 — UI Freezing 방지 일괄 처리

> 작성: 2026-03-01 05:19 KST
> 태그: `feature`
> 상태: 완료

---

## 목표

`Applications/` 하위 모든 프로젝트에서 UI Freezing 위험 요소 제거.
- 무거운 I/O(파일/폴더 열거, 대량 처리) → `async/await + Task.Run` 래핑
- 진행 상황 표시 (현재 파일명 출력, ProgressBar 등)
- 기존 비즈니스 로직 영향 최소화

---

## 분석 결과

| 위험 수준 | 프로젝트 | 주요 문제 |
|---------|---------|---------|
| 🔴 높음 | Music.Player | AddFiles() 동기 Directory.GetFiles(), RestorePlaylistState() |
| 🟡 중간 | Photo.Video.Organizer | AddFiles() 동기 파일 열거 |
| 🟡 중간 | File.Duplicates | 파일 스캔 동기 호출 |
| 🟡 중간 | Log.Lens | 초기 로그 파일 로드 동기 |
| ✅ 안전 | 나머지 20개 | 이미 async 처리 또는 문제 없음 |

---

## 작업 목록 (Todo)

- [x] 1. Music.Player — AddFiles() + RestorePlaylistState() async 처리 + 진행 표시
- [x] 2. Photo.Video.Organizer — AddFiles() async 처리 + 진행 표시
- [x] 3. File.Duplicates — 파일 스캔 async 처리 (이미 HashScanner는 async)
- [x] 4. Log.Lens — 초기 로그 로드 async 처리

---

## 작업 로그

### 05:19 — 탐색 완료 + 작업 계획 수립

### 05:20~05:35 — 4개 프로젝트 순차 수정

#### Music.Player
- `AddFiles` → `AddFilesAsync`: Directory.GetFiles + TrackInfo.FromFile을 Task.Run으로 래핑
- `RestorePlaylistState` → `RestorePlaylistStateAsync`: TrackInfo.FromFile 루프 배경 처리
- 진행 중 TitleText/ArtistText에 현재 파일명 표시
- 모든 호출부(Drop/버튼/콜백)를 async void + await로 변경

#### Photo.Video.Organizer
- `AddFiles` → `AddFilesAsync`: Directory.GetFiles를 Task.Run으로 래핑
- 탐색 중 StatusText에 "파일 탐색 중... N개" 실시간 표시
- DropZone_Drop, SelectFiles_Click을 async void + await로 변경

#### File.Duplicates
- FileScanner.ScanAsync() 1단계 Directory.EnumerateFiles를 Task.Run으로 래핑
- 수집 시작 시 "파일 목록 수집 중..." 진행 보고 추가
- (해시·이미지 스캔 Progress 보고는 기존 구현 완성도 높아 유지)

#### Log.Lens
- 배경 스레드에서 LogParserService.Parse() 파싱 수행
- 대량 추가(>200줄) 시 LstLog.ItemsSource 임시 분리로 렌더링 이벤트 억제
- 초기 로딩 중 "로딩 중..." 상태 표시

---

## 커밋 이력

| 해시 | 내용 |
|------|------|
| `a52d933` | [music.player] 파일 추가·플레이리스트 복원 UI Freezing 방지 |
| `d13b980` | [photo.video.organizer] 파일 추가 UI Freezing 방지 |
| `e53f889` | [file.duplicates] 파일 목록 수집 UI Freezing 방지 |
| `efd5eca` | [log.lens] 초기 로그 로딩 UI Freezing 방지 |
