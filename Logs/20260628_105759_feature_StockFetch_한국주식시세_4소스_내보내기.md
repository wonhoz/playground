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

## v1.1.0 후속 (사용자 요청)
- **컬럼 순서 변경**: 날짜-시가-고가-저가-종가-거래량 → **날짜-시가-종가-저가-고가-거래량**
  (DataGrid 표시 + 모든 내보내기 포맷 동일 순서, 종가는 굵게 유지).
- **컬럼 선택 내보내기/복사**: 내보내기 바에 컬럼 체크박스 6종 + 전체/해제 버튼.
  선택 컬럼만 CSV/TSV/JSON/XML/Markdown 복사·저장에 반영(`DataExporter`를 `ColumnSpec` 기반 리팩토링).
- **JSON 정수 가격 정리**: 다음 금융 decimal(343000.000) → 정수는 trailing zero 제거(343000).
- 헤드리스 러너로 순서·선택(날짜+종가, 날짜+종가+거래량, 시가+종가 등) 전수 검증.

## v1.2.0 후속 (사용자 요청)
- **즐겨찾기**: `AppConfig.Favorites`(List&lt;FavoriteStock&gt;), 콤보 선택·⭐추가·🗑제거. ToString으로 "코드 이름" 표시.
- **종목명 조회**: `NameResolver`(KRX finder_stkisu 무인증) — 코드→한글명. `🔍 이름 조회` 버튼 + 조회 시 자동.
  - finder는 통계 데이터와 달리 LOGOUT 차단 없이 동작(005930→삼성전자, 035720→카카오 검증).
- **마지막 선택값 복원**: `Last*`(Code·Name·From·To·Source·Format·Columns·IncludeHeader) 저장, 생성자에서 `RestoreState`, 종료 시 `SaveState`.
- **기간 ±1일**: `DayShift_Click` — from·to 동시 ±1일 이동.
- **헤더 포함 여부**: `IncludeHeaderCheck` → `DataExporter.Serialize(..., includeHeader)`. CSV/TSV/Markdown 헤더 행 on/off(JSON/XML은 키 구조라 무지원).
- **레이아웃**: 입력 패널 5행(종목코드·즐겨찾기·기간·프리셋·소스)으로 분리, 기간 프리셋 잘림 방지. 창 1040×780.
- 헤드리스 러너로 이름조회·헤더옵션 검증, TOPMOST 캡처로 신규 레이아웃 육안 확인.

## v1.3.0 후속 (사용자 요청)
- **다음 금융 종료일 누락 버그**: Daum `to`는 candleTime 미만(exclusive)이라 종료일 봉 빠짐
  → `to.AddDays(1)` 00:00:00로 보내 종료일 포함(상한초과는 c.Date>to 필터). 06-22~06-26 → 5건 확인.
- **매수/익절 래더 계산**: `LadderCalculator`(stock-update 스킬 로직 그대로) + `LadderResult`/`SellTarget`
  모델 + `LadderWindow`(새 창). `📊 매수/익절 계산` 버튼(조회 후 활성, 모달리스).
  - 원익IPS 검산: σ_down 5.96, 오프셋 0/−4/−9/−12, 매수가·평단·손절·익절 전부 스킬 시트와 일치.
- **익절 4방식**(사용자 지적: 전일고가 단일 앵커 노이즈): ① 전일고가 추종 ② 평단+8% 고정
  ③ 최근5일 고가중앙값 ④ ATR×2. 각 익절가+예상수익 병기. 스킬 문서(stock-update.md)에도 반영.
- **가격 표시**: DataGrid 가격 컬럼 `StringFormat=N0`(소수점 제거·천단위 콤마).
- **UI 다듬기**: 우측 버튼 폭 120→160(텍스트 잘림), 래더 창 스크롤바 우측 마진.

## v1.4.0 후속 (사용자 요청)
- **종목 이름 검색**: KRX finder가 코드·이름 둘 다 매칭(검증: 삼성전자→2건, 삼성→31건, 하이닉스→1건).
  - `NameResolver.SearchAsync`(block1 전체 파싱) + `StockHit` 모델 + `SearchResultWindow`(선택 다이얼로그).
  - `🔍 검색`: 0건 안내 / 1건 즉시 적용 / 다건 목록 선택(더블클릭). CodeBox MaxLength 제거(이름 입력 허용).
  - LookupAsync/SearchAsync 공통 `FinderBlock1Async`로 정리(JsonElement.Clone 반환).
- **v1.4.1**: 검색결과 ListBox 다크 테마 수정 — 비선택 항목이 밝은 배경+회색 텍스트로 안 보이던 것을
  ListBoxItem 배경 `PanelBg2` 명시 + ItemTemplate 텍스트 `FgBrush` 명시(선택 시 White DataTrigger)로 해결.
  (ContentPresenter가 App 암시적 스타일/시스템색을 받아 어두워지는 문제 → 명시로 강제. TOPMOST 전체캡처로 검증)

