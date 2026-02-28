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

### Phase 1 — 파일/폴더 I/O 작업

| 위험 수준 | 프로젝트 | 주요 문제 |
|---------|---------|---------|
| 🔴 높음 | Music.Player | AddFiles() 동기 Directory.GetFiles(), RestorePlaylistState() |
| 🟡 중간 | Photo.Video.Organizer | AddFiles() 동기 파일 열거 |
| 🟡 중간 | File.Duplicates | 파일 스캔 동기 호출 |
| 🟡 중간 | Log.Lens | 초기 로그 파일 로드 동기 |
| ✅ 안전 | 나머지 20개 | 이미 async 처리 또는 문제 없음 |

### Phase 2 — 비파일 작업 (심층 분석)

| 위험 수준 | 프로젝트 | 주요 문제 |
|---------|---------|---------|
| 🔴 높음 | DNS.Flip | RunNetsh() → proc.WaitForExit(10000) 최대 30s 블로킹 |
| 🔴 높음 | Stay.Awake | SimulateActivity() → Thread.Sleep(110ms) UI 스레드 블로킹 |
| 🟡 중간 | Hex.Peek | HexDocument.Load() → ReadAllBytes(50MB) UI 스레드 동기 I/O |
| 🟡 중간 | QR.Forge | QrService.Render() → ZXing + SkiaSharp CPU-bound, 매 키입력마다 실행 |
| 🟡 중간 | Env.Guard | LoadPathList() → Directory.Exists() 네트워크 드라이브 hang |
| ✅ 안전 | Char.Art | Task.Run + CancellationToken 디바운스 이미 구현됨 |

---

## 작업 목록 (Todo)

### Phase 1
- [x] 1. Music.Player — AddFiles() + RestorePlaylistState() async 처리 + 진행 표시
- [x] 2. Photo.Video.Organizer — AddFiles() async 처리 + 진행 표시
- [x] 3. File.Duplicates — 파일 스캔 async 처리 (이미 HashScanner는 async)
- [x] 4. Log.Lens — 초기 로그 로드 async 처리

### Phase 2
- [x] 5. Stay.Awake — SimulateActivity() Task.Run 래핑 (Thread.Sleep 110ms)
- [x] 6. DNS.Flip — RunNetshAsync + ApplyPresetAsync (WaitForExitAsync)
- [x] 7. Hex.Peek — OpenFileAsync + HexDocument.Load Task.Run 래핑
- [x] 8. QR.Forge — GenerateQr async 변환 + 150ms 디바운스
- [x] 9. Env.Guard — LoadPathListAsync + Directory.Exists Task.Run 래핑

---

## 작업 로그

### 05:19 — 탐색 완료 + 작업 계획 수립

### 05:20~05:35 — 4개 프로젝트 순차 수정 (Phase 1)

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

### 05:35~05:45 — 5개 프로젝트 순차 수정 (Phase 2)

#### Stay.Awake
- `OnTimerTick` → `async void`: `_simulator.SimulateActivity()`를 `await Task.Run()`으로 래핑
- `SimulateNow` → `async void`: 동일 처리
- WinForms Timer.Tick이 UI 스레드에서 실행되므로 Thread.Sleep(100+10ms) 블로킹 완전 제거

#### DNS.Flip
- `RunNetsh` → `RunNetshAsync`: `proc.WaitForExit(10000)` → `await proc.WaitForExitAsync()`, `ReadToEnd` → `ReadToEndAsync`
- `ApplyPreset` → `ApplyPresetAsync`: 최대 3회 netsh 호출(최대 30s 블로킹) 비동기 변환
- `TrayApp.OnPresetClick`: `await DnsService.ApplyPresetAsync()` 적용

#### Hex.Peek
- `OpenFile` → `OpenFileAsync`: `HexDocument.Load()` + `StructureParser.DetectFormat/Parse()`를 한번에 `Task.Run`으로 래핑
- `BtnCompare_Click` → `async void`: 비교 파일 로딩도 비동기 처리
- 로딩 중 TxtStatus에 "파일 로딩 중...", "비교 파일 로딩 중..." 표시

#### QR.Forge
- `GenerateQr` → `async void`: `CancellationTokenSource` 디바운스 150ms 추가
- `QrService.Render()`를 `Task.Run()`으로 래핑 (ZXing QR 인코딩 + SkiaSharp 512×512 픽셀 루프)
- 스타일 스냅샷 복사 후 배경 스레드 전달 (thread-safe)

#### Env.Guard
- `LoadPathList` → `LoadPathListAsync`: PATH 항목 전체 `Directory.Exists()` 체크를 `Task.Run`으로 일괄 처리
- `LoadPathEntries` → `LoadPathEntriesAsync`: User/System 순차 비동기 처리
- `OnLoaded`, `Refresh_Click`, `ListSnapshots_Click`, `MoveUp/Down/Add/RemovePath_Click` 모두 async void 변환
- PATH 확인 중 TxtStatus에 "PATH 경로 확인 중..." 표시

---

## 커밋 이력

| 해시 | 내용 |
|------|------|
| `a52d933` | [music.player] 파일 추가·플레이리스트 복원 UI Freezing 방지 |
| `d13b980` | [photo.video.organizer] 파일 추가 UI Freezing 방지 |
| `e53f889` | [file.duplicates] 파일 목록 수집 UI Freezing 방지 |
| `efd5eca` | [log.lens] 초기 로그 로딩 UI Freezing 방지 |
| `47acd91` | [stay.awake] 활동 시뮬레이션 UI Freezing 방지 |
| `e776acd` | [dns.flip] netsh 프로세스 실행 UI Freezing 방지 |
| `aa6c3b5` | [hex.peek] 파일 로딩 UI Freezing 방지 |
| `d5be232` | [qr.forge] QR 렌더링 UI Freezing 방지 (Task.Run + 150ms 디바운스) |
| `8928e25` | [env.guard] PATH 경로 확인 UI Freezing 방지 |
