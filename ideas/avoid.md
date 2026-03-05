# Avoid List — 아이디어 중복 방지 목록

> 이 파일은 브레인스토밍 시 **이미 제안되거나 구현된 아이디어를 다시 꺼내지 않도록** 하는 필터다.
> 새 아이디어를 제안하기 전 반드시 이 목록을 확인할 것.
> 마지막 갱신: 2026-03-05 (3차)

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
| `Photo.Video.Organizer` | Media | EXIF 날짜 기반 미디어 정리기 |
| `Api.Probe` | Dev/Tools | 미니멀 오프라인 API 테스터 (Postman 대체) |
| `Hash.Forge` → `Text.Forge`로 통합 | Dev/Tools | 해시·인코딩 올인원 (MD5/SHA/Base64/JWT/UUID) |
| `Log.Lens` | Dev/Tools | 로그 파일 분석 뷰어 |
| `Mock.Desk` → `Mock.Server`로 통합 | Dev/Tools | 로컬 Mock HTTP 서버 (GUI 라우트 정의) |
| `Log.Tail` → `Log.Lens`로 통합 | Dev/Tools | 실시간 로그 뷰어 (tail -f, 멀티탭, 색상 규칙) |
| `DNS.Flip` | Network | DNS 프리셋 원클릭 전환 트레이 앱 |
| `Port.Watch` | Network | 포트 점유 프로세스 모니터 + 원클릭 종료 |
| `Clipboard.Stacker` | Productivity | 클립보드 스택 관리 |
| `Code.Snap` | Productivity | 오프라인 코드 스크린샷 미화 도구 |
| `QR.Forge` | Productivity | 오프라인 QR 코드 생성기 (로고 삽입, 배치) |
| `Screen.Recorder` | Productivity | 화면 녹화 도구 |
| `Text.Forge` | Productivity | 텍스트·데이터 변환 도구 |
| `Word.Cloud` | Productivity | 오프라인 워드클라우드 생성기 |
| `Env.Guard` | System | Windows 환경변수 GUI 관리자 + 스냅샷/롤백 |
| `Sys.Clean` | System | CCleaner 유사 - 시스템 청소, 레지스트리, 시작프로그램, 설치프로그램 관리 |
| `Link.Vault` | Tools | 완전 오프라인 북마크 관리자 (페이지 스냅샷) |
| `Mark.View` | Productivity | Markdown 뷰어 + 실시간 에디터 (멀티탭, TOC, WebView2) |
| `Mouse.Flick` | Productivity | 전역 마우스 제스처 → 키보드 단축키 매핑 트레이 앱 |
| `Deep.Diff` | Dev/Tools | 텍스트·이미지·HEX·폴더 파일 비교기 (Myers diff, 픽셀 diff, 편집 모드) |

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

---

## 3. 아카이브 (구현 후 보관)

| 앱/게임 | 카테고리 | 비고 |
|---------|----------|------|
| `Ambient.Mixer` | Audio | 앰비언트 사운드 믹서 |
| `Sound.Board` | Audio | PCM 사운드보드 |
| `Commute.Buddy` | Automation | WiFi 출퇴근 자동 감지 |
| `Quick.Launcher` | Tools | Alt+Space 앱 런처 |
| `Window.Pilot` | Tools | 항상 위 + 투명도 제어 |
| `Workspace.Switcher` | Tools | 창 배치 프리셋 |
| `Fist.Fury` | Action game | — |
| `Track.Star` | Sports game | — |

---

## 4. 제안된 미구현 아이디어

### Productivity / Creative

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Clip.Annotate` | 클립보드 이미지 즉석 주석 (화살표·텍스트·블러·크롭) → 재복사 | idea_20260227 |
| `Thumb.Forge` | 유튜브 썸네일·소셜 OG 이미지 오프라인 빠른 제작 (템플릿, 그라디언트, 텍스트 쉐도우) | idea_20260227 |
| `Mood.Board` | 로컬 무드보드 (이미지 자유 배치·크기·회전, 텍스트 레이어, PNG/PDF 내보내기) | idea_20260227 |
| `Auto.Type` | 텍스트 자동 확장 스니펫 (::addr → 주소, ::date → 오늘 날짜) | idea_20260227 |
| `Macro.Pad` | 화면 소프트웨어 매크로 패드 (버튼 배치, 앱 실행·단축키·스크립트) | idea_20260227 |
| `Type.Rocket` | 실시간 WPM 타이핑 속도 트래커 + 코드·마크다운 연습 + 일별 통계 | idea_20260227 |
| `Mind.Map` | 로컬 오프라인 마인드맵 에디터 (자유 캔버스, 노드 드래그, 방사형/트리 레이아웃, PNG/SVG/Markdown 내보내기) | idea_20260228 |
| `Chart.Forge` | CSV 붙여넣기로 빠른 차트 생성기 (막대·선·원형·도넛·산점도 등, SVG/PNG/클립보드 내보내기) | idea_20260228 |
| `Pixel.Forge` | 픽셀아트 에디터·애니메이터 (8×8~128×128, 레이어, 프레임 애니메이션, GIF/ICO 내보내기) | idea_20260228 |

### Tray / Automation / System

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Sound.Cast` | 오디오 출력 장치 원클릭 전환 트레이 (스피커↔헤드폰, 장치 프리셋) | idea_20260227 |
| `Net.Speed` | 실시간 네트워크 다운/업 속도 트레이 오버레이 + 사용량 로그 | idea_20260227 |
| `Thermal.View` | CPU/GPU 온도·팬 속도 실시간 트레이 + 임계치 Toast 알림 | idea_20260227 |
| `Hot.Corner` | Windows 핫 코너 구현 (화면 4모서리 마우스 → 커스텀 동작) | idea_20260227 |
| `Wake.Cast` | WOL(Wake-on-LAN) 원클릭 PC 기동 트레이 + 핑 상태 모니터 | idea_20260227 |
| `Night.Cast` | 야간 청색광 차단 트레이 (위경도 기반 일출/일몰 자동 계산, 색온도 슬라이더, 앱별 예외) | idea_20260228 |
| `Power.Plan` | 전원 계획 자동 전환 트레이 (AC/배터리 감지, 지정 앱 실행 시 고성능 자동 전환) | idea_20260228 |

