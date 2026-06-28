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

## 참고
- KRX 통계는 차단(LOGOUT)이나 종목검색 finder는 무인증 동작 → 종목명 조회에 활용.
- KRX 차단은 거래소 정책 변화로, 향후 로그인/OTP 기반 우회가 필요. 현재는 다음 금융이 동등 대체.
- XAML은 컴파일 통과해도 런타임 로딩에서 깨질 수 있음 → 신규 WPF 창은 exe 스모크 실행 필수.
- 자동 캡처는 SetForegroundWindow 포그라운드 잠금으로 불안정 → `SetWindowPos` HWND_TOPMOST로 우회.
