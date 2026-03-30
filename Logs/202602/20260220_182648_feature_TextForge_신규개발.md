# Text.Forge 신규 개발

- **날짜**: 2026-02-20
- **태그**: feature
- **상태**: 완료

## 개요

`idea_20260220_135615.md`의 아이디어 #9 "Text.Forge"를 구현했습니다.
개발자용 텍스트/데이터 만능 변환 도구 — 오프라인 동작, 다크 테마, 탭 기반 WPF 앱.

## 구현 내용

### 프로젝트 구조

```
Tools/Text.Forge/
├── Text.Forge.csproj          (.NET 10.0, WPF)
├── App.xaml / App.xaml.cs
├── GlobalUsings.cs            (WPF+WinForms 타입 모호성 해결)
├── MainWindow.xaml/.cs        (좌측 사이드바 네비게이션 + 컨텐츠 영역)
├── Styles/
│   └── CommonStyles.xaml      (공통 다크 테마 스타일)
├── Resources/
│   └── app.ico                ({ } 오렌지 심볼 아이콘)
└── Views/
    ├── JsonXmlView             (JSON/XML 포맷팅/압축/검증 + XML→JSON)
    ├── Base64View              (Base64 인코딩/디코딩)
    ├── UrlEncoderView          (URL 인코딩/디코딩, RFC 3986)
    ├── HashView                (MD5/SHA-1/SHA-256/SHA-512, 실시간)
    ├── JwtView                 (JWT 디코딩, exp/iat 시간 표시)
    ├── RegexView               (실시간 매칭 하이라이트, 플래그 지원)
    ├── TimestampView           (Unix↔DateTime, 실시간 시계, KST 지원)
    └── CaseView                (UPPER/lower/Title/camel/snake/Pascal/kebab)
```

### 구현된 기능 (8개 탭)

| # | 탭명 | 주요 기능 |
|---|------|-----------|
| 1 | JSON/XML | Pretty Print, Minify, Validate, XML→JSON |
| 2 | Base64 | Encode/Decode (UTF-8), Swap |
| 3 | URL Encode | Encode/Decode (RFC 3986), Swap |
| 4 | Hash | MD5/SHA-1/SHA-256/SHA-512, **실시간 계산** |
| 5 | JWT | Header/Payload/Signature 분리, exp/iat 시간 표시 |
| 6 | Regex | 실시간 하이라이트, i/m/s 플래그, 매치 목록 |
| 7 | Timestamp | Unix↔DateTime, 실시간 KST 시계, 상대 시간 |
| 8 | Case Convert | 7가지 케이스 변환, 여러 줄 지원 |

### 기술 특이사항

- **외부 의존성 없음**: System.Text.Json, System.Security.Cryptography 등 .NET 내장 라이브러리만 사용
- **WPF + WinForms 혼용**: GlobalUsings.cs에서 모든 타입 모호성 해결
  - `UserControl`, `Color`, `Clipboard`, `MessageBox` 등
- **다크 타이틀바**: `DwmSetWindowAttribute(handle, 20, ...)` 적용
- **Regex 하이라이트**: RichTextBox + Inline Run 방식으로 실시간 Orange 하이라이트

## 빌드 결과

```
빌드했습니다.
  경고 0개 / 오류 0개
```

## 수정된 파일 (기존)

- `Playground.sln` — Text.Forge 프로젝트 추가 (GUID: E5F6A7B8...)
- `Playground.slnx` — Tools 폴더 내 Text.Forge 추가
- `+publish-all.cmd` — Text.Forge 배포 항목 추가

## 빌드 과정에서 발생한 이슈

1. `Text="{ }"` → `Text="{}{ }"` (XAML 마크업 확장 해석 문제)
2. `LetterSpacing` 속성 제거 (WPF 미지원)
3. `PlaceholderText` 속성 제거 (WPF TextBox 미지원)
4. `UserControl`, `Color` 모호성 → GlobalUsings.cs에 global using 추가
