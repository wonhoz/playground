# Avoid List — 아이디어 중복 방지 목록

> 이 파일은 브레인스토밍 시 **이미 제안되거나 구현된 아이디어를 다시 꺼내지 않도록** 하는 필터다.
> 새 아이디어를 제안하기 전 반드시 이 목록을 확인할 것.
> 마지막 갱신: 2026-03-14 (24차)
>
> **구조**: 구현완료 앱 → 구현완료 게임 → 아카이브 → 제안된 미구현 아이디어(앱/게임) → 유사 개념 그룹

---

## 1. 구현 완료 앱 (Applications)

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
| `Auto.Build` | Game (구현완료) | 컨베이어·기계·분류기 자동화 파이프라인 게임 |
| `Pad.Forge` | System | 게임패드 XInput/DInput 버튼 매핑 GUI |
| `Tray.Stats` | System | CPU/RAM/GPU/디스크/네트워크 통합 성능 트레이 모니터 |

---

## 2. 구현 완료 게임 (Games)

| 게임 | 장르 |
|------|------|
| `Dungeon.Dash` | Action |
| `Brick.Blitz` | Arcade |
| `Dash.City` | Arcade |
| `Neon.Run` | Arcade |
| `Neon.Slice` | Arcade |
| `Gravity.Flip` | Puzzle |
| `Hue.Flow` | Puzzle |
| `Nitro.Drift` | Racing |
| `Beat.Drop` | Rhythm |
| `Dodge.Blitz` | Shooter |
| `Star.Strike` | Shooter |
| `Tower.Guard` | Strategy |
| `Sand.Fall` | Sandbox/Simulation |
| `Stack.Crash` | Puzzle/Destruction |
| `Cipher.Quest` | Puzzle/Educational |
| `Persp.Shift` | Puzzle/Spatial |

---

## 3. 아카이브 (구현 후 보관)

| 앱/게임 | 카테고리 |
|---------|----------|
| `Ambient.Mixer` | Audio |
| `Sound.Board` | Audio |
| `Commute.Buddy` | Automation |
| `Quick.Launcher` | Tools |
| `Window.Pilot` | Tools |
| `Workspace.Switcher` | Tools |
| `Fist.Fury` | Action game |
| `Track.Star` | Sports game |

---

## 4. 제안된 미구현 아이디어

> 이름만 기재. 상세 내용은 각 차수 idea 파일 참조.

### Apps — Productivity / Creative / Notes

| 이름 | 핵심 요약 |
|------|----------|
| `Clip.Annotate` | 클립보드 이미지 즉석 주석 (화살표·텍스트·블러·크롭) |
| `Thumb.Forge` | 유튜브 썸네일·소셜 OG 이미지 오프라인 빠른 제작 |
| `Mood.Board` | 로컬 무드보드 (이미지 자유 배치·크기·회전, PNG/PDF 내보내기) |
| `Auto.Type` | 텍스트 자동 확장 스니펫 (::addr → 주소) |
| `Macro.Pad` | 화면 소프트웨어 매크로 패드 (버튼 배치, 앱 실행·단축키·스크립트) |
| `Type.Rocket` | 실시간 WPM 타이핑 속도 트래커 + 코드·마크다운 연습 |
| `Mind.Map` | 로컬 오프라인 마인드맵 에디터 (자유 캔버스, PNG/SVG/Markdown 내보내기) |
| `Chart.Forge` | CSV 붙여넣기로 빠른 차트 생성기 (SVG/PNG/클립보드 내보내기) |
| `Pixel.Forge` | 픽셀아트 에디터·애니메이터 (레이어, 프레임, GIF/ICO 내보내기) |
| `Ink.Cast` | 경량 로컬 마크다운 노트 (양방향 링크, 그래프 뷰, Obsidian lite) |
| `Form.Blast` | 문서 템플릿 변수 치환 자동 채우기 (CSV 일괄, DOCX/PDF 출력) |
| `Focus.Log` | 활성 창 자동 감지 앱 사용시간 추적 (생산성 점수, 히트맵) |
| `Timeline.Craft` | 드래그앤드롭 시각적 타임라인 편집기 (SVG/PDF 내보내기) |
| `Render.View` | Mermaid/PlantUML/Graphviz DOT 오프라인 실시간 렌더러 |
| `Task.Cast` | 완전 로컬 Kanban 보드 (드래그&드롭, JSON 단일 파일) |
| `Flash.Card` | SM-2 간격반복 플래시카드 (Anki .apkg 가져오기) |
| `Draw.Board` | 오프라인 인피니트 캔버스 화이트보드 |
| `Cal.Block` | 로컬 시간 블로킹 플래너 (계획 vs 실적 비교, iCal 내보내기) |
| `Num.Board` | 범용 자유 수치 지표 시계열 추적 대시보드 ★22차 |
| `Slide.Cast` | Markdown → 오프라인 발표자 뷰 지원 프레젠테이션 슬라이드 ★23차 |
| `Mouse.Map` | 마우스 이동 궤적·클릭 히트맵 패시브 분석기 ★23차 |

### Apps — Tray / Automation / System

| 이름 | 핵심 요약 |
|------|----------|
| `Sound.Cast` | 오디오 출력 장치 원클릭 전환 트레이 |
| `Net.Speed` | 실시간 네트워크 다운/업 속도 트레이 오버레이 |
| `Thermal.View` | CPU/GPU 온도·팬 속도 실시간 트레이 |
| `Hot.Corner` | Windows 핫 코너 (화면 4모서리 마우스 → 커스텀 동작) |
| `Wake.Cast` | WOL(Wake-on-LAN) 원클릭 PC 기동 트레이 |
| `Night.Cast` | 야간 청색광 차단 트레이 (일출/일몰 자동 계산) |
| `Power.Plan` | 전원 계획 자동 전환 트레이 (AC/배터리 감지) |
| `Watch.Dog` | 프로세스/서비스 감시 → 종료 시 자동 재시작 |
| `Proc.Timeline` | 프로세스별 CPU/메모리/디스크 타임라인 레코더 |
| `Sleep.Cast` | 스마트 절전/종료 스케줄러 트레이 |
| `Tile.Cast` | 키보드 중심 Windows 타일링 WM (FancyZones 대체) |
| `Burn.Rate` | 노트북 배터리 건강도 분석기 (충전 사이클, 방전 곡선) |
| `App.Temp` | 임시 실행 샌드박스 (파일시스템+레지스트리 변경 추적·롤백) |
| `Dupe.Guard` | 실시간 중복 파일 감시 트레이 (FSW + SHA+pHash) |
| `Voice.Cast` | 오프라인 커스텀 음성 Wake Word 트리거 트레이 |
| `BT.Cast` | Bluetooth 기기 원클릭 전환 트레이 |
| `Volume.Cast` | 앱별 볼륨 독립 제어 트레이 |
| `Fade.Out` | 시스템 볼륨 점진 페이드 수면 타이머 트레이 |
| `Noti.Hub` | Windows 알림 히스토리 허브 트레이 (모든 Toast 가로채기) |
| `Desk.Paint` | 시간·날씨·계절 기반 월페이퍼 자동 전환 스케줄러 ★22차 |
| `Time.Lapse` | 화면 타임랩스 레코더 (스크린샷 → MP4/GIF) ★22차 |

### Apps — Dev / Tools

