# Mark.View Markdown 뷰어 및 실시간 에디터 신규 개발

**날짜**: 2026-03-04
**태그**: feature
**경로**: `Applications/Tools/Productivity/Mark.View`

---

## 배경

개발자용 Markdown 파일 뷰어 + 실시간 에디터 앱. Ink.Cast는 DB 기반 노트 앱인 반면, Mark.View는 .md 파일 직접 편집/뷰 앱으로 성격이 다름.

---

## 구현 내용

### 기술 스택
- .NET 10 WPF (순수 .NET)
- Markdig 0.40.0 — MD → HTML 파이프라인
- Microsoft.Web.WebView2 1.0.3124.44 — HTML 렌더링
- `async/await` 비동기 WebView2 초기화

### 파일 구조
```
Applications/Tools/Productivity/Mark.View/
├── Mark.View.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/MarkDocument.cs
├── Services/MarkdownRenderer.cs
└── Resources/app.ico
```

### 주요 기능

#### 뷰 모드 (기본)
- Markdig 완전 파이프라인 (표, 작업목록, 강조, GridTable 등)
- WebView2 기반 다크 테마 CSS 적용 — 코드블록, 인용문, 테이블 등 미려하게 렌더링
- 파일 열기 후 즉시 렌더링

#### 편집 모드 (Ctrl+E 또는 툴바 버튼)
- 좌측: 코드 에디터 (Cascadia Code 폰트, 300ms 디바운스 실시간 프리뷰)
- 우측: WebView2 실시간 프리뷰
- GridSplitter로 패널 너비 조절 가능
- Tab 들여쓰기 (4칸), Shift+Tab 역들여쓰기

#### 멀티 탭
- 파일 여러 개 동시 열기, 커스텀 탭바
- 미저장 표시 ( • ), 저장 요청 대화상자
- 이미 열린 파일 재오픈 시 해당 탭으로 포커스

#### 목차(TOC) 패널 (Ctrl+T)
- WebView2에서 JS로 h1~h6 추출
- 클릭 시 해당 섹션으로 스무스 스크롤
- 레벨별 들여쓰기 + 폰트 굵기 차등 표시

#### HTML 내보내기 (Ctrl+B)
- 다크 CSS 포함 완전한 standalone HTML 파일로 저장

#### 찾기 (Ctrl+F)
- WebView2 window.find() API 활용

#### 드래그 앤 드롭
- .md/.markdown/.txt 파일 창에 드롭 → 탭 열기

#### 단축키 전체
| 단축키 | 동작 |
|--------|------|
| Ctrl+O | 열기 |
| Ctrl+S | 저장 |
| Ctrl+Shift+S | 다른 이름으로 저장 |
| Ctrl+N | 새 파일 |
| Ctrl+W | 탭 닫기 |
| Ctrl+E | 편집 모드 전환 |
| Ctrl+T | TOC 패널 |
| Ctrl+B | HTML 내보내기 |
| Ctrl+F | 찾기 |
| Ctrl+Tab | 다음 탭 |
| F5 | 새로고침 |

### Markdown CSS 다크 테마 특징
- Catppuccin Mocha 팔레트 기반 (`--bg: #1e1e2e`)
- 헤딩 색상: H1=파랑, H2=보라, H3=청록, H4=초록
- 코드블록: `Cascadia Code` 폰트, `#181825` 배경
- 인용문: 좌측 파란 보더 + 반투명 배경
- 테이블: 짝수 행 미세 하이라이트
- 커스텀 스크롤바

---

## 커밋
- `[mark.view] | Markdown 뷰어 및 실시간 에디터 신규 개발` (9f0d5b0)
