# QR.Forge — QR 코드 생성기 구현

- **날짜**: 2026-02-25
- **태그**: feature
- **상태**: 완료

## 개요

오프라인 QR 코드 생성기 `QR.Forge`를 구현했다.
카페/식당(메뉴 QR), 행사 기획자, 물류 라벨 담당자를 타겟으로 한다.

## 구현 내용

### 파일 구조

```
Applications/Tools/Productivity/QR.Forge/
├── QR.Forge.csproj               — net10.0-windows, WPF, WinForms, SkiaSharp, ZXing.Net
├── App.xaml / App.xaml.cs        — 다크 테마 (보라 강조 #7C5FE8)
├── MainWindow.xaml / .cs         — 메인 UI (850×600)
├── GlobalUsings.cs               — WPF+WinForms 타입 모호성 해결
├── Models/
│   ├── QrInputType.cs            — URL/Text/VCard/WiFi enum
│   ├── QrStyle.cs                — 색상·마커·로고·에러보정
│   ├── VCardData.cs              — vCard 3.0 포맷 생성
│   └── WiFiData.cs               — WIFI: 포맷 생성
├── Services/
│   ├── QrService.cs              — ZXing→SkiaSharp 렌더링 (사각/라운드/도트 마커)
│   ├── ExportService.cs          — PNG/SVG/PDF 내보내기
│   └── BatchService.cs           — CSV 배치 생성
├── Windows/
│   └── BatchWindow.xaml/.cs      — 배치 생성 전용 창 (ProgressBar)
├── Resources/app.ico             — 16/32/48/256px 멀티사이즈 ICO (보라 QR 패턴)
└── gen-icon.ps1                  — 아이콘 생성 스크립트
```

### NuGet 패키지

| 패키지 | 버전 | 용도 |
|--------|------|------|
| ZXing.Net | 0.16.9 | QR 비트매트릭스 생성 |
| SkiaSharp | 3.119.0 | 색상 렌더링·로고 합성·PNG |
| SkiaSharp.Views.WPF | 3.119.0 | WPF SKElement 미리보기 |
| PdfSharpCore | 1.3.65 | PDF A4 격자 내보내기 |

### 주요 기능

- **탭 입력**: URL / 텍스트 / 연락처(vCard) / WiFi 4가지 모드
- **실시간 미리보기**: SKElement에 즉시 렌더링
- **스타일 커스텀**: 전경색·배경색·마커(사각/라운드/도트)·에러보정·로고 삽입
- **내보내기**: PNG·SVG·PDF·클립보드 복사
- **배치 생성**: CSV `name,content` 포맷으로 일괄 PNG 생성

### 수정 파일

| 파일 | 변경 |
|------|------|
| `Playground.slnx` | QR.Forge 프로젝트 항목 추가 |
| `+publish.cmd` | 메뉴 번호 16 추가, 기존 17~29로 renumber |
| `+publish-all.cmd` | Productivity 섹션에 QR.Forge 추가 |

## 빌드 결과

```
경고 20개 (패키지 호환성 경고 — 기능 정상)
오류 0개
```

## 수정 이력

- `GlobalUsings.cs`: WPF+WinForms 모호성 해결 (`Application`, `Window`, `MessageBox` 등)
- `using System.IO;`: ImplicitUsings가 net10-windows WinForms 조합에서 IO를 포함 안 함 → 명시적 추가
- `SkiaSharp.Views.Desktop` using 추가: `SKPaintSurfaceEventArgs` 해결
