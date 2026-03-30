# Serve.Cast — 폴더 즉시 HTTP 서버 신규 개발

- **날짜**: 2026-03-03
- **태그**: feature
- **경로**: `Applications/Tools/Dev/Serve.Cast`

---

## 개요

로컬 폴더를 한 번의 클릭으로 HTTP/HTTPS 서버로 서빙하는 개발자용 GUI 앱 신규 구현.
프론트엔드 개발, 파일 공유, SPA 테스트 등에 즉시 활용 가능한 경량 로컬 서버.

---

## 구현된 기능

| 기능 | 설명 |
|------|------|
| 폴더 선택 → 서버 시작 | 폴더 선택 후 ▶ 버튼으로 즉시 서빙 |
| HTTP / HTTPS 지원 | 자체 서명 인증서 자동 생성 (`%APPDATA%\ServeCast\server.pfx`) |
| 실시간 요청 로그 | 메서드·경로·상태코드·응답시간·크기 컬럼, 상태별 색상 코딩 |
| CORS 설정 | Origins 지정 (쉼표 구분, * = 전체 허용) |
| Basic 인증 | 사용자명/비밀번호 보호 |
| SPA 모드 | 404 → index.html 폴백 (React/Vue 지원) |
| 디렉터리 목록 | ASP.NET Core DirectoryBrowser 내장 |
| QR 코드 | LAN IP URL QR 코드 (QRCoder) |
| 시스템 트레이 | 최소화 → 트레이, 더블클릭/메뉴로 복원 |
| 자동 스크롤 | 로그 자동 스크롤 토글 |

---

## 기술 스택

- **프레임워크**: .NET 10 WPF + WinForms (NotifyIcon)
- **서버**: ASP.NET Core Kestrel (`FrameworkReference Include="Microsoft.AspNetCore.App"`)
- **QR**: QRCoder 1.6.0
- **인증서**: `System.Security.Cryptography.X509Certificates.X509CertificateLoader`
- **DB 경로**: 없음 (스테이트리스)
- **인증서 경로**: `%APPDATA%\ServeCast\server.pfx`

---

## 파일 구조

```
Applications/Tools/Dev/Serve.Cast/
├── Serve.Cast.csproj
├── App.xaml / App.xaml.cs          ← 다크 테마 + 트레이 아이콘
├── GlobalUsings.cs
├── MainWindow.xaml / .cs           ← 2패널 레이아웃 (설정 + 로그)
├── gen-icon.ps1                    ← 앱 아이콘 생성 스크립트
├── Resources/app.ico               ← 폴더 + ⚡ 아이콘
├── Models/
│   ├── RequestLog.cs               ← 요청 로그 데이터
│   └── ServerConfig.cs             ← 서버 설정 모델
├── Services/
│   ├── ServerService.cs            ← IHost + Kestrel + 미들웨어 파이프라인
│   ├── CertService.cs              ← 자체 서명 X.509 인증서
│   └── QrService.cs                ← QR 코드 BitmapImage 생성
└── Views/
    └── DarkMenuRenderer.cs         ← 트레이 메뉴 다크 렌더러
```

---

## 빌드 결과

```
dotnet build Applications/Tools/Dev/Serve.Cast/Serve.Cast.csproj -c Debug
→ 경고 0개, 오류 0개 ✅
```

---

## 특이 사항

- `LetterSpacing` 속성: WPF TextBlock에 없음 → 제거
- `GlobalUsings.cs`의 `Color = System.Windows.Media.Color` alias → `DarkMenuRenderer.cs`(`System.Drawing.Color` 사용)와 충돌 → alias 제거
- `QRCoder.BitmapByteQRCode.GetGraphic` named parameter `darkColorRgba` 없음 → HTML hex string 오버로드 사용
- `builder.WebHost.UseKestrel` / `ConfigureKestrel` 미지원: `ConfigureWebHostBuilder` 타입이 해당 확장 미포함 → `Host.CreateDefaultBuilder` + `ConfigureWebHostDefaults` (진짜 `IWebHostBuilder`) 방식으로 변경
- `X509Certificate2(string/byte[])` 생성자 폐기(SYSLIB0057) → `X509CertificateLoader.LoadPkcs12FromFile/LoadPkcs12` 사용

---

## 등록 현황

- `Playground.slnx` 등록 완료
- `+publish.cmd` 항목 40번 추가
- `+publish-all.cmd` 항목 추가