| 이름 | 핵심 요약 |
|------|----------|
| `Git.Reel` | 시각적 Git 커밋 그래프 & 히스토리 탐색기 |
| `Regex.Lab` | 정규식 실시간 테스터 & 패턴 라이브러리 |
| `Json.Craft` | JSON/YAML/TOML 포맷터 + 상호 변환 + diff 뷰 |
| `Secret.Box` | 프로젝트별 .env 파일 AES-256 암호화 보관 |
| `Run.Deck` | 프로젝트 원클릭 런처 (IDE/터미널/스크립트 실행) |
| `Key.Tape` | 매크로 녹화기 (키보드/마우스 액션 녹화 → 재생) |
| `Repo.Radar` | Git 저장소 상태 대시보드 (여러 저장소 현황) |
| `Proc.Pilot` | 프로세스/포트 탐색기 (Port.Watch보다 넓은 범위) |
| `Win.Spy` | 모던 창 계층 인스펙터 (Spy++ 대체, UIAutomation) |
| `Stand.Up` | 개발팀 일일 스탠드업 기록 + Slack/Teams 포맷 복사 |
| `Deploy.Watch` | CI/CD 빌드 상태 트레이 모니터 (GitHub Actions/GitLab) |
| `Glyph.Map` | 유니코드 12만+ 문자 오프라인 탐색·복사·즐겨찾기 |
| `Hex.Peek` | 파일 16진수 뷰어·에디터 (패턴 검색, 구조체 파싱) |
| `Cron.Cast` | Cron 표현식 실시간 시각화 검증기 |
| `GIF.Cast` | 화면 영역 GIF 녹화·변환기 |
| `Patch.View` | .diff/.patch 파일 뷰어 (헝크 단위 선택 적용/스킵) |
| `DB.Peek` | SQLite 드래그&드롭 뷰어+SQL 에디터 |
| `Batch.Flow` | 노코드 파일 처리 파이프라인 빌더 |
| `Alias.Forge` | PowerShell/CMD 별칭 GUI 관리자 |
| `Msg.Forge` | 원문→Slack/Discord/Teams/Twitter 포맷 동시 변환 |
| `Win.Cast` | 특정 앱 창→가상 카메라 스트리밍 |
| `Locale.Forge` | JSON/YAML/RESX i18n 파일 시각적 관리 |
| `Icon.Hunt` | 30만+ 오픈소스 아이콘 라이브러리 탐색기 |
| `Schema.View` | JSON Schema / OpenAPI 3.x 시각적 다이어그램 |
| `Context.Cast` | 코드베이스 파일 트리+내용을 AI 입력용 컨텍스트 블록으로 패킹 |
| `Prompt.Forge` | AI 프롬프트 라이브러리 관리자 |
| `Key.Map` | 앱별 단축키 키보드 다이어그램 시각화 + PDF 치트시트 |
| `Quick.Calc` | 개발자 프로그래머 계산기 (Hex/Bin, IEEE 754 시각화) |
| `Color.Blind` | 실시간 화면 색맹 시뮬레이션 오버레이 |
| `Palette.Gen` | 이미지 → 지배 색상 팔레트 추출기 (k-Means) |
| `Access.Check` | 색상 WCAG 대비율 검사 + 색맹 시뮬레이션 |
| `Table.Craft` | CSV/TSV 경량 GUI 분석기 (100만 행, 필터·집계·Pivot) |
| `Win.Event` | Windows EVT/EVTX 이벤트 로그 경량 뷰어 |
| `Spec.View` | PC 하드웨어 스펙 스캐너·내보내기 |
| `Drive.Bench` | 디스크 벤치마크 (순차/랜덤 R/W, IOPS, S.M.A.R.T) |
| `Snip.Vault` | 코드 스니펫 관리자 (구문 강조, 변수 플레이스홀더) |
| `Ext.Boss` | 파일 형식 연결 관리자 (확장자→기본앱 시각화) |
| `Live.Doc` | 코드 주석 → 실시간 문서 렌더러 |
| `Seq.Shot` | 번호 레이블 자동 추가 튜토리얼 스크린샷 도구 |
| `Find.Fast` | NTFS MFT 즉시 파일명 검색 + 멀티스레드 grep |
| `Unit.Forge` | 완전 오프라인 단위 변환기 (12카테고리, 전역 팝업) |
| `Doc.Weave` | 혼합 포맷 문서 섹션 결합기 (DOCX/PDF/Markdown) |
| `Git.Stats` | Git 저장소 통계 분석기 (커밋 히트맵, 핫파일) |
| `Win.Tamer` | 프로세스 CPU 친화도·우선순위 GUI 관리자 |
| `Font.Draw` | 비트맵·픽셀 폰트 그리드 에디터 |
| `Dice.Shift` (game) | 격자 주사위 굴리기 소코반 퍼즐 |
| `Proto.Forge` | Protocol Buffers·gRPC 오프라인 테스터 |
| `Shader.Cast` | 실시간 HLSL/GLSL 쉐이더 에디터 |
| `Map.Forge` | 오프라인 타일맵 레벨 에디터 (TMX/JSON 내보내기) |
| `Bin.View` | Windows PE(EXE/DLL) 리소스 브라우저 |
| `Perf.Lens` | 게임/앱 DirectX 성능 오버레이 |
| `Pack.Cast` | WinGet/Scoop/Chocolatey 통합 패키지 관리 GUI |
| `Key.Stash` | 개발자 API 키·토큰 로컬 암호화 저장소 |
| `Ctx.Menu` | Windows 우클릭 컨텍스트 메뉴 GUI 편집기 |
| `Clip.Find` | 검색형 클립보드 히스토리 관리자 |
| `Svc.Guard` | Windows 서비스 GUI 관리자 |
| `Run.Book` | 인터랙티브 YAML Runbook/체크리스트 실행기 |
| `Archive.Forge` | ZIP/7z/TAR.GZ 아카이브 생성·관리자 |
| `DNS.Watch` | DNS 쿼리 실시간 모니터·차단 트레이 |
| `Reg.Vault` | Windows 레지스트리 고급 브라우저·비교기 |
| `Audio.Scope` | 시스템 오디오 실시간 스펙트럼 분석기·오실로스코프 |
| `VPN.Cast` | WireGuard/OpenVPN GUI 설정 관리자 |
| `Sprite.Forge` | 스프라이트 시트 패커 & 애니메이터 |
| `IP.Forge` | IPv4/IPv6 서브넷·CIDR·VLSM 계산기 |
| `Net.Trace` | 시각적 Traceroute + 세계지도 지오로케이션 핀 |
| `Topo.Cast` | LAN 네트워크 토폴로지 D3 Force 그래프 자동 생성 |
| `Ping.Map` | 전세계 CDN·클라우드 엔드포인트 지연시간 세계지도 히트맵 |
| `Mesh.View` | 오프라인 3D 메시 뷰어 (OBJ/STL/PLY/GLTF) |
| `MIDI.Forge` | 오프라인 MIDI 시퀀서·피아노롤 에디터 |
| `SVG.Forge` | 오프라인 SVG 벡터 에디터 |
| `Epub.Cast` | EPUB 2/3·MOBI 오프라인 리더 |
| `OCR.Forge` | Tesseract 5 오프라인 문서 일괄 OCR |
| `Score.Cast` | MusicXML/ABC 악보 오프라인 뷰어 + SF2 MIDI 재생 |
| `Vhd.Cast` | VHD/VHDX/ISO 가상 디스크 마운트·관리 트레이 |
| `LLM.Bench` | 로컬 GGUF LLM 실행·채팅 GUI (LlamaSharp) |
| `Serial.Cast` | COM/UART 시리얼 터미널 모니터 + 실시간 그래프 ★22차 |
| `Bit.Cast` | 비트필드·레지스터 시각 에디터 + C 매크로 자동 생성 ★22차 |
| `Merge.Cast` | 시각적 3-way 코드 병합 도구 (Git 충돌 해결) ★22차 |
| `Font.Ramp` | 타이포그래피 스케일 미리보기 + 디자인 토큰 내보내기 ★22차 |
| `Cov.Map` | LCOV/Cobertura 코드 커버리지 트리맵 + 소스 강조 뷰어 ★23차 |
| `Diff.Prompt` | AI 프롬프트 A/B 비교 실험실 (출력 diff + 비용 비교) ★23차 |
| `Cert.View` | X.509 인증서 심층 파서 + 체인 트리 시각화 ★23차 |

### Apps — Network / Security / Privacy

