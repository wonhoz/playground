# Playground 아이디어 노트

---

## 1. Stay.Awake (Slack)

WinForms 트레이 앱 · 마우스/키보드 시뮬레이션 + Slack 상태 자동 변경

| # | 개선 아이디어 | 난이도 | 상태 |
|---|---|---|---|
| 1 | Teams / Discord 상태 변경 지원 — 슬랙만 되는데 Teams도 같은 방식으로 가능 | ★★☆ | |
| 2 | 윈도우 잠금(Lock Screen) 감지 → 자동 Away — SystemEvents.SessionSwitch | ★☆☆ | ✅ 완료 |
| 3 | Slack 출퇴근 시간 여러 세트 — 유연근무/재택 패턴 설정 | ★☆☆ | |
| 4 | 특정 앱 실행 중일 때만 작동 — 예) 회의앱 실행 중엔 시뮬레이션 중단 | ★★☆ | |
| 5 | 일일 활동 통계 — 오늘 몇 번 시뮬레이션 됐는지, 활성 시간 등 | ★★☆ | ✅ 완료 |

### 추가 완료된 것들
- ✅ 한글 IME 상태에서도 슬랙 입력창에 정확히 입력 (클립보드 방식으로 변경)
- ✅ 명령 전송 후 원래 클립보드 내용 자동 복원
- ✅ 정보 창 내용 업데이트 (활동 유형, Slack 자동 상태 설정 표시) + v1.3 버전 반영

### 버그 수정 완료
- ✅ `Mutex` 인스턴스를 지역 변수 → `static` 필드로 변경 (GC 조기 해제 방지)
- ✅ PowerShell 임시 파일(`StayAwake_slack.ps1`) `finally`에서 항상 삭제 (호출마다 누적 방지)

---

## 2. Photo.Video.Organizer (Photo)

WPF · EXIF/QuickTime 메타데이터 기반 날짜별 자동 폴더 정리

| # | 개선 아이디어 | 난이도 | 상태 |
|---|---|---|---|
| 1 | 설정 저장 — 마지막 대상 폴더, 폴더 구조 선택 기억 (현재 없음) | ★☆☆ | |
| 2 | 이동 vs 복사 선택 — 현재 복사만 가능, 원본 삭제 옵션 | ★☆☆ | |
| 3 | 중복 파일 감지 — SHA256 해시 비교, 동일 파일 건너뜀 | ★★☆ | ✅ 완료 |
| 4 | 폴더 구조 커스터마이징 — yyyy/MM/dd, yyyy-MM 등 사용자 정의 패턴 | ★★☆ | ✅ 완료 |
| 5 | GPS → 지역명 폴더 — EXIF GPS 좌표로 "Seoul/Gangnam" 같은 폴더 생성 | ★★★ | |
| 6 | 처리 로그 파일 저장 — 어떤 파일이 어디로 이동됐는지 CSV/TXT 기록 | ★☆☆ | ✅ 완료 |

### 추가 완료된 것들
- ✅ 결과 패널 상세화 — 총 N개 중 M개 완료, 이미지/동영상 분리, 중복·미지원·오류 구분 표시
- ✅ 로그 보기 버튼 — 로그 저장 시 결과 패널에 버튼 표시, 클릭 시 CSV 즉시 오픈

### 버그 수정 완료
- ✅ 날짜 유효성 검증을 `day <= 31` 고정에서 `DateTime.DaysInMonth(year, month)` 기반으로 개선 (2월 30일 등 오파싱 방지)

---

## 3. Music.Player (Music)

WPF · NAudio + TagLibSharp · 플레이리스트, 셔플, 반복, 키보드 단축키

