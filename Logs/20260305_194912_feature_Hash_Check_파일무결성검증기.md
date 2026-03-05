# Hash.Check — 파일 무결성 검증기 개발

- **날짜**: 2026-03-05
- **태그**: feature
- **소요 시간**: 약 2시간 (2세션)

## 개요

MD5/SHA-1/SHA-256/SHA-512 해시 계산·비교·일괄 검증·변경 감시 WPF 앱.
외부 패키지 없이 `System.Security.Cryptography` 내장 API만 사용.

## 생성 파일 목록

### 프로젝트 뼈대
- `Hash.Check.csproj` — net10.0-windows, WPF, 외부 패키지 없음
- `GlobalUsings.cs`
- `App.xaml` / `App.xaml.cs` — 다크 테마, 포인트 색 초록 #22C55E
- `MainWindow.xaml` / `MainWindow.xaml.cs` — 4탭 레이아웃, DwmSetWindowAttribute 다크 타이틀바

### 서비스 레이어
- `Services/HashService.cs` — 한 번 파일 읽기로 4개 해시 동시 계산 (TransformBlock 패턴)
- `Services/ChecksumParser.cs` — BSD/GNU/SFV 체크섬 파일 파싱
- `Services/WatchService.cs` — FileSystemWatcher 기반 실시간 변경 감시

### 뷰 (4개)
- `Views/SingleView.xaml` / `.cs` — 단일 파일 드래그&드롭, 해시 비교, 카드 테두리 강조
- `Views/BatchView.xaml` / `.cs` — 체크섬 파일 일괄 검증, CSV 내보내기
- `Views/FolderView.xaml` / `.cs` — 폴더 전체 해시 계산, .txt(GNU 형식)/CSV 내보내기
- `Views/WatchView.xaml` / `.cs` — 파일 스냅샷 등록, 실시간 변경 감지, 즉시 검증

### 리소스
- `Resources/app.ico` — 다크 배경 + 초록 방패 + 체크마크 (16/32/48/256px)

## 수정 사항 (빌드 오류 수정)
1. `App.xaml` 라인 13 공백 누락 (`BrTxtSecondary"Color=`) → 수정
2. `ChecksumParser.cs` 참조 타입 nullable 비교 오류 (`.HasValue`/`.Value`) → `!= null`/직접 참조로 변경
3. `WatchView.xaml.cs` 미사용 `_isWatching` 필드 → 제거

## 등록/갱신
- `Playground.slnx` — Files 폴더에 Hash.Check 등록
- `+publish-all.cmd` — 48번 다음 Hash.Check 추가
- `+publish.cmd` — 메뉴 49번, 선택 섹션, PUBALL 섹션 추가
- `ideas/avoid.md` — 구현 완료 목록에 Hash.Check 추가

## 빌드 결과
```
경고 0개, 오류 0개
```
