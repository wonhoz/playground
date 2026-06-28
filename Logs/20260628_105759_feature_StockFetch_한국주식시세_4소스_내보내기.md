# Stock.Fetch — 한국 주식 시세 내보내기 앱 신규 (v1.0.0)

- **일시**: 2026-06-28 10:58 (KST)
- **태그**: feature
- **프로젝트**: `Applications/Finance/Stock.Fetch`

## 요청
한국 장 종목의 특정 기간 저가·고가·종가 데이터를 종목코드 입력만으로 CSV/JSON 등
편한 포맷으로 가져오는 앱. 카카오페이에서 쓰이는 데이터와 가장 유사·정확한 데이터 선호.

## 정렬된 요구사항 (AskUserQuestion)
- 앱 형태: **WPF GUI**
- 출력 포맷: **가능한 모든 포맷** (CSV·TSV·JSON·XML·Markdown)
- 데이터 소스: 사용자가 4곳(한국투자 OpenAPI·네이버·FinanceDataReader·pykrx) 선택형 요청
  - FinanceDataReader·pykrx는 **Python 전용** → 순수 .NET 4소스로 대체 합의
  - 초기 구성: KIS·네이버·KRX·Yahoo

## 핵심 이슈 — KRX 직접 호출 차단
- `data.krx.co.kr` 통계 bld(MDCSTAT01701/01501) 호출이 **400 + 본문 `LOGOUT`** 반환
- `.NET HttpClient`로도 동일 — 종목검색(finder)은 되지만 통계 데이터는 세션 검증으로 차단
- 2026년 현재 거래소가 비로그인 스크래핑을 차단 (pykrx도 동일 문제)
- **사용자 결정**: KRX 자리에 **다음(Daum) 금융** 추가 (KRX 원천 시세, 무인증)

## 최종 데이터 소스 (4종, 순수 .NET / HttpClient만)
| 소스 | 인증 | 구현 |
|------|------|------|
| 네이버 금융 | 불필요 | `siseJson.naver` 정규식 파싱 |
| 다음 금융 | 불필요 | `finance.daum.net/api/charts` 종료일 페이징 |
| Yahoo Finance | 불필요 | `v8/finance/chart` .KS→.KQ 시도, 종목명·시장 인식 |
| 한국투자증권 KIS | API 키 | OAuth 토큰 캐시 + 100봉 자동 페이징 |

## 구현 구조
- `Models/`: `Candle`(OHLCV record), `StockSeries`(메타+시계열, `SourceKind`)
- `Services/`: `IPriceSource` 인터페이스 + 4개 소스 + `PriceSourceRegistry`(공유 HttpClient+UA)
  - `AppConfig`(KIS 키·최근값, `%LocalAppData%` 저장), `DataExporter`(5포맷), `NativeTheme`
- `MainWindow`: 종목코드·기간 프리셋·소스 선택, DataGrid 미리보기, 요약, 포맷별 저장/복사
- `SettingsWindow`: KIS APP KEY/SECRET·모의서버 토글
- `App.xaml`: 다크 테마(Stock.Watch 기반) + DataGrid ControlTemplate

## 검증
- 빌드: 경고 0 / 오류 0
- 통합 러너(net10.0 콘솔, WPF 제외 링크)로 실제 실행:
  - 삼성전자 최근 1개월 → 네이버·다음·Yahoo **3소스 모두 21건, 종가 339500 동일** (정합성 확인)
  - Yahoo는 종목명 'Samsung Electronics Co., Ltd.' / 시장 'KOSPI' 자동 인식
  - 5개 포맷 전부 정상 직렬화
- XML 선언 인코딩 결함 수정: UTF-16 → MemoryStream+UTF8로 `encoding="utf-8"` 일치

## 등록 / 마무리
- `Resources/app.ico` 멀티사이즈(16~256) 생성 — 상승 캔들스틱 + 다운로드 화살표
- `Playground.slnx` Finance 폴더 등록
- `+publish.cmd`(109번) / `+publish-all.cmd` 등록
- `ideas/avoid/apps.md` 추가
- README 작성

## 실행 검증 후 수정 (사용자 VS 실행 피드백)
서비스 레이어는 통합 러너로 검증했으나 XAML 런타임 로딩은 미검증 → 실행 시 5건 발견·수정:
1. **XamlParseException** — `DataGridTextColumn.ElementStyle="{StaticResource NumCell}"`가
   `DataGrid.Resources`를 참조. DataGridColumn은 비주얼 트리 밖이라 해결 불가 →
   `NumCell`/`NumCellBold`를 App.xaml(Application.Resources)로 이동.
2. **컬럼 헤더 흰색** — 암시적 `DataGridColumnHeader` 스타일이 DataGrid 내부 템플릿에
   적용 안 됨. `x:Key="DarkColHeader"` 부여 후 DataGrid 스타일 Setter에
   `ColumnHeaderStyle="{DynamicResource DarkColHeader}"`로 연결했으나 **Setter.Value의
   DynamicResource가 해결되지 않아 미동작** → 최종적으로 `<DataGrid>` 엘리먼트에
   `ColumnHeaderStyle="{StaticResource DarkColHeader}"`를 **직접 명시**해 해결.
   (실행 캡처 + 헤더 픽셀 RGB 샘플링(37,37,53=PanelBg)으로 객관 검증)
3. **소스 콤보 타입명 표시** — 커스텀 ComboBox 템플릿과 `DisplayMemberPath` 충돌
   (Stock.Watch와 동일 증상) → 4개 PriceSource에 `ToString() => DisplayName` 오버라이드,
   `DisplayMemberPath` 제거.
4. **창 외곽 테두리 흰색** — 타이틀바만 다크였고 Win11 창 테두리는 시스템 기본(밝은 색)
   → `NativeTheme`에 `DWMWA_BORDER_COLOR`(34)로 `#1A1A1A` 지정. 테두리 픽셀 RGB(12,12,12)로 확인.
5. **패널(Border) 사이 흰색 가로줄**(사용자가 실제로 불편해한 지점) — Border 사이 12px 간격으로
   Window 기본 배경이 드러남. `Window` 암시적 스타일(`Background=WindowBg`)이 환경/타이밍에 따라
   미적용되면 배경이 시스템 흰색이 됨 → MainWindow·SettingsWindow의 `Window`와 최상위 `Grid`에
   `Background="{StaticResource WindowBg}"` **직접 명시**해 환경 무관하게 다크 고정.
- 자동 캡처는 SetForegroundWindow 포그라운드 잠금으로 불안정 → 코드 레벨 결정론적 수정으로 처리.

## 참고
- KRX 차단은 거래소 정책 변화로, 향후 로그인/OTP 기반 우회가 필요. 현재는 다음 금융이 동등 대체.
- XAML은 컴파일 통과해도 런타임 로딩에서 깨질 수 있음 → 신규 WPF 창은 exe 스모크 실행 필수.