| # | 개선 아이디어 | 난이도 | 상태 |
|---|---|---|---|
| 1 | 미니 플레이어 모드 — 작은 floating 창으로 축소 | ★★☆ | |
| 2 | 전역 미디어 키 지원 — 포커스 없어도 재생/정지/다음 키 동작 | ★★☆ | |
| 3 | 이퀄라이저 — NAudio로 구현 가능한 기본 EQ | ★★★ | |
| 4 | 재생 기록 & 즐겨찾기 — 자주 듣는 곡 통계, 즐겨찾기 핀 | ★★☆ | ✅ 완료 |
| 5 | 가사 표시 — .lrc 파일 파싱 또는 가사 API 연동 | ★★★ | ✅ 완료 |
| 6 | 앨범/아티스트 라이브러리 뷰 — 현재 플레이리스트만 있고 라이브러리 탐색 없음 | ★★★ | ✅ 완료 |

### 추가 완료된 것들
- ✅ 재생 기록 창 — 최근 재생 / 자주 듣는 곡 / 즐겨찾기 탭, 더블클릭으로 플레이리스트 추가
- ✅ 즐겨찾기 — 플레이리스트 항목 옆 ★ 버튼, AppData에 상태 영속 저장
- ✅ 가사 토글 — 앨범 아트 영역을 가사 패널로 전환, 현재 줄 오렌지 하이라이트 + 자동 스크롤
- ✅ 라이브러리 창 — 폴더 비동기 스캔, 아티스트 필터, 전체/선택 곡 추가

---

## 4. AI.Clip (AI)

WinForms 트레이 앱 · Anthropic Claude API · 클립보드 텍스트 즉시 AI 처리

| # | 구현 내용 | 상태 |
|---|---|---|
| 1 | 트레이 우클릭 메뉴 — 클립보드 미리보기 + 4가지 AI 작업 선택 | ✅ 완료 |
| 2 | Summarize — 클립보드 텍스트 요약 (입력 언어 자동 인식) | ✅ 완료 |
| 3 | Translate — 6개 언어 선택 서브메뉴 (Korean, English, Japanese, Chinese, Spanish, French) | ✅ 완료 |
| 4 | Proofread — 문법/맞춤법 교정 후 변경 목록 함께 출력 | ✅ 완료 |
| 5 | Convert Code — Python/JS/TS/C#/Java/Go 선택 서브메뉴 | ✅ 완료 |
| 6 | ResultForm — 다크 테마 결과 창, 처리 중 로딩 표시, Copy 버튼 | ✅ 완료 |
| 7 | SettingsForm — API 키 입력, 기본 번역/코드 변환 언어 설정 | ✅ 완료 |

### 추가 구현된 것들
- ✅ 창 닫기 시 진행 중인 API 요청 자동 취소 (CancellationToken 연동)
- ✅ DarkMenuRenderer — 다크 테마 라운드 컨텍스트 메뉴
- ✅ 오렌지 색상 'AI' 아이콘 GDI+ 런타임 생성 (이미지 파일 불필요)
- ✅ AppData에 설정 영속 저장

### 버그 수정 완료
- ✅ Anthropic API 응답 `content[0]` 접근 전 배열 길이 검증 추가 (빈 응답 시 IndexOutOfRange 방지)

---

## 5. File.Duplicates (Files)

WPF · SHA256 해시 + dHash 유사도 · 중복 파일 탐지 및 정리

| # | 구현 내용 | 상태 |
|---|---|---|
| 1 | 폴더 추가/제거 — 여러 폴더 동시 스캔 지원 | ✅ 완료 |
| 2 | SHA256 해시 스캔 — 파일 크기 사전 필터 후 완전 일치 탐지 | ✅ 완료 |
| 3 | dHash 유사 이미지 스캔 — 9×8 리사이즈 후 64bit 차이 해시, Union-Find 클러스터링 | ✅ 완료 |
| 4 | 유사도 임계값 슬라이더 — Hamming distance 1~20 bit 조절 | ✅ 완료 |
| 5 | 결과 카드 UI — SHA256(파란 배지) / 유사 이미지(보라 배지) 그룹 표시 | ✅ 완료 |
| 6 | 중복 자동 선택 — 각 그룹에서 첫 번째만 유지, 나머지 자동 체크 | ✅ 완료 |
| 7 | 휴지통으로 / 폴더 이동 — Shell32 SHFileOperation (복원 가능) | ✅ 완료 |

