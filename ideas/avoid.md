# Avoid List — 아이디어 중복 방지 목록

> 이 파일은 브레인스토밍 시 **이미 제안되거나 구현된 아이디어를 다시 꺼내지 않도록** 하는 필터다.
> 새 아이디어를 제안하기 전 반드시 이 목록을 확인할 것.
> 마지막 갱신: 2026-02-26

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
| `Hash.Forge` | Dev/Tools | 해시·인코딩 올인원 (MD5/SHA/Base64/JWT/UUID) |
| `Log.Lens` | Dev/Tools | 로그 파일 분석 뷰어 |
| `Mock.Desk` | Dev/Tools | 로컬 Mock HTTP 서버 (GUI 라우트 정의) |
| `Log.Tail` | Dev/Tools | 실시간 로그 뷰어 (tail -f, 멀티탭, 색상 규칙) |
| `DNS.Flip` | Network | DNS 프리셋 원클릭 전환 트레이 앱 |
| `Port.Watch` | Network | 포트 점유 프로세스 모니터 + 원클릭 종료 |
| `Clipboard.Stacker` | Productivity | 클립보드 스택 관리 |
| `Code.Snap` | Productivity | 오프라인 코드 스크린샷 미화 도구 |
| `QR.Forge` | Productivity | 오프라인 QR 코드 생성기 (로고 삽입, 배치) |
| `Screen.Recorder` | Productivity | 화면 녹화 도구 |
| `Text.Forge` | Productivity | 텍스트·데이터 변환 도구 |
| `Word.Cloud` | Productivity | 오프라인 워드클라우드 생성기 |
| `Env.Guard` | System | Windows 환경변수 GUI 관리자 + 스냅샷/롤백 |
| `Link.Vault` | Tools | 완전 오프라인 북마크 관리자 (페이지 스냅샷) |

---

## 2. 구현 완료 게임 (Games)

| 게임 | 장르 |
|------|------|
| `Dungeon.Dash` | Action |
| `Brick.Blitz` | Arcade |
| `Dash.City` | Arcade |
| `Neon.Run` | Arcade |
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

### Dev / Tools (개발자 도구)

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

### Text / Data Tools

| 이름 | 핵심 기능 | 출처 |
|------|----------|------|
| `Type.Wand` | 텍스트 즉석 변환 (대소문자, 케이스, 인코딩 등) — Text.Forge와 유사 | idea_20260220b |
| `Subtitle.Sync` | SRT 자막 싱크 오프셋 조정기 | idea.md |

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
