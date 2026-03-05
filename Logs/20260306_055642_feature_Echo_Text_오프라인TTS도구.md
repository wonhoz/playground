# Echo.Text — 오프라인 텍스트-음성 변환 도구 개발

- **날짜**: 2026-03-06
- **태그**: feature
- **소요 시간**: 약 1시간

## 개요

Windows SAPI 5 기반 오프라인 TTS WPF 앱.
`System.Speech` + `NAudio` 패키지. 설치된 TTS 음성 선택, WAV/MP3 내보내기, 챕터 일괄 출력.

## 생성 파일 목록

### 프로젝트 뼈대
- `Echo.Text.csproj` — net10.0-windows, System.Speech 8.0.0, NAudio 2.2.1
- `GlobalUsings.cs`
- `App.xaml` / `App.xaml.cs` — 다크 테마, 포인트 색 보라 #A855F7
- `MainWindow.xaml` / `MainWindow.xaml.cs` — 2탭 (편집기/일괄 출력), DwmSetWindowAttribute

### 서비스 레이어
- `Services/TtsService.cs`
  - SAPI SpeechSynthesizer 래퍼
  - 음성 목록 조회, Rate/Volume/Pitch 설정
  - SpeakAsync (스피커 재생), SpeakToWavAsync (WAV 저장 + SpeakProgress 진행률)
  - SSML 자동 래핑 (pitch prosody 태그), SSML 직접 입력 모드
  - Analyze() — 글자수/단어수/예상 재생 시간 계산
- `Services/ExportService.cs`
  - NAudio MediaFoundationEncoder → WAV→MP3 변환 (외부 DLL 불필요)
  - SplitChapters() — 구분자로 텍스트 챕터 분할

### 뷰 (2개)
- `Views/EditorView.xaml` / `.cs`
  - 좌측 사이드바 (음성 콤보, Rate/Volume/Pitch 슬라이더, 재생/중단, 클립보드, WAV/MP3 저장, SSML 모드 체크박스)
  - 우측 텍스트 에디터 (TextBox, 자동 줄바꿈 토글)
  - 하단 통계 (글자수, 단어수, 예상 시간)
  - 재생 완료 감지: DispatcherTimer로 IsSpeaking 폴링
- `Views/BatchView.xaml` / `.cs`
  - .txt 파일 로드, 구분자 입력, 챕터 분할 미리보기 (ListView)
  - 출력 폴더 선택, WAV/MP3 일괄 저장
  - 챕터별 상태 표시 (대기/변환 중.../✔ 완료/✘ 오류)

### 리소스
- `Resources/app.ico` — 다크 배경 + 보라색 스피커 + 음파 3개 (16/32/48/256px)

## 수정 사항 (빌드 오류)
1. `EditorView.xaml` `LineHeight` 속성 — WPF TextBox에 없음 → 제거

## 등록/갱신
- `Playground.slnx` — Tools/Productivity 폴더에 Echo.Text 등록
- `+publish-all.cmd` — Productivity 섹션에 Echo.Text 추가
- `+publish.cmd` — 메뉴 50번, 선택 섹션, PUBALL 섹션 추가
- `ideas/avoid.md` — 구현 완료 목록에 Echo.Text 추가

## 기술 포인트
- `System.Speech.Synthesis.SpeechSynthesizer.SpeakProgress` 이벤트 → 진행률 퍼센트 계산
- `NAudio.MediaFoundation.MediaFoundationEncoder.EncodeToMp3()` → Windows 내장 MF 사용, 외부 DLL 불필요
- SSML `<prosody pitch="+Nst">` — 음높이 조절 (음성 엔진에 따라 지원 여부 다름)
- DispatcherTimer로 `SpeechSynthesizer.State` 폴링 → 재생 완료 감지

## 빌드 결과
```
경고 0개, 오류 0개
```
