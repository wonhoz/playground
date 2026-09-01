# Link.Scan 신규 개발 — 페이지 링크 추출 콘솔 도구

- **일시**: 2026-08-08 21:44 (KST)
- **태그**: feature
- **컴포넌트**: `link.scan`
- **버전**: v1.0.0
- **경로**: `Applications/Tools.Utility/Link.Scan`

---

## 1. 배경

지정한 주소의 페이지를 1회 요청해서, 그 안에 링크된 하위 페이지 주소를 추출·출력하는 콘솔 도구가 필요했다.
재귀 크롤링이 아닌 **단일 페이지 1-depth 추출**로 범위를 한정했다.

## 2. 구성

| 파일 | 역할 |
|------|------|
| `Link.Scan.csproj` | net10.0 / Exe / 단일 파일 self-contained publish |
| `Program.cs` | 진입점, 렌더링, 파일 저장, 오류 처리 |
| `Options.cs` | 커맨드라인 옵션 파싱, 주소 정규화 |
| `LinkExtractor.cs` | HTTP 요청, 인코딩 판정, href 추출, 범위 필터 |
| `Ansi.cs` | ANSI 색상 상수 + Windows VT 모드 활성화 |

## 3. 주요 구현

### 3.1 링크 추출
- `<a>` / `<area>` 여는 태그를 정규식으로 훑고, 태그 내부에서 `href` 를 추출 (큰따옴표·작은따옴표·따옴표 없음 3가지 모두 대응)
- 링크 텍스트는 여는 태그 뒤 ~ 첫 `</a>` 구간에서 태그를 제거해 추출. 닫는 태그가 누락된 경우 다음 `<a>` 를 만나면 빈 문자열 처리
- `WebUtility.HtmlDecode` 로 엔티티 복원

### 3.2 주소 정규화
- `<base href>` 존재 시 상대 경로 해석 기준을 그쪽으로 이동
- 리다이렉트를 따라간 경우 **최종 주소**를 기준으로 상대 경로 해석
- 프래그먼트(`#...`) 제거 후 중복 판정 — 같은 문서 내 위치는 별개 페이지가 아님
- `javascript:` `mailto:` `tel:` `data:` `about:` `file:` `sms:` 스킴 제외

### 3.3 추출 범위 (`--scope`)
| 값 | 동작 |
|------|------|
| `sub` (기본) | 대상 주소의 디렉토리 하위 경로만 |
| `host` | 같은 호스트의 모든 경로 |
| `all` | 외부 도메인 포함 전부 |

### 3.4 인코딩 판정
`Content-Type` charset → UTF-8 BOM → `meta charset` 순으로 판정.
`CodePagesEncodingProvider` 등록으로 EUC-KR 등 레거시 한글 페이지 대응.

> .NET 10 에서 `System.Text.Encoding.CodePages` 패키지는 프레임워크에 포함되어 있어
> `PackageReference` 추가 시 NU1510 경고 발생 → 패키지 참조 제거

### 3.5 콘솔 색상
- 출력이 리다이렉트되면(`> file`, 파이프) 색상 상수가 전부 빈 문자열 → 파일에 제어 문자 미포함
- `NO_COLOR` 환경변수 지원
- Windows 는 `SetConsoleMode` 로 `ENABLE_VIRTUAL_TERMINAL_PROCESSING` 활성화

## 4. 시행착오

| 문제 | 원인 | 해결 |
|------|------|------|
| `error SYSLIB1062` / `CS0227` | `LibraryImport` 소스 생성기가 unsafe 코드를 요구 | `DllImport` 로 교체 (`AllowUnsafeBlocks` 불필요) |
| `NU1510` 경고 | .NET 10 에 CodePages 가 내장됨 | `PackageReference` 제거 |
| SSL 오류 원인 불명 | `HttpRequestException` 겉면 메시지만 출력, 실제 원인은 inner exception 에 있음 | inner exception 체인 전체 출력 + `AuthenticationException` / `SocketException` 유형별 힌트 추가 |

## 5. 검증

| 항목 | 결과 |
|------|------|
| `--help` 한글 출력 | 정상 |
| `example.com --scope all` | 1개 추출, exit 0 |
| `docs.python.org/3/ --max 8 --out` | 8개 추출 + 파일 저장 정상 (전체 href 94개 → 중복 제거 55개 → sub 범위 필터) |
| 404 응답 | `요청 실패 (HTTP 404 NotFound)`, exit 1 |
| 잘못된 `--scope` 값 | 오류 메시지, exit 1 |
| 잘못된 주소 형식 | 오류 메시지, exit 1 |
| 링크 0개 (`example.com` sub 범위) | 안내 + 범위 확대 제안, exit 2 |
| `dotnet build Link.Scan.csproj -c Release` | 경고 0 / 오류 0 |
| `dotnet build Playground.slnx -c Release` | Link.Scan 정상 빌드. 오류 2건은 기존 `Claude.Shell.Native` C++ vcxproj (dotnet CLI 에서 `VCTargetsPath` 미해석) — 본 작업과 무관 |

### 종료 코드
`0` 추출 성공 · `1` 오류 · `2` 링크 없음 · `130` 사용자 취소

## 6. 등록

- `Playground.slnx` — `Applications/Tools.Utility/` 폴더에 추가
- `+publish.cmd` — 번호 `110`
- `+publish-all.cmd` — Tools.Utility 섹션
- `+publish-selector.ps1` — `N=110`

## 7. 남은 작업

- `Resources/app.ico` 미생성 — 콘솔 앱이라 필수는 아니나 `/generate-icons` 로 후속 생성 가능
- README 버전 섹션 반영

## 8. 미해결 이슈 — 특정 호스트 TLS 연결 리셋

사용자 환경에서 특정 대상 주소에 대해 다음 오류 발생:

```
요청 실패: The SSL connection could not be established, see inner exception.
  └ IOException: Unable to read data from the transport connection: 현재 연결은 원격 호스트에 의해 강제로 끊겼습니다.
    └ SocketException: 현재 연결은 원격 호스트에 의해 강제로 끊겼습니다. (ConnectionReset)
```

TLS 핸드셰이크 도중 RST 수신. 인증서 검증 실패(`AuthenticationException`)가 **아니므로**
인증서 신뢰 문제나 TLS 버전 협상 실패가 아니라, **연결 자체가 외부에서 끊긴 것**이다.

가능한 원인:
1. 경로상 필터링 장비가 ClientHello 의 SNI 를 보고 RST 주입 (국내 ISP SNI 차단 방식과 동일한 증상)
2. 서버 측 봇 차단이 TLS 지문(JA3) 기준으로 핸드셰이크 단계에서 거부
3. 방화벽·보안 솔루션의 아웃바운드 차단

도구 자체 결함은 아님 — `example.com`, `docs.python.org` 등 다른 호스트에서는 정상 동작 확인됨.
