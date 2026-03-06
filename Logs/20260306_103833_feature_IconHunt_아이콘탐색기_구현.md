# Icon.Hunt — 오픈소스 아이콘 라이브러리 탐색기 구현

- **날짜**: 2026-03-06
- **태그**: feature
- **경로**: `Applications/Tools/Dev/Icon.Hunt`

## 개요

Iconify API를 기반으로 300,000개 이상의 오픈소스 아이콘을 로컬에서 탐색할 수 있는 WPF 앱 구현.
16개 인기 라이브러리(MDI, Heroicons, Phosphor, Lucide, Tabler 등) 지원.

## 구현 파일

| 파일 | 내용 |
|------|------|
| `Icon.Hunt.csproj` | net10.0-windows, SharpVectors.Wpf 1.8.1, Microsoft.Data.Sqlite 9.0.3 |
| `Models/IconEntry.cs` | 아이콘 항목 모델 (INotifyPropertyChanged) |
| `Models/IconCollection.cs` | 16개 기본 컬렉션 정의 |
| `Services/IconDatabase.cs` | SQLite + FTS5 풀텍스트 검색 |
| `Services/IconifyService.cs` | Iconify REST API + 로컬 SVG 캐시 |
| `Services/SvgRenderService.cs` | SharpVectors → DrawingImage/BitmapSource 렌더링 |
| `ViewModels/MainViewModel.cs` | MVVM, 검색 디바운스, 인덱싱, 즐겨찾기 |
| `Converters/Converters.cs` | BoolToColor, BoolToStar, PrefixBadgeColor 등 |
| `App.xaml` | 전역 다크 테마 스타일 |
| `MainWindow.xaml` | 3-패널 레이아웃 (라이브러리/그리드/상세) |
| `MainWindow.xaml.cs` | DwmSetWindowAttribute 다크 타이틀바, 이벤트 핸들러 |

## 주요 기능

- 라이브러리별 활성화/비활성화 필터링
- 실시간 검색 (200ms 디바운스, SQLite FTS5)
- SVG 미리보기 (SharpVectors 렌더링)
- 다크/라이트 배경 토글
- 즐겨찾기 (★/☆) + 최근 사용 목록
- SVG 복사 / 이름 복사 / ID 복사
- PNG 저장 (크기 선택: 16~512px)
- 인덱싱 진행 오버레이 (취소 가능)
- 로컬 SVG 캐시 (`%AppData%\IconHunt\svg\`)
- 캐시 정리 기능

## 트러블슈팅

| 오류 | 원인 | 해결 |
|------|------|------|
| `StreamSvgReader` 없음 | SharpVectors 1.8에 해당 클래스 없음 | 임시 파일 경유 `FileSvgReader` 사용 |
| `LetterSpacing` 없음 | WPF에 없는 속성 (WinUI 전용) | 속성 제거 |
| `Border`에 자식 두 개 | StackPanel + Grid 모두 직접 자식 | 감싸는 `Grid` 추가 |

## 빌드 결과

```
빌드했습니다. 경고 0개, 오류 0개
```
