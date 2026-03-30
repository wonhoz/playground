# SVG.Forge — 오프라인 SVG 벡터 에디터 신규 개발

**일시**: 2026-03-12 20:22 KST
**태그**: feature
**프로젝트**: Applications/Photo.Picture/SVG.Forge

---

## 작업 내용

### 생성된 파일

| 파일 | 설명 |
|------|------|
| `SVG.Forge.csproj` | .NET 8.0 WPF 프로젝트 파일 |
| `GlobalUsings.cs` | 공통 using 선언 |
| `App.xaml / App.xaml.cs` | 앱 진입점 |
| `Models/Enums.cs` | ToolMode, SvgShapeType 열거형 |
| `Models/SvgElement.cs` | 도형 모델 (위치, 채우기, 스트로크 등) |
| `Models/SvgLayer.cs` | 레이어 모델 |
| `Models/SvgDocument.cs` | 문서 모델 (캔버스 크기, 배경, 레이어 목록) |
| `ViewModels/BaseViewModel.cs` | INPC 기반 + RelayCommand |
| `ViewModels/MainViewModel.cs` | 메인 ViewModel (도구, 선택, 파일 명령) |
| `Services/SvgSerializer.cs` | SVG XML 직렬화/역직렬화 |
| `Services/ExportService.cs` | PNG 내보내기 (RenderTargetBitmap) |
| `MainWindow.xaml` | UI 레이아웃 (툴바, 캔버스, 레이어/속성 패널) |
| `MainWindow.xaml.cs` | 캔버스 상호작용, 도형 관리, 색상 선택기 |
| `Resources/app.ico` | 앱 아이콘 (임시, Color.Grade에서 복사) |

### 구현된 기능

- **캔버스**: Ctrl+마우스휠 줌, ScrollViewer 패닝
- **도구**: 선택(이동), 사각형, 원, 선, 텍스트
- **도형 관리**: 생성/삭제/복제/앞으로/뒤로
- **레이어 패널**: 레이어 추가, 요소 목록 표시, 클릭 선택
- **속성 패널**: 위치(X/Y/W/H), 채우기 색, 스트로크 색/폭, 투명도
- **색상 선택기**: 40색 팔레트 + 헥스 입력 팝업
- **SVG 내보내기/가져오기**: XML 직렬화
- **PNG 내보내기**: WPF RenderTargetBitmap

### 솔루션 등록

- `Playground.slnx`: `/Applications/Photo.Picture/SVG.Forge` 추가
- `+publish.cmd`: 메뉴 75번 (SVG.Forge), PUBALL 섹션 추가, 기존 75-87 → 76-88 리넘버링
- `+publish-all.cmd`: Photo.Picture 섹션에 SVG.Forge 추가

## 빌드 결과

```
경고 0개 / 오류 0개
```

## 향후 개선 사항 (v2)

- Clipper2Lib Boolean 연산 (Union/Subtract/Intersect)
- 베지어 패스 도구
- 선형/방사형 그래디언트 채우기
- 크기 조절 핸들 (8-point resize)
- Undo/Redo 스택
- 전용 app.ico 생성
