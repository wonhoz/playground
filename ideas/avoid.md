# Avoid List — 아이디어 중복 방지 목록

> 이 파일은 브레인스토밍 시 **이미 제안되거나 구현된 아이디어를 다시 꺼내지 않도록** 하는 필터다.
> 새 아이디어를 제안하기 전 반드시 이 목록을 확인할 것.
> 마지막 갱신: 2026-03-12 (17차)

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
| `Boot.Map` | Windows 부팅 ETW 타임라인 시각화 (드라이버·서비스별 지연 분석, 병목 식별) — **구현 완료 2026-03-06** | idea_20260305c |
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
| `PDF.Forge` | 오프라인 PDF 올인원 (병합·분리·페이지 조작·압축·워터마크, PdfSharpCore) | **implemented** |
| `Zip.Peek` | ZIP/7z/RAR/TAR.GZ 추출 없이 트리 탐색·내부 미리보기·선택 추출·내부 텍스트 검색 | **implemented** |
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

### Productivity / Dev / System / Media (신규 — idea_20260308_5차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Dict.Cast` | 완전 오프라인 영어·한국어 사전 + 유의어 사전 (전역 단축키 팝업, IPA 발음, SAPI 재생, 단어장 Anki 내보내기) | idea_20260308_5차 |
| `Tag.Forge` | MP3/FLAC/OGG ID3·Vorbis 태그 일괄 편집기 (파일명↔태그 규칙 동기화, MusicBrainz 자동 조회, 앨범아트 임베드, 트랙 순번 자동화) | **구현 완료** 20260308 |
| `Folder.Sync` | 로컬 폴더 단방향/양방향 동기화 GUI (미리보기 diff, 충돌 해결 정책, 필터, Task Scheduler 연동) | idea_20260308_5차 |
| `Layout.Forge` | 키보드 키 재배치 프로파일 에디터 (Caps→Ctrl 등, Scancode Map 레지스트리 자동 생성, 프로파일 저장/전환) | **구현 완료** 20260308 |
| `Sched.Cast` | Windows Task Scheduler GUI 대체 (cron 표현식, 스크립트 예약 실행, 성공/실패 Toast·이메일 알림, 실행 이력 차트) | **구현 완료** 20260308 |
| `Color.Grade` | 이미지 LUT 색보정 도구 (오픈 LUT 20+ 내장, 커스텀 .cube/.3dl 불러오기, Before/After 비교, 배치 처리, AVIF 출력) | **구현 완료** 20260308 |
| `Proc.Map` | 프로세스-네트워크 연결 실시간 방향 그래프 (IP 지오로케이션, 부모-자식 계층 트리, 의심 IP 플래그, Firewall 차단 규칙 생성) | idea_20260308_5차 |
| `Font.Sub` | 웹폰트 서브셋 + WOFF2 변환기 (KS X 1001 2350자 프리셋, HTML 파일 사용 문자 추출, CSS @font-face 코드 자동 생성) | idea_20260308_5차 |

---

### AI / Media / Tray / Productivity / Dev (신규 — idea_20260306_4차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Context.Cast` | 코드베이스 파일 트리+내용을 AI 입력용 단일 컨텍스트 블록으로 패킹 (토큰 카운터, .gitignore 인식, 프로파일 저장) | idea_20260306_4차 |
| `Mosaic.Forge` | 수백~수천 소스 사진을 타일로 목표 이미지 재현하는 포토모자이크 생성기 (k-d Tree 색상 매칭, 최대 1억 픽셀 출력) | idea_20260306_4차 |
| `Volume.Cast` | 앱별 볼륨 독립 제어 트레이 (볼륨 프로파일 전환, 전역 단축키, Windows Core Audio API) | idea_20260306_4차 |
| `Prompt.Forge` | AI 프롬프트 라이브러리 관리자 (변수 플레이스홀더, 버전 관리, 태그 검색, 전역 단축키 팝업) | idea_20260306_4차 |
| `Timeline.Craft` | 드래그앤드롭 시각적 타임라인 편집기 (다중 레인, 연결 화살표, SVG/PDF 내보내기, 프레젠테이션 모드) | idea_20260306_4차 |
| `Render.View` | Mermaid/PlantUML/Graphviz DOT/D2 다이어그램 오프라인 실시간 렌더러 (좌우 분할, PNG/SVG 내보내기) | idea_20260306_4차 |
| `Noti.Hub` | Windows 알림 히스토리 허브 트레이 앱 (모든 앱 Toast 가로채기, 앱별 필터, 재발송, 무음 앱 지정) | idea_20260306_4차 |
| `Tray.Stats` | CPU/RAM/GPU/디스크/네트워크 통합 성능 트레이 모니터 (실시간 미니 그래프, 임계치 Toast, 상위 프로세스 표시) | idea_20260306_4차 |
| `Key.Map` | 앱별 단축키 키보드 다이어그램 시각화 + PDF 치트시트 출력 (내장 프리셋, 전역 단축키 실시간 인식) | idea_20260306_4차 |
| `Spell.Cast` | 오프라인 한국어·영어 맞춤법·문법 검사기 (LanguageTool 내장, 유의어 사전, 문체 제안, 전역 단축키 팝업) | idea_20260306_4차 |

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
| `Auto.Build` | Puzzle / Automation / Sandbox | 컨베이어·기계·분류기 배치 자동화 파이프라인, 목표 처리량 달성, 샌드박스 모드 — **구현 완료 2026-03-06** |
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

### 게임 — 제안된 미구현 아이디어 (idea_20260308_5차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Warp.Drift` | Arcade / Physics Shooter | RK4 중력 렌즈 탄환 궤도, 블랙홀 활용 커브 샷, 탄환 예측선 시각화 |
| `Rift.Jump` | Puzzle / Platformer | 마우스 2점 균열 생성, 모멘텀 보존 순간이동, 레벨 에디터+UGC |
| `Prism.Break` | Puzzle / Optics | 백색광 프리즘 R·G·B 분리, 색 가산 혼합(R+G=Yellow), 거울·필터 배치 |
| `Atom.Craft` | Puzzle / Educational | 원자가 규칙 기반 분자 합성, H₂O~C₆H₁₂O₆, 자유 모드 샌드박스, 교육 B2B |
| `Maze.Dread` | Arcade / Survival | Wilson's 절차 생성 미로, 원형 시야 제한, Dijkstra 추격자 AI, 데일리 챌린지 |
| `Colony.Sim` | Simulation / Sandbox | 영양소 확산 + 페로몬 신호 군집 AI, 다종 경쟁, 사용자 인터랙션, 도전 모드 |
| `Hex.Storm` | Strategy / Micro RTS | 7~19 헥사 격자, 자원-유닛-전투 30~90초 사이클, 지형 어드밴티지, 스피드런 |

---

### Apps / Games (신규 — idea_20260308_6차)

| 이름 | 카테고리 | 핵심 기능 | 출처 |
|------|----------|----------|------|
| `AI.Recap` | AI / Productivity | 회의 트랜스크립트 → Executive Summary + Action Items + Key Decisions (Claude API, Markdown/Slack 출력) | idea_20260308_6차 |
| `Live.Widget` | Productivity / System | 데스크탑 앰비언트 위젯 레이어 (날씨·캘린더·RSS·시스템 미니 그래프, click-through, 플러그인 아키텍처) | idea_20260308_6차 |
| `Quick.Calc` | Dev / Tools | 개발자 프로그래머 계산기 (hex/dec/oct/bin 실시간 동기화, 비트연산, IEEE 754 float 구조 시각화·편집, 전역 팝업) | idea_20260308_6차 |
| `Color.Blind` | Dev / Accessibility | 실시간 화면 색맹 시뮬레이션 오버레이 (Protanopia·Deuteranopia·Tritanopia·Achromatopsia, 분할 뷰, DXGI 캡처) | idea_20260308_6차 |
| `Palette.Gen` | Dev / Creative | 이미지 → 지배 색상 팔레트 추출기 (k-Means, Lab 색공간, CSS/Tailwind/SCSS/Swift/Android XML 동시 출력) | idea_20260308_6차 |
| `Cursor.Lens` | Productivity / Capture | 프레젠테이션·스크린캐스트 커서 스포트라이트+줌 오버레이 (클릭 파문, 키스트로크 HUD, 드래그 잔상) | idea_20260308_6차 |
| `Burn.Rate` | System / Tray | 노트북 배터리 건강도 분석기 (충전 사이클, 실제 용량 vs 설계 용량, 방전 곡선, 교체 권장 알림) | idea_20260308_6차 |
| `App.Temp` | System / Security | 임시 실행 샌드박스 (파일시스템+레지스트리 변경 추적, 종료 시 롤백, 변경 리포트 HTML 내보내기) | idea_20260308_6차 |
| `Snap.Cast` | Productivity / AI | 화면 캡처 영역 → OCR + 레이아웃 분석 → 구조화된 마크다운 즉시 변환 (표/코드블록/헤딩/리스트 자동 감지) | idea_20260308_6차 |
| `Path.Link` | Files / Dev | Windows 심볼릭 링크·정션·하드링크 GUI 관리자 (드래그&드롭 생성, 깨진 링크 감지·수정, 전체 시스템 스캔) | idea_20260308_6차 |

### 게임 — 제안된 미구현 아이디어 (idea_20260308_6차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Dish.Rush` | Casual / Action / Time Management | 탑다운 주방 쿠킹 러시 — 주문 큐, 재료 수집→썰기→볶기→플레이팅→서빙 체인, 러시타임 이벤트, 네온 사이버펑크 주방 |
| `Hook.Cast` | Casual / Skill / Simulation | 플라이 피싱 — Verlet 낚싯줄 캐스팅 물리, 물고기 AI 탐색·접근·입질·챔질, 날씨 시스템, 물고기 도감 |
| `Rush.Cross` | Puzzle / Casual / Strategy | 교차로 교통 신호 타이밍 퍼즐 — 클릭으로 신호 전환, 구급차 최우선, 멀티 교차로, 데일리 챌린지, 레벨 에디터 |
| `Type.Race` | Arcade / Racing / Skill | 타이핑 레이싱 — 문단 타이핑 = 레이서 전진, 오타 패널티, 고스트 레이서, WPM 히트맵, 커스텀 텍스트 임포트 |
| `Ice.Slide` | Puzzle / Logic | 얼음 블록 슬라이딩 퍼즐 — 방향키 = 막힐 때까지 미끄러짐, 다중 블록 상호작용, 돌·폭탄·잠금·전송기 타일, 레벨 에디터 |
| `Rise.Match` | Puzzle / Casual | 상승 격자 보석 교환 퍼즐 (Panel de Pon) — 좌우 교환으로 3매칭, 하단부터 새 줄 상승, 체인 보너스, 로컬 2P VS |
| `Chord.Strike` | Rhythm / Music | 멀티키 동시 누르기 리듬 게임 — 단음→화음→아르페지오 레인, 커스텀 MIDI 채보, 악보 에디터, 구간 연습 모드 |
| `Last.Spark` | Survival / Casual / Atmospheric | 캠프파이어 생존 — 모닥불 꺼짐 방지, 자원 수집, 비바람·야생동물 방해, 밤낮 사이클, 픽셀아트 힐링 분위기 |
| `Jenga.Pull` | Arcade / Physics / Casual | 2D 물리 블록 제거 — 드래그로 블록 빼내기, 무게중심 물리, 금속·유리·폭발물 블록 종류, 로컬 2P 교대 |
| `Leaf.Grow` | Simulation / Puzzle / Creative | L-시스템 식물 성장 샌드박스 — 햇빛 방향·수분·N/P/K 조절로 가지·잎 프랙탈 성장, 퍼즐 목표 모드, 타임랩스 GIF |

