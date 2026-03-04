# Disk.Lens 디스크 공간 분석기 신규 개발

**날짜**: 2026-03-04
**태그**: feature
**경로**: `Applications/Files/Disk.Lens`

---

## 배경

기존 `Archieve/Applications/Files/Disk.Lens`는 소스 파일 없이 csproj만 존재하는 빈 프로젝트였음.
TreeSize(https://www.jam-software.com/treesize) 스타일의 디스크 분석기를 스크린샷 3종을 참고하여 완전히 새로 구현.

---

## 구현 내용

### 기술 스택
- .NET 10 WPF (순수 .NET, 외부 NuGet 없음)
- 비동기 스캔: `async/await` + `IProgress<T>` + `CancellationToken`
- `FileSystemWatcher` 파일 변경 감지

### 파일 구조
```
Applications/Files/Disk.Lens/
├── Disk.Lens.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/DiskItem.cs
├── Services/DiskScanner.cs
└── Resources/app.ico
```

### 주요 기능

#### 트리 뷰 (기본 화면)
- TreeSize 스타일 컬럼: 이름, 크기 바(색상 그라데이션), 크기, 할당 크기, %, 파일 수, 수정일
- 폴더(파란 바) / 파일(초록 바) 색상 구분
- 클릭 정렬 (이름/크기/할당/% 등)
- 더블클릭 → 해당 폴더 드릴다운
- 브레드크럼 네비게이션

#### 트리맵 뷰
- Squarified 알고리즘 기반 트리맵 Canvas 렌더링
- 클릭 → 드릴다운, 우클릭 → 상위 이동
- 크기별 색상 팔레트 (6가지 색조)

#### 상위 파일 뷰
- 현재 스캔 경로에서 가장 큰 파일 200개 목록
- 더블클릭 → 탐색기에서 선택

#### 확장자 분석 뷰
- 확장자별 총 크기 / 파일 수 / 평균 크기
- 크기 바 시각화

#### 파일 작업 (우클릭 컨텍스트 메뉴)
- 탐색기에서 열기
- 이 폴더 분석 (드릴다운)
- 경로/크기 복사
- 삭제 (확인 대화상자 포함, 트리에서 즉시 제거)

#### 자동 갱신
- `FileSystemWatcher`로 변경 감지 → 3초 디바운스 후 "F5로 새로고침" 안내

#### CSV 내보내기
- 전체 트리 구조를 CSV로 저장 (경로/종류/크기/파일수/수정일)

### TreeSize 대비 개선 사항
- 완전한 다크 테마 (TreeSize는 기본 라이트 테마)
- 트리맵 인터랙티브 드릴다운/백 탐색
- CSV 내보내기 기본 제공
- 개발자 친화적 확장자 분석 뷰
- `FileSystemWatcher` 실시간 변경 감지

---

## 빌드 수정 이력

1. `DiskItem.cs` — `wpftmp` 프로젝트에서 `System.IO` explicit using 누락 → 추가
2. emoji switch expression → `string` 타입 추론 오류 → Unicode escape 방식으로 변환
3. XAML `Converter={x:Null}` → `DataTrigger` + `StaticResource` 방식으로 교체
4. `MainWindow.xaml.cs` `Path` 모호 참조 → `System.IO.Path`로 명시

---

## 커밋
- `[publish]` 커밋: publish 스크립트 Disk.Lens 추가 (번호 5)
- `[disk.lens]` 커밋: 신규 앱 개발
