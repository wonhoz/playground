# Avoid — 구현 완료 앱 (Applications)

> ★N차 = 해당 차수 브레인스토밍에서 처음 제안됨 / ✅ = 구현 완료
> 마지막 갱신: 2026-03-15 (30차)

---

| 앱 | 카테고리 | 핵심 기능 |
|----|----------|----------|
| `AI.Clip` | AI | Claude API 클립보드 자동 처리 |
| `Music.Player` | Audio | WPF 음악 플레이어 (플레이리스트, 셔플) |
| `Stay.Awake` | Automation | 트레이 상주, 화면 꺼짐 방지, Slack 상태 연동 |
| `Toast.Cast` | Health | 건강 루틴 반복 알림 (눈 휴식, 스트레칭, 물 마시기) |
| `Batch.Rename` | Files | 파일 일괄 이름 바꾸기 |
| `File.Duplicates` | Files | SHA-256 + dHash 중복 파일 탐지기 |
| `Hash.Check` | Files | MD5/SHA-1/SHA-256/SHA-512 해시 계산·비교·일괄 검증·변경 감시 |
| `Echo.Text` | Tools/Productivity | 오프라인 TTS, SAPI 음성 선택, 속도/볼륨/음높이, WAV/MP3 내보내기, 챕터 일괄 출력 |
| `Net.Scan` | Tools/Network | LAN ARP 스캐너, 기기 탐지·제조사(OUI) 조회·포트 스캔·핑 모니터·핑 히스토리 그래프 |
| `Photo.Video.Organizer` | Media | EXIF 날짜 기반 미디어 정리기 |
| `Api.Probe` | Dev/Tools | 미니멀 오프라인 API 테스터 (Postman 대체) |
| `Text.Forge` | Dev/Tools | 해시·인코딩 올인원 (MD5/SHA/Base64/JWT/UUID) |
| `Log.Lens` | Dev/Tools | 로그 파일 분석 뷰어 |
| `Mock.Server` | Dev/Tools | 로컬 Mock HTTP 서버 (GUI 라우트 정의) |
| `DNS.Flip` | Network | DNS 프리셋 원클릭 전환 트레이 앱 |
| `Port.Watch` | Network | 포트 점유 프로세스 모니터 + 원클릭 종료 |
| `Clipboard.Stacker` | Productivity | 클립보드 스택 관리 |
| `Code.Snap` | Productivity | 오프라인 코드 스크린샷 미화 도구 |
| `QR.Forge` | Productivity | 오프라인 QR 코드 생성기 (로고 삽입, 배치) |
| `Screen.Recorder` | Productivity | 화면 녹화 도구 |
| `Word.Cloud` | Productivity | 오프라인 워드클라우드 생성기 |
| `Env.Guard` | System | Windows 환경변수 GUI 관리자 + 스냅샷/롤백 |
| `Sys.Clean` | System | CCleaner 유사 - 시스템 청소, 레지스트리, 시작프로그램 |
| `Link.Vault` | Tools | 완전 오프라인 북마크 관리자 (페이지 스냅샷) |
| `Mark.View` | Productivity | Markdown 뷰어 + 실시간 에디터 (멀티탭, TOC, WebView2) |
| `Mouse.Flick` | Productivity | 전역 마우스 제스처 → 키보드 단축키 매핑 트레이 앱 |
| `Deep.Diff` | Dev/Tools | 텍스트·이미지·HEX·폴더 파일 비교기 |
| `PDF.Forge` | Files | 오프라인 PDF 올인원 (병합·분리·압축·워터마크) |
| `Zip.Peek` | Files | ZIP/7z/RAR 추출 없이 트리 탐색·선택 추출 |
| `Boot.Map` | System | Windows 부팅 ETW 타임라인 시각화 |
| `Tag.Forge` | Audio | MP3/FLAC ID3 태그 일괄 편집기 (MusicBrainz 연동) |
| `Layout.Forge` | System | 키보드 키 재배치 프로파일 에디터 |
| `Sched.Cast` | System | Windows Task Scheduler GUI 대체 |
| `Color.Grade` | Media | 이미지 LUT 색보정 도구 |
| `Pad.Forge` | System | 게임패드 XInput/DInput 버튼 매핑 GUI |
| `Tray.Stats` | System | CPU/RAM/GPU/디스크/네트워크 통합 성능 트레이 모니터 |
| `Key.Map` | System | 앱별 단축키 키보드 다이어그램 시각화 + PDF 치트시트 |
| `Key.Test` | System | 하드웨어 키보드 키 입력 테스트 도구 |
| `WiFi.Cast` | Network | Wi-Fi 채널 분석기 + 신호 강도 트레이 모니터 |
| `Glyph.Map` | Dev/Tools | 유니코드 12만+ 문자 오프라인 탐색·복사·즐겨찾기 |
| `Icon.Hunt` | Dev/Tools | 30만+ 오픈소스 아이콘 라이브러리 탐색기 |
| `Disk.Lens` | Files | 트리맵 디스크 사용량 시각화 (WinDirStat 대체) |
| `Char.Art` | Text | 이미지→ASCII/Unicode 아트 변환기 |
| `Diff.Prompt` | AI | AI 프롬프트 A/B 비교 실험실 (출력 diff + 비용) ★23차 |
| `Hotkey.Map` | System | 전역 단축키 충돌 감지 + AHK 내보내기 ★24차 |
| `Locale.View` | Dev/Tools | 200+ 로케일 날짜·숫자·통화·달력 형식 브라우저 ★24차 |
| `Manga.View` | Files | CBZ/CBR/7Z 만화·망가 오프라인 리더 (RTL/이중 페이지) ★24차 |
| `ANSI.Forge` | Text | ANSI 아트 에디터 + 터미널 이스케이프 코드 뷰어 ★24차 |
| `Img.Compare` | Media | 픽셀 diff + SSIM/PSNR 이미지 품질 비교기 ★24차 |
| `Badge.Forge` | Dev/Tools | 오프라인 shields.io 스타일 SVG/PNG 배지 생성기 ★24차 |
| `SQL.Lens` | Dev/Tools | SQLite EXPLAIN QUERY PLAN 시각화 + 최적화 힌트 ★24차 |
| `Icon.Maker` | Tools.Utility | SVG/PNG→ICO 멀티사이즈 변환 + WriteableBitmap 픽셀 그리드 에디터 ★25차 |
| `JSON.Tree` | Dev/Inspector | JSON/YAML/TOML 트리 탐색기 + 검색 필터 + Side-by-Side Diff + 형식 변환 ★25차 |
| `JSON.Fmt` | Tools.Utility | JSON 붙여넣기 즉시 beautify + 구문 강조 + 오류 줄/열 진단 + Lenient 파싱(주석·trailing comma·따옴표) ★26차 |
| `Str.Forge` | Development/Analyzer | 멀티파일 Find & Replace 엔진 ★27차 |
| `Win.Scope` | System/Manager | 실행 창 Z-order·투명도·클릭통과 실시간 조작 인스펙터 ★27차 |
| `OAuth.Peek` | Dev/Inspector | JWT 디코더 + JWK 서명 검증 + OIDC Discovery + OAuth2 플로 다이어그램 + Auth 헤더 생성기 ★28차 |
| `Win.Event` | Dev/Tools | Windows EVT/EVTX 이벤트 로그 경량 뷰어 ★29차 |
| `Spec.View` | System | PC 하드웨어 스펙 스캐너·내보내기 ★29차 |
| `Drive.Bench` | Dev/Tools | 디스크 벤치마크 (순차/랜덤 R/W, IOPS, S.M.A.R.T) ★29차 |
| `Token.Calc` | AI | LLM 멀티모델 토큰 수·비용 실시간 계산기 (GPT/Claude/Gemini 20+종 단가 내장) ★30차 |
| `Pane.Cast` | System | Windows Terminal 멀티패인 세션 레이아웃 저장·복원기 (프리셋 라이브러리·트레이) ★30차 |
| `Prompt.Forge` | AI | AI 프롬프트 라이브러리 관리자 ★30차 |
| `Schema.Mock` | Dev/Tools | JSON Schema/OpenAPI 3.x → Bogus Faker 가짜 데이터 즉시 생성 + JSON/CSV 내보내기 ★29차 |
| `Proc.Bench` | System | CPU 단일·멀티스레드 / 메모리 대역폭·레이턴시 / 스토리지 IOPS 종합 벤치마크 스위트 ★29차 |