### 게임 — 제안된 미구현 아이디어 (idea_20260306_4차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Beat.Rogue` | Roguelite / Rhythm / Dungeon Crawler | 비트 타이밍에만 이동·공격 가능, 완벽 박자 콤보=치명타 배율, 절차 생성 10층 던전, 커스텀 BGM BPM 분석 지원 |
| `Pixel.Cross` | Puzzle / Logic (노노그램) | 행·열 숫자 힌트로 격자 채우기, 완성 시 픽셀 아트 출현, 절차적 유일해 생성, 일일 챌린지 |
| `Forge.Idle` | Idle / Incremental / Casual | 광물 채굴→제련→판매 방치형 체인, 오프라인 생산, 프레스티지, 시장 이벤트 |
| `Tilt.Ball` | Arcade / Physics Puzzle | WASD로 판 기울여 공 유도, 특수 타일(얼음·자석·바람·스프링), 50개 레벨+레벨 에디터 |
| `Room.Code` | Puzzle / Point & Click Adventure | 한 화면짜리 오브젝트 클릭&조합 방탈출 20개 컬렉션, 힌트 별 소비, 히든 엔딩 |
| `Pool.Break` | Sports / Physics / Casual | 정밀 2D 당구 물리 (회전·마찰·쿠션), 8볼·9볼·스트레이트 포켓, AI 상대, 로컬 2P |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260309_7차, 누락 항목)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Wave.Surf` | Casual / Simulation | 사인파+FFT 노이즈 파도 생성, COM 균형 서핑, 공중 묘기 콤보 멀티플라이어, GIF 자동 저장 |

---

### Apps (신규 — idea_20260309_8차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Task.Cast` | 완전 로컬 Kanban 보드 (드래그&드롭, 마감 알림, JSON 단일 파일 저장, Trello 오프라인 대체) | idea_20260309_8차 |
| `Bin.View` | Windows PE(EXE/DLL) 리소스 브라우저 (아이콘·버전정보·매니페스트·문자열 테이블, ICO 내보내기) | idea_20260309_8차 |
| `Fade.Out` | 시스템 볼륨 점진 페이드 수면 타이머 트레이 앱 (선형/지수 곡선, 재생 정지, 모니터 끄기) | idea_20260309_8차 |
| `Web.Shot` | WebView2 전체 페이지 웹스크린샷 (다중 뷰포트, PDF 출력, 배치 URL 목록 캡처) | idea_20260309_8차 |
| `Perm.Audit` | Windows 앱 권한 감사·관리 (카메라·마이크·위치·백그라운드, 원클릭 허용/거부, 스냅샷 비교) | idea_20260309_8차 |
| `Img.Meta` | 이미지 EXIF/IPTC/XMP 뷰어·편집기 (GPS 지도, 프라이버시 제거, 일괄 처리, ExifTool GUI 대체) | idea_20260309_8차 |
| `Crypt.Drop` | 드래그&드롭 AES-256-GCM 파일 암호화 금고 (Argon2id, 키 파일, 마스크 미리보기, 보안 삭제) | idea_20260309_8차 |
| `Geo.Tag` | 사진 GPS 태거 (지도 클릭 수동·GPX 트랙 자동 매칭, 역지오코딩, EXIF GPS 삽입) | idea_20260309_8차 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260309_8차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Knot.Craft` | Puzzle (위상) | 매듭진 실 교차점 드래그 분리 → 단순 루프 완성, 물리 없음 순수 위상 퍼즐, 레벨 에디터+일일 챌린지 |
| `Fluid.Paint` | Sandbox / Creative | Jos Stam 유체 시뮬 색상 혼합 페인팅 (물감 혼합 모델), 라이브 배경화면 모드, GIF 저장 |
| `Reflex.Tap` | Casual / Skill | 반응속도 ms 정밀 측정 4종 테스트 (시각·색상·청각·혼합), 피로도 회귀 그래프, SNS 결과 카드 |
| `Neon.Wall` | Arcade / VS | Pong 기반 + 공 궤적이 영구 벽으로 남아 경기장 좁아짐 (Tron 혼합), 싱글+로컬 2P |
| `Spark.Chain` | Strategy / Casual | 제한된 폭죽 배치 → "FIRE!" 연쇄 터뜨려 모든 타겟 격파, 타겟 종류 다양, 레벨 에디터+UGC |
| `Sym.Draw` | Puzzle / Creative | 4배 거울 대칭 실시간 드로잉 만다라, 목표 실루엣 완성 목표 모드, 대칭 축 설정, 애니메이션 내보내기 |
| `Tower.Fall` | Strategy / Action | 역 타워디펜스 — 침략자 시점, 유닛 코스트 배치로 자동 방어선 돌파, 연막·해커 특수 유닛 |
| `Loop.Race` | Racing / Arcade | 프로시저럴 자기교차 트랙 + 이전 랩 궤적이 다음 랩 물리 장애물로 누적, 일일 시드 랭킹 |

---

### Apps / Games (신규 — idea_20260310_10차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Find.Fast` | NTFS MFT 즉시 파일명 검색 + 멀티스레드 내용 grep 통합 GUI (Everything+ripgrep 대체, 미리보기 패널, 전역 팝업) | idea_20260310_10차 |
| `Unit.Forge` | 완전 오프라인 단위 변환기 (길이/무게/온도/데이터/압력/요리 등 12카테고리, 공식 표시, 선택적 환율 API, 전역 팝업) | idea_20260310_10차 |
| `Win.Event` | Windows EVT/EVTX 이벤트 로그 경량 뷰어 (실시간 스트리밍, 정규식 필터, Toast 알림 규칙, CSV/JSON 내보내기) | idea_20260310_10차 |
| `Spec.View` | PC 하드웨어 스펙 스캐너·내보내기 (CPU/RAM/GPU/스토리지/네트워크, S.M.A.R.T, Markdown/HTML/PDF 리포트) | idea_20260310_10차 |
| `Drive.Bench` | 디스크 벤치마크 (순차/랜덤 R/W, IOPS, 히스토리 차트, 드라이브 간 비교, S.M.A.R.T 탭 내장) | idea_20260310_10차 |
| `Snip.Vault` | 코드 스니펫 관리자 (구문 강조, 변수 플레이스홀더, 태그 검색, 전역 팝업 삽입, JSON 단일 파일 저장) | idea_20260310_10차 |
| `Ext.Boss` | 파일 형식 연결 관리자 (전체 확장자→기본앱 시각화, 프로파일 저장/복원, 깨진 연결 감지) | idea_20260310_10차 |
| `Sleep.Cast` | 스마트 절전/종료 스케줄러 트레이 (시각/유휴/앱 종료/배터리 조건 AND 조합, 카운트다운 취소 알림) | idea_20260310_10차 |
| `Doc.Weave` | 혼합 포맷 문서 섹션 결합기 (DOCX/PDF/Markdown 섹션 드래그 재조합, TOC 생성, DOCX/PDF 내보내기) | idea_20260310_10차 |
| `Net.Ghost` | MAC 주소 스푸퍼·관리자 트레이 (OUI 기반 랜덤화, 제조사 위장, SSID별 프로파일 자동 전환, 원본 복원) | idea_20260310_10차 |

---

### 게임 — 제안된 미구현 아이디어 (idea_20260310_10차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Gravity.Well` | Puzzle / Simulation / Zen | 클릭으로 중력 우물·척력 노드 배치 → 파티클 스트림 휘어 타겟 수집, 다중 우물 상호작용, 샌드박스 파티클 아트 모드 |
| `Spin.Gate` | Puzzle / Casual / Meditative | 낙하 공 → 클릭 시 90° 회전 게이트가 방향 전환, 연동 게이트, 다색 공 분류, 횟수 제한 효율 퍼즐 |
| `Echo.Hunt` | Puzzle / Memory / Atmospheric | 완전 암흑, 소나 핑(클릭) → 1.5초 지형 실루엣 출현, 핑 횟수 제한, 기억에 의존 이동, 적 AI 소음 반응 |
| `Burst.Canvas` | Arcade / Survival / Creative | 적 처치 → 색상 잉크 바닥 번짐, 목표 팔레트 비율 달성+생존 동시 조건, 완성 캔버스 PNG 자동 저장 |
| `Flux.Drift` | Racing / Arcade / Physics | 차량 N/S 극성 보유, 트랙 극성 구역 인력·척력 활용 레이싱, 극성 즉시 반전(쿨타임), 자기 폭풍 아이템 |
| `Fold.Grid` | Puzzle / Logic / Spatial | N×M 격자 오리가미 접기 퍼즐, 접기 순서에 따라 색상 겹침 패턴 변화, 목표 도안 일치, 자유 창작 모드 |
| `Trap.Rush` | Strategy / Action / Reverse TD | 역방어 함정 배치 — 고정 경로 적에게 지뢰·끈끈이·낙하 함정 실시간 배치, 연쇄 폭발 콤보, 코스트 경제 |
| `Warp.Ball` | Puzzle / Physics / Arcade | 화면 경계=토러스 위상 공간(반대편 재출현), 포탈 쌍 배치로 구슬 타겟 유도, 방향·속도 보존 포탈 물리 |
| `Signal.Cast` | Memory / Puzzle / Brain | 시각+청각 신호 시퀀스 재생 후 키보드 재현, 4종 신호(단/장/강/약), 노이즈 간섭, 멀티채널 동시 신호 |
| `Pulse.Grid` | Puzzle / Music / Logic | N×M 격자 에미터 배치 → 박자 기반 펄스 전파·충돌로 노트 발음, 목표 멜로디 완성 퍼즐, 자유 창작 모드 |

---

### Apps (신규 — idea_20260309_8차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Perf.Lens` | 게임/앱 DirectX 성능 오버레이 (FPS·프레임 타임·GPU/VRAM·CPU 온도, DXGI 훅, 세션 로그, MSI Afterburner 경량 대체) | idea_20260310_9차 |
| `Pack.Cast` | WinGet/Scoop/Chocolatey 통합 패키지 관리 GUI (통합 검색·일괄 설치·업데이트, 환경 프로파일 JSON 내보내기, 신규 PC 원클릭 셋업) | idea_20260310_9차 |
| `Clip.Smart` | AI 스마트 클립보드 처리기 (콘텐츠 자동 감지·번역·요약·코드 설명·포맷 변환, 역할 프리셋, Claude API, AI.Clip 진화형) | idea_20260310_9차 |
| `Key.Stash` | 개발자 API 키·토큰 로컬 암호화 저장소 (.env 자동 스캔, 앱 실행 시 env 자동 주입, 만료 알림, 팀 공유 암호화 내보내기) | idea_20260310_9차 |
| `Voice.Cast` | 오프라인 커스텀 음성 Wake Word 트리거 트레이 (Vosk 소형 모델, 키워드→앱 실행/키 전송/스크립트, 감도 조절) | idea_20260310_9차 |
| `Dupe.Guard` | 실시간 중복 파일 감시 트레이 (FSW 실시간, 새 파일 즉시 SHA+pHash 중복 체크, Toast 원클릭 삭제, 감시 프로파일) | idea_20260310_9차 |
| `Live.Doc` | 코드 주석 → 실시간 문서 렌더러 (XML doc/JSDoc/docstring 파싱, 로컬 웹서버, live-reload, 관계 다이어그램 자동 생성) | idea_20260310_9차 |
| `Seq.Shot` | 번호 레이블 자동 추가 튜토리얼 스크린샷 도구 (순번 자동 부여, 화살표 오버레이, DOCX/HTML/PDF 자동 조합 내보내기) | idea_20260310_9차 |

### 게임 — 제안된 미구현 아이디어 (idea_20260310_9차, 물리 엔진 특화)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Pendulum.Blitz` | Arcade / Action / Physics | Verlet 다관절 진자 스윙 전투, 앵커 전환, 운동에너지 비례 공격력, 네온 궤적 잔상 |
| `Bubble.Pop` | Casual / Arcade / Physics | 표면장력 최소 에너지 거품 합치기·분리·파열, 바람·가시·자석 방해, 연쇄 파열 콤보 |
| `Crumble.Run` | Arcade / Runner / Physics | Voronoi 파단 지형 붕괴 러너, 발 디디면 균열→파단, RigidBody 파편 장애물, 절차 생성 |
| `Wobble.Stack` | Casual / Arcade / Physics | Shape Matching 소프트 바디 젤리 타워 쌓기, PBD 탄성 블록, 균형 판정, 네온 젤리 비주얼 |
| `Slice.Chain` | Puzzle / Casual / Physics | Verlet 제약 체인 절단 퍼즐, 특수 링크(자석·폭발·탄성·철), 마우스 드래그 절단 경로 |
| `Throw.Stars` | Arcade / Action / Physics | 무기별 완전 다른 비행 물리 (표창·부메랑·창·원반), 벽 반사·관통 콤보, Magnus/양력 효과 |
| `Crush.Box` | Sandbox / Casual / Physics | 유압 압착 물리 샌드박스, 물질별 파단 패턴(강체+연성체), 유압 프레스·롤러·분쇄기, 목표 파괴율 퍼즐 |
| `Shock.Wave` | Puzzle / Strategy / Physics | 충격파 확산 연쇄 파괴 퍼즐, 제한 폭약 배치·순서 폭발, 블록별 반응(반사·흡수·굴절·연쇄), 슬로우모 리플레이 |

---

### Apps (신규 — idea_20260312_17차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Flash.Card` | SM-2 간격반복 플래시카드 앱 (Anki .apkg 가져오기, 이미지 카드, 복습 통계 대시보드, SAPI 발음, 전역 팝업 추가) | idea_20260312_17차 |
| `Epub.Cast` | EPUB 2/3·MOBI 오프라인 리더 (WebView2 렌더, 라이브러리 관리, 하이라이트·메모, TTS, FTS5 전문 검색) | idea_20260312_17차 |
| `SVG.Forge` | 오프라인 SVG 벡터 에디터 (Bezier 패스, Boolean 연산, CSS 애니메이션 키프레임, SVG/PNG/PDF 내보내기) | idea_20260312_17차 |
| `Health.Log` | 개인 건강 수동 기록기 (체중·혈압·혈당·수면·기분 시계열 차트, 정상 범위 알림, 복약 리마인더, CSV 내보내기) | idea_20260312_17차 |
| `Net.Trace` | 시각적 Traceroute + 세계지도 지오로케이션 핀 (GeoLite2 오프라인, 홉별 RTT 히스토리, 복수 목적지 탭) | idea_20260312_17차 |
| `Audio.Cut` | 오디오 파형 트리머·편집기 (MP3/WAV/FLAC, 페이드·정규화·무음 제거·속도변경, NAudio+SoundTouch) | idea_20260312_17차 |
| `OCR.Forge` | Tesseract 5 오프라인 문서 일괄 OCR (스캔 PDF/이미지→검색가능 PDF/DOCX, 기울기 보정, 폴더 감시) | idea_20260312_17차 |
| `IP.Forge` | IPv4/IPv6 서브넷·CIDR·VLSM 계산기 (비트 블록 다이어그램, 수퍼넷, IP 범위↔CIDR 변환) | idea_20260312_17차 |
| `Score.Cast` | MusicXML/ABC 악보 오프라인 뷰어 + SF2 MIDI 재생 (조옮김, 파트 선택, 구간 반복, PNG/PDF 인쇄) | idea_20260312_17차 |
| `Key.Heat` | 패시브 키스트로크 히트맵 분석 트레이 (키 빈도·WPM·손가락 비율·앱별 타이핑, 내용 무저장, SkiaSharp) | idea_20260312_17차 |

