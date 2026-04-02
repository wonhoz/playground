# Avoid — 구현 완료 + 채택 결정 앱 (Applications)

> ★N차 = 해당 차수 브레인스토밍에서 처음 제안됨
> 상태: ✅ 구현 완료 | 📌 채택 결정 (미구현) | 🗃️ 아카이브
> 마지막 갱신: 2026-03-29 (v5 중복·레거시 상태 수정)

---

| 앱 | 카테고리 | 핵심 기능 | 상태 |
|----|----------|----------|------|
| `AI.Clip` | AI | Claude API 클립보드 자동 처리 | ✅ |
| `Prompt.Forge` | AI | AI 프롬프트 라이브러리 관리자 ★30차 | ✅ |
| `Diff.Prompt` | AI | AI 프롬프트 A/B 비교 실험실 (출력 diff + 비용) ★23차 | 🗃️ |
| `Token.Calc` | AI | LLM 멀티모델 토큰 수·비용 실시간 계산기 (GPT/Claude/Gemini 20+종 단가 내장) ★30차 | 🗃️ |
| `Music.Player` | Audio | WPF 음악 플레이어 (플레이리스트, 셔플) | ✅ |
| `Tag.Forge` | Audio | MP3/FLAC ID3 태그 일괄 편집기 (MusicBrainz 연동) | ✅ |
| `Stay.Awake` | Automation | 트레이 상주, 화면 꺼짐 방지, Slack 상태 연동 | ✅ |
| `Pane.Cast` | Automation/System | Windows Terminal 멀티패인 세션 레이아웃 저장·복원기 (프리셋 라이브러리·트레이) ★30차 | ✅ |
| `Dep.Graph` | Dev/Analyzer | .NET 솔루션 NuGet 패키지 의존성 그래프 시각화 | ✅ |
| `Git.Stats` | Dev/Analyzer | Git 저장소 커밋 통계·기여자·활동 히트맵 분석기 | ✅ |
| `Log.Merge` | Dev/Analyzer | 다중 로그 소스 시간순 통합 뷰어 | ✅ |
| `Win.Event` | Dev/Analyzer | Windows EVT/EVTX 이벤트 로그 경량 뷰어 ★29차 | ✅ |
| `Api.Probe` | Dev/Inspector | 미니멀 오프라인 API 테스터 (Postman 대체) | ✅ |
| `App.Temp` | Dev/Inspector | 앱 임시 실행 샌드박스·임시 파일·캐시 폴더 관리 | ✅ |
| `Hex.Peek` | Dev/Inspector | 바이너리 파일 HEX/ASCII 뷰어 + 편집 | ✅ |
| `JSON.Tree` | Dev/Inspector | JSON/YAML/TOML 트리 탐색기 + 검색 필터 + Side-by-Side Diff + 형식 변환 ★25차 | ✅ |
| `Locale.View` | Dev/Inspector | 200+ 로케일 날짜·숫자·통화·달력 형식 브라우저 ★24차 | ✅ |
| `Quick.Calc` | Dev/Inspector | 트레이 팝업 프로그래머 계산기 (진수·비트연산) | ✅ |
| `Signal.Flow` | Dev/Inspector | SignalR/SSE 실시간 메시지 스트림 인스펙터 | ✅ |
| `Skill.Cast` | Dev/Inspector | Claude Code 개발 환경 스킬·워크플로 관리자 | ✅ |
| `OAuth.Peek` | Dev/Inspector | JWT 디코더 + JWK 서명 검증 + OIDC Discovery + OAuth2 플로 다이어그램 ★28차 | 🗃️ |
| `Log.Lens` | Dev/Tools | 로그 파일 분석 뷰어 | ✅ |
| `Mock.Server` | Dev/Tools | 로컬 Mock HTTP 서버 (GUI 라우트 정의) | ✅ |
| `Drive.Bench` | Dev/Tools | 디스크 벤치마크 (순차/랜덤 R/W, IOPS, S.M.A.R.T) ★29차 | ✅ |
| `Glyph.Map` | Dev/Tools | 유니코드 12만+ 문자 오프라인 탐색·복사·즐겨찾기 | ✅ |
| `Icon.Hunt` | Dev/Tools | 30만+ 오픈소스 아이콘 라이브러리 탐색기 | ✅ |
| `Deep.Diff` | Dev/Tools | 텍스트·이미지·HEX·폴더 파일 비교기 | 🗃️ |
| `SQL.Lens` | Dev/Tools | SQLite EXPLAIN QUERY PLAN 시각화 + 최적화 힌트 ★24차 | 🗃️ |
| `Str.Forge` | Dev/Tools | 멀티파일 Find & Replace 엔진 ★27차 | 🗃️ |
| `Schema.Mock` | Dev/Tools | JSON Schema/OpenAPI 3.x → Bogus Faker 가짜 데이터 즉시 생성 + JSON/CSV 내보내기 ★29차 | 🗃️ |
| `Batch.Rename` | Files | 파일 일괄 이름 바꾸기 | ✅ |
| `Disk.Lens` | Files | 트리맵 디스크 사용량 시각화 (WinDirStat 대체) | ✅ |
| `File.Duplicates` | Files | SHA-256 + dHash 중복 파일 탐지기 | ✅ |
| `File.Unlocker` | Files | 파일 잠금 핸들 탐색·강제 해제 도구 | ✅ |
| `Folder.Purge` | Files | 빈 폴더 일괄 탐색·삭제 정리 도구 | ✅ |
| `Hash.Check` | Files | MD5/SHA-1/SHA-256/SHA-512 해시 계산·비교·일괄 검증·변경 감시 | ✅ |
| `Manga.View` | Files | CBZ/CBR/7Z 만화·망가 오프라인 리더 (RTL/이중 페이지) ★24차 | ✅ |
| `PDF.Forge` | Files | 오프라인 PDF 올인원 (병합·분리·압축·워터마크) | ✅ |
| `Shortcut.Forge` | Files | Windows 바로가기(.lnk) 일괄 생성·편집 관리자 | ✅ |
| `Zip.Peek` | Files | ZIP/7z/RAR 추출 없이 트리 탐색·선택 추출 | ✅ |
| `DNS.Flip` | Network | DNS 프리셋 원클릭 전환 트레이 앱 | ✅ |
| `Net.Scan` | Network | LAN ARP 스캐너, 기기 탐지·제조사(OUI) 조회·포트 스캔·핑 모니터 | ✅ |
| `Port.Watch` | Network | 포트 점유 프로세스 모니터 + 원클릭 종료 | ✅ |
| `Serve.Cast` | Network | ASP.NET Core Kestrel 기반 로컬 파일 서버 + QR | ✅ |
| `WiFi.Cast` | Network | Wi-Fi 채널 분석기 + 신호 강도 트레이 모니터 | 🗃️ |
| `Brush.Scale` | Photo.Picture | waifu2x/RealESRGAN ONNX 오프라인 AI 업스케일러 (2x/4x/8x, 배치, Before/After 슬라이더) ★35차 | ✅ |
| `Color.Grade` | Photo.Picture | 이미지 LUT 색보정 도구 | ✅ |
| `Comic.Cast` | Photo.Picture | CBZ/이미지 폴더 만화 슬라이드쇼 프레젠테이션 | ✅ |
| `Img.Cast` | Photo.Picture | SVG/PNG/JPG/BMP → ICO(멀티사이즈)/PNG/JPG/BMP 포맷 변환기 | ✅ |
| `Img.Compare` | Photo.Picture | 픽셀 diff + SSIM/PSNR 이미지 품질 비교기 ★24차 | ✅ |
| `Mosaic.Forge` | Photo.Picture | 이미지 모자이크·픽셀화 효과 변환기 | ✅ |
| `Photo.Video.Organizer` | Photo.Picture | EXIF 날짜 기반 미디어 정리기 | ✅ |
| `SVG.Forge` | Photo.Picture | SVG 벡터 파일 인라인 편집·최적화·PNG 변환 | ✅ |
| `Web.Shot` | Photo.Picture | WebView2 기반 웹페이지 자동 스크린샷 캡처 | ✅ |
| `Boot.Map` | System | Windows 부팅 ETW 타임라인 시각화 | ✅ |
| `Env.Guard` | System | Windows 환경변수 GUI 관리자 + 스냅샷/롤백 | ✅ |
| `Hotkey.Map` | System | 전역 단축키 충돌 감지 + AHK 내보내기 ★24차 | ✅ |
| `Key.Map` | System | 앱별 단축키 키보드 다이어그램 시각화 + PDF 치트시트 | ✅ |
| `Key.Test` | System | 하드웨어 키보드 키 입력 테스트 도구 | ✅ |
| `Layout.Forge` | System | 키보드 키 재배치 프로파일 에디터 | 🗃️ |
| `Pad.Forge` | System | 게임패드 XInput/DInput 버튼 매핑 GUI | ✅ |
| `Sched.Cast` | System | Windows Task Scheduler GUI 대체 | ✅ |
| `Sys.Clean` | System | CCleaner 유사 - 시스템 청소, 레지스트리, 시작프로그램 | ✅ |
| `Tray.Stats` | System | CPU/RAM/GPU/디스크/네트워크 통합 성능 트레이 모니터 | ✅ |
| `Win.Scope` | System | 실행 창 Z-order·투명도·클릭통과 실시간 조작 인스펙터 ★27차 | ✅ |
| `Burn.Rate` | System/Monitor | 배터리 소모율 실시간 분석·잔량 예측 트레이 모니터 | ✅ |
| `Claude.Shell` | System/Manager | Windows 11 새 컨텍스트 메뉴용 COM 쉘 확장 (IContextMenu + shellex) | ✅ |
| `Claude.Shell.Native` | System/Manager | 네이티브 C++ COM DLL 컨텍스트 메뉴 익스텐션 | ✅ |
| `Ctx.Menu` | System/Manager | Windows 탐색기 우클릭 컨텍스트 메뉴 항목 편집기 | ✅ |
| `Ext.Boss` | System/Manager | 파일 확장자 ↔ 앱 연결 일괄 편집·백업 관리자 | ✅ |
| `Mem.Lens` | System/Monitor | 프로세스별 메모리 상세 분석기 (Working Set·히트맵) | ✅ |
| `Path.Guard` | System/Manager | PATH 환경변수 중복·충돌 탐지 및 정리 관리자 | ✅ |
| `Proc.Bench` | System/Monitor | CPU 단일·멀티스레드 / 메모리 대역폭·레이턴시 / 스토리지 IOPS 종합 벤치마크 ★29차 | ✅ |
| `Reg.Vault` | System/Manager | 레지스트리 키 탐색·백업·복원 브라우저 | ✅ |
| `Spec.Report` | System/Monitor | 전체 시스템 사양 리포트 생성·내보내기 | ✅ |
| `Spec.View` | System/Monitor | PC 하드웨어 스펙 스캐너·내보내기 ★29차 | ✅ |
| `Svc.Guard` | System/Manager | Windows 서비스 상태 모니터·시작/중지·자동시작 관리자 | ✅ |
| `ANSI.Forge` | Text | ANSI 아트 에디터 + 터미널 이스케이프 코드 뷰어 ★24차 | ✅ |
| `Char.Art` | Text | 이미지→ASCII/Unicode 아트 변환기 | ✅ |
| `Echo.Text` | Text | 오프라인 TTS, SAPI 음성 선택, 속도/볼륨/음높이, WAV/MP3 내보내기 | ✅ |
| `Mark.View` | Text | Markdown 뷰어 + 실시간 에디터 (멀티탭, TOC, WebView2) | ✅ |
| `Text.Forge` | Text | 해시·인코딩 올인원 (MD5/SHA/Base64/JWT/UUID) | ✅ |
| `Word.Cloud` | Text | 오프라인 워드클라우드 생성기 | ✅ |
| `Badge.Forge` | Tools.Utility | 오프라인 shields.io 스타일 SVG/PNG 배지 생성기 ★24차 | ✅ |
| `Clipboard.Stacker` | Tools.Utility | 클립보드 스택 관리 | ✅ |
| `Code.Snap` | Tools.Utility | 오프라인 코드 스크린샷 미화 도구 | 🗃️ |
| `Dict.Cast` | Tools.Utility | Win+Shift+D 팝업 단어 검색·번역 트레이 앱 | ✅ |
| `Icon.Maker` | Tools.Utility | SVG/PNG→ICO 멀티사이즈 변환 + WriteableBitmap 픽셀 그리드 에디터 ★25차 | ✅ |
| `JSON.Fmt` | Tools.Utility | JSON 붙여넣기 즉시 beautify + 구문 강조 + 오류 줄/열 진단 + Lenient 파싱 ★26차 | ✅ |
| `Mouse.Flick` | Tools.Utility | 전역 마우스 제스처 → 키보드 단축키 매핑 트레이 앱 | ✅ |
| `QR.Forge` | Tools.Utility | 오프라인 QR 코드 생성기 (로고 삽입, 배치) | ✅ |
| `Screen.Recorder` | Video | 화면 녹화 도구 (영역 선택, MP4 출력) | ✅ |
| `Link.Vault` | 🗃️ Tools | 완전 오프라인 북마크 관리자 (페이지 스냅샷) | 🗃️ |
| `Toast.Cast` | 🗃️ Health | 건강 루틴 반복 알림 (눈 휴식, 스트레칭, 물 마시기) | 🗃️ |
| `Code.Time` | Dev/Tray | 파일 변경 감지 기반 프로젝트별 코딩 시간 자동 추적 트레이 ★33차 | 📌 채택 (2026-03-22) |
| `Code.Map` | Dev/Visual | 코드베이스 언어별 LOC 트리맵 시각화 (Disk.Lens의 코드판) ★33차 | 📌 채택 (2026-03-22) |
| `Folder.Watch` | System/Tray | FSW 다중 폴더 변경 감시 트레이, 확장자 필터·Toast 즉시 알림 ★33차 | 📌 채택 (2026-03-22) |
| `Git.Quick` | Dev/Tray | 트레이 팝업 Git status·add·commit·push 원클릭 (LibGit2Sharp) ★33차 | 📌 채택 (2026-03-22) |
| `Img.Strip` | Media/Privacy | EXIF/GPS/IPTC 이미지 메타데이터 일괄 제거기 (무손실) ★33차 | 📌 채택 (2026-03-22) |
| `Pipe.View` | Dev/Debug | Windows Named Pipe IPC 실시간 캡처·디코딩 인스펙터 ★33차 | 📌 채택 (2026-03-22) |
| `Proc.Shot` | System/Auto | 특정 프로세스 창 pHash 변화 감지 자동 스크린샷 스케줄러 ★33차 | 📌 채택 (2026-03-22) |
| `Readme.Craft` | Dev/Docs | README.md 섹션 블록 드래그&드롭 대화형 빌더 ★33차 | 📌 채택 (2026-03-22) |
| `Reg.Watch` | System/Security | 레지스트리 키 변경 실시간 감시 + 변경 전후 스냅샷 트레이 ★33차 | 📌 채택 (2026-03-22) |
| `Tab.Space` | Dev/Quality | 프로젝트 인덴테이션 스타일 감지·일괄 변환 (.editorconfig 지원) ★33차 | 📌 채택 (2026-03-22) |
| `Char.Pad` | Tools.Utility | 특수문자·수학기호·이모지 즐겨찾기 퀵 팝업 입력기 ★34차 | 📌 채택 (2026-03-31) |
| `Case.Forge` | Text | camelCase·snake_case 등 10가지 케이스 전역 팝업 변환기 ★34차 | ✅ 구현 완료 (2026-04-01) |
| `Copy.Path` | Files/Manager | 파일 경로를 다양한 포맷으로 팝업 복사 (전체·슬래시·UNC 등 9종) ★34차 | ✅ 구현 완료 (2026-04-01) |