### Dev / Tools (개발자 도구) — 신규 제안

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Glyph.Map` | 유니코드 12만+ 문자 오프라인 탐색·복사·즐겨찾기 (이모지·특수문자·HTML 엔티티 동시 복사) | idea_20260227 |
| `Hex.Peek` | 파일 16진수 뷰어·에디터 (패턴 검색, 구조체 파싱 템플릿, 선택 영역 디코딩, 파일 diff 비교) | idea_20260228 |
| `Cron.Cast` | Cron 표현식 실시간 시각화 검증기 (다음 실행 목록, 타임라인 뷰, Unix↔Windows 변환) | idea_20260228 |
| `GIF.Cast` | 화면 영역 GIF 녹화·변환기 (FPS/품질 설정, 마우스 하이라이트, 팔레트 최적화, 트림·속도 조절) | idea_20260228 |

---

### Dev / Tools (개발자 도구) — 기존 제안

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Git.Reel` | 시각적 Git 커밋 그래프 & 히스토리 탐색기 (로컬, libgit2sharp) | idea_20260224 |
| `Regex.Lab` | 정규식 실시간 테스터 & 패턴 라이브러리 (오프라인 regex101) | idea_20260224 |
| `Json.Craft` | JSON/YAML/TOML 포맷터 + 상호 변환 + diff 뷰 + jq 실행 | idea_20260224 |
| `Secret.Box` | 프로젝트별 .env 파일 AES-256 암호화 보관 (≠ Env.Guard) | idea_20260224 |
| `Run.Deck` | 프로젝트 원클릭 런처 (폴더 등록 → IDE/터미널/스크립트 실행) | idea_20260220c |
| `Key.Tape` | 매크로 녹화기 (키보드/마우스 액션 녹화 → 재생) | idea_20260220c |
| `Snap.Diff` | 텍스트·파일·이미지 즉석 비교기 | idea_20260220c |
| `Repo.Radar` | Git 저장소 상태 대시보드 (여러 저장소 브랜치/커밋 현황) | idea_20260220c |
| `Proc.Pilot` | 프로세스/포트 탐색기 (Port.Watch보다 넓은 범위) | idea_20260220c |
| `Win.Spy` | 모던 창 계층 인스펙터 (Spy++ 대체, UIAutomation) | idea_20260225 |
| `Stand.Up` | 개발팀 일일 스탠드업 기록 + Slack/Teams 포맷 복사 | idea_20260224 |
| `Deploy.Watch` | CI/CD 빌드 상태 트레이 모니터 (GitHub Actions/GitLab/Jenkins) | idea_20260224 |

### Network / Monitoring

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Cert.Watch` | SSL 인증서 만료 모니터 (30일/7일/1일 Toast 알림) | idea_20260223, idea_20260225 |
| `Uptime.Eye` | 웹사이트·API 가동 여부 HTTP 폴링 트레이 앱 | idea_20260224 |
| `Wifi.Vault` | 저장된 WiFi 비밀번호 조회 + 공유 QR 생성 | idea_20260224 |
| `Whisper.Ping` | LAN 내 가족/팀 간 간단 메신저 | idea_20260220b |

### Productivity / Automation

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Focus.Guard` | 포모도로 타이머 + 집중 시 앱 차단 | idea.md, idea_20260220a |
| `Meeting.Cost` / `Meet.Cost` | 실시간 회의 비용 계산기 (참석자 수 × 시급 × 시간) | idea_20260223, idea_20260225 |
| `Time.Track` | 프리랜서용 프로젝트별 자동 업무시간 추적 + 청구서 | idea_20260223 |
| `Invoice.Quick` | 프리랜서 초간단 인보이스 생성기 (PDF 내보내기) | idea_20260225 |
| `Mute.Master` | 전체 미팅 앱(Zoom/Teams/Discord) 원터치 뮤트 토글 | idea_20260225 |
| `Habit.Chain` | GitHub 잔디 스타일 데스크탑 습관 트래커 | idea_20260225 |
| `Quick.Memo` | 전역 단축키 플로팅 메모장 | idea_20260220a |
| `Rush.Clock` | 출발 카운트다운 타이머 (집 → 약속장소) | idea_20260220b |
| `Split.Ring` | 다중 타이머 (여러 개 동시 카운트다운) | idea_20260220b |
| `Slide.Timer` | 발표자용 타이머 오버레이 (2번 모니터 지원) | idea_20260225 |
| `Calc.Pop` | 팝업 계산기 (전역 단축키 호출) | idea_20260220b |
| `Smart.Paste` | 붙여넣기 불가 필드에 SendInput으로 타이핑 우회 | idea_20260225 |
| `Daily.Dash` | 아침 대시보드 (날씨/일정/RSS 한눈에) | idea_20260220a |
| `Meeting.Guard` | 화상회의 자동 감지 → 방해금지 모드 자동 전환 | idea_20260220a |
| `Desk.Radio` | 인터넷 라디오 스트리밍 트레이 앱 (NAudio) | idea_20260225 |