### 게임 — 제안된 미구현 아이디어 (idea_20260312_17차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Sudoku.Cast` | Puzzle / Logic | Backtracking+DLX 유일해 생성, 4×4~16×16 변형, 기법 설명 힌트 팝업, 일일 챌린지 시드 |
| `Card.Solo` | Casual / Card | 솔리테어 5종 컬렉션 (Klondike·Spider·FreeCell·Pyramid·Golf), 카드 물리 애니메이션, 승률 통계 |
| `Astro.Mine` | Arcade / Shooter / Roguelite | 소행성 3단계 분열+중력 물리, 광물 채굴 자원 경제, 웨이브 상점 선택형 업그레이드 루프 |
| `Cave.Run` | Roguelike / Adventure | 절차 생성 수직 동굴, 산소 게이지+지진 붕괴, 영구 사망+이전 런 스케치 잔상 가이드 |
| `Logic.Zebra` | Puzzle / Logic | 아인슈타인 격자 소거 퍼즐(3×3~7×7), 유일해 역산 생성, 클루별 단계 설명 힌트 |
| `Peg.Solo` | Puzzle / Casual | 말뚝 솔리테어 5형판(십자·삼각·육각·별형·영국형), DFS 솔버 힌트, 일일 챌린지 |
| `Geo.Quiz` | Casual / Educational | 실루엣·국기·수도·지도 클릭·혼합 5모드, SM-2 오답 재출제, 세계지도 국가 채우기 |
| `Pipe.Lay` | Puzzle / Casual | 타이머 카운트다운+파이프 회전(L·T·크로스·단방향 밸브·포탈), BFS 흐름 시뮬, 절차 생성 |
| `Trail.Blaze` | Arcade / Platformer | 파쿠르 벽달리기·레지 그랩·슬라이드 콤보, 모멘텀 비례 가속, 절차 생성 도시, 데일리 랭킹 |
| `Swarm.Ant` | Simulation / Strategy | ACO 페로몬(α·β·ρ 파라미터) 개미 군집 시각화, 멀티 군집 경쟁, 샌드박스+퍼즐 챌린지 15+ |

---

### Apps (신규 — idea_20260312_16차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `DNS.Watch` | DNS 쿼리 실시간 모니터·차단 트레이 (앱별 쿼리 로그, 광고/추적 도메인 블랙리스트 차단, 로컬 오버라이드 규칙, CSV/JSON 내보내기) | idea_20260312_16차 |
| `Mesh.View` | 오프라인 3D 메시 뷰어 (OBJ/STL/PLY/GLTF, 솔리드·와이어프레임·노멀 뷰, 치수 측정, STL 슬라이스 미리보기, 형식 변환) | idea_20260312_16차 |
| `Cal.Block` | 로컬 시간 블로킹 플래너 (하루 타임라인 블록 UI, 카테고리 색상, 계획 vs 실적 비교, 주간 히트맵, iCal 내보내기) | idea_20260312_16차 |
| `Trans.Quick` | 오프라인 빠른 번역기 팝업 (클립보드 자동 감지, Bergamot/Opus-MT 내장 엔진, 언어 자동 감지, 전역 단축키) | idea_20260312_16차 |
| `Draw.Board` | 오프라인 인피니트 캔버스 화이트보드 (스티커 메모, 도형, 연결선, 자유 잉크, 이미지 임베드, PNG/SVG/PDF 내보내기) | idea_20260312_16차 |
| `Stock.Watch` | 로컬 투자 포트폴리오 추적기 (매수가 기록, 가격 API, 수익률·배당금 관리, 섹터 파이차트, 리밸런싱 안내) | idea_20260312_16차 |
| `Frame.Pick` | 비디오 최적 프레임 자동 추출기 (모션블러·흔들림 감지 제거, 얼굴 감지 최적 프레임, 시간대별 썸네일 스트립, FFmpeg 내장) | idea_20260312_16차 |

### 게임 (신규 — idea_20260312_16차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Wave.Form` | Puzzle / Music / Educational | 사인파 조각 드래그 배치로 목표 합성파 완성, 가산/감산 혼합, 상쇄간섭 시각화, 완성 시 실제 소리 재생, 푸리에 급수 직관 학습 |
| `Crystal.Grow` | Simulation / Sandbox / Puzzle | 셀룰러 오토마타 결정화(눈꽃·소금·금속), 온도·농도·씨앗 위치 조절, 타임랩스 GIF 저장, 목표 결정 형태 달성 퍼즐 모드 |
| `Balance.Act` | Casual / Physics / Arcade | 다양한 형태 블록 정밀 균형 쌓기, 바람·지진 랜덤 이벤트, 특수 블록(자석·탄성·얼음·납), 일일 온라인 랭킹, 슬로우모 리플레이 |

---

### Apps / System / Network / Files / Dev (신규 — idea_20260312_15차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Audio.Scope` | 시스템 오디오 실시간 스펙트럼 분석기·오실로스코프 (WASAPI loopback, FFT 스펙트럼, 스펙트로그램, 레벨 미터, PNG/SVG 내보내기) | idea_20260312_15차 |
| `VPN.Cast` | WireGuard/OpenVPN GUI 설정 관리자 (피어 추가·삭제·QR 가져오기, 킬 스위치, 트레이 프로파일 전환, 연결 이력) | idea_20260312_15차 |
| `Reg.Vault` | Windows 레지스트리 고급 브라우저·비교기 (정규식 검색, 북마크, 스냅샷 diff, JSON/REG 내보내기, HKLM 쓰기 자동 백업) | idea_20260312_15차 |
| `Run.Book` | 인터랙티브 YAML Runbook/체크리스트 실행기 (스텝+조건 분기, 쉘 명령 실행+로그 스트리밍, 배포·인시던트 대응 프로시저 실행) | idea_20260312_15차 |
| `Archive.Forge` | ZIP/7z/TAR.GZ 아카이브 생성·관리자 (드래그&드롭, AES-256 암호화, 분할 볼륨, 제외 패턴 glob 규칙) | idea_20260312_15차 |

### 앱 (신규 — idea_20260311_11차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `BT.Cast` | Bluetooth 기기 원클릭 전환 트레이 (헤드폰·스피커·게임패드 페어링 목록 팝업, 연결/해제 토글, 프리셋 그룹, 배터리 Toast 알림) | idea_20260311_11차 |
| `Ctx.Menu` | Windows 우클릭 컨텍스트 메뉴 GUI 편집기 (HKCR Shell 항목 추가·삭제·숨김 토글, 확장자별 규칙, SHChangeNotify 즉시 반영, 레지스트리 직접 편집 불필요) | idea_20260311_11차 |
| `Clip.Find` | 검색형 클립보드 히스토리 관리자 (FTS5 전문 검색, 핀 고정, 카테고리 태그, 민감정보 자동 마스킹, 자동 만료 — Clipboard.Stacker 스택 방식과 차별화) | idea_20260311_11차 |
| `Svc.Guard` | Windows 서비스 GUI 관리자 (시작·중지·재시작 원클릭, 의존성 방향 그래프, 복구 설정, 안전/불필요 서비스 분류 라벨, services.msc 대체) | idea_20260311_11차 |
| `RSS.Cast` | 완전 오프라인 RSS/Atom/JSON Feed 리더 (SQLite 로컬 캐싱, FTS5 아티클 검색, 키워드 Toast 알림, OPML 내보내기/가져오기, 팟캐스트 오디오 지원) | idea_20260311_11차 |
| `Theme.Forge` | 색상 테마 팔레트 디자이너 (HSL 색상환 인터랙티브, 보색·삼색·테트라딕 조화 규칙, 명도 토큰 자동 파생, WCAG 접근성 검사, CSS/Tailwind/Material 내보내기) | idea_20260311_11차 |
| `Sprite.Forge` | 스프라이트 시트 패커 & 애니메이터 (MaxRects 최적 패킹, Trim 투명 영역, 멀티 아틀라스, 프레임 애니메이션 편집, JSON/XML 메타데이터 내보내기, 게임 개발 필수) | idea_20260311_11차 |
| `Git.Stats` | Git 저장소 통계 분석기 (커밋 히트맵, 핫파일·코드 이탈 분석, 기여자 활동, 언어 분포, 시간대 패턴, Markdown 리포트 내보내기 — Git.Reel과 차별화) | idea_20260311_11차 |
| `Win.Tamer` | 프로세스 CPU 친화도·우선순위 GUI 관리자 (코어 체크박스 할당, WMI 프로세스 감시로 실행 시 자동 적용, 게이밍·개발 프리셋) | idea_20260311_11차 |
| `Font.Draw` | 비트맵·픽셀 폰트 그리드 에디터 (8×8~32×32 글리프 편집, BDF/PNG 글리프시트 내보내기, 미리보기 렌더, ASCII~유니코드 범위 지원) | idea_20260311_11차 |

### 게임 — 제안된 미구현 아이디어 (idea_20260312_15차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Dice.Shift` | Puzzle / Logic | 격자 위 주사위 굴리기 소코반 — 굴릴 때마다 바닥 면 값 변경, 목표 칸마다 요구 숫자 조건, 방향 잠금·숫자 변환 특수 타일, 레벨 에디터 |
| `Maze.Craft` | Puzzle / Strategy | 역발상 미로 — 플레이어가 벽 배치로 미로 설계, BFS/A* AI 탈출자가 해법 탐색, 탈출 막으면 승리, 최소 벽 수 도전, 솔버 시각화 |
| `Lock.Pick` | Casual / Skill / Puzzle | 자물쇠 해제 미니게임 컬렉션 — 핀텀블러(청각 타이밍)·다이얼 조합(진동)·슬라이딩·원판·체인 5종, 물리+오디오 피드백, heist 스토리 캠페인 |
| `Frame.Jump` | Platformer / Puzzle / Meta | 만화 패널 메타 플랫포머 — 패널 경계 순간이동, 패널마다 독립 물리, 크기가 공간 변형, 패널 이동 순서 퍼즐 |
| `Chip.Logic` | Puzzle / Educational | 논리 게이트 회로 빌더 퍼즐 — AND/OR/NOT/NAND/XOR 배치로 목표 진리표 달성, 반가산기→ALU 단계 진행, 빛 흐름 시각화, 최소 게이트 수 도전, 샌드박스 |