| 이름 | 핵심 요약 |
|------|----------|
| `Cert.Watch` | SSL 인증서 만료 모니터 (30일/7일/1일 Toast 알림) |
| `Uptime.Eye` | 웹사이트·API 가동 여부 HTTP 폴링 트레이 |
| `Wifi.Vault` | 저장된 WiFi 비밀번호 조회 + 공유 QR 생성 |
| `Whisper.Ping` | LAN 내 가족/팀 간 간단 메신저 |
| `Drop.Bridge` | LAN mDNS 파일 전송기 (드래그&드롭, 재개 지원) |
| `Serve.Cast` | 폴더 즉시 HTTP 서버 GUI (CORS, HTTPS, SPA 모드) |
| `SSH.Vault` | ~/.ssh/config GUI 편집·그룹핑·원클릭 터미널 연결 |
| `Cert.Forge` | 로컬 CA 생성·서버 인증서 발급·Windows 신뢰 저장소 설치 |
| `Proxy.Cast` | 로컬 HTTP 프록시 트래픽 인스펙터 (Fiddler 대체) |
| `Host.Edit` | hosts 파일 GUI 편집기 (프로파일 전환, 자동 DNS 플러시) |
| `Pass.Vault` | 완전 로컬 비밀번호 관리자 (AES-256-GCM + Argon2id) |
| `Zone.Watch` | 다중 시간대 세계시계 트레이 (팀원 매핑, 미팅 타임파인더) |
| `Clip.Cast` | LAN mDNS 클립보드 브로드캐스터 (기기 간 즉시 공유) |
| `Proc.Map` | 프로세스-네트워크 연결 실시간 방향 그래프 |
| `Spy.Guard` | 앱의 클립보드·마이크·카메라 무단 접근 감지 + 차단 |
| `App.Cage` | 앱별 네트워크 접근 허용/차단 GUI (Windows Firewall 래퍼) |
| `Crypt.Drop` | 드래그&드롭 AES-256-GCM 파일 암호화 금고 |
| `Net.Ghost` | MAC 주소 스푸퍼·관리자 트레이 |
| `Perm.Audit` | Windows 앱 권한 감사·관리 (카메라·마이크·위치) |
| `Token.Watch` | API 키·서비스 토큰·라이선스 만료일 통합 로컬 대시보드 |
| `WHOIS.Cast` | WHOIS·DNS 레코드·IP 지오로케이션 통합 도메인 조회 GUI ★23차 |
| `SQL.Lens` | SQLite 쿼리 실행 계획 시각화 + 미인덱스 탐지 + 최적화 힌트 ★24차 |
| `Hotkey.Map` | 전역 단축키 충돌 감지 + 미사용 단축키 발굴기 ★24차 |
| `Badge.Forge` | 완전 오프라인 shields.io 스타일 SVG/PNG 배지 생성기 ★24차 |
| `ANSI.Forge` | ANSI 아트 에디터 + 터미널 이스케이프 코드 뷰어 ★24차 |
| `Locale.View` | 200+ 로케일 날짜·숫자·통화·달력 형식 오프라인 브라우저 ★24차 |

### Apps — Screen / UI / Media / Files

| 이름 | 핵심 요약 |
|------|----------|
| `Screen.OCR` | 화면 OCR + 색상 피커 + 픽셀 자 |
| `Privacy.Lens` | 스크린샷 공유 전 개인정보 자동 블러 (로컬 ML) |
| `Paste.Mask` | 화면 공유 중 민감정보 오버레이 마스킹 |
| `Color.Drop` | 단일 화면 색상 피커 |
| `Palette.Cast` | 팔레트 단위 색상 수집·관리·CSS/Tailwind 내보내기 |
| `Font.Scout` | 설치 폰트 미리보기 + 비교 + 페어링 도구 |
| `Font.Probe` | 화면 어디서나 폰트 정보 추출기 (UIAutomation) |
| `Pixel.Tape` | 화면 픽셀 줄자 |
| `Res.Swap` | 해상도·주사율 빠른 전환 트레이 |
| `Stamp.It` | 사진 도장/워터마크 삽입 |
| `Screen.Split` | 화면 구역 시각적 정의 → 창 드래그 스냅 |
| `Clip.Annotate` | 클립보드 이미지 즉석 주석 |
| `Cursor.Lens` | 프레젠테이션·스크린캐스트 커서 스포트라이트+줌 오버레이 |
| `Snap.Cast` | 화면 캡처 영역 → OCR + 마크다운 즉시 변환 |
| `Mosaic.Forge` | 수백~수천 소스 사진을 타일로 포토모자이크 생성 |
| `Photo.Squash` | PNG/JPG/WebP/AVIF 오프라인 일괄 압축 |
| `Video.Trim` | MP4/MKV/MOV 무손실 컷 + FFmpeg 정밀 재인코딩 |
| `Screen.Stitch` | 연속 스크린샷 자동 이어붙이기 |
| `Icon.Forge` | PNG/SVG → ICO/ICNS/Android/iOS 멀티사이즈 일괄 생성 |
| `Img.Forge` | 이미지 일괄 처리기 (리사이즈·변환·필터·워터마크) |
| `Color.Blind` | 실시간 화면 색맹 시뮬레이션 오버레이 |
| `Disk.Lens` | 트리맵 디스크 사용량 시각화 (WinDirStat 대체) |
| `Img.Meta` | 이미지 EXIF/IPTC/XMP 뷰어·편집기 |
| `Geo.Tag` | 사진 GPS 태거 (지도 클릭 수동·GPX 트랙 자동 매칭) |
| `Frame.Pick` | 비디오 최적 프레임 자동 추출기 |
| `Web.Shot` | WebView2 전체 페이지 웹스크린샷 |
| `Sticker.Forge` | Telegram/WhatsApp/Line 커스텀 스티커 팩 제작 ★22차 |
| `Dither.Art` | 이미지 디더링 레트로 픽셀 필터 효과기 ★22차 |
| `Print.Forge` | 프린터 진단·대기열·이력·토너 잔량 통합 관리 GUI ★22차 |
| `OCR.Live` | 화면 영역 실시간 스트리밍 OCR + 번역 파이프라인 ★23차 |
| `Tape.Delay` | 화면 타임시프트 뷰어 (지정 초 딜레이 2번 모니터 표시) ★23차 |
| `Kiosk.Mode` | 이미지·동영상·웹 무한 루프 키오스크 디스플레이 관리자 ★23차 |
| `Manga.View` | CBZ/CBR/7Z 만화·망가 오프라인 리더 (RTL/이중 페이지) ★24차 |
| `Img.Compare` | 픽셀 diff + SSIM/PSNR 이미지 품질 비교기 ★24차 |

### Apps — Productivity / Personal Finance / Health