### 버그 수정 완료
- ✅ `ImageScanner`의 미사용 `int[n,n]` 배열 제거 (n² × 4B 메모리 낭비 방지)
- ✅ `HammingDistance` 수동 비트 루프 → `BitOperations.PopCount(a ^ b)` 단일 명령으로 교체 (성능 향상)
- ✅ `SHFileOperation` 반환값 확인 — 0 이외 값이면 `IOException` throw
- ✅ `Directory.EnumerateFiles` 폴더별 try-catch 추가 (UnauthorizedAccessException 등 부분 실패 허용)
- ✅ 재스캔 시 이전 `CancellationTokenSource` Dispose (메모리 누수 방지)

---

## 신규 프로젝트 아이디어

### 🔥 강추 (재미 + 실용성 모두)

| 프로젝트 | 설명 | 그룹 | 상태 |
|---|---|---|---|
| **Clipboard.Manager** | 클립보드 히스토리 + 즐겨찾기 + 텍스트 변환(대소문자, trim 등). Win+V보다 강력한 버전. 전역 단축키로 플로팅 팝업 | Tools | |
| **Screen.OCR** | 단축키 → 영역 드래그 → 텍스트 자동 추출/복사. 이미지 속 텍스트, PDF 캡처 등. Windows.Media.Ocr API 사용 | Tools | |
| **Batch.Rename** | 파일 대량 이름 변경. 정규식/패턴/번호 매기기/날짜 삽입 등. before/after 미리보기 | Files | |
| **Quick.Launcher** | 키워드 → 앱/파일/URL/스니펫 즉시 실행. Spotlight/Alfred의 Windows 버전. 전역 단축키로 팝업 | Tools | ✅ 완료 |
| **AI.Clip** | 클립보드 텍스트를 Anthropic API로 요약/번역/교정/코드변환. 트레이 우클릭 → AI 메뉴 | Tools | ✅ 완료 |

### 💡 신박한 것들

| 프로젝트 | 설명 | 상태 |
|---|---|---|
| **Focus.Guard** | 집중 모드: 특정 앱 차단 + 포모도로 타이머 + 업무/휴식 통계. Stay.Awake와 반대 개념 | |
| **Workspace.Switcher** | 업무 컨텍스트 프리셋 저장 (앱 목록 + 창 배치). "출근 모드" 누르면 Slack·VS·브라우저 자동 오픈 | ✅ 완료 |
| **Sound.Board** | 사운드 버튼 보드. 핫키로 효과음 즉시 재생. 회의 중 박수/웃음 버튼 같은 장난끼 있는 것도 ㅎ | ✅ 완료 |
| **File.Duplicates** | 해시 기반 중복 파일 탐지 + 정리. 사진 중복 탐지는 유사도 기반으로 | ✅ 완료 |
| **Subtitle.Sync** | SRT 자막 싱크 조정 + 번역 연동. 영화 볼 때 자막 타이밍 안 맞을 때 유용 | |

---

## 6. Sound.Board (Sound)

WPF · NAudio 2.2.1 · PCM 사운드 합성 · 전역 단축키 · 사운드패드 앱

| # | 구현 내용 | 상태 |
|---|---|---|
| 1 | 사운드 버튼 보드 — 130×110px 컬러 카드, WrapPanel 자동 줄바꿈 | ✅ 완료 |
| 2 | 8종 내장 사운드 PCM 합성 — AirHorn·Applause·Rimshot·SadTrombone·Ding·Laser·Boom·Fanfare | ✅ 완료 |
| 3 | 외부 오디오 파일 지원 — MP3/WAV/OGG 등 NAudio AudioFileReader | ✅ 완료 |
| 4 | 드래그앤드롭 — 오디오 파일을 보드에 끌어다 놓으면 버튼 자동 생성 | ✅ 완료 |
| 5 | 전역 단축키 — RegisterHotKey Win32 API, Ctrl/Alt/Shift 조합 지원 | ✅ 완료 |
| 6 | 편집 모드 — 카드 위 오버레이 ✏ 편집 / 🗑 삭제, 오른쪽 빠른 추가 카드 | ✅ 완료 |
| 7 | EditButtonDialog — 이름/이모지/소스선택/색상팔레트(15색)/단축키 캡처 | ✅ 완료 |
| 8 | 겹쳐 재생 옵션, 볼륨 슬라이더, ■ 정지 버튼 | ✅ 완료 |
| 9 | AppData JSON 영속 설정 저장 | ✅ 완료 |