### 게임 — 제안된 미구현 아이디어 (idea_20260311_11차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Word.Weave` | Word / Puzzle / Casual | N×N 격자 글자 타일 인접 8방향 체인으로 단어 완성 (Boggle 변형), 희귀 단어 배수 점수, 낙하 타일, 와일드카드·폭탄 특수 타일, 일일 챌린지 시드 |
| `Code.Idle` | Idle / Incremental / Casual | 코드 라인 생산→기능 완성→앱 출시→팀 고용→AI 자동화 체인, 기술 부채·버그 이벤트, Product Hunt·NPM 취약점 등 메타 유머 이벤트, 프레스티지 시스템 |
| `Magnet.Jump` | Puzzle / Platformer | 캐릭터 N/S 극성 전환, 자기 발판 흡착·반발 도약, 회전 자기장 구역, 전자기 차폐, 50레벨 + 레벨 에디터 |
| `Root.Spread` | Strategy / Simulation | 지하 타일맵 단면뷰, 뿌리 성장 방향 지정, 질소·인·칼륨 영양소 수집, 암석·산성 토양·경쟁 식물 장애물, 지상 식물 성장 목표 연동 |
| `Zipline.Rush` | Puzzle / Physics | Verlet 케이블 집라인 네트워크 빌더, 앵커 배치 → 화물 중력+마찰 물리 이동, 최소 앵커 수 도전, 도르래·탄성 앵커·바람 특수 요소 |
| `Spore.Net` | Simulation / Strategy | Physarum polycephalum 슬라임 몰드 알고리즘, 균류 실 탐색·강화·퇴화, 영양원 최적 네트워크 연결, 종 간 경쟁, 최소 스패닝 트리 도전 |
| `Echo.Drum` | Rhythm / Memory | 드럼 패턴 재생→정확 재현 (킥/스네어/하이햇/심벌), 8→16박자→폴리리듬 난이도, 블라인드 재현·노이즈 혼입 도전, 자유 창작+공유 코드 |
| `Block.Shift` | Puzzle / Casual | 색상(3종)+기호(4종) 타일 4×4 격자, 행/열 루프 슬라이드로 목표 패턴 달성, 고정·와일드·회전 특수 타일, 역방향 생성으로 유일해 보장 |
| `Light.Cast` | Puzzle / Optics | 볼록·오목렌즈·반사경 배치 빛 세기 집중 퍼즐, 스넬 굴절 2D 근사, 다중 목표 광도 동시 달성, 보호 대상 과열 방지 조건 |
| `Sky.Drift` | Arcade / Physics / Casual | Verlet 날개 양력+중력, 열상승기류 탐색·하강기류 회피, 새떼·폭풍 장애물, 수직 무한 스크롤 절차 생성, 일일 최고 고도 랭킹 |

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
| **AI 클립보드 처리** | AI.Clip ✅ · Clip.Smart (AI.Clip은 단순 처리, Clip.Smart는 콘텐츠 타입 자동 감지+멀티 기능) |
| **비밀키 / .env 보관** | Secret.Box · Key.Stash (Key.Stash는 자동 스캔+환경변수 주입+만료 알림 특화) |
| **음성 입력 / Wake Word** | Speak.Type (STT 받아쓰기) · Voice.Cast (Wake Word 트리거, ≠ 받아쓰기) |
| **성능 모니터 오버레이** | Perf.Lens (DirectX 게임 오버레이), Tray.Stats ✅ (트레이 CPU/RAM 모니터) |
| **패키지 관리 GUI** | Pack.Cast |
| **중복 파일** | File.Duplicates ✅ (온디맨드 스캔) · Dupe.Guard (실시간 FSW 감시) |
| **코드 문서화** | Live.Doc |
| **튜토리얼 스크린샷** | Seq.Shot · Screen.Stitch (이어붙이기 별도) |
| **진자/스윙 물리 게임** | Pendulum.Blitz · Rope.Swing (Rope.Swing은 플랫포머, Pendulum.Blitz는 전투 아케이드) |
| **거품/유체 물리 게임** | Bubble.Pop (표면장력 거품) · Fluid.Rush (SPH 색상 혼합) · Tidal.Wave (파동 방정식) |
| **지형 파괴 게임** | Crumble.Run (Voronoi 붕괴 러너) · Fracture.Fall (목표 파괴 퍼즐) · Stack.Crash (탑 무너뜨리기) |
| **소프트바디 게임** | Wobble.Stack (젤리 쌓기) · Jelly.Jump (소프트바디 플랫포머) |
| **체인/로프 절단 게임** | Slice.Chain (Verlet 체인) · Spring.Web (거미줄 인장) · Rope.Swing (스윙) |
| **투척 물리 게임** | Throw.Stars (무기별 비행 물리) · Arc.Blast (포물선 탄도 구조물 파괴, 유사 주의) |
| **파괴 샌드박스** | Crush.Box (압착 파괴) · Fracture.Fall (Voronoi 파단 퍼즐) · Jenga.Pull (블록 제거) |
| **충격파 / 폭발 게임** | Shock.Wave (충격파 연쇄) · Domino.Chain (도미노 충격량) · Spark.Chain (폭죽 연쇄) |
| **무드보드 / 이미지 콜라주** | Mood.Board |
| **이미지 주석 / 스크린샷 편집** | Clip.Annotate |
| **썸네일 / 소셜 이미지 제작** | Thumb.Forge |
| **유니코드 / 특수문자 탐색** | Glyph.Map |
| **타이핑 속도 / WPM 연습** | Type.Rocket |
| **파일 내용 검색 GUI** | Find.Fast (파일명+내용 통합) |
| **단위 변환기** | Unit.Forge |
| **Windows 이벤트 로그 뷰어** | Win.Event (EVT/EVTX 전용, Log.Lens는 텍스트 파일) |
| **하드웨어 스펙 조회** | Spec.View |
| **디스크 벤치마크** | Drive.Bench |
| **코드 스니펫 관리** | Snip.Vault (≠ Alias.Forge 쉘 별칭, ≠ Prompt.Forge AI 프롬프트) |
| **파일 형식 연결 관리** | Ext.Boss |
| **절전/종료 스케줄러** | Sleep.Cast (Stay.Awake와 반대 개념) |
| **문서 섹션 결합** | Doc.Weave (≠ PDF.Forge 단순 PDF 병합, ≠ Form.Blast 템플릿 변수 채우기) |
| **MAC 주소 스푸퍼** | Net.Ghost (≠ WiFi.Vault 비밀번호 조회) |
| **중력 우물 파티클 퍼즐** | Gravity.Well (≠ Orbit.Craft 궤도 역학, ≠ Vortex.Pull 단일 소용돌이) |
| **회전 게이트 낙하 퍼즐** | Spin.Gate |
| **소나 어둠 탐색** | Echo.Hunt (≠ Maze.Dread 시야 반경 제한) |
| **액션 페인팅 생존** | Burst.Canvas |
| **자기 극성 레이싱** | Flux.Drift (≠ Nitro.Drift 일반 레이싱) |
| **오리가미 폴딩 퍼즐** | Fold.Grid |
| **함정 배치 역방어** | Trap.Rush (≠ Tower.Guard 타워 방어, ≠ Tower.Fall 침략자 시점) |
| **토러스 포탈 구슬 퍼즐** | Warp.Ball (≠ Echo.Grid 반사 채우기, ≠ Orbit.Craft) |
| **신호 패턴 해독 재현** | Signal.Cast (≠ Rune.Match 시각 심볼 기억) |
| **펄스 충돌 음악 퍼즐** | Pulse.Grid (≠ Sound.Grid 자유 시퀀서 창작) |
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
| **DNS 쿼리 모니터·차단** | DNS.Watch (≠ DNS.Flip: 서버 전환) |
| **3D 메시 뷰어** | Mesh.View |
| **시간 블로킹 플래너** | Cal.Block (≠ Daily.Dash: 정보 대시보드, ≠ Focus.Log: 자동 추적) |
| **오프라인 번역기** | Trans.Quick (≠ Dict.Cast: 사전+단어장) |
| **인피니트 캔버스 화이트보드** | Draw.Board (≠ Mind.Map: 트리 레이아웃 전용) |
| **투자 포트폴리오 추적** | Stock.Watch (≠ Budget.Cast: 일상 지출) |
| **비디오 선명 프레임 추출** | Frame.Pick (≠ Video.Trim: 구간 편집) |
| **파형 합성 퍼즐** | Wave.Form (≠ Tidal.Wave: 파동 전파, ≠ Pulse.Grid: 격자 시퀀서) |
| **결정 성장 시뮬레이션** | Crystal.Grow (≠ Sand.Fall: 모래 물리, ≠ Leaf.Grow: 식물 L시스템) |
| **균형 타워 쌓기 아케이드** | Balance.Act (≠ Wobble.Stack: 소프트바디, ≠ Stack.Race: 대전 모드) |
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
| **AI 컨텍스트 파일 패커** | Context.Cast (≠ AI.Clip: Claude API 처리기, ≠ Batch.Flow: 일반 파일 파이프라인) |
| **포토모자이크 생성기** | Mosaic.Forge (≠ Img.Forge: 일괄 변환, ≠ Word.Cloud: 워드클라우드, ≠ Char.Art: 문자 아트) |
| **앱별 볼륨 독립 제어** | Volume.Cast (≠ Sound.Cast: 출력 장치 전환, ≠ Mute.Master: 전체 뮤트 토글) |
| **AI 프롬프트 라이브러리** | Prompt.Forge (≠ AI.Clip: Claude API 자동 처리, ≠ Quick.Memo: 단순 메모, ≠ Context.Cast: 코드 패킹) |
| **시각적 타임라인 편집기** | Timeline.Craft (≠ Chart.Forge: 일반 차트, ≠ Table.Craft: CSV 분석) |
| **다이어그램 마크업 렌더러** | Render.View (≠ Schema.View: JSON Schema/OpenAPI 특화, ≠ Mark.View: 마크다운 뷰어) |
| **Windows 알림 히스토리** | Noti.Hub (≠ Toast.Cast: 직접 알림 발송, ≠ Stay.Awake: 화면 꺼짐 방지) |
| **통합 시스템 성능 트레이** | Tray.Stats (≠ Thermal.View: 온도만, ≠ Net.Speed: 네트워크만, ≠ Proc.Timeline: 프로세스 타임라인 레코더) |
| **단축키 키보드 시각화** | Key.Map (≠ Glyph.Map: 유니코드 문자, ≠ Alias.Forge: PowerShell 별칭 관리) |
| **오프라인 맞춤법·문법 검사** | Spell.Cast (≠ Text.Forge: 형식·인코딩 변환, ≠ Echo.Text: TTS, ≠ Type.Wand: 텍스트 변환) |
| **리듬 동기화 던전 크롤러** | Beat.Rogue (≠ Beat.Drop: 리듬 게임, ≠ Neon.Card: 덱빌딩, ≠ Pulse.Run: 리듬 러너, ≠ Dungeon.Dash: 실시간 액션) |
| **노노그램 / 피크로스** | Pixel.Cross (≠ Hue.Flow: 경로 연결, ≠ Gravity.Flip: 플랫포머, ≠ Pixel.Drop: 픽셀아트 테트리스) |
| **방치형 공장 빌더** | Forge.Idle (≠ Auto.Build: 실시간 자동화 퍼즐, ≠ Sand.Fall: 시뮬레이션, ≠ Gear.Works: 기어 기계) |
| **기울기 미로 볼 굴리기** | Tilt.Ball (≠ Marble.Run: 경로 빌더, ≠ Orbit.Craft: N-체 중력, ≠ Magnet.Maze: 자기력 유도) |
| **방탈출 어드벤처 컬렉션** | Room.Code (≠ 기존 모든 게임: 포인트앤클릭 어드벤처 첫 도입) |
| **물리 당구 게임** | Pool.Break (≠ Pinball.Forge: 핀볼 빌더, ≠ Bounce.House: 탄성 슈터, ≠ Arc.Blast: 포물선 파괴) |
| **오프라인 사전 팝업** | Dict.Cast (≠ Spell.Cast: 맞춤법 교정, ≠ Echo.Text: TTS, ≠ Quick.Memo: 메모장) |
| **MP3/FLAC 태그 일괄 편집** | Tag.Forge (≠ Music.Player: 재생기, ≠ Pitch.Find: BPM 분석, ≠ Batch.Rename: 파일명만) |
| **로컬 폴더 동기화 GUI** | Folder.Sync (≠ Drop.Bridge: LAN 전송, ≠ File.Guard: 감사 로거, ≠ Batch.Flow: 파일 파이프라인) |
| **키보드 키 재배치 에디터** | Layout.Forge (≠ Mouse.Flick: 마우스 제스처, ≠ Key.Map: 시각화, ≠ Key.Tape: 매크로 녹화) |
| **Task Scheduler GUI 대체** | Sched.Cast (≠ Cron.Cast: 검증기, ≠ Batch.Flow: 파일 파이프라인, ≠ Watch.Dog: 크래시 재시작) |
| **LUT 기반 이미지 색보정** | Color.Grade (≠ Img.Forge: 리사이즈·필터, ≠ Photo.Squash: 압축만, ≠ Photo.Video.Organizer: 날짜 정리) |
| **프로세스-네트워크 연결 그래프** | Proc.Map (≠ Port.Watch: 로컬 포트, ≠ Net.Scan: LAN 탐지, ≠ App.Cage: 방화벽 래퍼) |
| **웹폰트 서브셋 + WOFF2 변환** | Font.Sub (≠ Font.Scout: 미리보기·비교, ≠ Font.Probe: 화면 추출, ≠ Icon.Forge: ICO 변환) |
| **중력 렌즈 탄환 궤도 슈터** | Warp.Drift (≠ Orbit.Craft: N-체 퍼즐, ≠ Vortex.Pull: 소용돌이 유도, ≠ Dodge.Blitz: 탄막 회피) |
| **2점 포털 순간이동 플랫포머** | Rift.Jump (≠ Phase.Gate: 두 차원 전환, ≠ Chrono.Drop: 시간 역행 협동) |
| **백색광 RGB 분리 퍼즐** | Prism.Break (≠ Laser.Net: 단일 레이저 반사, ≠ Hue.Flow: 경로 연결) |
| **원자가 분자 합성 퍼즐** | Atom.Craft (≠ Life.Sim: 셀룰러 오토마타, ≠ Sand.Fall: 물리 낙하 시뮬) |
| **절차 생성 미로 탈출 + 추격** | Maze.Dread (≠ Dungeon.Dash: 실시간 액션 RPG, ≠ Rogue.Tile: 턴제 로그라이크) |
| **군집 생명체 진화 시뮬 샌드박스** | Colony.Sim (≠ Life.Sim: Conway 규칙, ≠ Sand.Fall: 물리 입자, ≠ Auto.Build: 공장 자동화) |
| **헥사 격자 마이크로 RTS** | Hex.Storm (≠ Cell.War: 원형 세포 분열, ≠ Ink.Spread: BFS 차단, ≠ Tower.Guard: 타워 디펜스) |
| **회의 트랜스크립트 AI 요약** | AI.Recap (≠ AI.Clip: 클립보드 처리, ≠ Stand.Up: 스탠드업 기록, ≠ Msg.Forge: 메시지 포매터) |
| **데스크탑 앰비언트 위젯 레이어** | Live.Widget (≠ Daily.Dash: 열어야 하는 풀 창, ≠ Tray.Stats: 트레이만, ≠ Quick.Memo: 단순 메모) |
| **개발자 프로그래머 계산기** | Quick.Calc (≠ Calc.Pop: 일반 팝업 계산기 — IEEE 754·bitwise·hex 특화) |
| **실시간 화면 색맹 시뮬레이션** | Color.Blind (≠ Access.Check: 색상 대비율 수치 계산, ≠ Color.Grade: 이미지 LUT 보정) |
| **이미지 → 색상 팔레트 추출** | Palette.Gen (≠ Palette.Cast: 색상 수집·관리, ≠ Color.Grade: LUT 보정, ≠ Access.Check: WCAG 비율) |
| **커서 스포트라이트+줌 오버레이** | Cursor.Lens (≠ Screen.Recorder: 녹화기, ≠ Code.Snap: 정적 스크린샷, ≠ Key.Map: 단축키 PDF) |
| **배터리 건강도·방전 분석** | Burn.Rate (≠ Tray.Stats: CPU/RAM/GPU 통합 성능, ≠ Thermal.View: 온도 모니터) |
| **임시 실행 샌드박스·롤백** | App.Temp (≠ App.Cage: 네트워크 방화벽, ≠ Spy.Guard: 클립보드 감시, ≠ File.Guard: 감사 로거) |
| **스크린샷 → 구조화 마크다운** | Snap.Cast (≠ Screen.OCR·Screen.Pickup: 단순 텍스트 추출, ≠ Clip.Annotate: 이미지 주석) |
| **심볼릭 링크·정션 GUI 관리** | Path.Link (≠ Batch.Rename: 이름 변경, ≠ Disk.Lens: 크기 시각화, ≠ File.Guard: 변경 감사) |
| **탑다운 쿠킹 러시 게임** | Dish.Rush (≠ 기존 모든 게임: Time Management 장르 첫 도입) |
| **플라이 피싱 물리 시뮬** | Hook.Cast (≠ 기존 모든 게임: Fishing/Casual 힐링 장르 첫 도입, Verlet 낚싯줄 물리) |
| **교차로 신호 타이밍 퍼즐** | Rush.Cross (≠ 기존 모든 게임: Traffic Management 장르 첫 도입) |
| **문단 타이핑 레이싱** | Type.Race (≠ Glyph.Rush: 문자 격추 슈터, ≠ Type.Rocket: WPM 트래커 앱) |
| **얼음 블록 슬라이딩 퍼즐** | Ice.Slide (≠ Tilt.Ball: 판 기울이기, ≠ Marble.Run: 구슬 경로 빌더, ≠ Neon.Push: 소코반+색상) |
| **상승 격자 보석 교환 (Panel de Pon)** | Rise.Match (≠ Match.Drop: 중력 방향 전환 매치3, ≠ Hue.Flow: 경로 연결, ≠ Hexa.Drop: 헥사 배치) |
| **멀티키 동시 누르기 리듬** | Chord.Strike (≠ Beat.Drop: 단일 입력 박자, ≠ Signal.Rush: 리듬+슈터, ≠ Sound.Grid: 격자 시퀀서) |
| **캠프파이어 생존 관리** | Last.Spark (≠ 기존 모든 게임: 불꽃 보호 서바이벌 분위기 게임 첫 도입) |
| **2D 물리 블록 제거 (Jenga)** | Jenga.Pull (≠ Stack.Crash: 탑 파괴, ≠ Stack.Race: 탑 쌓기 대전, ≠ Stack.Pop: 폭발 제거) |
| **L-시스템 식물 성장 샌드박스** | Leaf.Grow (≠ Colony.Sim: 동물 군집 AI, ≠ Life.Sim: Conway 셀룰러, ≠ Sand.Fall: 물리 입자) |
| **목업 데이터 일괄 생성기** | Mock.Data (≠ Table.Craft: CSV 분석, ≠ Data.Cast: 노코드 DB — Faker·SQL INSERT 배치 특화) |
| **시스템 사양 HTML/PDF 리포트** | Spec.Report (≠ Tray.Stats: 실시간 트레이, ≠ Proc.Timeline: 프로세스 타임라인 — 정적 문서화 특화) |
| **로컬 노코드 데이터베이스** | Data.Cast (≠ Table.Craft: CSV만, ≠ DB.Peek: 읽기 전용 — 칸반·갤러리·수식·관계형 포함) |
| **X.509 인증서 체인 분석기** | Cert.View (≠ Cert.Forge: 인증서 생성 — 체인 시각화·유효기간 타임라인·OCSP 검증 특화) |
| **전역 핫키 자동 스크롤 트레이** | Scroll.Cast (≠ Screen.Recorder: 녹화, ≠ Win.Cast: 가상 카메라 — 문서·텔레프롬프터 자동 스크롤) |
| **AES-256 암호화 로컬 일기장** | Daily.Log (≠ Quick.Memo: 단순 메모, ≠ Stand.Up: 스탠드업 기록 — 마크다운·무드·연간 히트맵) |
| **패키지 CVE·라이선스 감사기** | Pack.View (≠ Api.Probe: HTTP, ≠ Schema.View: JSON Schema — NuGet/npm/pip SBOM·CVE 감사) |
| **소나 음파 항법 미로 탈출** | Echo.Maze (≠ Dungeon.Dash: 액션 RPG, ≠ Maze.Dread: 추격 미로 — 칠흑+소나 링 시각화 독자 메커닉) |
| **정밀 저격 물리 퍼즐** | Crack.Shot (≠ Dodge.Blitz: 탄막 회피, ≠ Star.Strike: 실시간 슈터 — 턴 기반 탄도 보정 퍼즐) |
| **3D 와이어프레임 실루엣 매칭** | Wire.Mesh (≠ Orbit.Craft: N-체 궤도, ≠ Prism.Break: RGB 분리 — 3D→2D 투영 각도 퍼즐) |
| **진화형 두더지 잡기** | Mole.Strike (≠ Hook.Cast: 낚시 캐주얼, ≠ Dish.Rush: 쿠킹 관리 — 반응속도 분석·폭탄/황금 두더지) |
| **픽셀아트 15퍼즐 (A* 시각화)** | Slide.Rush (≠ Ice.Slide: 얼음 물리, ≠ Neon.Push: 소코반+레이저 — A* 솔버 가르치는 모드 포함) |
| **주사위 빌딩 로그라이크** | Dungeon.Dice (≠ Neon.Card: 카드 덱빌딩, ≠ Beat.Rogue: 리듬 크롤러 — 6면 주사위 면 조각 시너지) |
| **FFT 파도 균형 서핑 게임** | Wave.Surf (≠ Hook.Cast: 낚시 캐주얼, ≠ Nitro.Drift: 레이싱 — COM 균형+묘기 콤보 독자 메커닉) |
| **로컬 칸반 작업 관리** | Task.Cast (≠ Ink.Cast: 마크다운 노트, ≠ Data.Cast: 관계형 DB, ≠ Quick.Memo: 단순 메모) |
| **PE 바이너리 리소스 탐색기** | Bin.View (≠ Zip.Peek: 아카이브, ≠ Hex.Peek: 헥스 뷰어 — PE 내부 아이콘·매니페스트·문자열 특화) |
| **볼륨 페이드 수면 타이머** | Fade.Out (≠ Toast.Cast: 건강 루틴 알림, ≠ Sound.Cast: 출력 전환, ≠ Volume.Cast: 앱별 볼륨) |
| **전체 페이지 웹 스크린샷** | Web.Shot (≠ Screen.Recorder: 비디오, ≠ Screen.Stitch: SIFT 이어붙이기, ≠ Code.Snap: 코드 이미지) |
| **앱 OS 권한 감사·관리** | Perm.Audit (≠ Spy.Guard: 실시간 무단 접근 감지, ≠ App.Cage: 네트워크 방화벽 — 권한 DB 뷰 특화) |
| **이미지 EXIF 메타데이터 편집** | Img.Meta (≠ Photo.Video.Organizer: 날짜 정리, ≠ Img.Forge: 픽셀 처리, ≠ Geo.Tag: GPS 태깅 특화) |
| **드래그&드롭 파일 암호화 금고** | Crypt.Drop (≠ Pass.Vault: 비밀번호 관리, ≠ Secret.Box: .env 파일, ≠ Daily.Log: 일기장) |
| **GPX 매칭 EXIF GPS 태거** | Geo.Tag (≠ Photo.Video.Organizer: 날짜 정리, ≠ Img.Meta: 다목적 메타 편집 — GPX 자동 매칭 특화) |
| **위상 매듭 풀기 퍼즐** | Knot.Craft (≠ Rope.Swing: 로프 물리 아케이드, ≠ Cloth.Cut: 천 물리 절단, ≠ Spring.Web: 거미줄) |
| **유체역학 색상 혼합 페인팅** | Fluid.Paint (≠ Sand.Fall: 셀룰러 오토마타, ≠ Fluid.Rush: 파이프·밸브 퍼즐 — 연속체 유체+예술 창작) |
| **반응속도 과학 측정 게임** | Reflex.Tap (≠ Mole.Strike: 스코어 기반 두더지, ≠ Color.Blitz: 스트룹 인지 — 순수 ms 측정 도구) |
| **Pong+궤적 영구 벽 대전** | Neon.Wall (≠ Brick.Blitz: 벽돌 깨기, ≠ Bounce.House: 탄성 슈터, ≠ Echo.Grid: 타일 페인팅) |
| **폭죽 연쇄 전략 배치 게임** | Spark.Chain (≠ Domino.Chain: 물리 충격량, ≠ Chain.Blast: 색상 구슬, ≠ Volt.Chain: 전류 회로) |
| **4배 대칭 드로잉 만다라** | Sym.Draw (≠ Pixel.Cross: 노노그램, ≠ Pixel.Forge: 레이어 픽셀아트, ≠ Fluid.Paint: 자유 유체 페인팅) |
| **역 타워디펜스 침략자 시점** | Tower.Fall (≠ Tower.Guard: 방어 배치 전략, ≠ Dungeon.Dash: 실시간 액션, ≠ Hex.Storm: 헥사 RTS) |
| **자기교차 트랙 궤적 장애물 레이싱** | Loop.Race (≠ Nitro.Drift: 드리프트, ≠ Shadow.Run: 고스트 레이스 — 궤적이 능동적 장애물) |
| **픽셀 1:1 영역 전쟁 전략** | Pixel.War (≠ Cell.War: 세포 분열, ≠ Ink.Spread: BFS 차단, ≠ Hex.Storm: 헥사 RTS — 1픽셀씩 교대) |
| **FFT 파도 물리 서핑 게임** | Wave.Surf (≠ Hook.Cast: 낚시, ≠ Nitro.Drift: 레이싱 — 무게중심 균형·묘기·GIF 자동 저장) |
| **Bluetooth 기기 트레이 전환** | BT.Cast (≠ DNS.Flip: DNS 서버, ≠ Sound.Cast: 오디오 출력 장치, ≠ Volume.Cast: 앱별 볼륨 — Bluetooth 페어링·연결 전환 특화) |
| **우클릭 컨텍스트 메뉴 편집** | Ctx.Menu (≠ Sys.Clean: 시작프로그램·레지스트리 청소, ≠ Env.Guard: 환경변수 — HKCR Shell 항목 추가/숨김 특화) |
| **검색형 클립보드 히스토리** | Clip.Find (≠ Clipboard.Stacker: 스택 순환 방식, ≠ Clip.Cast: LAN 브로드캐스트 — FTS5 전문 검색+민감정보 마스킹) |
| **Windows 서비스 GUI 관리** | Svc.Guard (≠ Port.Watch: 포트 점유 프로세스, ≠ Watch.Dog: 크래시 재시작, ≠ Proc.Pilot: 프로세스 탐색기 — 서비스 의존성 그래프+복구 설정) |
| **오프라인 RSS 피드 리더** | RSS.Cast (≠ Daily.Dash: 날씨+일정+RSS 대시보드, ≠ Ink.Cast: 노트 앱 — 피드 구독+로컬 캐싱+전문 검색 전문 리더) |
| **색상 조화 규칙 테마 디자이너** | Theme.Forge (≠ Palette.Gen: 이미지에서 추출, ≠ Palette.Cast: 팔레트 컬렉션 관리, ≠ Access.Check: WCAG 검사만 — 처음부터 설계+디자인 토큰) |
| **스프라이트 시트 패커** | Sprite.Forge (≠ Img.Forge: 일반 이미지 처리, ≠ Pixel.Forge: 픽셀아트 에디터 — MaxRects 패킹+애니메이션 프레임+JSON 메타) |
| **Git 저장소 통계 분석기** | Git.Stats (≠ Git.Reel: 커밋 그래프 탐색기 — 히트맵·핫파일·기여자·언어 분포 분석) |
| **프로세스 CPU 친화도 관리** | Win.Tamer (≠ Tray.Stats: 성능 모니터링, ≠ Perf.Lens: DirectX 게임 오버레이 — SetProcessAffinityMask+자동 적용 프로파일) |
| **픽셀 폰트 그리드 에디터** | Font.Draw (≠ Font.Scout: 폰트 미리보기, ≠ Font.Probe: 화면 추출, ≠ Pixel.Forge: 픽셀아트 — BDF 글리프 창작) |
| **격자 인접 글자 체인 단어 찾기** | Word.Weave (≠ Word.Bomb: 음절 타이핑, ≠ Glyph.Rush: 타이핑 슈터 — Boggle 변형 공간 단어 탐색) |
| **개발자 스타트업 방치형 게임** | Code.Idle (≠ Forge.Idle: 광물 채굴·제련, ≠ Auto.Build: 실시간 공장 퍼즐 — 코드 라인+기술 부채+개발자 유머 이벤트) |
| **자기 극성 전환 플랫포머** | Magnet.Jump (≠ Magnet.Maze: 자기력으로 볼 유도, ≠ Tilt.Ball: 판 기울이기 — 캐릭터 N/S 극성 전환 실시간 플랫포머) |
| **오디오 스펙트럼 시각화** | Audio.Scope (≠ Pitch.Find: BPM·키 분석, ≠ Noise.Guard: 마이크 노이즈 필터 — 실시간 오실로스코프+FFT 시각화 도구) |
| **VPN 설정 GUI 관리** | VPN.Cast (≠ SSH.Vault: SSH config, ≠ DNS.Flip: DNS 서버 전환, ≠ Net.Ghost: MAC 스푸퍼 — WireGuard/OpenVPN 피어 관리) |
| **레지스트리 브라우저·비교기** | Reg.Vault (≠ Sys.Clean: 레지스트리 청소만, ≠ App.Temp: 앱 세션 롤백 — 정규식 검색+스냅샷 diff+JSON 내보내기) |
| **인터랙티브 Runbook 실행기** | Run.Book (≠ Task.Cast: Kanban 보드, ≠ Batch.Flow: 파일 파이프라인, ≠ Sched.Cast: 예약 실행기 — YAML 스텝+쉘 명령 실행) |
| **ZIP/7z 아카이브 생성기** | Archive.Forge (≠ Zip.Peek: 읽기 전용 탐색, ≠ Crypt.Drop: 파일 암호화 금고 — 아카이브 생성+분할+제외 패턴) |
| **주사위 면 값 소코반 퍼즐** | Dice.Shift (≠ Dungeon.Dice: 덱빌딩 로그라이크, ≠ Ice.Slide: 얼음 슬라이딩, ≠ Neon.Push: 소코반+색상+레이저) |
| **미로 설계자 vs AI 탈출 대결** | Maze.Craft (≠ Maze.Dread: 탈출자 시점, ≠ Tower.Fall: 역 타워디펜스 — 벽 배치+BFS AI 탈출자 방해) |
| **자물쇠 해제 미니게임** | Lock.Pick (≠ Room.Code: 오브젝트 클릭&조합 방탈출 — 핀텀블러·다이얼·슬라이딩 등 자물쇠 메커닉 특화) |
| **만화 패널 메타 플랫포머** | Frame.Jump (≠ Rift.Jump: 2점 포털, ≠ Phase.Gate: 두 차원 전환 — 만화 패널 경계 순간이동+독립 물리) |
| **논리 게이트 회로 빌더 퍼즐** | Chip.Logic (≠ Volt.Chain: 타이밍 전류 스위치 순서, ≠ Laser.Net: 레이저 반사 — AND/OR/NOT 게이트 배치+진리표 달성) |
| **지하 뿌리 성장 자원 전략** | Root.Spread (≠ Leaf.Grow: L-시스템 지상 성장, ≠ Colony.Sim: 곤충 군집 — 지하 타일맵 영양소 쟁탈 전략) |
| **집라인 케이블 화물 배송 퍼즐** | Zipline.Rush (≠ Bridge.Craft: 정적 교량 응력, ≠ Drone.Haul: 드론 운반 — Verlet 집라인 동적 이동 물리) |
| **균류 슬라임 몰드 네트워크 성장** | Spore.Net (≠ Colony.Sim: 곤충 페로몬 군집, ≠ Leaf.Grow: 식물 L-시스템 — Physarum 알고리즘 최적 네트워크) |
| **드럼 패턴 청각 기억 재현** | Echo.Drum (≠ Beat.Drop: 박자 레인 리듬, ≠ Chord.Strike: 동시 누르기, ≠ Sound.Grid: 격자 시퀀서 창작 — 청각 기억+재현) |
| **색상+기호 타일 슬라이딩 퍼즐** | Block.Shift (≠ Ice.Slide: 단방향 미끄러짐, ≠ Neon.Push: 소코반+레이저, ≠ Rise.Match: 매치3 — 행/열 루프 슬라이드 큐브 퍼즐) |
| **볼록·오목렌즈 빛 세기 집중 퍼즐** | Light.Cast (≠ Laser.Net: 단일 레이저 경로, ≠ Prism.Break: 색 RGB 분리 — 굴절+열 집중 세기 게이지 퍼즐) |
| **상승기류 글라이더 수직 아케이드** | Sky.Drift (≠ Neon.Run: 수평 장애물 러너, ≠ Pulse.Run: 리듬 러너 — Verlet 양력+열상승기류 수직 무한 스크롤) |
| **간격반복 플래시카드 앱** | Flash.Card (≠ Type.Rocket: 타이핑 WPM 연습, ≠ Dict.Cast: 사전 조회 — SM-2 알고리즘 장기 기억 내재화, Anki 대체) |
| **전자책 EPUB 리더** | Epub.Cast (≠ PDF.Forge: PDF 편집, ≠ Mark.View: 마크다운 뷰어, ≠ Ink.Cast: 노트 앱 — EPUB/MOBI 전자책 전용) |
| **SVG 벡터 에디터** | SVG.Forge (≠ Pixel.Forge: 래스터 픽셀아트, ≠ Draw.Board: 자유형 화이트보드, ≠ Icon.Forge: ICO 변환 — Bezier 패스·Boolean 연산 SVG 에디터) |
| **개인 건강 수치 기록·차트** | Health.Log (≠ Habit.Chain: GitHub 잔디 습관 체크, ≠ Budget.Cast: 금융, ≠ Toast.Cast: 리마인더 — 체중·혈압·혈당 시계열 수치 로깅) |
| **시각적 Traceroute 세계지도** | Net.Trace (≠ Net.Scan: LAN ARP 탐지, ≠ Uptime.Eye: HTTP 폴링 — 인터넷 경로 홉 지리적 지도 시각화) |
| **오디오 파형 트리머·편집기** | Audio.Cut (≠ Music.Player: 재생기, ≠ Video.Trim: 영상 트리머, ≠ Pitch.Find: 분석 전용 — 오디오 전용 파형 비파괴 편집) |
| **오프라인 문서 일괄 OCR** | OCR.Forge (≠ Screen.OCR·Screen.Pickup: 화면 영역 실시간, ≠ Snap.Cast: 레이아웃 분석 — Tesseract 문서 파일 배치 디지털화) |
| **IP 서브넷·CIDR 계산기** | IP.Forge (≠ Quick.Calc: 16진수·IEEE 754 비트 연산 — IP 서브넷·VLSM·수퍼넷 네트워크 설계 특화) |
| **악보 뷰어 + MIDI 재생** | Score.Cast (≠ Music.Player: MP3/FLAC 오디오, ≠ Pitch.Find: BPM 분석 — MusicXML/ABC 오선보 렌더 + SF2 합성 + 파트 연습) |
| **패시브 키스트로크 히트맵 트레이** | Key.Heat (≠ Type.Rocket: 능동적 WPM 훈련 — 백그라운드 키 빈도 히트맵·WPM 분석, 내용 절대 무저장) |
| **스도쿠 퍼즐** | Sudoku.Cast (≠ Pixel.Cross: 노노그램, ≠ Logic.Zebra: 격자 소거 논리, ≠ Number.Storm: 2048 변형 — 9×9 표준+변형 스도쿠) |
| **솔리테어 카드 게임** | Card.Solo (≠ Neon.Card: 로그라이크 덱빌딩 — 클래식 패시엔스 솔리테어 5종 컬렉션) |
| **소행성 채굴 로그라이트 슈터** | Astro.Mine (≠ Star.Strike: 고정 스크롤 슈터, ≠ Dodge.Blitz: 탄막 회피 — 자유 탐색+채굴+웨이브 상점 성장) |
| **산소 관리 채굴 로그라이크** | Cave.Run (≠ Rogue.Tile: RPG 전투, ≠ Dungeon.Dice: 주사위 덱빌딩 — 수직 동굴·산소·지진 붕괴 생존) |
| **아인슈타인 격자 논리 퍼즐** | Logic.Zebra (≠ Chip.Logic: 회로 게이트, ≠ Sudoku.Cast: 숫자 배치 — 범주 교차 소거 논리) |
| **말뚝 솔리테어** | Peg.Solo (≠ Ice.Slide: 얼음 물리, ≠ Neon.Push: 소코반+레이저, ≠ Slide.Rush: 15퍼즐 — 말뚝 건너뛰기 제거 퍼즐) |
| **오프라인 세계지리 퀴즈** | Geo.Quiz (≠ Color.Blitz: 스트룹 인지, ≠ Word.Bomb: 단어 폭탄 — 지리 지식 5모드 + SM-2 재출제) |
| **타이머 파이프 회전 퍼즐** | Pipe.Lay (≠ Flow.Pipe: 색상 점 연결 채우기, ≠ Volt.Chain: 전류 타이밍 — 타이머+파이프 타일 회전+BFS 흐름) |
| **파쿠르 콤보 모멘텀 러너** | Trail.Blaze (≠ Neon.Run·Dash.City: 단순 점프 러너, ≠ Crumble.Run: Voronoi 파괴 러너 — 벽달리기·레지 그랩 콤보 체인) |
| **ACO 개미 군집 시뮬 샌드박스** | Swarm.Ant (≠ Colony.Sim: 범용 에이전트, ≠ Spore.Net: 균류 네트워크 — ACO 페로몬 경로 시각화·개미 특화) |
---