| 이름 | 핵심 요약 |
|------|----------|
| `Focus.Guard` | 포모도로 타이머 + 집중 시 앱 차단 |
| `Meet.Cost` | 실시간 회의 비용 계산기 (참석자 수 × 시급 × 시간) |
| `Time.Track` | 프리랜서용 프로젝트별 자동 업무시간 추적 + 청구서 |
| `Invoice.Quick` | 프리랜서 초간단 인보이스 생성기 (PDF 내보내기) |
| `Mute.Master` | 전체 미팅 앱(Zoom/Teams/Discord) 원터치 뮤트 토글 |
| `Habit.Chain` | GitHub 잔디 스타일 데스크탑 습관 트래커 |
| `Quick.Memo` | 전역 단축키 플로팅 메모장 |
| `Rush.Clock` | 출발 카운트다운 타이머 |
| `Split.Ring` | 다중 타이머 (여러 개 동시 카운트다운) |
| `Slide.Timer` | 발표자용 타이머 오버레이 (2번 모니터 지원) |
| `Calc.Pop` | 팝업 계산기 (전역 단축키 호출) |
| `Smart.Paste` | 붙여넣기 불가 필드에 SendInput으로 타이핑 우회 |
| `Daily.Dash` | 아침 대시보드 (날씨/일정/RSS 한눈에) |
| `Meeting.Guard` | 화상회의 자동 감지 → 방해금지 모드 자동 전환 |
| `Desk.Radio` | 인터넷 라디오 스트리밍 트레이 앱 |
| `Budget.Cast` | 개인 예산 추적기 (카테고리 자동 분류, 은행 CSV 가져오기) |
| `Health.Log` | 개인 건강 수동 기록기 (체중·혈압·혈당·수면·기분) |
| `Stock.Watch` | 로컬 투자 포트폴리오 추적기 |
| `Book.Log` | 독서 기록·목표 트래커 |
| `Key.Heat` | 패시브 키스트로크 히트맵 분석 트레이 |
| `Trans.Quick` | 오프라인 빠른 번역기 팝업 (Bergamot/Opus-MT) |
| `Dict.Cast` | 완전 오프라인 영어·한국어 사전 + 유의어 사전 |
| `Spell.Cast` | 오프라인 한국어·영어 맞춤법·문법 검사기 |
| `Folder.Sync` | 로컬 폴더 단방향/양방향 동기화 GUI |
| `Font.Sub` | 웹폰트 서브셋 + WOFF2 변환기 |
| `Path.Link` | Windows 심볼릭 링크·정션·하드링크 GUI 관리자 |
| `RSS.Cast` | 완전 오프라인 RSS/Atom 리더 (SQLite, 팟캐스트 오디오) |
| `Theme.Forge` | 색상 테마 팔레트 디자이너 (HSL 색상환, WCAG 검사) |
| `Timer.Chain` | 운동·요리 프로토콜 순차 자동 실행 타이머 체인 ★24차 |

### Apps — Audio / Sound

| 이름 | 핵심 요약 |
|------|----------|
| `Sound.Jar` | 앰비언트 사운드 항아리 UI |
| `Noise.Guard` | 마이크 실시간 노이즈 필터 (가상 마이크 출력) |
| `Breath.Box` | 데스크탑 오버레이 호흡 가이드 (박스 호흡/4-7-8) |
| `Speak.Type` | 로컬 Whisper STT 받아쓰기 (완전 오프라인) |
| `Pitch.Find` | 오디오 파일 BPM + 음악 키 분석기 |
| `Audio.Cut` | 오디오 파형 트리머·편집기 (페이드·정규화·무음 제거) |
| `Karaoke.Cast` | 로컬 가라오케 플레이어 (LRC 동기화, Demucs 보컬 분리) |
| `Flow.Beat` | 바이노럴 비트·집중 음악 로컬 생성기 |
| `Wave.Gen` | 사인파·구형파·핑크노이즈·스위프 정밀 오디오 테스트 신호 생성기 ★24차 |
| `Sample.Forge` | WAV/AIFF 샘플 파형 미리보기·BPM 감지·멀티태그 라이브러리 ★24차 |

### Apps — AI / Family / Social

| 이름 | 핵심 요약 |
|------|----------|
| `AI.Recap` | 회의 트랜스크립트 → Executive Summary + Action Items (Claude API) |
| `Clip.Smart` | AI 스마트 클립보드 처리기 (콘텐츠 자동 감지·번역·요약, AI.Clip 진화형) |
| `Live.Widget` | 데스크탑 앰비언트 위젯 레이어 (날씨·캘린더·RSS·시스템 그래프) |
| `Img.Prompt` | 이미지 AI 프롬프트 빌더 (Danbooru 태그 자동완성, LoRA 라이브러리) |
| `Desk.Pet` | 인터랙티브 투명 데스크탑 펫 (시스템 상태 기반 감정) |
| `Mirror.Cast` | Android USB/WiFi 미러링 scrcpy GUI 래퍼 |
| `Family.Pin` | 가족 공유 메모보드 (로컬 LAN) |
| `Kid.Timer` | 자녀 스크린 사용시간 관리 + 잠금 |
| `Receipt.Snap` | 영수증 OCR → 가계부 자동 기록 |

---

## 5. 제안된 미구현 게임