### Screen / UI Tools

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Screen.Pickup` / `Screen.OCR` | 화면 OCR + 색상 피커 + 픽셀 자 | idea.md, idea_20260220a |
| `Privacy.Lens` | 스크린샷 공유 전 개인정보 자동 블러 (로컬 ML) | idea_20260225 |
| `Paste.Mask` | 화면 공유 중 민감정보 오버레이 마스킹 | idea_20260223 |
| `Color.Drop` | 단일 화면 색상 피커 | idea_20260220b |
| `Palette.Cast` / `Color.Palette` | 팔레트 단위 색상 수집·관리·CSS/Tailwind 내보내기 | idea_20260224, idea_20260225 |
| `Font.Scout` | 설치 폰트 미리보기 + 비교 + 페어링 도구 | idea_20260224 |
| `Font.Probe` | 화면 어디서나 폰트 정보 추출기 (UIAutomation) | idea_20260225 |
| `Pixel.Tape` | 화면 픽셀 줄자 | idea_20260220b |
| `Res.Swap` | 해상도·주사율 빠른 전환 트레이 (게임 ↔ 작업) | idea_20260224 |
| `Stamp.It` | 사진 도장/워터마크 삽입 (날짜, 로고) | idea_20260220b |
| `Screen.Split` | 화면 구역 시각적 정의 → 창 드래그 스냅, 레이아웃 저장/전환, 창 배치 스냅샷 복원 | idea_20260228 |

### Audio / Sound

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Sound.Jar` | 앰비언트 사운드 항아리 UI (Ambient.Mixer 변형) | idea_20260220b |
| `Noise.Guard` | 마이크 실시간 노이즈 필터 (가상 마이크 출력) | idea_20260223 |
| `Breath.Box` | 데스크탑 오버레이 호흡 가이드 (박스 호흡/4-7-8) | idea_20260225 |

### Family / Social

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Family.Pin` | 가족 공유 메모보드 (로컬 LAN) | idea_20260220a |
| `Kid.Timer` | 자녀 스크린 사용시간 관리 + 잠금 | idea_20260220a |
| `Receipt.Snap` | 영수증 OCR → 가계부 자동 기록 | idea_20260220a |

### Security / Privacy

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Spy.Guard` | 앱의 클립보드·마이크·카메라 무단 접근 감지 + 차단 | idea_20260225 |
| `App.Cage` | 앱별 네트워크 접근 허용/차단 GUI (Windows Firewall 래퍼, 임시 허용 타이머, 프로필 저장) | idea_20260228 |

### Text / Data Tools

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Type.Wand` | 텍스트 즉석 변환 (대소문자, 케이스, 인코딩 등) — Text.Forge와 유사 | idea_20260220b |
| `Subtitle.Sync` | SRT 자막 싱크 오프셋 조정기 | idea.md |

### System / Files / Media

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Disk.Lens` | 트리맵(Squarified) 디스크 사용량 시각화 (WinDirStat 대체, 드릴다운, 확장자 색상 필터) | idea_20260302 |
| `Img.Forge` | 이미지 일괄 처리기 (리사이즈·변환·필터·워터마크, 폴더 감시 자동 처리) | idea_20260302 |

### Security

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Pass.Vault` | 완전 로컬 비밀번호 관리자 (AES-256-GCM + Argon2id, Windows Hello, 감사 리포트) | idea_20260302 |

### Tray / Network

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Zone.Watch` | 다중 시간대 세계시계 트레이 (팀원 매핑, 미팅 타임파인더, 세계지도 야간 경계선) | idea_20260302 |
| `Drop.Bridge` | LAN mDNS 파일 전송기 (드래그&드롭, 재개 지원, AirDrop Windows 대체) | idea_20260302 |
| `Serve.Cast` | 폴더 즉시 HTTP 서버 GUI (CORS 설정, HTTPS 자가 서명, SPA 모드, QR 접속) | idea_20260302 |

### Productivity / Notes / Finance

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Ink.Cast` | 경량 로컬 마크다운 노트 (양방향 링크, 그래프 뷰, 태그, FTS, Obsidian lite) | idea_20260302 |
| `Form.Blast` | 문서 템플릿 변수 치환 자동 채우기 (CSV 일괄, DOCX/PDF 출력, 조건부 블록) | idea_20260302 |
| `Focus.Log` | 활성 창 자동 감지 앱 사용시간 추적 (생산성 점수, 히트맵, 제한 알림) | idea_20260302 |
| `Budget.Cast` | 개인 예산 추적기 (카테고리 자동 분류, 월별 예산 한도, 차트, 은행 CSV 가져오기) | idea_20260302 |

### Dev / Tools / Data

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Access.Check` | 색상 WCAG 대비율 검사 + 색맹 시뮬레이션 (팔레트 매트릭스, 개선 제안) | idea_20260302 |
| `Table.Craft` | CSV/TSV 경량 GUI 분석기 (100만 행 가상 스크롤, 필터·집계·Pivot, 계산 컬럼) | idea_20260302 |

### System / Files / Dev / Audio (신규 — idea_20260305_3차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Speak.Type` | 로컬 Whisper STT 받아쓰기 (전역 단축키, 완전 오프라인, 클립보드 자동 붙여넣기) | idea_20260305c |
| `File.Guard` | 감시 폴더 파일 변경 이벤트 감사 로거 (어떤 프로세스가 무엇을 건드렸나, 스냅샷 비교) | idea_20260305c |
| `Net.Scan` | LAN ARP 스캐너 (기기 탐지·제조사 조회·포트 스캔·새 기기 알림) | idea_20260305c |
| `Locale.Forge` | JSON/YAML/RESX i18n 파일 시각적 관리 (언어별 나란히 편집, 누락 키 감지, 미사용 키 탐지) | idea_20260305c |
| `Icon.Hunt` | 30만+ 오픈소스 아이콘 라이브러리 탐색기 (Fluent·Material·Phosphor 등, SVG/PNG 다운로드) | idea_20260305c |
| `Schema.View` | JSON Schema / OpenAPI 3.x 시각적 다이어그램 + 스키마 검증 도구 | idea_20260305c |
| `Boot.Map` | Windows 부팅 ETW 타임라인 시각화 (드라이버·서비스별 지연 분석, 병목 식별) | idea_20260305c |
| `Clip.Cast` | LAN mDNS 클립보드 브로드캐스터 (텍스트·이미지 기기 간 즉시 공유, AES-256 암호화) | idea_20260305c |
| `Pitch.Find` | 오디오 파일 BPM + 음악 키 분석기 (NAudio, 일괄 분석, ID3 태그 자동 기록) | idea_20260305c |
| `Term.Pad` | 전역 단축키 플로팅 터미널 패드 (멀티탭, 투명도, 스니펫 사이드바, 세션 복원) | idea_20260305c |