### Apps (신규 — idea_20260311_12차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Note.File` | 파일·폴더 스티커 메모 첨부 (IShellIconOverlayIdentifier 아이콘 배지, SQLite 저장, 탐색기 툴팁 미리보기, 태그+검색, Markdown 리포트) | idea_20260311_12차 |
| `Dir.Cast` | 폴더 구조 트리 시각화·내보내기 (ASCII Tree/Markdown/JSON/SVG 다이어그램, .gitignore 필터, 변경 스냅샷 비교, 클립보드 즉시 복사) | idea_20260311_12차 |
| `Screen.Annotate` | 화면 오버레이 실시간 주석 레이어 (투명 Topmost 창, 펜·화살표·도형·형광 도구, WS_EX_TRANSPARENT 마우스 통과, OBS/Zoom 화면공유 포함) | idea_20260311_12차 |
| `Print.Cast` | 인쇄 큐 시각적 관리자 (순서 드래그, PDF 미리보기·병합, 페이지별 재인쇄, 히스토리 로그, 잉크 비용 계산) | idea_20260311_12차 |
| `Path.Guard` | PATH 환경변수 전용 관리자 (드래그 순서 재배치, 깨진·중복·비활성 경로 색상 진단, 실행파일 위치 검색, 버전 충돌 감지, 즉시 적용) | idea_20260311_12차 |
| `ISO.Mount` | ISO·VHD·VHDX 가상 디스크 마운트 GUI (드래그&드롭 마운트, 드라이브 문자 고정, 자동 마운트 목록, VHD 생성 마법사, 즐겨찾기) | idea_20260311_12차 |
| `Crash.View` | Windows 미니덤프(.dmp) GUI 분석기 (콜스택·로드 모듈·스레드 목록, ClrMD .NET 관리 힙, PDB 심볼 자동 다운로드, HTML 리포트) | idea_20260311_12차 |
| `Log.Merge` | 다중 소스 로그 통합 타임라인 뷰어 (50+ 타임스탬프 포맷 자동 파싱, 코릴레이션 ID 추적 하이라이트, 오류 빈도 히트맵, 가상 스크롤) | idea_20260311_12차 |
| `Shortcut.Forge` | Windows .lnk 바로가기 일괄 생성·관리 (CSV/JSON 배치 생성, 아이콘 브라우저, 깨진 경로 감지·재연결, ZIP 내보내기) | idea_20260311_12차 |
| `Mem.Lens` | 프로세스 메모리 심층 분석기 (Working Set·Private·Shared·Heap 분류, 가상 주소 맵, .NET Gen0/Gen1/Gen2/LOH, 누수 추세 감지) | idea_20260311_12차 |

