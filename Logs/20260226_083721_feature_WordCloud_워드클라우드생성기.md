# Word.Cloud 워드클라우드 생성기 구현

**날짜**: 2026-02-26
**태그**: feature
**커밋 수**: 7

---

## 작업 내용

오프라인 워드클라우드 생성기 `Word.Cloud` WPF 앱 구현.

### 생성 파일

| 파일 | 설명 |
|------|------|
| `Word.Cloud.csproj` | net10.0-windows, Sdcb.WordCloud 2.0.1, SkiaSharp 3.119.0 |
| `gen-icon.ps1` | 구름+W 심볼 Cyan 아이콘 (16/32/48/256px) |
| `App.xaml` | Cyan (#06B6D4) 다크 테마 |
| `GlobalUsings.cs` | WPF+WinForms 모호성 해결 (`SkiaSharp.Views.Desktop` 포함) |
| `MainWindow.xaml` | 960×680 2패널 레이아웃 |
| `MainWindow.xaml.cs` | 전체 이벤트 처리, IsLoaded 가드, DwmSetWindowAttribute |
| `Models/CloudShape.cs` | Rectangle/Circle/Heart/Star/Diamond/Cloud/Random |
| `Models/TextOrientation.cs` | Horizontal/Vertical/Mixed/Random |
| `Models/CloudConfig.cs` | MaxWords, MinFreq, Shape, Orientation, FontName, ThemeIndex, BgColor |
| `Services/TextAnalysisService.cs` | 공백 분리, 한국어 불용어 35개 + 영어 30개 + 사용자 정의 |
| `Services/MaskService.cs` | SkiaSharp으로 6가지 모양 SKBitmap 마스크 생성 |
| `Services/ColorTheme.cs` | 8종 팔레트 (Viridis/Ocean/Sunset/Forest/Pastel/Mono/Rainbow/Random) |
| `Services/CloudGeneratorService.cs` | Sdcb.WordCloud.WordCloud.Create() API 사용 |
| `Services/ExportService.cs` | PNG/JPEG 저장, 클립보드 복사 |

### 수정 파일

| 파일 | 변경 내용 |
|------|----------|
| `Playground.slnx` | Word.Cloud 프로젝트 추가 |
| `+publish.cmd` | 19번 Word.Cloud, Env.Guard 20→, Games 21~31 |
| `+publish-all.cmd` | Productivity 섹션에 Word.Cloud 추가 |

---

## 해결한 이슈

### SkiaSharp.Views.WPF - SKPaintSurfaceEventArgs 미발견
- `SKPaintSurfaceEventArgs`는 `SkiaSharp.Views.Desktop` 네임스페이스에 있음
- `GlobalUsings.cs`에 `global using SkiaSharp.Views.Desktop;` 추가로 해결

### WPF + WinForms 모호성
- `UseWindowsForms + ImplicitUsings`로 `System.Windows.Forms` 전역 import됨
- `SaveFileDialog`, `OpenFileDialog` → `Microsoft.Win32.*` 완전 한정명 사용
- `Color` → `using WpfColor = System.Windows.Media.Color;` 별칭 사용
- `Cursors` → `using WpfCursors = System.Windows.Input.Cursors;` 별칭 사용

---

## 빌드 결과

```
dotnet build Applications/Tools/Productivity/Word.Cloud/Word.Cloud.csproj -c Release
→ 경고 0개, 오류 0개
```

---

## 커밋 목록

1. `22611eb` — [word.cloud] | 프로젝트 초기화
2. `62ac9dc` — [word.cloud] | 다크 테마 + 기본 레이아웃
3. `b7b27a3` — [word.cloud] | 텍스트 분석 + 모델
4. `ed19964` — [word.cloud] | 워드클라우드 생성 엔진
5. `ba08f6e` — [word.cloud] | 내보내기 서비스
6. `ac6647f` — [word.cloud] | 메인 윈도우 로직
7. `971ad0a` — [word.cloud] | 배포 스크립트 등록
