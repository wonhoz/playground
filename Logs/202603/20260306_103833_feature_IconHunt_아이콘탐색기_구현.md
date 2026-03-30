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

## 버그 수정 (2차~4차, 2026-03-06)

| 오류 | 원인 | 해결 |
|------|------|------|
| 전체 인덱싱 후 아이콘 0개 | FTS5 `content='icons'` 모드에서 직접 INSERT 시 rowid 불일치로 트랜잭션 롤백 | `USING fts5(id UNINDEXED, name, tags)` standalone 모드로 전환, `PRAGMA user_version=2` 마이그레이션 |
| static HttpClient 해제 | `Dispose()`에서 static `_http`를 해제해 이후 HTTP 요청 불가 | `Dispose()`에서 해제 제거 |
| 인덱싱 실패 무음 | 에러가 상태바에만 표시되어 사용자가 인지 못함 | `IndexCollectionAsync`가 에러 문자열 반환, 전체 실패 시 `MessageBox` 표시 |
| `Command="{x:Null}"` | Border.InputBindings에 null Command MouseBinding | 해당 InputBindings 제거 |

| SVG 미리보기 검은색 | currentColor가 SharpVectors에서 검정으로 렌더링 | 렌더링 전 `ApplyColor(svg, PreviewFg)` 치환 |
| 그리드 썸네일 앱 프리즈 | `Task.Run(SharpVectors)` → MTA 스레드에서 WPF 객체 생성 예외 | `Dispatcher.InvokeAsync(Background)` — UI 스레드 Background 우선순위로 렌더링 |
| Iconify API 404 | `/{prefix}.json` 엔드포인트 Iconify API v3에서 미지원 | `/collection?prefix={prefix}` + uncategorized/categories 배열 파싱으로 변경 |
| `DrawingImage` 크로스 스레드 | Task.Run에서 생성된 DrawingImage를 UI에서 접근 불가 | `DrawingImage.Freeze()` 호출 |

## 최종 완성 기능

- 16개 라이브러리 인덱싱 (75,741개+ 아이콘)
- 실시간 FTS5 검색
- 그리드 썸네일: 검색 결과 순차 비동기 로딩 (다운로드 await + Background 렌더링)
- SVG 미리보기: 다크/라이트 배경 토글, 색상 치환 렌더링
- 즐겨찾기, 최근 사용, SVG/이름/ID 복사, PNG 저장

## 빌드 결과

```
빌드했습니다. 경고 0개, 오류 0개
```