### 게임 — 제안된 미구현 아이디어 (idea_20260311_12차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Drift.Paint` | Arcade / Racing / Creative | 탑다운 드리프트 차량 타이어 자국이 컬러 잉크 → 캔버스 채움% 목표, 색상 혼합, 드리프트 콤보 배율, 레벨 에디터 |
| `Morph.Rush` | Arcade / Platformer / Speed | 원(구르기/곡면)/삼각(관통)/사각(충격파) 폼 실시간 전환, 폼별 고유 장벽, 연속 전환 콤보, 절차 생성 무한 레벨 |
| `Flock.Guide` | Puzzle / Strategy / Simulation | Boids 새떼 + 유인·반발 비콘 배치로 게이트 통과 유도, 포식자 AI·강풍 방해, 최소 비콘 수 도전, 샌드박스 |
| `Carbon.Chain` | Puzzle / Casual / Educational | 원자(H·C·O·N) 블록 낙하 → 원자가 규칙 자동 결합 → 안정 분자(H₂O/CH₄/CO₂) 완성 소거, 고분자 체인 고점수 |
| `Weight.Cross` | Puzzle / Physics | 지렛대 위 무게 블록 드래그 배치 → 토크 균형 달성 → 2단계 다리 건너기 (바람·충격 방해), 60레벨 + 에디터 |
| `Strobe.Blitz` | Shooter / Rhythm / Arcade | BGM BPM 위상 사이클로 적 실체화·투명 전환, 밝음에서 격파·어두움에서 포지셔닝, 역위상 적·공명 적, 커스텀 음악 |
| `Laser.Blitz` | Shooter / Action / Puzzle | 적 레이저 실시간 거울 배치 역반사, 제한 거울 수량, 프리즘·충전 레이저 특수 요소, 연속 반사 콤보, 무한 서바이벌 |
| `Totem.Fall` | Arcade / Physics / Casual | 동물별 고유 물리(뱀=감김·개구리=탄성·코끼리=무게·독수리=바람저항·고슴도치=최대마찰) 토템 쌓기, 이벤트 생존 |
| `Sand.Castle` | Sandbox / Building / Survival | 모래 안식각·수분 점착력 시뮬, 조수 사이클, 파도 침식 물리, 지지대·해자 업그레이드, 창작 자유 모드 |
| `Ghost.Type` | Skill / Casual / Typing | 입력 글자 완전 마스킹, 오타 시 화면 빨간 플래시만 피드백, 무음·타임어택·코드 스니펫 챌린지, 손가락별 오타 히트맵 |

---