## v1.5.0 후속 (사용자 요청 — 차트)
- **지표 포팅**: Stock.Watch `IndicatorMath`(SMA·EMA·RSI·볼린저)·`IndicatorSet`을 Stock.Fetch.Indicators로 포팅(차트 전용 간소화).
- **CandleChart 포팅+확장**: 캔들+볼린저+거래량+RSI에 **이동평균선(MA5/20/60)·지표 토글·x축 시간라벨** 추가, 패널 동적 배분.
- **차트 데이터(`ChartDataService`)**: `BarInterval`(1·5·15·30·60분/일/주/월). Yahoo는 전 interval(chart API interval/range), KIS는 일/주/월(`KisPriceSource.FetchChartAsync`, period D/W/M 파라미터화).
- **ChartWindow**: 봉종류·소스(Yahoo/KIS)·지표토글 콤보/체크, 자동갱신(DispatcherTimer, 주기 선택)+수동 새로고침. 지표 토글은 재조회 없이 Redraw.
- **메인 📈 차트 버튼**: 종목코드 기반으로 차트 창(모달리스) 표시.
- 검증: 헤드리스로 Yahoo 5분(1423봉)·일(243)·주(262)·월(121)봉 + RSI·볼린저·SMA 계산 확인. TOPMOST 전체캡처로 렌더 육안 확인.

## v1.6.0 후속 (사용자 피드백 — 차트 개선)
- **Yahoo 시장 판별 버그**: 코스닥 종목을 .KS로 조회하면 Yahoo가 빈 응답이 아니라 소수 가짜봉(브이엠 5분봉 21개)을
  주어 잘못 채택됨 → **.KS/.KQ 둘 다 조회(Task.WhenAll) 후 봉 수 많은 쪽 채택**. 브이엠 21→1433봉으로 정상화.
- **캔들 너비 상한**: 데이터 적을 때 캔들이 과대해지던 문제 → `slot = min(plotW/visible, 14)`(일봉 크기), 부족분 우측 여백.
- **분봉 x축 라벨**: 여러 날 걸치는 분봉을 `MM-dd HH:mm`로(이전 `HH:mm`이라 날짜 구분 안 됨).
- **마우스 오버 상세정보**: `OverlayCanvas` 크로스헤어(세로 점선) + `InfoBox`(날짜·시고저종·거래량·MA20·BB·RSI). MouseMove에서 인덱스 역산.
- **차트 선택 저장/복원**: `AppConfig`에 Chart*(Interval·Source·지표토글·AutoRefresh·PeriodSec) 추가, ChartWindow 생성 시 복원·Closed 시 저장.

## v1.7.0 후속 (사용자 요청 — KIS 차트 분봉)
- **KIS 당일 분봉**: `inquire-time-itemchartprice`(FHKST03010200, output2: stck_bsop_date+stck_cntg_hour·oprc·hgpr·lwpr·prpr·cntg_vol).
  당일 1분봉을 30건씩 inputHour 페이징(과거로). `ChartDataService.Aggregate`로 5·15·30·60분 집계(O=첫·H=max·L=min·C=끝·V=합).
- **유량 제한 대응**: KIS 실전 "초당 거래건수 초과" → 페이징 사이 160ms throttle + 유량 초과 시 그때까지 수집분 부분반환.
- `KisSupports` 제거(분봉 포함 전 interval 지원), 소스 라벨 "KIS (당일분봉·일/주/월)".
- 검증(실전 키·장중): KIS 1분봉 270봉·5분 집계 55봉 정상. 시각이 PC 테스트시각과 달라 미래처럼 보이나 봉 자체는 정상.

## 참고
- KRX 통계는 차단(LOGOUT)이나 종목검색 finder는 무인증 동작 → 종목명 조회·이름 검색에 활용.
- 차트 분봉: Yahoo(여러 날·15분 지연)·KIS(당일치·실시간성↑). KIS 과거 분봉은 미제공.
- 코스닥/코스피 종목은 Yahoo .KS/.KQ 둘 다 조회해 많은 쪽 채택(시장 자동 판별).
- KRX 차단은 거래소 정책 변화로, 향후 로그인/OTP 기반 우회가 필요. 현재는 다음 금융이 동등 대체.
- XAML은 컴파일 통과해도 런타임 로딩에서 깨질 수 있음 → 신규 WPF 창은 exe 스모크 실행 필수.
- 자동 캡처는 SetForegroundWindow 포그라운드 잠금으로 불안정 → `SetWindowPos` HWND_TOPMOST로 우회.
