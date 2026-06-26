# Stock.Watch — WebSocket 실시간 체결가 추가 · 다크 테마/콤보 표시 수정

- **일시**: 2026-06-26 20:13 (KST)
- **태그**: feature
- **컴포넌트**: stock.watch
- **버전**: v1.0.0 → v1.1.0

## 요청
1. WebSocket 실시간 체결가 추가
2. 설정 창 등 흰색 부분·텍스트 안 보이는 문제 다크 테마로 수정 (스크린샷 첨부)
3. 매수 조건 콤보박스 content가 이상하게 표시됨

## 1. WebSocket 실시간 체결가
- **`KisRealtimeClient`** 신규: `ClientWebSocket`으로 KIS 실시간(`H0STCNT0` 체결가) 구독.
  - approval_key 발급(`KisApiClient.GetApprovalKeyAsync` — `/oauth2/Approval`, `secretkey` 필드, 12h 캐시)
  - 구독/해지 JSON(`tr_type` 1/2), `content-type` 헤더 키 처리
  - 수신 파싱: `flag|tr_id|count|body(^)` → 마지막 체결 레코드(종목코드·현재가·전일대비율·누적거래량·체결시각)
  - PINGPONG echo 응답, 끊김 시 지수 백오프 자동 재연결
  - WS URL: 실전 `ws://ops.koreainvestment.com:21000` / 모의 `:31000`
- **`MonitorService`** 통합: 폴링(일봉·지표 기준선) + 실시간(틱) 병행.
  - `_baseCandles`에 폴링 일봉 보관 → 틱 수신 시 당일 봉에 실시간 가격 병합 → 지표 재계산·조건 재평가
  - 틱 평가는 종목당 0.8초 스로틀(가격 표시는 즉시)
  - 감시 중 종목 추가/삭제 시 실시간 구독 즉시 반영(`AddRealtimeCode`/`RemoveRealtimeCode`)
- **설정**: `UseRealtime`(기본 true) + approval_key 캐시 필드, 설정창 체크박스 추가
- **MainWindow**: 종료 시 `DisposeRealtime`, 종목 추가/삭제 시 구독 연동

## 2. 다크 테마 수정 (흰색 부분)
- **원인**: `Window` implicit 스타일을 `TargetType="{x:Type Window}"`로 줬으나 **WPF implicit 스타일은 파생 클래스(SettingsWindow/MainWindow)에 적용되지 않음** → SettingsWindow 클라이언트 배경이 기본 흰색, 밝은 회색 설명 텍스트 가독성 저하.
- **수정**: SettingsWindow·MainWindow에 명시적 `Background="#FF1A1A1A"` + 루트 Grid 배경 지정, FontFamily 명시.
- **검증**: UI 자동화로 설정 창 캡처 → 배경·입력박스 모두 다크, 텍스트 선명 확인.

## 3. 콤보박스 content 수정
- **원인**: 닫힌 콤보(SelectionBox)가 `Opt` 레코드의 기본 `ToString()`("Opt { Value = ... }")을 표시.
- **수정**: `Opt` 레코드에 `public override string ToString() => Label;` 추가.
- **검증**: 메인 창 캡처 → `RSI`/`현재가`/`상수값`/`지표 × 배수`/`볼린저하단` 등 한글 라벨 정상 표시 확인.

## 빌드/검증
- `dotnet build Stock.Watch.csproj -c Release` → 경고 0, 오류 0
- 앱 기동 정상, 설정 창·조건 콤보 UI 자동화 캡처로 육안 확인

## 후속
- 실시간 체결 동작은 KIS 키 입력 + 장중에 실측 필요(코드는 KIS 스펙 기준 구현)