| **파일·폴더 인라인 스티커 메모** | Note.File (≠ Ink.Cast: 마크다운 노트 앱, ≠ Quick.Memo: 플로팅 메모, ≠ Stand.Up: 스탠드업 기록 — 탐색기 아이콘 배지+쉘 익스텐션) |
| **폴더 구조 트리 시각화 내보내기** | Dir.Cast (≠ Disk.Lens: 용량 시각화, ≠ Find.Fast: 파일 검색 — ASCII·SVG·Markdown 구조 다이어그램 + 변경 비교) |
| **화면 오버레이 실시간 주석 레이어** | Screen.Annotate (≠ Cursor.Lens: 스포트라이트+줌, ≠ Clip.Annotate: 스크린샷 후 편집 — 투명 레이어 위 직접 그리기) |
| **인쇄 큐 시각적 관리자** | Print.Cast (≠ Sys.Clean: PC 청소 — 인쇄 순서 조정+PDF 병합+히스토리+비용 계산 특화) |
| **PATH 환경변수 전용 진단 관리** | Path.Guard (≠ Env.Guard: 모든 환경변수 관리 — PATH 드래그 순서+중복·깨진 경로+버전 충돌 감지 특화) |
| **ISO·VHD 가상 디스크 마운트 GUI** | ISO.Mount (≠ Disk.Lens: 용량 시각화, ≠ Drive.Bench: 벤치마크 — 가상 디스크 마운트/해제+자동 마운트+VHD 생성) |
| **Windows 미니덤프 분석 뷰어** | Crash.View (≠ Log.Lens: 텍스트 로그 뷰어, ≠ Proc.Timeline: 리소스 타임라인 — .dmp 콜스택·모듈·예외 GUI 분석) |
| **다중 로그 소스 통합 타임라인** | Log.Merge (≠ Log.Lens: 단일 파일 뷰어, ≠ Win.Event: EVT/EVTX 뷰어 — 다중 소스 시간순 머지+코릴레이션 ID 추적) |
| **Windows .lnk 바로가기 일괄 관리** | Shortcut.Forge (≠ Batch.Rename: 파일명 변경, ≠ Run.Deck: 프로젝트 런처 — .lnk 배치 생성·깨진 경로 재연결·아이콘 피커) |
| **프로세스 메모리 구조 심층 분석** | Mem.Lens (≠ Tray.Stats: 트레이 실시간 모니터, ≠ Proc.Timeline: 시계열 레코더 — Working Set·Private·Heap 분류+누수 감지) |
| **드리프트 타이어 자국 캔버스 채우기** | Drift.Paint (≠ Nitro.Drift: 레이싱 완주, ≠ Burst.Canvas: 슈터+페인팅, ≠ Fluid.Paint: 유체 페인팅 — 탑다운 드리프트 물리+채움% 목표) |
| **원·삼각·사각 폼 전환 러너** | Morph.Rush (≠ Neon.Run: 고정 캐릭터 러너, ≠ Jelly.Jump: 소프트바디 플랫포머 — 폼별 다른 물리 특성 활용 실시간 전환) |
| **Boids 새떼 유인·반발 비콘 퍼즐** | Flock.Guide (≠ Colony.Sim: 군집 생존 시뮬, ≠ Swarm.Rush: Boids 적 AI 슈터 — 플레이어가 비콘으로 새떼 조종하는 퍼즐) |
| **낙하 원자 원자가 결합 테트리스** | Carbon.Chain (≠ Atom.Craft: 정적 배치 분자 합성, ≠ Sand.Fall: 물리 입자 — 낙하 블록+원자가 규칙 자동 결합+분자 소거) |
| **지렛대 균형추 배치 물리 퍼즐** | Weight.Cross (≠ Tilt.Ball: 판 기울이기, ≠ Drone.Haul: 드론 케이블 — 토크·모멘트 균형 달성 후 2단계 건너기) |
| **BGM 위상 동기화 생존 슈터** | Strobe.Blitz (≠ Signal.Rush: 리듬+슈터(박자 대미지), ≠ Beat.Rogue: 비트 타이밍 이동 — BGM 밝음/어두움 위상이 적 실체화 조건) |
| **실시간 거울 배치 레이저 역반사 슈터** | Laser.Blitz (≠ Laser.Net: 정적 배치 경로 퍼즐, ≠ Echo.Shot: 음파 반사 — 실시간 액션에서 즉흥 거울 배치로 역공격) |
| **동물 고유 물리 토템 균형 쌓기** | Totem.Fall (≠ Wobble.Stack: 소프트바디 젤리, ≠ Stack.Race: 랜덤 블록 쌓기 대전 — 동물별 고유 마찰·탄성·질량 전략 선택) |
| **모래 퇴적·침식 물리 성 건축 생존** | Sand.Castle (≠ Sand.Fall: 물질 반응 관찰 샌드박스, ≠ Last.Spark: 캠프파이어 생존 — 안식각+수분 물리 건축+파도 방어) |
| **입력 안 보이는 블라인드 타이핑** | Ghost.Type (≠ Type.Rocket: WPM 트래커 앱, ≠ Type.Race: 레이싱 게임, ≠ Glyph.Rush: 슈터 — 완전 마스킹+오타 플래시만 피드백) |

---

### Apps (신규 — idea_20260311_13차)

| 이름 | 카테고리 | 핵심 기능 |
|------|----------|----------|
| `Win.Rules` | Productivity / Automation | 창 자동화 규칙 엔진 — 프로세스+타이틀 조건 → 크기·위치·모니터·투명도 자동 적용, 규칙 충돌 감지, 프리셋 공유 |
| `Desk.Grid` | Productivity / Desktop | 바탕화면 아이콘 가상 그리드 레이아웃 저장·복원 — 이름/정렬 기준 슬롯 고정, 해상도 변경 후 복원, 여러 프리셋 |
| `Key.Test` | Dev Tools / Diagnostics | 키보드 진단 도구 — 키 반응 속도·멀티키 동시 입력 시각화, N-Key Rollover 테스트, 사각 지대(ghosting) 탐지 |
| `Win.Pop` | Dev Tools / Productivity | WPF DWM 썸네일 Picture-in-Picture — 임의 앱 창을 최소화 없이 반투명 미니 오버레이로 상시 표시, 클릭 투과 |
| `Hotkey.Cast` | Productivity / Automation | 전역 단축키 통합 관리자 — 앱별 핫키 등록·충돌 감지·비활성 앱 명령 전송, 조건부(활성 창별) 컨텍스트 단축키 |
| `Ink.Board` | Productivity / Collaboration | LAN 협업 화이트보드 — mDNS 자동 디스커버리, 레이어·벡터 객체·스티커, PDF/SVG 내보내기, 오프라인 우선 |
| `Ping.Grid` | Dev Tools / Network | 멀티 호스트 핑 그리드 — 최대 64 호스트 동시 모니터, RTT 히트맵·손실률 차트, 임계값 알림, CSV 내보내기 |
| `Sound.Vis` | Multimedia / Dev Tools | FFT 오디오 시각화 — WASAPI 루프백 캡처, 주파수 스펙트럼·웨이브폼·스펙트로그램, 커스텀 팔레트, PNG 스냅샷 |
| `Win.Title` | Dev Tools / Productivity | 타이틀바 커스텀 액션 버튼 — DWM HWND 후킹으로 최소화 버튼 옆 커스텀 버튼(항상 위·투명도·다크모드) 주입 |
| `App.Hours` | Productivity / Monitoring | 앱 사용시간 스케줄 제한 — 일별 앱 사용 시간 쿼터, 예약 차단 시간대, 집중 모드, 주간 리포트 CSV |

### 게임 — 제안된 미구현 아이디어 (idea_20260311_13차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Blade.Storm` | Arcade / Physics / Slicer | 재질별 다른 절단 물리(나무=섬유 분리·금속=파편·젤리=변형) + 연쇄 슬라이스 콤보, 거스름 각도 판정, 무기 내구 |
| `Dodge.Craft` | Arcade / Action / Strategy | 격자 위 회피 이동 + 제한 시간 내 장벽·함정 배치로 다음 웨이브 패턴 유도, 방어물 업그레이드 트리 |
| `Plasma.Cut` | Puzzle / Physics / Precision | 열선 절단 궤적 드래그로 물체를 정확한 모양으로 분할 — 열 전도 시뮬, 목표 실루엣 매칭%, 연쇄 절단 콤보 |
| `Vox.Drop` | Puzzle / Rhythm / Arcade | BGM 주파수 분석 → 고음·저음·중음 대역 블록 낙하 색상 지정, 동일 주파수 블록 수직 4개 소거, 반박자 예측 |
| `Rope.Bridge` | Puzzle / Physics / Adventure | 밧줄 앵커 배치로 흔들리는 다리 건설 → 캐릭터 하중·바람·적 충격 생존 횡단, 재료 수량 제한, 공동 건설 Co-op |
| `Mirror.Blitz` | Arcade / Action / Puzzle | 과거 자기 플레이 고스트 다중 복사 → 고스트들과 협력해 빛 반사 퍼즐 동시 해결, 루프 기록 전략 |
| `Pixel.Farm` | Simulation / Idle / Creative | 16×16 픽셀 격자 농장 경영 — 픽셀 단위 토양 수분·영양 시뮬, 작물 교배 색상 유전, 마을 납품 주문, 계절 이벤트 |
| `Bubble.Burst` | Arcade / Rhythm / Casual | 거품 팽창 속도 예측 → 정점 직전 타이밍 터치 파열, 연쇄 파열 콤보, 점성·압력 변수, BGM BPM 동기화 |
| `Chain.Hook` | Puzzle / Physics / Platformer | 체인+갈고리를 앵커에 걸어 스윙·당기기 — 텐션·진자 물리, 체인 길이 제한, 연결 구조물 무게 균형, 물리 샌드박스 |
| `Veil.Break` | Shooter / Mystery / Exploration | 오버레이 베일이 반쯤 가린 공간을 탐색 슈터로 점진 해제, 적 위치 소음 단서+베일 찢기 패턴, 레이어드 맵 |

---

| **창 크기·위치 자동화 규칙 엔진** | Win.Rules (≠ Desk.Grid: 아이콘 레이아웃 저장, ≠ Win.Pop: DWM 오버레이, ≠ Win.Title: 타이틀바 버튼 — 프로세스 조건 기반 창 배치 규칙) |
| **바탕화면 아이콘 그리드 레이아웃 저장** | Desk.Grid (≠ Win.Rules: 창 배치 자동화, ≠ Folder.Purge: 폴더 정리 — 아이콘 슬롯 고정+해상도 변경 후 복원) |
| **키보드 키 반응·멀티키 시각화 진단** | Key.Test (≠ Hotkey.Cast: 단축키 관리자, ≠ Key.Trace: 입력 기록 리플레이 — ghosting/N-KRO 하드웨어 한계 진단) |
| **DWM 썸네일 앱 창 PiP 오버레이** | Win.Pop (≠ Win.Rules: 규칙 배치, ≠ Win.Title: 타이틀바 버튼, ≠ Screen.Annotate: 주석 오버레이 — DWM API 썸네일 미니 오버레이) |
| **전역 단축키 통합 관리·충돌 감지** | Hotkey.Cast (≠ Key.Test: 키보드 하드웨어 진단, ≠ Key.Trace: 입력 리플레이, ≠ Macro.Deck: 매크로 실행 — 앱별 핫키 등록+충돌 감지) |
| **LAN mDNS 협업 화이트보드** | Ink.Board (≠ Screen.Annotate: 개인 오버레이, ≠ Quick.Memo: 플로팅 메모 — mDNS 자동 발견+다중 사용자 실시간 공동 편집) |
| **64 호스트 동시 핑 RTT 히트맵** | Ping.Grid (≠ Port.Watch: 포트 상태 감시, ≠ Api.Probe: HTTP API 테스트 — 대규모 ICMP 동시 핑+RTT 히트맵+손실률 차트) |
| **WASAPI 루프백 FFT 오디오 시각화** | Sound.Vis (≠ Serve.Cast: 미디어 스트리밍, ≠ Music.Player: 플레이어 — 시스템 오디오 스펙트럼+웨이브폼+스펙트로그램 실시간 렌더링) |
| **DWM 후킹 타이틀바 커스텀 버튼 주입** | Win.Title (≠ Win.Rules: 창 위치 규칙, ≠ Win.Pop: PiP 썸네일 — HWND 후킹으로 타이틀바에 커스텀 액션 버튼 추가) |
| **앱별 일일 사용 쿼터·집중 모드** | App.Hours (≠ Stay.Awake: 절전 방지, ≠ Win.Rules: 창 규칙 — 앱별 시간 쿼터 제한+예약 차단+주간 리포트) |
| **재질별 절단 물리 슬라이서** | Blade.Storm (≠ Neon.Slice: 단일 물리 슬라이싱, ≠ Plasma.Cut: 열선 분할 — 재질(나무/금속/젤리)별 다른 절단 물리+파편 시뮬) |
| **회피+장벽 배치 하이브리드 아케이드** | Dodge.Craft (≠ Dodge.Blitz: 순수 회피 아케이드, ≠ Laser.Blitz: 거울 배치 슈터 — 회피 이동+제한 자원 장벽 배치 전략) |
| **열선 궤적 드래그 정밀 절단 퍼즐** | Plasma.Cut (≠ Blade.Storm: 재질별 슬라이서, ≠ Neon.Slice: 타이밍 슬라이서 — 열 전도 시뮬+목표 실루엣 매칭% 정밀 분할) |
| **주파수 대역별 낙하 블록 리듬 퍼즐** | Vox.Drop (≠ Strobe.Blitz: BGM 위상 슈터, ≠ Signal.Rush: 리듬 슈터 — FFT 주파수 분석 낙하 블록+반박자 예측) |
| **밧줄 앵커 물리 다리 건설 퍼즐** | Rope.Bridge (≠ Weight.Cross: 지렛대 균형추, ≠ Chain.Hook: 갈고리 스윙 — 밧줄 하중·진동·바람 물리+횡단 생존) |
| **과거 고스트 다중 복사 협력 슈터** | Mirror.Blitz (≠ Echo.Shot: 음파 반사 슈터, ≠ Laser.Blitz: 거울 배치 역반사 — 루프 기록 고스트 협력 동시 퍼즐 해결) |
| **픽셀 격자 토양·작물 유전 농장 경영** | Pixel.Farm (≠ Sand.Fall: 입자 시뮬, ≠ Root.Spread: 뿌리 성장 — 픽셀 단위 수분·영양 + 작물 교배 색상 유전) |
| **팽창 정점 타이밍 거품 파열 리듬** | Bubble.Burst (≠ Strobe.Blitz: 위상 동기화 슈터, ≠ Vox.Drop: 블록 낙하 — 거품 팽창 물리 정점 예측+연쇄 파열 콤보) |
| **갈고리 체인 텐션 스윙 물리 퍼즐** | Chain.Hook (≠ Rope.Bridge: 밧줄 다리 건설, ≠ Zipline.Rush: 집라인 활주 — 갈고리 앵커+텐션 진자 물리+구조물 무게 균형) |
| **베일 점진 해제 탐색 슈터** | Veil.Break (≠ Dodge.Blitz: 순수 회피, ≠ Dodge.Craft: 장벽 배치 — 불투명 오버레이 레이어드 맵+소음 단서+찢기 패턴 탐색) |

