---
date: 2026-03-13 21:23:38 KST
tag: feature
project: Skill.Cast
---

# Skill.Cast — Claude Code 개발 환경 관리자 구현

## 작업 개요

Claude Code를 활용한 개발 시 commands, skills, memory, plugins 등을 체계적으로
관리할 수 있는 WPF 도구 구현. Claude Code 기능에 대한 지식 베이스도 내장.

## 구현 내용

### 프로젝트 구조
```
Applications/Development/Inspector/Skill.Cast/
├── Skill.Cast.csproj          (.NET 10, WPF, PublishSingleFile)
├── App.xaml / App.xaml.cs     (다크 테마 리소스 딕셔너리)
├── MainWindow.xaml            (6탭 UI 레이아웃)
├── MainWindow.xaml.cs         (이벤트 핸들러 전체)
├── NewItemDialog.cs           (순수 코드 다이얼로그)
├── Models/
│   └── ClaudeItem.cs          (ClaudeItem, PluginInfo, KnowledgeArticle)
├── Services/
│   ├── ClaudeFileService.cs   (파일 로드/저장 서비스)
│   └── KnowledgeService.cs    (내장 지식 베이스 9개 문서)
├── ViewModels/
│   └── MainViewModel.cs       (MVVM, INotifyPropertyChanged)
├── Helpers/
│   └── DwmHelper.cs           (다크 타이틀바)
└── Resources/
    └── app.ico
```

### 주요 기능

| 탭 | 기능 |
|----|------|
| 📊 개요 | 통계 카드 (Commands/Skills/Memory/Plugins 수) |
| ⚡ Commands | 글로벌+프로젝트 slash command 목록, 검색, 새 생성, 편집, 저장 |
| 🧠 Skills | skill 목록, frontmatter 뱃지, 편집, 저장 |
| 💾 Memory | 메모리 파일 목록 및 편집 (MEMORY.md + memory/*.md) |
| 🔌 Plugins | 플러그인 목록, README·통계·키워드 상세 보기 |
| ⚙️ 설정 & Hooks | settings.json / .mcp.json / hooks/*.json 뷰어 |
| 📚 지식 베이스 | Claude Code 완전 가이드 9개 문서 내장 |

### 기술 스택
- .NET 10.0-windows / WPF / MVVM
- 다크 테마: #1A1A1A 계열 + AccentBrush #5B8AF0
- DwmSetWindowAttribute (attr 20) 다크 타이틀바
- System.Text.Json (JSON 파싱)
- 커스텀 YAML frontmatter 파서 (외부 의존성 없음)
- OpenFolderDialog (.NET 6+ WPF 내장)

## 수정 파일

| 파일 | 구분 |
|------|------|
| Skill.Cast.csproj | 신규 |
| App.xaml / App.xaml.cs | 신규 |
| MainWindow.xaml / .cs | 신규 |
| NewItemDialog.cs | 신규 |
| Models/ClaudeItem.cs | 신규 |
| Services/ClaudeFileService.cs | 신규 |
| Services/KnowledgeService.cs | 신규 |
| ViewModels/MainViewModel.cs | 신규 |
| Helpers/DwmHelper.cs | 신규 |
| Resources/app.ico | 신규 |
| Playground.slnx | Inspector 폴더에 Skill.Cast 등록 |

## 빌드 결과

```
dotnet build Applications/Development/Inspector/Skill.Cast/Skill.Cast.csproj -c Release
→ 경고 0개, 오류 0개 ✅
```
