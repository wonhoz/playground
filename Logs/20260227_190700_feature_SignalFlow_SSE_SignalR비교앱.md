# Signal.Flow — SSE vs SignalR 비교 샘플 앱

**날짜**: 2026-02-27
**태그**: feature
**작업자**: Claude Code

---

## 목표

서버 푸시 기술(SSE, SignalR)을 한눈에 비교할 수 있는 학습/샘플 앱 구현.
Mock.Server와 동일한 WPF + 내장 ASP.NET Core 패턴 사용.

## 위치

`Applications/Tools/Dev/Signal.Flow/`

## 구현 내용

### 파일 구조
- `Signal.Flow.csproj` — 프로젝트 파일
- `App.xaml / App.xaml.cs` — 다크 테마 (청록 #06B6D4 계열)
- `AssemblyInfo.cs` / `GlobalUsings.cs`
- `MainWindow.xaml / MainWindow.xaml.cs` — WPF UI
- `Models/ServerEvent.cs` — 이벤트 데이터 레코드
- `Server/FlowServer.cs` — ASP.NET Core 호스트 + SSE 브로드캐스터
- `Server/EventHub.cs` — SignalR Hub
- `Clients/SignalRFlowClient.cs` — WPF용 SignalR 클라이언트
- `Clients/SseFlowClient.cs` — WPF용 SSE 클라이언트
- `Resources/WebClient.html` — 브라우저용 HTML 클라이언트
- `Resources/gen_icon.ps1` + `app.ico`

### 핵심 기능
- 좌측 패널: 서버 시작/중지, 이벤트 발송 (알림/업데이트/경고/오류), 자동 이벤트
- 우측: SignalR 수신 로그 + SSE 수신 로그 나란히 비교
- 브라우저 HTML 클라이언트: SignalR JS + EventSource API 동시 표시
- 연결 상태 실시간 표시

## 작업 로그

| 시각 | 내용 |
|------|------|
| 19:07 | 작업 시작, 디렉토리 구조 생성 |
| 19:07 | 프로젝트 파일 작성 (csproj, GlobalUsings, AssemblyInfo) |
| 진행 중 | Models, Server, Clients, Resources, XAML 구현 |

## 결과

- [ ] dotnet build 성공
- [ ] Playground.slnx 등록
- [ ] 커밋 완료