---

### Apps (신규 — idea_20260311_14차)

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Conf.Guard` | 앱 설정 파일 자동 버전 백업 트레이 — AppData/LocalAppData/.config 감시 FSW, .json/.xml/.ini/.toml/.yaml 변경 시 n세대 스냅샷 자동 저장, DiffPlex diff 뷰어, 원클릭 롤백 | idea_20260311_14차 |
| `Pcap.View` | 네트워크 패킷 캡처 경량 뷰어 — Npcap/SharpPcap 기반 실시간 캡처, HTTP/DNS 평문 디코딩, TCP 스트림 대화 재조립, BPF 필터, .pcap/.pcapng 저장·열기, Wireshark 경량 대체 | idea_20260311_14차 |
| `Regex.Sweep` | 정규식 다중 파일 일괄 Find-Replace GUI — 폴더 재귀, 미리보기 diff(행 단위), 드라이런(변경 없이 결과 확인), 파일 타입 필터, 변경 이력 로그, sed/awk GUI 대체 | idea_20260311_14차 |
| `Wi.Probe` | WiFi 채널 간섭 분석기 트레이 — NativeWifi API로 주변 AP SSID/BSSID/RSSI/채널 스캔, 2.4/5/6GHz 채널 히트맵, 겹침 간섭 분석, 최적 채널 추천, 시계열 신호 그래프 | idea_20260311_14차 |
| `Tab.Vault` | 브라우저 탭 세션 독립 저장 트레이 — Chrome/Edge/Firefox remote-debugging-port API로 열린 탭 URL 스냅샷, 탭 그룹 보존, 중복 탭 감지, 날짜별 타임라인, 세션 검색·복원 | idea_20260311_14차 |
| `Code.Format` | 범용 코드 클립보드 포매터 팝업 — 전역 단축키 팝업, 자동 언어 감지, C#(Roslyn)/JS·TS(Prettier)/Python(Black)/JSON/YAML/SQL 포매터 내장, 결과 즉시 재복사 | idea_20260311_14차 |
| `Clip.Clean` | 클립보드 HTML/RTF 서식 자동 정제 트레이 — Word/웹 복사 시 서식 자동 제거→순수 텍스트, URL utm 파라미터 정규화, 줄바꿈 정규화, 단축키 토글, 예외 앱 화이트리스트 | idea_20260311_14차 |
| `Temp.Drop` | 일회성 파일 공유 드롭존 — 드래그&드롭→Kestrel HTTP 즉시 시작+QR 생성, 자동 만료(X분/Y회 다운로드), AES-256 패스워드 옵션, mDNS 디스커버리, 다운로드 로그 | idea_20260311_14차 |
| `Proc.Env` | 실행 중 프로세스 환경변수 실시간 조회 — ReadProcessMemory+PEB 파싱(32/64bit/WOW64), 필터 검색, 스냅샷 diff 비교, 변수 변경 추적, JSON/CSV 내보내기 | idea_20260311_14차 |
| `Mirror.Dev` | LAN Git-Aware 코드 미러링 트레이 — LibGit2Sharp diff 기반 증분 동기화(변경분만 전송), .gitignore 인식, 브랜치별 프로파일, mDNS 피어 자동 발견, AES 암호화 전송 | idea_20260311_14차 |

### 게임 — 제안된 미구현 아이디어 (idea_20260311_14차)

| 이름 | 장르 | 핵심 메커닉 |
|------|------|------------|
| `Nano.Forge` | Puzzle / Programming / Strategy | 시각 블록 코딩(IF/WHILE/REPEAT)으로 나노봇 행동 지시 → 시뮬레이션 실행 → 목표 도형 조립, 명령 수 제한 최적화, 순차·병렬 실행, 레벨 에디터+솔루션 공유 코드 |
| `Drop.Zone` | Puzzle / Physics / Casual | 삼각 페그 배치·제거로 공 낙하 경로 조정 → 목표 슬롯 착지 (Plinko 변형), 탄성·자석·이동 특수 페그, 다중 공 동시, 최소 페그 수 별점, 일일 챌린지 |
| `Burst.Climb` | Arcade / Roguelite / Physics | 방향 조준 폭발 반동으로 캐릭터 수직 상승 — 폭발물 종류별 다른 반동 물리, 연쇄 에어콤보, 절차 생성 장애물, 영구 업그레이드 트리, 수직 무한 스크롤, 일일 시드 랭킹 |
| `Steam.Valve` | Puzzle / Physics / Engineering | 밸브 개폐·파이프 연결 배치로 증기 압력 분산 — 과부하 폭발 방지, 터빈·피스톤·냉각기 부품, 다중 목표 압력 동시 달성, T/Y 분기 파이프, 60레벨+에디터 |
| `Paint.Clash` | Arcade / Strategy / Casual | 탑다운 이동으로 바닥 영역 페인팅 점령 (Splatoon 2D) — AI 대전, 색상 혼합 중립 구역, 특수 능력(포탈·잉크 폭탄·실드), 로컬 2P, 맵 에디터 |
| `Warp.Gate` | Puzzle / Logic / Physics | N개 포탈 게이트 쌍 배치 → 발사 구슬이 포탈 통과해 모든 골인 슬롯 동시 도달, 방향·크기 조절, 체인 포탈, 속도 보존 물리, 레벨 에디터+UGC |
| `Heat.Spread` | Puzzle / Simulation / Logic | 열원·냉각원 배치 → 열 확산 시뮬 → 타겟 셀 정확한 온도 범위 달성, 전도율 다른 타일(금속·돌·진공·얼음), 위상 변화 타일, 다중 타겟 동시, 50레벨 |
| `Knit.Path` | Puzzle / Creative / Geometric | 원형 보드 핀들을 순서 클릭 → 실 감기 → 완성 패턴이 목표 실루엣 일치, 교차점 색상 혼합, 최소 선분 수 도전, 다색 실 교대, 자유 창작+PNG 저장 |
| `Grid.Lock` | Puzzle / Logic / Casual | N×N 격자 행·열·2×2 블록 단위 회전으로 목표 색상+기호 패턴 달성, 최소 회전 수 별점, 잠금·연동 타일, 역방향 생성 유일해 보장, 무한 절차 생성 |
| `Pixel.Quest` | RPG / Narrative / Casual | 16×16 픽셀 세계, 전투 없는 대화·선택지·미니 퍼즐 힐링 RPG, 챕터 10개 단편 스토리, 분기 3엔딩, 픽셀 히트맵 수집, 칩튠 BGM |

---

| **설정 파일 자동 버전 백업 트레이** | Conf.Guard (≠ File.Guard: 어느 프로세스가 건드렸나 감사, ≠ Folder.Sync: 폴더 동기화 — .json/.xml/.ini 전용+n세대 스냅샷+diff 롤백) |
| **네트워크 패킷 캡처 경량 뷰어** | Pcap.View (≠ Proxy.Cast: HTTP 능동 인터셉트, ≠ Net.Scan: ARP LAN 기기 탐지 — NIC 레벨 패시브 캡처+TCP 스트림 재조립) |
| **정규식 다중 파일 일괄 Find-Replace** | Regex.Sweep (≠ Regex.Lab: 단일 문자열 정규식 테스터 — 실제 파일 시스템 대상 일괄 치환+미리보기 diff+드라이런) |
| **WiFi 채널 간섭 분석기** | Wi.Probe (≠ Net.Scan: ARP LAN 기기, ≠ Ping.Grid: ICMP 핑 — 802.11 채널 히트맵+간섭 분석+최적 채널 추천) |
| **브라우저 탭 세션 독립 저장 트레이** | Tab.Vault (≠ Link.Vault: 의도적 북마크+스냅샷 — 자동 주기적 탭 URL 백업+브라우저 크래시 복원+탭 그룹 보존) |
| **범용 코드 클립보드 포매터 팝업** | Code.Format (≠ Text.Forge: 텍스트 인코딩·해시 변환 — 소스 코드 구문 인식 다중 언어 포매팅 전역 팝업) |
| **클립보드 HTML/RTF 서식 자동 정제** | Clip.Clean (≠ Smart.Paste: SendInput 우회, ≠ Clip.Find: FTS 히스토리 검색 — 백그라운드 서식 자동 제거→순수 텍스트 교체) |
| **일회성 만료 파일 공유 드롭존** | Temp.Drop (≠ Serve.Cast: 폴더 범용 HTTP 서버, ≠ Drop.Bridge: P2P LAN 전송 — 만료 조건+횟수 제한+비밀번호+QR 일회성 특화) |
| **실행 중 프로세스 환경변수 조회** | Proc.Env (≠ Env.Guard: 시스템/사용자 레지스트리 — ReadProcessMemory+PEB 파싱 실행 중 프로세스 환경 블록 특화) |
| **LAN Git-Aware 코드 미러링 트레이** | Mirror.Dev (≠ Folder.Sync: 일반 파일 동기화, ≠ Drop.Bridge: 수동 전송 — LibGit2Sharp diff 증분+.gitignore+브랜치 프로파일) |
| **나노봇 명령어 프로그래밍 조립 퍼즐** | Nano.Forge (≠ Auto.Build: 컨베이어 공장 배치, ≠ Volt.Chain: 전류 회로 — 시각 블록 코딩 IF/WHILE 나노봇 순차·병렬 제어) |
| **Plinko 페그 배치 낙하 경로 퍼즐** | Drop.Zone (≠ Spin.Gate: 회전 게이트 방향 전환, ≠ Gravity.Well: 중력 우물 파티클 — 삼각 페그 전략 배치+확률 물리 낙하) |
| **폭발 반동 수직 상승 로그라이크** | Burst.Climb (≠ Sky.Drift: 글라이더 양력, ≠ Neon.Run: 수평 러너 — 폭발 방향·타이밍 물리 반동+연쇄 에어콤보+로그라이크) |
| **증기 압력 배관 밸브 물리 퍼즐** | Steam.Valve (≠ Flow.Pipe: 색상 경로 연결 — 압력 수치 시뮬+분기 배관+과부하 방지 엔지니어링 퍼즐) |
| **탑다운 영역 페인팅 전략 아케이드** | Paint.Clash (≠ Burst.Canvas: 슈터+팔레트 비율 생존, ≠ Drift.Paint: 드리프트 자국 채우기 — PVP 영역 점령+색상 혼합 중립 구역) |
| **포탈 네트워크 구슬 동시 착점 퍼즐** | Warp.Gate (≠ Rift.Jump: 플레이어 포탈 플랫포머, ≠ Spin.Gate: 단일 방향 전환 — 복수 구슬 N개 포탈 동시 골인 네트워크) |
| **열 확산 목표 온도 달성 퍼즐** | Heat.Spread (≠ Tidal.Wave: 2D 파동 방정식, ≠ Steam.Valve: 배관 압력 — 열역학 확산 방정식 타겟 온도 범위 달성) |
| **핀 실 감기 기하학 패턴 퍼즐** | Knit.Path (≠ Knot.Craft: 위상 매듭 풀기, ≠ Spring.Web: 거미줄 물리 — 기하학적 직선 실 감기+실루엣 매칭+아트 창작) |
| **행·열·블록 회전 목표 패턴 퍼즐** | Grid.Lock (≠ Block.Shift: 행/열 루프 선형 슬라이드, ≠ Ice.Slide: 단방향 미끄러짐 — 회전(Rotation) 기반 큐브 퍼즐) |
| **전투 없는 힐링 픽셀아트 내러티브 RPG** | Pixel.Quest (≠ Room.Code: 방탈출 포인트앤클릭, ≠ Rogue.Tile: 영구사망 로그라이크 — 감성 스토리+분기 선택+수집 요소 첫 도입) |