| 이름 | 장르 | 핵심 메커닉 요약 |
|------|------|-----------------|
| `Glyph.Rush` | Typing Shooter | 화면에 쏟아지는 문자/단어를 타이핑해서 격추 |
| `Mirror.Run` | Arcade (Unique) | 좌우 대칭 두 캐릭터를 단일 입력으로 동시 조작 |
| `Shadow.Run` | Arcade (Self-Race) | 자신의 이전 런 고스트와 경쟁하는 무한 달리기 |
| `Chain.Blast` | Puzzle | 색상 구슬 발사 → 같은 색 3개+ 연쇄 폭발 |
| `Color.Blitz` | Brain/Speed | 스트룹 효과 기반 색상 초고속 매칭 |
| `Flip.Grid` | Puzzle | N×N 격자 타일 뒤집기 (클릭 시 인접 타일 연동) |
| `Stack.Pop` | Casual/Hyper | 컬러 블록 타워 → 같은 색 3레이어 연쇄 폭발 제거 |
| `Worm.Rush` | Arcade (Classic+) | Snake 진화판 — 아이템마다 뱀이 변형 |
| `Pixel.Drop` | Puzzle/Tetris | 목표 픽셀 아트 패턴을 완성하는 테트리스 변형 |
| `Rope.Swing` | Arcade/Physics | 로프 세그먼트 스윙, Verlet Integration |
| `Bounce.House` | Arcade/Physics Shooter | 탄성 반사각, 재질별 반발 계수 |
| `Domino.Chain` | Puzzle/Physics | 도미노 충격량 전달, 연쇄 쓰러짐 |
| `Magnet.Maze` | Puzzle/Physics | 자기력 벡터 필드, 인력·척력 배치, 공 유도 |
| `Cloth.Cut` | Puzzle/Physics | 스프링-질량 천 시뮬레이션, 절단 처리 |
| `Gear.Works` | Puzzle/Engineering | 회전 관절, 기어비, 토크 전달, 벨트·체인 |
| `Vortex.Pull` | Puzzle/Physics | 소용돌이 벡터 필드, RK4 궤도 예측 |
| `Orbit.Craft` | Puzzle/Space Sim | N-체 중력, 궤도 역학, 케플러 법칙 |
| `Rogue.Tile` | Roguelike/Strategy | 절차 생성 던전, 영구 사망, 턴제 전투 |
| `Word.Bomb` | Word/Speed | 음절 포함 단어 입력, 폭탄 타이머 |
| `Ink.Spread` | Puzzle/Strategy | 잉크 BFS 전파, 차단벽 설치 최적화 |
| `Pulse.Run` | Arcade/Rhythm Runner | 음악 파일 BPM 실시간 분석, 비트에 맞춰 장애물 |
| `Neon.Card` | Roguelike/Deckbuilding | 공격·방어·버프 카드 덱빌딩, 매 층 카드 추가·강화 |
| `Shadow.Trap` | Puzzle/Stealth | 실시간 그림자 레이캐스팅, 빛 회피 스텔스 |
| `Neon.Push` | Puzzle/Casual | 소코반+색상 매칭+레이저 연동, 레벨 에디터 |
| `Rune.Match` | Memory/Puzzle/Speed | 룬 심볼 순차 기억·재현, 시간 감소·길이 증가 |
| `Trace.Run` | Puzzle/Casual | 교차 없는 한붓 그리기 해밀턴 경로 |
| `Match.Drop` | Puzzle/Casual | 매치3+중력 방향 전환(상하좌우) |
| `Life.Sim` | Puzzle/Simulation/Sandbox | Conway's Life 규칙, 목표 패턴 달성 퍼즐 |
| `Number.Storm` | Puzzle/Casual | 행/열 선택 슬라이드 숫자 합산(2048 변형) |
| `Bullet.Craft` | Arcade/Bullet Hell+Builder | 탄막 패턴 시각적 에디터, 직접 만든 패턴 생존 도전 |
| `Swarm.Rush` | Shooter/Survival | Boids 군집 AI, 페로몬 폭탄·EMP |
| `Cell.War` | Strategy/Casual | 원형 세포 분열, 크기 충돌 흡수, 영역 점령 |
| `Chrono.Drop` | Puzzle/Platformer | 8초 역행+자기 유령 협동, 최대 3 유령 |
| `Stack.Race` | Arcade/VS | 물리 기반 타워 쌓기 대전, 로컬 2P |
| `Echo.Shot` | Puzzle/Shooter | 음파 탄환 최대 5회 반사, 벽 재질별 효과 |
| `Drone.Haul` | Puzzle/Physics | 4로터 드론+케이블 화물, 관성·케이블 진동 |
| `Phase.Gate` | Puzzle/Platformer | 두 차원 동시 표시, 차원 전환으로 발판 전환 |
| `Volt.Chain` | Puzzle/Casual | 전류 경로 스위치, AND/NOT 게이트·축전기 |
| `Sound.Grid` | Puzzle/Music | N×M 격자 노트 배치 시퀀서, 목표 멜로디 일치 |
| `Fluid.Rush` | Puzzle/Simulation | SPH 유체 시뮬, 색 혼합, 파이프·밸브 배치 |
| `Bridge.Craft` | Puzzle/Engineering | 절점-부재 응력 FEM, 재료별 응력 색상화 |
| `Jelly.Jump` | Arcade/Platformer | Shape Matching Soft Body, 변형으로 퍼즐 해결 |
| `Marble.Run` | Sandbox/Builder | Verlet 구슬+경로 제약, 루프·분기 부품 빌더 |
| `Fracture.Fall` | Puzzle/Destruction | Voronoi 파단+강체 물리, 재질별 균열 패턴 |
| `Zero.G` | Puzzle/Strategy | 뉴턴 역학 무중력, 각운동량 보존, 스러스터 Δv |
| `Wrecking.Ball` | Arcade/Destruction | 비선형 진자(RK4), 철구 스윙, 재질별 건물 파괴 |
| `Tidal.Wave` | Puzzle/Physics | 2D 파동 방정식 finite-difference, 보강·상쇄 간섭 |
| `Spring.Web` | Puzzle/Arcade | Verlet Spring-Mass 거미줄, 낙하체 잡기 |
| `Pinball.Forge` | Arcade/Sandbox | 고속 원형 충돌+플리퍼 관절, 테이블 빌더 |
| `Laser.Net` | Puzzle | 격자 내 거울·프리즘 배치로 레이저 빔 타겟 도달 |
| `Echo.Grid` | Puzzle/Physics | 각도 조준 공 발사 → 벽 반사 → 전체 타일 채우기 |
| `Orb.Wave` | Shooter/Arcade | 코어 주위 궤도 회전 포탑, 360도 웨이브 적 방어 |
| `Signal.Rush` | Rhythm+Shooter | BGM 박자 파동에 맞춰 발사 → Perfect=대미지 2배 |
| `Flow.Pipe` | Puzzle/Casual | N×N 격자 같은 색 점 파이프 연결 |
| `Arc.Blast` | Physics/Arcade | 포물선 탄도 조준·발사 → 구조물·적 파괴 |
| `Hexa.Drop` | Puzzle/Casual | 육각형 격자에 헥사 조각 배치, 완전한 줄/링 소거 |
| `Fuse.Ball` | Puzzle/Action | 단일 입력으로 두 공을 반대 방향 동시 조종 |
| `Warp.Drift` | Arcade/Physics Shooter | RK4 중력 렌즈 탄환 궤도, 블랙홀 커브 샷 |
| `Rift.Jump` | Puzzle/Platformer | 마우스 2점 균열 생성, 모멘텀 보존 순간이동 |
| `Prism.Break` | Puzzle/Optics | 백색광 프리즘 R·G·B 분리, 색 가산 혼합 |
| `Atom.Craft` | Puzzle/Educational | 원자가 규칙 기반 분자 합성, 자유 모드 샌드박스 |
| `Maze.Dread` | Arcade/Survival | Wilson's 절차 생성 미로, 원형 시야 제한 |
| `Colony.Sim` | Simulation/Sandbox | 영양소 확산 + 페로몬 신호 군집 AI |
| `Hex.Storm` | Strategy/Micro RTS | 7~19 헥사 격자, 자원-유닛-전투 30~90초 사이클 |
| `Dish.Rush` | Casual/Action/Time Mgmt | 탑다운 주방 쿠킹 러시 — 주문 큐, 재료 수집→서빙 |
| `Hook.Cast` | Casual/Skill/Simulation | 플라이 피싱 — Verlet 낚싯줄, 물고기 AI, 날씨 |
| `Rush.Cross` | Puzzle/Casual/Strategy | 교차로 교통 신호 타이밍 퍼즐 |
| `Type.Race` | Arcade/Racing/Skill | 타이핑 레이싱 — 문단 타이핑 = 레이서 전진 |
| `Ice.Slide` | Puzzle/Logic | 얼음 블록 슬라이딩 — 막힐 때까지 미끄러짐 |
| `Rise.Match` | Puzzle/Casual | 상승 격자 보석 교환 퍼즐 (Panel de Pon) |
| `Chord.Strike` | Rhythm/Music | 멀티키 동시 누르기 리듬 게임 (화음·아르페지오 레인) |
| `Last.Spark` | Survival/Casual | 캠프파이어 생존 — 모닥불 꺼짐 방지, 자원 수집 |
| `Jenga.Pull` | Arcade/Physics/Casual | 2D 물리 블록 제거 — 드래그로 블록 빼내기 |
| `Leaf.Grow` | Simulation/Puzzle/Creative | L-시스템 식물 성장 샌드박스 |
| `Beat.Rogue` | Roguelite/Rhythm | 비트 타이밍에만 이동·공격 가능, 절차 생성 10층 |
| `Pixel.Cross` | Puzzle/Logic (노노그램) | 행·열 숫자 힌트로 격자 채우기, 픽셀 아트 출현 |
| `Forge.Idle` | Idle/Incremental | 광물 채굴→제련→판매 방치형, 프레스티지 |
| `Tilt.Ball` | Arcade/Physics Puzzle | WASD로 판 기울여 공 유도, 특수 타일 |
| `Room.Code` | Puzzle/Point&Click | 한 화면짜리 오브젝트 클릭&조합 방탈출 20개 컬렉션 |
| `Pool.Break` | Sports/Physics | 정밀 2D 당구 물리 (회전·마찰·쿠션), 8볼·9볼 |
| `Wave.Surf` | Casual/Simulation | 사인파+FFT 노이즈 파도, COM 균형 서핑, 공중 묘기 |
| `Knot.Craft` | Puzzle (위상) | 매듭진 실 교차점 드래그 분리 → 단순 루프 완성 |
| `Fluid.Paint` | Sandbox/Creative | Jos Stam 유체 시뮬 색상 혼합 페인팅 |
| `Reflex.Tap` | Casual/Skill | 반응속도 ms 정밀 측정 4종 테스트 |
| `Neon.Wall` | Arcade/VS | Pong 기반 + 공 궤적이 영구 벽으로 (Tron 혼합) |
| `Spark.Chain` | Strategy/Casual | 제한된 폭죽 배치 → 연쇄 터뜨려 타겟 격파 |
| `Sym.Draw` | Puzzle/Creative | 4배 거울 대칭 실시간 드로잉 만다라 |
| `Tower.Fall` | Strategy/Action | 역 타워디펜스 — 침략자 시점 |
| `Loop.Race` | Racing/Arcade | 프로시저럴 자기교차 트랙 + 이전 랩 궤적이 장애물 |
| `Gravity.Well` | Puzzle/Simulation/Zen | 클릭으로 중력 우물·척력 노드 배치 → 파티클 유도 |
| `Spin.Gate` | Puzzle/Casual/Meditative | 낙하 공 → 클릭 시 90° 회전 게이트 방향 전환 |
| `Echo.Hunt` | Puzzle/Memory | 완전 암흑, 소나 핑 → 1.5초 지형 실루엣 출현 |
| `Burst.Canvas` | Arcade/Survival/Creative | 적 처치 → 색상 잉크 바닥 번짐, 팔레트 비율 달성 |
| `Flux.Drift` | Racing/Arcade/Physics | 차량 N/S 극성, 트랙 극성 구역 인력·척력 레이싱 |
| `Fold.Grid` | Puzzle/Logic/Spatial | N×M 격자 오리가미 접기 퍼즐, 색상 겹침 패턴 |
| `Trap.Rush` | Strategy/Action/Reverse TD | 역방어 함정 배치 — 지뢰·끈끈이·낙하 함정 실시간 |
| `Warp.Ball` | Puzzle/Physics | 화면 경계=토러스 위상, 포탈 쌍 배치로 구슬 유도 |
| `Signal.Cast` | Memory/Puzzle/Brain | 시각+청각 신호 시퀀스 재생 후 키보드 재현 |
| `Pulse.Grid` | Puzzle/Music/Logic | N×M 격자 에미터 → 박자 기반 펄스 전파로 멜로디 |
| `Pendulum.Blitz` | Arcade/Action/Physics | Verlet 다관절 진자 스윙 전투 |
| `Bubble.Pop` | Casual/Arcade/Physics | 표면장력 거품 합치기·분리·파열, 연쇄 파열 콤보 |
| `Crumble.Run` | Arcade/Runner/Physics | Voronoi 파단 지형 붕괴 러너 |
| `Wobble.Stack` | Casual/Arcade/Physics | Shape Matching 소프트 바디 젤리 타워 쌓기 |
| `Slice.Chain` | Puzzle/Casual/Physics | Verlet 제약 체인 절단 퍼즐 |
| `Throw.Stars` | Arcade/Action/Physics | 무기별 완전 다른 비행 물리 (표창·부메랑·창·원반) |
| `Crush.Box` | Sandbox/Casual/Physics | 유압 압착 물리 샌드박스, 물질별 파단 패턴 |
| `Shock.Wave` | Puzzle/Strategy/Physics | 충격파 확산 연쇄 파괴 퍼즐 |
| `Sudoku.Cast` | Puzzle/Logic | Backtracking+DLX 유일해 생성, 4×4~16×16 변형 |
| `Card.Solo` | Casual/Card | 솔리테어 5종 컬렉션 (Klondike·Spider·FreeCell 등) |
| `Astro.Mine` | Arcade/Shooter/Roguelite | 소행성 3단계 분열+중력, 광물 채굴 자원 경제 |
| `Cave.Run` | Roguelike/Adventure | 절차 생성 수직 동굴, 산소 게이지+지진 붕괴 |
| `Logic.Zebra` | Puzzle/Logic | 아인슈타인 격자 소거 퍼즐 |
| `Peg.Solo` | Puzzle/Casual | 말뚝 솔리테어 5형판, DFS 솔버 힌트 |
| `Geo.Quiz` | Casual/Educational | 실루엣·국기·수도·지도 클릭 5모드 |
| `Pipe.Lay` | Puzzle/Casual | 타이머+파이프 회전, BFS 흐름 시뮬 |
| `Trail.Blaze` | Arcade/Platformer | 파쿠르 벽달리기·레지 그랩·슬라이드 콤보 |
| `Swarm.Ant` | Simulation/Strategy | ACO 페로몬 개미 군집 시각화, 멀티 군집 경쟁 |
| `Wave.Form` | Puzzle/Music/Educational | 사인파 조각 드래그 → 목표 합성파 완성, 푸리에 학습 |
| `Crystal.Grow` | Simulation/Sandbox/Puzzle | 셀룰러 오토마타 결정화, 온도·농도·씨앗 위치 조절 |
| `Balance.Act` | Casual/Physics/Arcade | 다양한 형태 블록 정밀 균형 쌓기, 바람·지진 이벤트 |
| `Dice.Shift` | Puzzle/Logic | 격자 위 주사위 굴리기 소코반, 바닥 면 값 변경 |
| `Maze.Craft` | Puzzle/Strategy | 역발상 미로 — 플레이어가 벽 배치, BFS/A* AI 탈출 |
| `Lock.Pick` | Casual/Skill/Puzzle | 자물쇠 해제 미니게임 컬렉션 (핀텀블러·다이얼·슬라이딩) |
| `Frame.Jump` | Platformer/Puzzle/Meta | 만화 패널 메타 플랫포머, 패널 경계 순간이동 |
| `Chip.Logic` | Puzzle/Educational | 논리 게이트 회로 빌더 (AND/OR/NOT/NAND/XOR) |
| `Word.Weave` | Word/Puzzle/Casual | N×N 격자 글자 타일 인접 8방향 체인으로 단어 완성 |
| `Code.Idle` | Idle/Incremental | 코드 라인 생산→앱 출시→팀 고용→AI 자동화 체인 |
| `Magnet.Jump` | Puzzle/Platformer | 캐릭터 N/S 극성 전환, 자기 발판 흡착·반발 도약 |
| `Root.Spread` | Strategy/Simulation | 지하 타일맵 단면뷰, 뿌리 성장 방향 지정 |
| `Zipline.Rush` | Puzzle/Physics | Verlet 케이블 집라인, 화물 중력+마찰 물리 이동 |
| `Spore.Net` | Simulation/Strategy | Physarum 슬라임 몰드, 균류 실 탐색·강화·퇴화 |
| `Echo.Drum` | Rhythm/Memory | 드럼 패턴 재생→정확 재현 (킥/스네어/하이햇) |
| `Block.Shift` | Puzzle/Casual | 색상+기호 타일 4×4 격자, 행/열 루프 슬라이드 |
| `Light.Cast` | Puzzle/Optics | 볼록·오목렌즈·반사경 배치 빛 세기 집중 퍼즐 |
| `Sky.Drift` | Arcade/Physics | Verlet 날개 양력+중력, 열상승기류 탐색 |
| `Case.File` | Puzzle/Detective | 증거 코르크보드 실 연결 + 타임라인 재구성 |
| `Eco.Chain` | Simulation/Sandbox | Lotka-Volterra 포식자-피식자 먹이사슬 생태계 |
| `Shutter.Run` | Puzzle/Creative | 이동 궤적=장노출 빛 드로잉, 목표 실루엣 완성 |
| `Asm.Quest` | Puzzle/Educational | 제한 어셈블리 명령어로 레지스터 목표값 달성, 가상 CPU |
| `Deck.Crawl` | Roguelite/Tactical | 이동·공격·방어 각각 카드 소비 그리드 던전 |
| `Bid.Rush` | Strategy/Bluffing | 제한 코인 실시간 경매 입찰, AI 블러핑 |
| `Puck.Rush` | Sports/Physics | 마찰=0 완전 탄성 충돌 2D 아이스 하키 |
| `Tune.Cast` | Puzzle/Mystery | 라디오 주파수 다이얼 탐색, 냉전 스파이 암호 내러티브 |
| `Word.Crack` | Word/Puzzle | 한국어 자모 분리 피드백 Wordle, 4단어 동시 Quordle |
| `Auto.Battle` | Strategy/Idle/Auto-battler | 6×3 헥스 보드 유닛 배치 오토배틀러 |
| `Mines.X` | Puzzle/Logic | 마인스위퍼 넥스트 — 헥사고날·토러스 보드, 확률 % 오버레이 |
| `Tactics.Rush` | Strategy/Tactics | 6인 분대 턴제 전술 (엄폐·플랭킹), 영구 사망 캠페인 |
| `City.Click` | Idle/Casual | 건물 클릭→인구→세금 체인, 픽셀아트 도시 성장 |
| `Plasma.Drift` | Arcade/Shooter/Physics | 자기장 구역 이동·탄도 방향 굴절, RK4 궤적 예측선 |
| `Spell.Forge` | Arcade/Action/Roguelite | 4원소 2~3개 조합→16+ 마법 합성, 마우스 드로잉 제스처 |
| `Rumble.Box` | Arcade/Physics | 2D 수축 발판 박스 Sumo 밀치기, 로컬 2P |
| `Slide.Path` | Puzzle/Logic | 15-퍼즐 슬라이딩 × 미로 통로 타일 조합 |
| `Virus.Spread` | Strategy/Puzzle | 플레이어=바이러스, 도시 네트워크 그래프 최소 턴 전파 |
| `Chess.Forge` | Strategy/Board | 5종 변형 체스 컬렉션 + Alpha-Beta AI ★22차 |
| `Star.Map` | Puzzle/Educational | 실제 별 데이터 기반 별자리 연결 퍼즐 + 신화 스토리 ★22차 |
| `Haiku.Run` | Narrative/Roguelite | 하이쿠 형식 선택지 텍스트 어드벤처 로그라이트 ★22차 |
| `Flip.Card` | Puzzle/Strategy | 오델로 변형 + 능력 카드 배틀 ★22차 |
| `Net.Weave` | Puzzle/Logic | 그래프 간선 교차 없는 평면화 위상 퍼즐 ★22차 |
| `Time.Farm` | Idle/Simulation | 현실 시계 연동 농사 방치형 ★22차 |
| `Word.Sneak` | Word/Social | 숨겨진 단어 AI 유사도 심판 설명 게임 ★22차 |
| `Mural.Rush` | Arcade/VS | 128px 캔버스 2P 픽셀 영역 전쟁 ★22차 |
| `Bridge.Run` | Arcade/Action | 달리는 캐릭터 앞에 실시간 다리 배치 러너 ★22차 |
| `Ink.Trail` | Puzzle/Casual | 잉크 경로 설계 + 색 혼합 물리 퍼즐 ★22차 |
| `Ear.Train` | Educational/Music | 음정·화음 청음 인식 ELO 기반 훈련 게임 ★23차 |
| `Trade.Star` | Strategy/Space | 가격 변동 성간 무역 경로 최적화 전략 ★23차 |
| `Dots.Cast` | Strategy/Board | Dots and Boxes 격자 선 긋기 박스 완성 전략 ★23차 |
| `Anagram.Dash` | Word/Casual | 타이머 아나그램 — 타일 재조합 단어 발굴 ★23차 |
| `Circuit.Break` | Puzzle/Educational | 버그 있는 전기 회로도 디버깅 퍼즐 ★23차 |
| `Relic.Run` | Puzzle/Adventure | 절차 생성 발굴지 유물 발굴 어드벤처 ★23차 |
| `Frost.Line` | Arcade/Survival | 위에서 내려오는 결빙선 피해 생존 ★23차 |
| `Bloom.Craft` | Strategy/Simulation | 암세포 BFS 확산 vs 면역세포 AI 격자 전략 ★23차 |
| `Pivot.Chase` | Puzzle/Arcade | 45° 각도 제약 이동으로 추격·회피 ★23차 |
| `Snap.Duel` | Casual/2P Local | 반응속도 로컬 2P+ 대전 파티 게임 ★23차 |
| `Cargo.Pack` | Puzzle/Logic | 3D 아이소메트릭 박스 최적 패킹 퍼즐 ★24차 |
| `Rhythm.Type` | Rhythm/Educational | 가사를 박자 타이밍에 맞춰 입력하는 리듬 타이핑 ★24차 |
| `Bubble.Tube` | Puzzle/Casual | 컬러 볼 파이프 소팅 — 같은 색을 같은 튜브로 ★24차 |
| `Escape.Key` | Puzzle/Educational | 현대 암호학(RSA·AES·해시) 기반 방탈출 ★24차 |
| `Rain.Maker` | Strategy/Simulation | 구름·바람 조절 날씨 신으로 농작물 성장 최적화 ★24차 |
| `Color.Map` | Puzzle/Educational | 수학 4색 정리 체험 지도 채색 퍼즐 ★24차 |
| `Phantom.Step` | Arcade/Runner | 내 발자국이 N초 후 함정이 되는 자기 회피 러너 ★24차 |
| `Bug.Hunt` | Puzzle/Casual | 코드 스니펫 버그 줄 클릭 찾기 파티 퀴즈 ★24차 |
| `Chord.Build` | Puzzle/Music | 분위기 목표에 맞는 코드 진행 선택 화성학 퍼즐 ★24차 |
| `Path.Grid` | Strategy/Educational | 격자 최단경로 설계 vs AI 장애물 방해 대결 ★24차 |