### System / Files / Dev (신규 — idea_20260305_2차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Hash.Check` | 파일 드래그&드롭 → MD5/SHA256/SHA512 계산, 예상 해시 자동 비교, .sha256 파일 파싱, 폴더 일괄 검증 | idea_20260305b |
| `Watch.Dog` | 프로세스/서비스 감시 → 종료 시 자동 재시작, 최대 재시작 횟수·딜레이 설정, 트레이 상주 | idea_20260305b |
| `Echo.Text` | 오프라인 TTS (SAPI/Edge Neural), 속도·피치 조절, WAV/MP3 내보내기, 클립보드 즉시 읽기 | idea_20260305b |
| `Proc.Timeline` | 프로세스별 CPU/메모리/디스크 타임라인 레코더, 이벤트 마커, CSV 내보내기 | idea_20260305b |
| `Patch.View` | .diff/.patch 파일 뷰어 (구문 강조), 헝크 단위 선택 적용/스킵, 역방향 revert, 미리보기 | idea_20260305b |
| `DB.Peek` | SQLite 드래그&드롭 뷰어+SQL 에디터, JSON/CSV 테이블 파싱, 결과 CSV/JSON 내보내기 | idea_20260305b |
| `Batch.Flow` | 노코드 파일 처리 파이프라인 빌더 (필터→변환→액션 블록 조합, 폴더 감시, 드라이런) | idea_20260305b |
| `Alias.Forge` | PowerShell/CMD 별칭 GUI 관리자, 충돌 감지, 카테고리 태그, 즉시 프로파일 적용 | idea_20260305b |
| `Msg.Forge` | 원문→Slack/Discord/Teams/Twitter/LinkedIn 포맷 동시 변환, 문자 수 제한 체크, 템플릿 | idea_20260305b |
| `Win.Cast` | 특정 앱 창→가상 카메라 스트리밍 (Zoom/Teams 소스 선택, DXGI 저CPU, 배경 주석) | idea_20260305b |

### Files / Productivity / Media (신규 — idea_20260305)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `PDF.Forge` | 오프라인 PDF 올인원 (병합·분리·페이지 조작·압축·워터마크, PdfSharpCore) | idea_20260305 |
| `Zip.Peek` | ZIP/7z/RAR/TAR.GZ 추출 없이 트리 탐색·내부 미리보기·선택 추출·내부 텍스트 검색 | idea_20260305 |
| `Photo.Squash` | PNG/JPG/WebP/AVIF 오프라인 일괄 압축 (Lossy/Lossless 슬라이더, Before/After 비교, 폴더 감시) | idea_20260305 |
| `Video.Trim` | MP4/MKV/MOV 무손실 컷 + FFmpeg 정밀 재인코딩 트리머 (다중 구간, GIF 변환) | idea_20260305 |
| `Screen.Stitch` | 연속 스크린샷 자동 이어붙이기 (SIFT 특징점 매칭, 자동 스크롤 캡처 모드, PDF 출력) | idea_20260305 |