### 버그 수정 완료
- ✅ `MemoryStream`을 `extraCleanup`으로 별도 추적하여 `PlaybackStopped` 핸들러에서 함께 Dispose (`RawSourceWaveStream`이 소유권 없음)
- ✅ `Dispose()`에서 풀 스냅샷 후 `_pool.Clear()` 선행 — `PlaybackStopped` 핸들러와의 이중 Dispose 충돌 방지
- ✅ `CreateCard()`에서 `DropShadowEffect`를 카드당 2개만 사전 생성 재사용 (MouseEnter/Leave마다 `new` 제거)

---

## 7. Quick.Launcher (Tools)

WPF · Alt+Space 전역 단축키 · 시작 메뉴 앱 인덱싱 · Fuzzy 검색 · 커스텀 URL/스니펫

| # | 구현 내용 | 상태 |
|---|---|---|
| 1 | 트레이 아이콘 — 더블클릭/메뉴로 런처 토글, 설정 열기, 종료 | ✅ 완료 |
| 2 | 전역 단축키 — Alt+Space 기본값, 커스텀 단축키 설정 가능 | ✅ 완료 |
| 3 | 앱 검색 — 시작 메뉴 .lnk 파일 백그라운드 인덱싱, Fuzzy Score(정확/시작/포함/문자순) | ✅ 완료 |
| 4 | URL / 스니펫 — 설정에서 커스텀 항목 추가, 🌐/📋 아이콘 구분 | ✅ 완료 |
| 5 | LauncherWindow — 다크 보더리스 팝업, 키보드 Up/Down/Enter/Esc 네비게이션 | ✅ 완료 |
| 6 | SettingsWindow — 단축키 캡처, 커스텀 URL·스니펫 목록 관리 | ✅ 완료 |

### 버그 수정 완료
- ✅ `GDI+ StringFormat` → `using var` 명시적 Dispose (GDI+ 비관리 리소스 누수 방지)
- ✅ `Task.Run` fire-and-forget을 `_ = Task.Run(...)` 패턴으로 변경 (컴파일러 경고 제거)
- ✅ `LaunchExecutor`에서 `Process.Start(...)?.Dispose()` — `UseShellExecute=true` 시 반환되는 프로세스 핸들 해제

---

## 8. Workspace.Switcher (Tools)

WPF · 워크스페이스 프리셋 카드 UI · 앱 순차 실행 · 실행 중인 창 캡처

| # | 구현 내용 | 상태 |
|---|---|---|
| 1 | 워크스페이스 카드 — 170px 컬러 카드, 이모지+이름+앱수 표시 | ✅ 완료 |
| 2 | 앱 순차 실행 — 300ms 간격으로 순서대로 실행, 실패 시 오류 요약 | ✅ 완료 |
| 3 | 현재 앱으로 만들기 — EnumWindows P/Invoke로 실행 중인 창 캡처 → 새 워크스페이스 | ✅ 완료 |
| 4 | EditWorkspaceDialog — 이름/이모지/색상팔레트(15색)/앱 목록 편집, exe찾기 + URL 지원 | ✅ 완료 |
| 5 | 앱 순서 조정 — ↑ 버튼으로 목록 순서 변경, 실행 미리보기 | ✅ 완료 |
| 6 | AppData JSON 영속 설정 저장 | ✅ 완료 |

### 버그 수정 완료
- ✅ `WindowCapture`에서 `Process.GetProcessById()` → `using var` 패턴으로 핸들 누수 방지
- ✅ `WorkspaceLauncher`에서 `Process.Start(...)?.Dispose()` — 프로세스 핸들 해제
- ✅ `CreateCard()`에서 `DropShadowEffect`를 카드당 2개만 사전 생성 재사용 (MouseEnter/Leave마다 `new` 제거)