---

## 6. 유사 개념 그룹 (중복 주의)

브레인스토밍 시 아래 그룹 내 변형 아이디어는 **이미 제안된 것으로 간주**한다.

| 그룹 | 포함된 아이디어들 |
|------|-----------------|
| **클립보드 관리** | Clipboard.Stacker ✅ · Clip.Find · Clip.Smart |
| **앰비언트 사운드** | Ambient.Mixer ✅(archived) · Sound.Jar |
| **색상 피커 / 팔레트** | Color.Drop · Screen.OCR · Palette.Cast · Color.Palette |
| **QR 코드** | QR.Forge ✅ · QR.Snap |
| **회의 비용 계산** | Meeting.Cost · Meet.Cost |
| **모킹 서버** | Mock.Server ✅ |
| **포트 / 프로세스 관리** | Port.Watch ✅ · Proc.Pilot |
| **OCR / 화면 정보** | Screen.OCR · Snap.Cast · Privacy.Lens |
| **텍스트 변환** | Text.Forge ✅ · Type.Wand |
| **인보이스 / 시간 추적** | Time.Track · Invoice.Quick |
| **SSL 인증서 감시** | Cert.Watch (SSL 전용) · Token.Watch (범용 만료 관리) |
| **폰트 도구** | Font.Scout · Font.Probe · Font.Sub · Font.Ramp |
| **호흡 / 웰니스** | Toast.Cast ✅ · Breath.Box |
| **오디오 출력 전환** | Sound.Cast |
| **네트워크 속도 모니터** | Net.Speed |
| **온도 모니터** | Thermal.View |
| **핫 코너 / 모서리 제스처** | Hot.Corner |
| **마우스 제스처 → 단축키** | Mouse.Flick |
| **텍스트 스니펫 자동확장** | Auto.Type (≠ Smart.Paste) |
| **AI 클립보드 처리** | AI.Clip ✅ · Clip.Smart |
| **비밀키 / .env 보관** | Secret.Box · Key.Stash |
| **음성 입력 / Wake Word** | Speak.Type (STT 받아쓰기) · Voice.Cast (Wake Word 트리거) |
| **성능 모니터 오버레이** | Perf.Lens (DirectX 게임) · Tray.Stats ✅ (트레이 통합) |
| **패키지 관리 GUI** | Pack.Cast |
| **중복 파일** | File.Duplicates ✅ (온디맨드 스캔) · Dupe.Guard (실시간 FSW) |
| **코드 문서화** | Live.Doc |
| **월페이퍼 / 화면 설정** | Desk.Paint (스케줄러) · Res.Swap (해상도 전환) |
| **화면 녹화 계열** | Screen.Recorder ✅ (실시간) · GIF.Cast (짧은 GIF) · Time.Lapse (장기 타임랩스) |
| **진자/스윙 물리 게임** | Pendulum.Blitz · Rope.Swing |
| **거품/유체 물리 게임** | Bubble.Pop · Fluid.Rush · Tidal.Wave |
| **지형 파괴 게임** | Crumble.Run · Fracture.Fall · Stack.Crash ✅ |
| **소프트바디 게임** | Wobble.Stack · Jelly.Jump |
| **체인/로프 절단 게임** | Slice.Chain · Spring.Web · Rope.Swing |
| **투척 물리 게임** | Throw.Stars · Arc.Blast |
| **파괴 샌드박스** | Crush.Box · Fracture.Fall · Jenga.Pull |
| **충격파 / 폭발 게임** | Shock.Wave · Domino.Chain · Spark.Chain |
| **오델로/뒤집기 게임** | Flip.Grid · Flip.Card |
| **별/천문 게임** | Star.Map |
| **텍스트 어드벤처 / 내러티브** | Haiku.Run |
| **그래프/위상 퍼즐** | Knot.Craft (매듭) · Net.Weave (평면화) |
| **잉크/색 번짐 게임** | Ink.Spread (BFS 전파) · Burst.Canvas (생존+잉크) · Ink.Trail (경로 설계) · Fluid.Paint (자유 페인팅) |
| **영역 전쟁 / 2P 대전** | Mural.Rush (픽셀 그래피티) · Cell.War (세포 흡수) |
| **다리 건설** | Bridge.Craft (FEM 퍼즐) · Bridge.Run (실시간 액션) |
| **방치형 게임** | Forge.Idle (채굴) · Code.Idle (코딩) · City.Click (도시) · Time.Farm (농사) |
| **변형 체스** | Chess.Forge |
| **단어 추측/설명 게임** | Word.Sneak (AI 심판) · Word.Bomb · Glyph.Rush |
| **데이터 추적 대시보드** | Health.Log (건강) · Budget.Cast (예산) · Num.Board (범용) |
| **시리얼/하드웨어 통신** | Serial.Cast |
| **레지스터/비트 편집** | Bit.Cast |
| **3-way 코드 병합** | Merge.Cast (≠ Deep.Diff 2-way) |
| **타이포그래피 디자인** | Font.Ramp (스케일+토큰) ≠ Font.Scout (미리보기) |
| **스티커 제작** | Sticker.Forge |
| **이미지 디더링/픽셀 필터** | Dither.Art (≠ Img.Forge 일괄 처리, ≠ Pixel.Forge 그리기 도구) |
| **프린터 관리** | Print.Forge |
| **화면 OCR 실시간** | OCR.Live (스트리밍) ≠ Snap.Cast (1회 스냅샷) ≠ OCR.Forge (문서 배치) |
| **마크다운 프레젠테이션** | Slide.Cast (발표자 뷰+PDF) ≠ Mark.View (단순 뷰어) ≠ Render.View (다이어그램) |
| **화면 딜레이/타임시프트** | Tape.Delay (실시간 딜레이 표시) ≠ Screen.Recorder (파일 저장) ≠ Time.Lapse (타임랩스) |
| **마우스 히트맵** | Mouse.Map (이동+클릭) ≠ Key.Heat (키보드 전용) |
| **코드 커버리지 시각화** | Cov.Map |
| **도메인 WHOIS/DNS 조회** | WHOIS.Cast |
| **키오스크 디스플레이** | Kiosk.Mode |
| **프롬프트 A/B 비교** | Diff.Prompt ≠ Prompt.Forge (라이브러리 저장) |
| **인증서 파싱/분석** | Cert.View (분석) ≠ Cert.Watch (만료 모니터) ≠ Cert.Forge (생성) |
| **음감 훈련 게임** | Ear.Train ≠ Beat.Drop/Chord.Strike (리듬 퍼포먼스) ≠ Echo.Drum (패턴 재현) |
| **무역/교역 전략** | Trade.Star (성간) |
| **선긋기 박스 완성** | Dots.Cast ≠ Net.Weave (그래프 평면화) |
| **아나그램 퍼즐** | Anagram.Dash ≠ Word.Bomb ≠ Word.Crack ≠ Glyph.Rush |
| **회로 디버깅** | Circuit.Break ≠ Volt.Chain (설계) ≠ Chip.Logic (게이트) |
| **발굴/고고학 게임** | Relic.Run |
| **결빙선 생존** | Frost.Line ≠ Last.Spark (모닥불 생존) |
| **세포 확산 전략** | Bloom.Craft ≠ Virus.Spread (도시 그래프) ≠ Colony.Sim (개미) |
| **각도 제약 이동** | Pivot.Chase |
| **반응속도 2P 대전** | Snap.Duel ≠ Reflex.Tap (솔로 훈련) |
| **만화 리더** | Manga.View (CBZ/CBR/PDF 양면+세로) ≠ Mark.View (Markdown) ≠ PDF.Forge (편집) |
| **오디오 테스트 신호 생성** | Wave.Gen (사인/구형/삼각파 출력) ≠ Sample.Forge (드럼 샘플 시퀀서) ≠ Ear.Train (청음 훈련) |
| **순차/체인 타이머** | Timer.Chain (직렬 순서 실행) ≠ Split.Ring (병렬 인터벌) ≠ Sched.Cast (작업 스케줄러) |
| **배지/레이블 이미지 생성** | Badge.Forge (SVG shields.io 스타일) ≠ QR.Forge (QR코드) ≠ Icon.Hunt (아이콘 검색) |
| **ANSI/터미널 아트** | ANSI.Forge (256색 ANSI 에디터) ≠ Char.Art (ASCII 변환) ≠ Word.Cloud (단어 시각화) |
| **SQL 쿼리 계획 시각화** | SQL.Lens (EXPLAIN ANALYZE 트리 뷰어) ≠ DB.Peek (스키마 탐색) ≠ Table.Craft (CSV 편집) |
| **이미지 품질 비교** | Img.Compare (SSIM/PSNR/픽셀 diff) ≠ Deep.Diff (텍스트 diff) ≠ Diff.Prompt (AI 출력 diff) |
| **단축키 충돌 감지** | Hotkey.Map (전역 단축키 시각화+충돌) ≠ Key.Map (리맵핑 설정) ≠ Key.Test (키보드 테스트) |
| **드럼 샘플 시퀀서** | Sample.Forge (WAV 라이브러리+그리드 패턴) ≠ Beat.Drop (리듬 퍼포먼스) ≠ Wave.Gen (신호 생성) |
| **로케일/i18n 브라우저** | Locale.View (날짜·숫자·통화 렌더링 비교) ≠ Dict.Cast (사전) ≠ Text.Forge (텍스트 변환) |
| **3D 아이소메트릭 패킹 퍼즐** | Cargo.Pack ≠ Auto.Build (테트로미노 2D) ≠ Orbit.Craft (궤도 설계) |
| **리듬 타이핑** | Rhythm.Type (비트에 맞춰 타이핑) ≠ Type.Race (속도 경쟁) ≠ Glyph.Rush (글리프 스피드) ≠ Beat.Drop (악기 퍼포먼스) |
| **튜브 색상 소팅** | Bubble.Tube ≠ Hue.Flow (파이프 색 연결) ≠ Color.Map (4색 정리) |
| **암호학 방탈출** | Escape.Key (암호 해독 체인 탈출) ≠ Cipher.Quest (역사적 암호 해독) ≠ Room.Code (일반 퍼즐 방탈출) |
| **날씨 시뮬레이션 퍼즐** | Rain.Maker (구름 배치→강수량 목표) ≠ Sand.Fall (모래 낙하 샌드박스) ≠ Leaf.Grow (L-시스템 성장) |
| **4색 정리 퍼즐** | Color.Map ≠ Bubble.Tube (수직 소팅) ≠ Hue.Flow (파이프 연결) |
| **발자국 함정 러너** | Phantom.Step (발자국 잔상→함정) ≠ Loop.Race (궤도 추월) ≠ Shadow.Run (그림자 2중 이동) |
| **코드 버그 찾기** | Bug.Hunt (소스코드 차이→버그 탐색) ≠ Circuit.Break (전기 회로 디버깅) |
| **화성학 코드 진행 게임** | Chord.Build (코드 연결 설계) ≠ Ear.Train (청음 훈련) ≠ Sound.Grid (사운드 합성) ≠ Chord.Strike (리듬 퍼포먼스) |
| **경로 최적화 대결** | Path.Grid (격자 최단경로 vs AI 장애물) ≠ Trace.Run (실시간 경로 레이싱) |