### Dev/Tools / Network / Security (신규 — idea_20260305)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `SSH.Vault` | `~/.ssh/config` GUI 편집·그룹핑·원클릭 터미널 연결·키 생성·ssh-agent 관리·터널 시각화 | idea_20260305 |
| `Icon.Forge` | PNG/SVG → ICO/ICNS/Android/iOS/WinStore 멀티사이즈 일괄 생성·미리보기·배지 도우미 | idea_20260305 |
| `Host.Edit` | hosts 파일 GUI 편집기 (프로필 전환, 체크박스 토글, 자동 DNS 플러시, 핑 상태) | idea_20260305 |
| `Cert.Forge` | 로컬 CA 생성·서버 인증서 발급·Windows 신뢰 저장소 설치·PFX/PEM 변환 (mkcert GUI 대체) | idea_20260305 |
| `Proxy.Cast` | 로컬 HTTP 프록시 트래픽 인스펙터 (요청·응답 실시간 캡처, Replay, HAR 내보내기, Fiddler 대체) | idea_20260305 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260227)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Glyph.Rush` | Typing Shooter | 화면에 쏟아지는 문자/단어를 타이핑해서 격추, WPM = 전투력 |
| `Mirror.Run` | Arcade (Unique) | 화면 좌우 대칭 두 캐릭터를 단일 입력으로 동시 조작, 비대칭 장애물 |
| `Shadow.Run` | Arcade (Self-Race) | 자신의 이전 런 고스트와 경쟁하는 무한 달리기 |
| `Chain.Blast` | Puzzle | 색상 구슬 발사 → 같은 색 3개+ 연쇄 폭발 (Puzzle Bobble 변형) |
| `Color.Blitz` | Brain / Speed | 스트룹 효과 기반 색상 초고속 매칭 (글자 의미 vs 실제 색) |
| `Flip.Grid` | Puzzle | N×N 격자 타일 뒤집기 (클릭 시 인접 타일 연동), 전체 같은 색 목표 |
| `Stack.Pop` | Casual / Hyper | 컬러 블록 타워 → 같은 색 3레이어 연쇄 폭발 제거 |
| `Worm.Rush` | Arcade (Classic+) | Snake 진화판 — 아이템마다 뱀이 변형 (속도, 역방향, 벽통과 등) |
| `Pixel.Drop` | Puzzle / Tetris | 목표 픽셀 아트 패턴을 완성하는 테트리스 변형 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260302)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Rope.Swing` | Arcade / Physics | 로프 세그먼트 스윙, Verlet Integration, 앵커 해제·재결합 |
| `Bounce.House` | Arcade / Physics Shooter | 탄성 반사각, 재질별 반발 계수, 적 격파 |
| `Stack.Crash` ✅ | Puzzle / Destruction | 강체 물리(Box2D), 재질별 블록 탑 무너뜨리기 |
| `Sand.Fall` ✅ | Sandbox / Simulation | 셀룰러 오토마타, 물질 반응 (모래·물·불·기름·씨앗) |
| `Domino.Chain` | Puzzle / Physics | 도미노 충격량 전달, 연쇄 쓰러짐 퍼즐 |
| `Magnet.Maze` | Puzzle / Physics | 자기력 벡터 필드, 인력·척력 배치, 공 유도 |
| `Cloth.Cut` | Puzzle / Physics | 스프링-질량 천 시뮬레이션, 절단 처리 |
| `Gear.Works` | Puzzle / Engineering | 회전 관절, 기어비, 토크 전달, 벨트·체인 |
| `Vortex.Pull` | Puzzle / Physics | 소용돌이 벡터 필드, RK4 궤도 예측 |
| `Orbit.Craft` | Puzzle / Space Sim | N-체 중력, 궤도 역학, 케플러 법칙 |
| `Rogue.Tile` | Roguelike / Strategy | 절차 생성 던전, 영구 사망, 턴제 전투, 아이템 시너지 |
| `Word.Bomb` | Word / Speed | 음절 포함 단어 입력, 폭탄 타이머, 콤보 |
| `Ink.Spread` | Puzzle / Strategy | 잉크 BFS 전파, 차단벽 설치 최적화, 영역 분리 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260305_3차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Pulse.Run` | Arcade / Rhythm Runner | 음악 파일 BPM 실시간 분석, 비트에 맞춰 장애물 등장, 커스텀 음악 지원 |
| `Auto.Build` | Puzzle / Automation / Sandbox | 컨베이어·기계·분류기 배치 자동화 파이프라인, 목표 처리량 달성, 샌드박스 모드 |
| `Neon.Card` | Roguelike / Deckbuilding | 공격·방어·버프 카드 덱빌딩, 매 층 카드 추가·제거·강화, 캐릭터별 고유 덱 |
| `Shadow.Trap` | Puzzle / Stealth | 실시간 그림자 레이캐스팅, 빛 회피 스텔스 이동, 장애물 회전으로 그림자 경로 생성 |
| `Neon.Push` | Puzzle / Casual | 소코반+색상 매칭+레이저 연동, 얼음·철 특수 상자, 레벨 에디터 |
| `Rune.Match` | Memory / Puzzle / Speed | 룬 심볼 순차 기억·재현, 표시 시간 감소·길이 증가, 특수 변형 조건 |
| `Trace.Run` | Puzzle / Casual | 교차 없는 한붓 그리기 해밀턴 경로, 방향 제한 노드, 절차적 무한 생성 |
| `Match.Drop` | Puzzle / Casual | 매치3+중력 방향 전환(상하좌우), 중력 쏠림으로 연쇄 폭발 조합 |
| `Life.Sim` | Puzzle / Simulation / Sandbox | Conway's Life 규칙, 초기 배치→목표 패턴 달성 퍼즐, 역산 퍼즐, 커스텀 룰 셋 |
| `Number.Storm` | Puzzle / Casual | 행/열 선택 슬라이드 숫자 합산(2048 변형), 폭탄·잠금·배수기 특수 타일 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260305_2차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Bullet.Craft` | Arcade / Bullet Hell + Builder | 탄막 패턴 시각적 에디터 (파동·나선·부채꼴), 직접 만든 패턴 생존 도전, UGC 공유 |
| `Swarm.Rush` | Shooter / Survival | Boids 군집 AI (분리·정렬·응집), 페로몬 폭탄·EMP 특수무기, 흩어졌다 재집결하는 적 |
| `Cell.War` | Strategy / Casual | 원형 세포 분열(절반으로 나뉨), 크기 충돌(큰 쪽 흡수), 영역 점령 전략 |
| `Chrono.Drop` | Puzzle / Platformer | 8초 역행+자기 유령 협동, 과거 자신이 버튼 유지, 최대 3 유령 동시 활용 |
| `Stack.Race` | Arcade / VS | 물리 기반 타워 쌓기 대전, 랜덤 블록 공급, 진동파·바람 방해 아이템, 로컬 2P |
| `Echo.Shot` | Puzzle / Shooter | 음파 탄환 최대 5회 반사, 보강간섭 2배 대미지, 벽 재질(거울·스폰지·프리즘) |
| `Drone.Haul` | Puzzle / Physics | 4로터 드론+케이블 화물, 관성·케이블 진동 물리, 정밀 하강·좁은 틈 통과 |
| `Phase.Gate` | Puzzle / Platformer | 두 차원 동시 표시(파란/붉은 세계), 차원 전환으로 발판·장벽 전환, 차원별 물리 레이어 |
| `Volt.Chain` | Puzzle / Casual | 전류 경로 스위치 순서 조작, 과부하 합선 방지, AND/NOT 게이트·축전기 부품 |
| `Sound.Grid` | Puzzle / Music | N×M 격자 노트 배치 시퀀서, 목표 멜로디 일치 퍼즐, 자유 창작+공유 코드 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260305)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Fluid.Rush` | Puzzle / Simulation | SPH 유체 시뮬레이션, 색 혼합(빨강+파랑=보라), 파이프·밸브 배치로 단일색 분리 |
| `Bridge.Craft` | Puzzle / Engineering | 절점-부재 응력 FEM 근사, 재료(나무/철/케이블), 응력 색상화, 예산 최적화 |
| `Jelly.Jump` | Arcade / Platformer | Shape Matching Soft Body, 점 집합+스프링, 변형으로 퍼즐 해결 (좁은 틈·레버) |
| `Marble.Run` | Sandbox / Builder | Verlet 구슬+경로 제약, 루프·분기 부품 빌더, 구슬 추적 카메라, 샌드박스 공유 |
| `Fracture.Fall` | Puzzle / Destruction | Voronoi 파단+강체 물리, 재질별 균열 패턴, 목표만 파괴·보호 대상 보존 |
| `Zero.G` | Puzzle / Strategy | 뉴턴 역학 무중력, 각운동량 보존, 스러스터 Δv 제한, 연료 없이 우아한 도킹 |
| `Wrecking.Ball` | Arcade / Destruction | 비선형 진자(RK4), 철구 스윙 타이밍, 재질별 건물 파괴, 연쇄 붕괴 콤보 |
| `Tidal.Wave` | Puzzle / Physics | 2D 파동 방정식 finite-difference, 보강·상쇄 간섭, 진동원 주파수·위상 조절 |
| `Spring.Web` | Puzzle / Arcade | Verlet Spring-Mass 거미줄, 인장 강도 초과 끊김, 낙하체 잡기, 특수 실 종류 |
| `Pinball.Forge` | Arcade / Sandbox | 고속 원형 충돌+플리퍼 관절, 테이블 빌더+플레이+공유, 범퍼·자석·레일 재질 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260228)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Laser.Net` | Puzzle | 격자 내 거울·프리즘 배치로 레이저 빔을 모든 타겟에 도달 — 반사 경로 네온 시각화 |
| `Echo.Grid` | Puzzle / Physics | 각도 조준 공 발사 → 벽 반사 이동 → 지나간 타일 페인팅 → 전체 타일 채우기 |
| `Orb.Wave` | Shooter / Arcade | 코어 주위 궤도 회전 포탑, 마우스로 이동·클릭으로 발사, 360도 방향 웨이브 적 방어 |
| `Signal.Rush` | Rhythm + Shooter | BGM 박자 파동에 맞춰 발사 → Perfect 타이밍 = 대미지 2배, 적이 박자에 맞춰 이동 |
| `Flow.Pipe` | Puzzle / Casual | N×N 격자 같은 색 점을 파이프로 연결 + 빈 칸 없이 전체 셀 가득 채우기 |
| `Arc.Blast` | Physics / Arcade | 포물선 탄도 조준·발사 → 구조물·적 파괴 (나무/돌/얼음/폭발물, 기본/분열/관통 탄종) |
| `Hexa.Drop` | Puzzle / Casual | 육각형 격자에 헥사 조각 드래그 배치 → 완전한 줄/링 형성 시 소거 + 연쇄 콤보 |
| `Fuse.Ball` | Puzzle / Action | 단일 입력으로 두 공을 반대 방향 동시 조종 → 각자 목표 지점에 동시 도달 |

