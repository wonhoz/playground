# 20260225_052647 | feature | Api.Probe + Mock.Desk 신규 앱 개발

## Api.Probe — 미니멀 오프라인 API 테스터

**경로**: `Applications/Tools/Api.Probe/`
**커밋**: `1bcd031`

### 기능
| 기능 | 구현 방법 |
|------|-----------|
| REST 요청 전송 | `HttpClient` → GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS |
| 컬렉션 관리 | `CollectionService` — JSON 영속 (`%AppData%\ApiProbe\collections.json`) |
| 환경변수 프리셋 | `{{BASE_URL}}` 문법 → dev/stage/prod URL 자동 치환 |
| 응답 표시 | `TryPrettyJson` — JSON 자동 들여쓰기, 헤더 탭 분리 |
| 상태 배지 | 2xx(초록) / 3xx(노랑) / 4xx(빨강) / 5xx(다크레드) 색상 구분 |
| cURL 복사 | `CurlConverter` — 헤더/바디 포함 cURL 명령어 생성 |
| 사이드바 | 메서드별 색상 태그 + 요청 이름 클릭 로드 |

### 파일 구조
```
Api.Probe.csproj
App.xaml                (전역 다크 테마, teal #14B8A6 액센트)
App.xaml.cs
MainWindow.xaml         (좌측 사이드바 + 우측 요청/응답 분할)
MainWindow.xaml.cs
Models/
  ApiRequest.cs         (Method, Url, Headers, Body, ContentType)
  ApiCollection.cs      (Name + ObservableCollection<ApiRequest>)
  HeaderItem.cs         (Enabled, Key, Value)
  EnvPreset.cs          (Name, Dictionary<string,string> Variables)
Services/
  HttpService.cs        (SendAsync → HttpResponse record)
  CollectionService.cs  (Load/Save JSON)
  CurlConverter.cs      (Convert → curl 문자열)
```

### 빌드 이슈 해결
- `ItemsControl`은 `Children` 없음 → 사이드바를 `StackPanel`로 변경

---

## Mock.Desk — 로컬 Mock HTTP 서버

**경로**: `Applications/Tools/Mock.Desk/`
**커밋**: `96e0553`

### 기능
| 기능 | 구현 방법 |
|------|-----------|
| Mock 서버 시작/중지 | `WebApplication.CreateBuilder()` + `app.Run()` 터미널 미들웨어 |
| 엔드포인트 정의 | 메서드 + 경로 + 상태코드 + 응답JSON + 지연ms |
| 경로 매칭 | 정확일치 + 와일드카드 `*` (접두사 매칭) |
| 실시간 요청 로그 | `Dispatcher.BeginInvoke` → ListBox 역순 삽입 |
| JSON 내보내기/가져오기 | `SaveFileDialog` / `OpenFileDialog` |
| 상태코드 시나리오 | 2xx/4xx/5xx 프리셋 ComboBox |
| 지연 시뮬레이션 | `Task.Delay(ep.DelayMs)` |

### 파일 구조
```
Mock.Desk.csproj        (FrameworkReference: Microsoft.AspNetCore.App)
App.xaml                (전역 다크 테마, amber #F59E0B 액센트)
App.xaml.cs
MainWindow.xaml         (헤더 서버제어 + 좌측 엔드포인트목록 + 우측 에디터/로그)
MainWindow.xaml.cs
Models/
  MockEndpoint.cs       (Id, Enabled, Method, Path, StatusCode, ResponseBody, DelayMs)
  RequestLogEntry.cs    (Timestamp, Method, Path, StatusCode, Matched, DelayMs)
Services/
  MockServerService.cs  (StartAsync/StopAsync, catch-all middleware, Export/Import JSON)
```

### 빌드 이슈 해결
1. `app.Use(async (context, next) => ...)` → `Use` 오버로드 모호성 오류
   - 해결: `app.Run(async context => ...)` — 터미널 미들웨어로 대체 (catch-all이므로 next 불필요)

---

## 메모
- ASP.NET Core 내장 서버: csproj에 `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` 필수 (NuGet 패키지 아님)
- `builder.Logging.ClearProviders()` — WPF 앱에서 ASP.NET Core 콘솔 로그 억제
- `app.Run()` vs `app.Use()`: catch-all 터미널 미들웨어에는 `app.Run()`이 타입 모호성 없이 안전
- 환경변수 치환: `{{BASE_URL}}` 문법 (Postman 스타일)
