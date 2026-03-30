# Clipboard.Stacker + Ambient.Mixer 구현

**날짜**: 2026-02-21
**태그**: feature
**커밋**: `cd2d111` (Clipboard.Stacker), `0143214` (Ambient.Mixer)

---

## 개요

`idea_20260220_135615.md`의 두 앱을 구현했다.

---

## Clipboard.Stacker (Tools)

### 기능
- `WM_CLIPBOARDUPDATE` 이벤트로 복사 감지 → LinkedList 스택에 push
- `Ctrl+Shift+V` 글로벌 핫키 → 팝업 토글 / 스택 FIFO pop
- 항목 클릭 → 즉시 붙여넣기 (SendInput Ctrl+V 시뮬레이션)
- 📌 버튼 → 즐겨찾기 고정
- 텍스트 변환: UPPER / lower / Trim
- 설정 JSON 저장 (`%LocalAppData%\ClipboardStacker\settings.json`)
- 트레이 앱 (NotifyIcon + DarkMenuRenderer)

### 주요 파일
| 파일 | 설명 |
|------|------|
| `Services/ClipboardMonitor.cs` | `AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE` |
| `Services/ClipboardStack.cs` | `LinkedList<ClipEntry>` FIFO |
| `Services/PasteService.cs` | `SendInput` API Ctrl+V 시뮬레이션 |
| `PopupWindow.xaml/.cs` | 팝업 UI (WPF, Topmost) |
| `App.xaml.cs` | 트레이 초기화, HWND 확보, WndProc 훅 |
| `IconGenerator.cs` | 3겹 클립보드 아이콘 (`clipstacker.ico`) |

### 해결한 오류
- `Color` 모호성: GlobalUsings 대신 파일 레벨 `using Color = System.Windows.Media.Color;` 사용
- `HorizontalAlignment` 인스턴스 참조: `global using HorizontalAlignment = System.Windows.HorizontalAlignment;` 추가

---

## Ambient.Mixer (Audio)

### 기능
- 8개 트랙 PCM 실시간 합성 (외부 파일 없음):
  - ☔ 빗소리, 💨 바람, 🌊 파도, 🐦 새소리
  - ☕ 카페, ⌨️ 키보드, 🔥 모닥불, 〰 화이트노이즈
- 개별 트랙 볼륨 슬라이더 (0~100%)
- 마스터 볼륨 슬라이더
- 3개 기본 프리셋 (카페 모드 / 숲속 모드 / 비 오는 날)
- 슬립 타이머 (15/30/45분, 1/2시간) — 마지막 30초 선형 페이드아웃
- 설정 JSON 저장 (`%LocalAppData%\AmbientMixer\settings.json`)
- 트레이 앱 (더블클릭으로 창 토글)

### 주요 파일
| 파일 | 설명 |
|------|------|
| `Services/AmbientProviders.cs` | 8개 `ISampleProvider` 구현 (44100Hz float Mono) |
| `Services/MixerService.cs` | `MixingSampleProvider` + `VolumeSampleProvider` 오케스트레이션 |
| `Services/SettingsService.cs` | JSON 설정 저장/로드 |
| `MainWindow.xaml/.cs` | 다크 테마 슬라이더 UI, 코드비하인드 트랙 행 생성 |
| `IconGenerator.cs` | 이퀄라이저 바 5개 (청록→보라 그라디언트) |

### NAudio 구조
```
RainProvider (ISampleProvider)
  └─ VolumeSampleProvider (trackVol * master * fade)
       └─ MixingSampleProvider
            └─ WaveOutEvent (DirectSound, 100ms latency)
```

### 해결한 오류
- `LetterSpacing` WPF 미지원 → 제거
- `Button` 모호성 → `global using Button = System.Windows.Controls.Button;`
- `Path/Directory/File` 없음 → `global using System.IO;` 추가
- `Color` 모호성 → `using Color = System.Windows.Media.Color;` (파일 레벨)

---

## 솔루션 등록

- `Playground.sln`: `dotnet sln add --solution-folder` 로 양쪽 추가
- `Playground.slnx`: Audio/Tools 폴더에 수동 추가
- `+publish-all.cmd`: 두 `call :pub` 항목 추가

---

## 빌드 결과

```
솔루션 전체 (13 프로젝트): 경고 0 / 오류 0
```