---

## 5. 유사 개념 그룹 (중복 주의)

브레인스토밍 시 아래 그룹 내 변형 아이디어는 **이미 제안된 것으로 간주**한다.

| 그룹 | 포함된 아이디어들 |
|------|-----------------|
| **클립보드 관리** | Clipboard.Stacker ✅ · Clip.Pad · Clipboard.Manager |
| **앰비언트 사운드** | Ambient.Mixer ✅(archived) · Sound.Jar |
| **색상 피커 / 팔레트** | Color.Drop · Screen.Pickup · Palette.Cast · Color.Palette |
| **QR 코드** | QR.Forge ✅ · QR.Snap |
| **회의 비용 계산** | Meeting.Cost · Meet.Cost |
| **모킹 서버** | Mock.Desk ✅ · Mock.Server |
| **포트 / 프로세스 관리** | Port.Watch ✅ · Proc.Pilot |
| **OCR / 화면 정보** | Screen.OCR · Screen.Pickup · Privacy.Lens |
| **텍스트 변환** | Text.Forge ✅ · Type.Wand |
| **인보이스 / 시간 추적** | Time.Track · Invoice.Quick |
| **SSL 인증서 감시** | Cert.Watch (두 파일에서 중복 제안됨) |
| **폰트 도구** | Font.Scout · Font.Probe |
| **호흡 / 웰니스** | Toast.Cast ✅ · Breath.Box |
| **오디오 출력 전환** | Sound.Cast |
| **네트워크 속도 모니터** | Net.Speed |
| **온도 모니터** | Thermal.View |
| **핫 코너 / 모서리 제스처** | Hot.Corner |
| **마우스 제스처 → 단축키** | Mouse.Flick |
| **텍스트 스니펫 자동확장** | Auto.Type (≠ Smart.Paste) |
| **매크로 패드 / 런처 버튼** | Macro.Pad (≠ Run.Deck, Key.Tape) |
| **무드보드 / 이미지 콜라주** | Mood.Board |
| **이미지 주석 / 스크린샷 편집** | Clip.Annotate |
| **썸네일 / 소셜 이미지 제작** | Thumb.Forge |
| **유니코드 / 특수문자 탐색** | Glyph.Map |
| **타이핑 속도 / WPM 연습** | Type.Rocket |
| **WOL / 원격 PC 기동** | Wake.Cast |
| **타이핑 Shooter 게임** | Glyph.Rush |
| **좌우 대칭 조작 게임** | Mirror.Run |
| **고스트 레이스 달리기** | Shadow.Run |
| **색상 구슬 연쇄 폭발** | Chain.Blast |
| **스트룹 색상 퍼즐** | Color.Blitz |
| **타일 뒤집기 퍼즐** | Flip.Grid |
| **컬러 타워 폭발** | Stack.Pop |
| **슬라이싱 아케이드** | Neon.Slice ✅ |
| **Snake 진화판** | Worm.Rush |
| **픽셀아트 테트리스** | Pixel.Drop |
| **레이저 반사 퍼즐** | Laser.Net |
| **반사 공 타일 페인팅** | Echo.Grid |
| **궤도 회전 포탑 슈터** | Orb.Wave |
| **리듬+슈터 혼합** | Signal.Rush |
| **파이프 연결 퍼즐** | Flow.Pipe |
| **포물선 탄도 파괴 슈터** | Arc.Blast |
| **헥사고날 블록 퍼즐** | Hexa.Drop |
| **듀얼 볼 반대 방향 조종** | Fuse.Ball |
| **GIF 화면 녹화** | GIF.Cast (≠ Screen.Recorder) |
| **야간 청색광 차단** | Night.Cast |
| **오프라인 마인드맵** | Mind.Map |
| **픽셀아트 에디터·애니메이터** | Pixel.Forge |
| **빠른 차트 생성기** | Chart.Forge |
| **헥스 뷰어·에디터** | Hex.Peek |
| **Cron 표현식 검증기** | Cron.Cast |
| **전원 계획 자동 전환** | Power.Plan |
| **앱별 네트워크 차단** | App.Cage |
| **창 분할 레이아웃 매니저** | Screen.Split (≠ Workspace.Switcher) |
| **트리맵 디스크 시각화** | Disk.Lens (≠ Photo.Video.Organizer) |
| **CCleaner 유사 PC 청소기** | Sys.Clean (시스템 청소 + 레지스트리 + 시작프로그램 + 설치프로그램) |
| **이미지 일괄 처리** | Img.Forge (≠ Photo.Video.Organizer: 날짜 정리기) |
| **로컬 비밀번호 관리자** | Pass.Vault (≠ Secret.Box: .env 파일 특화) |
| **다중 시간대 세계시계** | Zone.Watch |
| **LAN 파일 전송** | Drop.Bridge (≠ Whisper.Ping: 메신저) |
| **경량 마크다운 노트** | Ink.Cast (≠ Quick.Memo: 단순 플로팅 메모) |
| **문서 템플릿 변수 치환** | Form.Blast (≠ Auto.Type: 키 스니펫 확장) |
| **앱 사용시간 자동 추적** | Focus.Log (≠ Time.Track: 프리랜서 청구서) |
| **색상 접근성 WCAG 검사** | Access.Check |
| **폴더 즉시 HTTP 서버** | Serve.Cast |
| **CSV GUI 분석기** | Table.Craft |
| **개인 예산 추적** | Budget.Cast |
| **로프 스윙 물리 아케이드** | Rope.Swing |
| **탄성 반사 슈터** | Bounce.House (≠ Echo.Grid: 타일 페인팅) |
| **물리 탑 파괴** | Stack.Crash |
| **Falling Sand 시뮬레이터** | Sand.Fall |
| **도미노 연쇄 물리** | Domino.Chain |
| **자기력 유도 퍼즐** | Magnet.Maze |
| **천 물리 절단** | Cloth.Cut |
| **기어 기계 물리 퍼즐** | Gear.Works |
| **소용돌이 중력 유도** | Vortex.Pull (≠ Orbit.Craft) |
| **N-체 궤도 시뮬레이션** | Orbit.Craft (≠ Vortex.Pull) |
| **절차 생성 턴제 로그라이크** | Rogue.Tile (≠ Dungeon.Dash: 실시간 액션) |
| **음절 단어 폭탄 게임** | Word.Bomb (≠ Glyph.Rush: 타이핑 슈터) |
| **잉크 전파 차단 퍼즐** | Ink.Spread |
| **오프라인 PDF 올인원** | PDF.Forge (≠ Word.Cloud: 이미지 생성) |
| **아카이브 브라우저** | Zip.Peek (≠ File.Duplicates: 중복 탐지) |
| **이미지 압축 최적화** | Photo.Squash (≠ Img.Forge: 필터·워터마크 중심) |
| **무손실 영상 트리머** | Video.Trim (≠ Screen.Recorder: 녹화기) |
| **스크롤 스크린샷 이어붙이기** | Screen.Stitch (≠ Privacy.Lens: 단순 블러) |
| **SSH config GUI 관리** | SSH.Vault (≠ Secret.Box: .env 암호화) |
| **앱 아이콘 생성기** | Icon.Forge (≠ Img.Forge: 일반 이미지 처리) |
| **hosts 파일 GUI 편집** | Host.Edit (≠ DNS.Flip: DNS 서버 전환) |
| **로컬 SSL 인증서 생성** | Cert.Forge (≠ Cert.Watch: 만료 알림) |
| **HTTP 트래픽 인스펙터** | Proxy.Cast (≠ Api.Probe: 능동적 API 테스터) |
| **텍스트·이미지·폴더 비교기** | Deep.Diff ✅ (≠ Snap.Diff: 미구현 유사 아이디어) |
| **SPH 유체 색 혼합 퍼즐** | Fluid.Rush |
| **브리지 빌더 응력 퍼즐** | Bridge.Craft (≠ Gear.Works: 기어 기계) |
| **소프트바디 젤리 플랫포머** | Jelly.Jump (≠ Rope.Swing: 로프 스윙) |
| **구슬 롤러코스터 빌더** | Marble.Run (≠ Bounce.House: 반사 슈터) |
| **Voronoi 파단 파괴 게임** | Fracture.Fall (≠ Stack.Crash: 강체 탑 무너뜨리기) |
| **무중력 뉴턴 도킹 퍼즐** | Zero.G (≠ Orbit.Craft: N-체 궤도 시뮬레이션) |
| **진자 추 철거 게임** | Wrecking.Ball (≠ Domino.Chain: 도미노 연쇄) |
| **2D 파동 간섭 퍼즐** | Tidal.Wave (≠ Vortex.Pull: 소용돌이 유도) |
| **거미줄 장력 낙하체 포획** | Spring.Web (≠ Cloth.Cut: 천 물리 절단) |
| **물리 핀볼 테이블 빌더** | Pinball.Forge (≠ Bounce.House: 탄성 슈터) |
| **파일 해시 무결성 검증** | Hash.Check (≠ Text.Forge: 텍스트 해시 생성) |
| **프로세스 자동 재시작** | Watch.Dog (≠ Port.Watch: 포트 모니터) |
| **오프라인 TTS 음성 합성** | Echo.Text (≠ Noise.Guard: 마이크 노이즈 필터) |
| **프로세스 리소스 타임라인** | Proc.Timeline (≠ Thermal.View: 온도 모니터) |
| **Git Patch 파일 뷰어·적용** | Patch.View (≠ Deep.Diff: 라이브 파일 비교) |
| **로컬 SQLite DB 뷰어** | DB.Peek (≠ Api.Probe: HTTP, ≠ Table.Craft: CSV) |
| **노코드 파일 파이프라인** | Batch.Flow (≠ Batch.Rename: 이름만, ≠ Img.Forge: 이미지만) |
| **PowerShell/CMD 별칭 관리** | Alias.Forge (≠ Env.Guard: 환경변수 전체) |
| **플랫폼별 메시지 포매터** | Msg.Forge (≠ Stand.Up: 일일 스탠드업 기록) |
| **창→가상 카메라 스트리밍** | Win.Cast (≠ Screen.Recorder: 녹화기) |
| **탄막 패턴 빌더+생존 슈터** | Bullet.Craft (≠ Dodge.Blitz: 패턴 빌더 없음) |
| **Boids 군집 AI 슈터** | Swarm.Rush (≠ Star.Strike: 군집 AI 없음) |
| **세포 분열 영역 전략** | Cell.War (≠ Ink.Spread: 잉크 BFS 차단) |
| **시간 역행 자기협동 퍼즐** | Chrono.Drop |
| **물리 타워 쌓기 대전** | Stack.Race (≠ Stack.Crash: 파괴, ≠ Stack.Pop: 폭발 제거) |
| **음파 반사 보강간섭 슈터** | Echo.Shot (≠ Echo.Grid: 타일 페인팅) |
| **드론 케이블 화물 운반** | Drone.Haul (≠ Zero.G: 우주 무중력) |
| **이중 차원 전환 플랫포머** | Phase.Gate |
| **전류 타이밍 회로 퍼즐** | Volt.Chain (≠ Laser.Net: 레이저 반사) |
| **격자 음악 시퀀서 퍼즐** | Sound.Grid (≠ Signal.Rush: 리듬+슈터) |
| **로컬 Whisper STT 받아쓰기** | Speak.Type (≠ Echo.Text: TTS 반대 방향) |
| **파일 변경 감사 로거** | File.Guard (≠ Watch.Dog: 프로세스 재시작, ≠ Deep.Diff: 내용 비교) |
| **LAN 기기 ARP 스캐너** | Net.Scan (≠ Port.Watch: 로컬 포트, ≠ Net.Speed: 대역폭) |
| **i18n 다국어 문자열 관리** | Locale.Forge (≠ Json.Craft: 포맷팅, ≠ Text.Forge: 변환) |
| **오픈소스 아이콘 라이브러리** | Icon.Hunt (≠ Glyph.Map: 유니코드, ≠ Icon.Forge: ICO 변환) |
| **JSON Schema / OpenAPI 시각화** | Schema.View (≠ Api.Probe: HTTP 실행, ≠ Json.Craft: 포맷팅) |
| **Windows 부팅 타임라인 분석** | Boot.Map (≠ Sys.Clean: 시작프로그램 목록, ≠ Proc.Timeline: 실행 중 리소스) |
| **LAN 클립보드 브로드캐스터** | Clip.Cast (≠ Clipboard.Stacker: 로컬 히스토리, ≠ Drop.Bridge: 파일 전송) |
| **오디오 BPM·키 분석기** | Pitch.Find (≠ Music.Player: 재생기, ≠ Echo.Text: TTS) |
| **플로팅 터미널 패드** | Term.Pad (≠ Quick.Launcher: 앱 런처, ≠ Alias.Forge: 별칭 관리) |
| **BPM 동기화 무한 달리기** | Pulse.Run (≠ Shadow.Run: 고스트 레이스, ≠ Signal.Rush: 리듬+슈터) |
| **자동화 공장 미니 퍼즐** | Auto.Build (≠ Gear.Works: 기어 기계, ≠ Sand.Fall: 낙하 시뮬) |
| **덱빌딩 로그라이크 카드 배틀** | Neon.Card (≠ Rogue.Tile: 턴제 이동+전투) |
| **빛·그림자 스텔스 퍼즐** | Shadow.Trap (≠ Laser.Net: 레이저 반사 연결) |
| **소코반+색상+레이저 퍼즐** | Neon.Push (≠ Flow.Pipe: 파이프 연결, ≠ Laser.Net: 레이저만) |
| **룬 심볼 기억·재현 게임** | Rune.Match |
| **한붓 그리기 해밀턴 경로** | Trace.Run (≠ Flow.Pipe: 색상 칸 채우기) |
| **중력 전환 매치3** | Match.Drop (≠ Gravity.Flip: 플랫포머, ≠ Hue.Flow: 경로 연결) |
| **Conway's Life 퍼즐** | Life.Sim (≠ Sand.Fall: 물리 낙하 시뮬) |
| **행/열 선택 슬라이드 숫자** | Number.Storm (≠ Gravity.Flip: 플랫포머) |
