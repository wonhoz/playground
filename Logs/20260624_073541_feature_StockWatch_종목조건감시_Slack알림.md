# Stock.Watch — 종목 조건 감시 · Slack 알림 WPF 앱 신규 개발

- **일시**: 2026-06-24 07:35 (KST)
- **태그**: feature
- **컴포넌트**: stock.watch
- **버전**: v1.0.0

## 배경 / 요청
사용자가 특정 주식의 매수/매도 조건을 사전 설정해 두고, 조건 충족 시 Slack으로 알림을 받는 프로그램을 요청.
- 캔들·볼린저밴드 등 주요 지표 시각화
- RSI < 30, 볼린저 하단 터치, 거래량 2배 등 조건을 자유롭게 설정
- 초기 종목 4종(SK하이닉스 000660, SK스퀘어 402340, 보해양조 000890, 부국철강 026940), 종목 추가 확장성
- 데이터: 미래에셋/키움/한국투자 Open API 검토

## 결정 사항 (사용자 확인)
- **증권사 API**: 한국투자증권 KIS (순수 REST/WebSocket, .NET 최적, 무료, 모의투자 시세 조회 가능)
- **앱 형태**: WPF 차트 대시보드
- **갱신 방식**: 폴링 (N초 주기)
- API 키는 사용자가 KIS Developers에서 직접 발급 → 앱 설정창에 입력 (소스에 미포함)

## 구현 내용
**위치**: `Applications/Finance/Stock.Watch/` (단일 WPF 프로젝트, net10.0-windows, 단일파일 배포)

### 구조
- **Models**: `Candle`, `Quote`, `AlertLog`(+RuleKind), `WatchedStock`(종목+룰셋+쿨다운상태)
- **Indicators**: `IndicatorMath`(SMA·EMA·RSI(Wilder)·Bollinger·MACD), `IndicatorSet`(캔들→전체 시리즈 빌드 + Latest 스냅샷)
- **Conditions**: `Condition`(피연산자·연산자·상수/지표×배수, 크로스 포함), `RuleSet`(AND/OR 결합), 한글 Summary
- **Services**:
  - `KisApiClient` — OAuth tokenP(24h 캐시) + inquire-price(현재가) + inquire-daily-itemchartprice(일봉), 실전/모의 토글, 외부 NuGet 무의존
  - `SlackNotifier` — Incoming Webhook POST + 테스트 전송
  - `MonitorService` — 폴링 루프, 장중시간(평일 09:00~15:30) 체크, 당일봉 실시간 갱신, 엣지트리거+쿨다운 알림
  - `AppConfig` — %LocalAppData%\Playground\Stock.Watch\config.json 영속화 (기본 4종목 프리셋)
  - `NativeTheme` — DwmSetWindowAttribute 다크 타이틀바
- **UI**: `App.xaml`(다크 테마 전역 + ComboBox/CheckBox/ScrollBar ControlTemplate), `MainWindow`(좌 관심종목/중 차트/우 조건+알림로그), `SettingsWindow`(키·Slack·폴링), `CandleChart`(Canvas 직접 렌더 — 캔들+볼린저+거래량+RSI), `RuleSetEditor`(조건 동적 추가/삭제), `StockVm`

### 기본 조건 프리셋
- 매수(OR): RSI < 30 / 현재가 ≤ 볼린저하단
- 매도(OR): RSI > 70 / 현재가 ≥ 볼린저상단

## 마무리
- `Playground.slnx` Finance 폴더 등록
- `+publish.cmd`(108) · `+publish-all.cmd` · `+publish-selector.ps1`(N=108) 등록
- `README.md` 작성 (API 키 발급 가이드 포함)
- `ideas/avoid/apps.md` 채택 기록 (구현 완료 2026-06-24)
- 아이콘 후보 5종 + preview.html 생성 (`Resources/Candidate_20260624_0731/`)

## 빌드 검증
- `dotnet build Stock.Watch.csproj -c Release` → 경고 0, 오류 0
- 전체 솔루션 빌드 → Stock.Watch 정상(기존 C++ Claude.Shell.Native 2건은 dotnet CLI 대상 외, 무관)

## 후속 / 미결
- 사용자: KIS APP KEY/SECRET + Slack Webhook 발급 후 설정 입력 → 실제 시세 동작 확인
- 아이콘 최종 선택 → ICO 변환 → app.ico 반영
- (확장 후보) WebSocket 실시간 체결가, 분봉 조건, 다중 알림 채널
