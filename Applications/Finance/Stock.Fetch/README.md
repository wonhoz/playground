# Stock.Fetch — 한국 주식 시세 내보내기

한국 장 종목의 일별 시세(저가·고가·종가·시가·거래량)를 **종목코드 + 기간**만 입력하면
**CSV·TSV·JSON·XML·Markdown** 등 원하는 포맷으로 손쉽게 내보내는 WPF 데스크톱 앱.

> 일별 OHLC는 모든 국내 증권사·서비스가 **한국거래소(KRX) 공식 시세**를 원천으로 쓰므로,
> 어느 소스를 골라도 종가/고가/저가 숫자는 카카오페이·네이버 화면값과 사실상 일치한다.

---

## 주요 기능

- **종목코드 입력** (6자리, 예: `005930` 삼성전자 / `000660` SK하이닉스)
- **기간 선택**: 직접 입력(yyyy-MM-dd) 또는 빠른 프리셋(1·3·6개월 / 1·3년 / 올해)
- **4개 데이터 소스 선택형** (아래 표 참조)
- **미리보기 그리드**: 조회 즉시 표로 확인, 기간 고가/저가 요약
- **컬럼 선택**: 날짜·시가·종가·저가·고가·거래량 중 원하는 컬럼만 체크해 내보내기/복사
- **모든 포맷 내보내기**: CSV / TSV / JSON / XML / Markdown
  - 파일로 저장 (UTF-8 BOM — 엑셀 한글 호환) 또는 클립보드 복사

---

## 데이터 소스

| 소스 | 인증 | 특징 |
|------|------|------|
| **네이버 금융** | 불필요 | 가장 빠름. KRX 원천 시세. |
| **다음(Daum) 금융** | 불필요 | KRX 원천 시세. 종료일 기준 페이징. |
| **Yahoo Finance** | 불필요 | 글로벌. 종목명·시장(KOSPI/KOSDAQ) 자동 인식, KST 거래일 보정. |
| **한국투자증권 OpenAPI (KIS)** | **API 키 필요** | 증권사 공식 API. 100봉 제한 자동 페이징. |

> **참고** — 한국거래소(KRX) 정보데이터시스템 직접 호출은 2026년 현재 거래소가
> 비로그인 통계 조회를 세션 검증(`LOGOUT`)으로 차단해 제외했다. 대신 **다음 금융**이
> 동일한 KRX 원천 시세를 무인증으로 제공한다.

### KIS 키 설정 (선택)
KIS 소스를 쓰려면 [KIS Developers](https://apiportal.koreainvestment.com)에서 발급한
`APP KEY` / `APP SECRET`을 `⚙ KIS 키 설정`에서 입력한다.
키는 이 PC의 `%LocalAppData%\Playground\Stock.Fetch\config.json`에만 저장되며 저장소에 포함되지 않는다.

---

## 출력 예시

**CSV**
```
date,open,high,low,close,volume
2026-05-28,305000,306500,287500,299500,30035561
2026-05-29,309500,319000,305500,317000,32795059
```

**JSON**
```json
{
  "code": "005930",
  "name": "Samsung Electronics Co., Ltd.",
  "market": "KOSPI",
  "source": "Yahoo Finance",
  "count": 21,
  "candles": [
    { "date": "2026-05-28", "open": 305000, "high": 306500, "low": 287500, "close": 299500, "volume": 30035561 }
  ]
}
```

---

## 빌드 / 배포

```bash
dotnet build Applications/Finance/Stock.Fetch/Stock.Fetch.csproj -c Release
dotnet publish Applications/Finance/Stock.Fetch/Stock.Fetch.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

- **플랫폼**: .NET 10.0 (net10.0-windows) / WPF / C# 13
- **외부 NuGet 의존 없음** — `HttpClient`만 사용
- 다크 테마 + DWM 다크 타이틀바

---

## 버전

- **v1.1.0** (2026-06-28) — 컬럼 순서 변경(날짜-시가-종가-저가-고가-거래량), 컬럼 선택
  내보내기/복사 기능 추가(체크박스 + 전체/해제), JSON 정수 가격 trailing zero 제거.
- **v1.0.0** (2026-06-28) — 신규. 4개 소스(네이버·다음·Yahoo·KIS) 선택형 일별 시세 조회,
  CSV/TSV/JSON/XML/Markdown 내보내기, 미리보기 그리드, 기간 프리셋, 다크 테마.
