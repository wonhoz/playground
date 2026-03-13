using System.Collections.Generic;
using SkillCast.Models;

namespace SkillCast.Services;

public static class KnowledgeService
{
    public static List<KnowledgeArticle> GetArticles() =>
    [
        new()
        {
            Title = "Slash Commands 완전 가이드",
            Icon = "⚡",
            Category = "핵심",
            Content = """
# Slash Commands (슬래시 커맨드) 완전 가이드

## 파일 위치
- 전역: ~/.claude/commands/*.md
- 프로젝트: .claude/commands/*.md
- 네임스페이스: .claude/commands/git/commit.md → /git:commit

## Frontmatter 필드

---
description: 명령어 설명 (/help에 표시)
allowed-tools: Read, Write, Edit, Bash(git:*)
model: haiku | sonnet | opus
argument-hint: [branch-name] [message]
disable-model-invocation: true   # 사용자만 호출 가능
---

## 동적 컨텍스트 주입

!`git status --short` 처럼 ! 뒤 백틱 명령은
Claude가 받기 전에 실행되어 결과로 대체됩니다.

## 인자 치환

$ARGUMENTS     — 전체 인자
$ARGUMENTS[0]  — 첫 번째 인자 (또는 $1)
$ARGUMENTS[1]  — 두 번째 인자 (또는 $2)
${CLAUDE_SESSION_ID}  — 현재 세션 ID

## 예시: Git 커밋 커맨드

.claude/commands/commit.md:

---
description: Git 커밋 생성
allowed-tools: Bash(git:*)
argument-hint: [message]
---

현재 변경사항: !`git diff --stat`

$1 메시지로 스테이징된 파일을 커밋하세요.

## 네임스페이스 구조

.claude/commands/
├── commit.md          → /commit
├── deploy.md          → /deploy
└── workflows/
    ├── feature.md     → /workflows:feature
    └── hotfix.md      → /workflows:hotfix
"""
        },
        new()
        {
            Title = "Skills / SKILL.md 레퍼런스",
            Icon = "🧠",
            Category = "핵심",
            Content = """
# Skills (스킬) 레퍼런스

## 파일 위치
- 전역: ~/.claude/skills/<name>/SKILL.md
- 프로젝트: .claude/skills/<name>/SKILL.md
- 플러그인: <plugin>/skills/<name>/SKILL.md

## 디렉터리 구조

.claude/skills/my-skill/
├── SKILL.md           ← 필수 (메인 지침)
├── templates/         ← 참조 템플릿
├── scripts/           ← 실행 스크립트
└── examples/          ← 참조 예시

## SKILL.md Frontmatter 전체 필드

---
name: skill-name
description: 스킬 설명 (Claude가 자동 호출 여부 판단)
argument-hint: [arg1] [optional]
disable-model-invocation: true   # 사용자만 호출
user-invocable: false            # Claude만 호출 (메뉴에 숨김)
allowed-tools: Read, Grep, Glob  # 권한 없이 허용
model: claude-sonnet-4-6         # 이 스킬용 모델 오버라이드
context: fork                    # 격리된 서브에이전트 실행
agent: Explore                   # fork 시 에이전트 타입
hooks: {...}                     # 스킬 수명주기 훅
---

## 호출 제어

설정 없음:              사용자 ✓  Claude ✓  (범용)
disable-model-invocation: true  사용자 ✓  Claude ✗  (배포 등 부작용)
user-invocable: false           사용자 ✗  Claude ✓  (배경 지식)

## 인자 치환

$ARGUMENTS               — 전체 인자
$ARGUMENTS[0] / $1       — 첫 번째 인자
${CLAUDE_SKILL_DIR}      — 스킬 디렉터리 경로

## 동적 컨텍스트

## 현재 Git 상태
브랜치: !`git branch --show-current`
마지막 커밋: !`git log -1 --oneline`

## Bundled Skills (내장 스킬)

/simplify     — 코드 품질 리뷰 (병렬 에이전트 3개)
/batch        — 코드베이스 대규모 병렬 변경
/debug        — 세션 문제 해결
/loop [5m]    — 지정 간격으로 반복 실행
/claude-api   — Claude API 참조 문서 로드

## 예시: 프로젝트 컨벤션

---
name: project-conventions
description: 코딩 스타일. 코드 작성/검토 시 자동 적용.
user-invocable: false
---

## 네이밍 규칙
- 클래스: PascalCase
- 변수: camelCase

## 금지 사항
- any 타입 사용 금지
- console.log 프로덕션 금지
"""
        },
        new()
        {
            Title = "Memory 시스템 가이드",
            Icon = "💾",
            Category = "핵심",
            Content = """
# Memory (메모리) 시스템 가이드

## CLAUDE.md (사용자 작성 지침)

우선순위 순서:
1. 관리형 정책 (IT 배포, 재정의 불가)
   - Windows: C:\Program Files\ClaudeCode\CLAUDE.md
2. 프로젝트: .claude/CLAUDE.md 또는 ./CLAUDE.md
3. 전역: ~/.claude/CLAUDE.md

## 파일 임포트

다른 파일 참조: @README 또는 @~/.claude/my-prefs.md
최대 200줄 유지 권장.

## 경로별 규칙 (.claude/rules/)

.claude/rules/ 디렉터리에 주제별 파일:

---
paths:
  - "src/api/**/*.ts"
  - "*.test.ts"
---
# 이 경로의 파일을 편집할 때만 적용되는 규칙

## Auto Memory (자동 메모리)

Claude가 세션 간 학습 내용을 자동 저장.
저장 위치: ~/.claude/projects/<project>/memory/

MEMORY.md   — 인덱스 (세션 시작 시 앞 200줄 자동 로드)
topic.md    — 주제별 메모리 파일 (필요시 로드)

비활성화: autoMemoryEnabled: false (settings.json)
         또는 CLAUDE_CODE_DISABLE_AUTO_MEMORY=1

## 메모리 타입

user      — 사용자 역할, 전문성, 선호도
feedback  — 행동 교정 지침 (가장 중요)
project   — 작업 목표, 결정사항, 마감일
reference — 외부 시스템 위치 포인터

## 메모리 파일 형식

---
name: no_db_mocks
description: DB를 mock으로 대체하지 말 것
type: feedback
---

통합 테스트는 실제 DB 사용 필수.

**Why:** mock/prod 불일치로 마이그레이션 실패를 숨긴 적 있음.
**How to apply:** 테스트 코드 작성 시 항상 실제 DB 연결.

## /memory 명령어

Claude Code에서 /memory 실행 시:
- 모든 CLAUDE.md 파일 목록 표시
- 자동 메모리 파일 목록 표시
- 직접 편집 가능

## 저장하지 말 것

- 코드 패턴, 아키텍처 (코드에서 파생 가능)
- Git 히스토리 (git log로 확인 가능)
- 임시 작업 세부 사항 (현재 대화 컨텍스트)
- CLAUDE.md에 이미 문서화된 내용
"""
        },
        new()
        {
            Title = "Plugins 구조 가이드",
            Icon = "🔌",
            Category = "플러그인",
            Content = """
# Plugins (플러그인) 구조 가이드

## 기본 구조

my-plugin/
├── .claude-plugin/
│   └── plugin.json          ← 필수 manifest
├── commands/
│   └── my-command.md
├── skills/
│   └── my-skill/
│       └── SKILL.md
├── agents/
│   └── my-agent.md
├── hooks/
│   └── hooks.json
├── .mcp.json
└── README.md

## plugin.json 필드

{
  "name": "my-plugin",
  "version": "1.0.0",
  "description": "플러그인 설명",
  "author": {
    "name": "개발자 이름",
    "email": "email@example.com"
  },
  "license": "MIT",
  "keywords": ["tag1", "tag2"],
  "commands": "./commands",
  "agents": "./agents",
  "hooks": "./hooks/hooks.json",
  "mcpServers": "./.mcp.json"
}

## 플러그인 설치

/plugin install <plugin-name>
/plugin list
/plugin remove <plugin-name>

## 마켓플레이스 유용한 플러그인

- plugin-dev       — 플러그인 개발 도구
- commit-commands  — Git 커밋 워크플로우
- feature-dev      — 기능 개발 워크플로우
- hookify          — 자동화 규칙 생성
- code-review      — 코드 리뷰 자동화
- pr-review-toolkit — PR 리뷰 도구킷
- claude-code-setup — 환경 설정 추천

설치 위치: ~/.claude/plugins/
"""
        },
        new()
        {
            Title = "Hooks 설정 가이드",
            Icon = "🪝",
            Category = "자동화",
            Content = """
# Hooks (훅) 설정 가이드

## 전체 이벤트 목록

| 이벤트 | 시점 | 매처 |
|--------|------|------|
| SessionStart | 세션 시작/재개 | startup, resume, clear, compact |
| UserPromptSubmit | 사용자 프롬프트 제출 | (없음) |
| PreToolUse | 도구 실행 전 (차단 가능) | 도구 이름 |
| PermissionRequest | 권한 다이얼로그 표시 | 도구 이름 |
| PostToolUse | 도구 성공 후 | 도구 이름 |
| PostToolUseFailure | 도구 실패 후 | 도구 이름 |
| Notification | Claude 알림 발생 | permission_prompt, idle_prompt 등 |
| SubagentStart/Stop | 서브에이전트 생성/종료 | 에이전트 타입 |
| InstructionsLoaded | CLAUDE.md/rules 로드 | (없음) |
| ConfigChange | 설정 파일 변경 | user_settings, skills 등 |
| WorktreeCreate/Remove | Worktree 생성/제거 | (없음) |
| PreCompact | 컨텍스트 압축 전 | manual, auto |
| SessionEnd | 세션 종료 | clear, logout, other |
| Stop | Claude 응답 완료 | (없음) |

## 훅 타입

1. command — 쉘 스크립트 실행
2. http    — URL에 POST (이벤트 데이터)
3. prompt  — LLM 단일 평가
4. agent   — 도구 포함 멀티턴 검증

## 입출력

- 입력: stdin에 JSON 이벤트 데이터
- 출력: 종료코드 + 선택적 JSON 응답
  exit 0 = 허용 / exit 2 = 차단 (PreToolUse만)

## settings.json 구조

{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "command": "bash validate.sh",
            "timeout": 30
          }
        ]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "prettier --write \"$CLAUDE_TOOL_INPUT_FILE_PATH\""
          }
        ]
      }
    ],
    "Notification": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "powershell -File notify.ps1"
          }
        ]
      }
    ]
  }
}

## 환경 변수

$CLAUDE_TOOL_INPUT_FILE_PATH  — 편집/생성 파일 경로
$CLAUDE_TOOL_INPUT_COMMAND    — 실행 중인 bash 명령
$CLAUDE_PLUGIN_ROOT           — 플러그인 루트

## 매처 예시

"Write"        — Write 도구만
"Edit|Write"   — Edit 또는 Write
"Bash(git:*)"  — git 명령 Bash
"*"            — 모든 도구

## 실용 예시: 민감 파일 보호

{
  "PreToolUse": [{
    "matcher": "Write|Edit",
    "hooks": [{
      "type": "command",
      "command": "bash -c 'if [[ \"$CLAUDE_TOOL_INPUT_FILE_PATH\" == *\".env\"* ]]; then echo \"BLOCKED\" >&2; exit 2; fi'"
    }]
  }]
}

## 설정 위치

~/.claude/settings.json            — 모든 프로젝트
.claude/settings.json              — 프로젝트 (git)
.claude/settings.local.json        — 프로젝트 로컬 (gitignore)
.claude/hooks/hooks.json (plugin)  — 플러그인

설정 UI: Claude Code에서 /hooks 실행
"""
        },
        new()
        {
            Title = "MCP Servers 가이드",
            Icon = "🌐",
            Category = "통합",
            Content = """
# MCP (Model Context Protocol) Servers 가이드

## 설치 방법

# HTTP 원격 서버 (권장)
claude mcp add --transport http github https://api.githubcopilot.com/mcp/

# SSE 원격 서버 (구버전)
claude mcp add --transport sse asana https://mcp.asana.com/sse

# 로컬 stdio 서버
claude mcp add --transport stdio --env API_KEY=value airtable -- npx -y airtable-mcp-server

## 서버 타입별 .mcp.json 구조

{
  "mcpServers": {
    "github": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp/",
      "headers": {"Authorization": "Bearer ${GITHUB_TOKEN}"}
    },
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path"],
      "env": {"API_KEY": "${MY_API_KEY}"}
    }
  }
}

환경 변수: ${VAR} 또는 ${VAR:-default} 형식

## 서버 관리 명령

claude mcp list
claude mcp get <name>
claude mcp remove <name>
claude mcp reset-project-choices

## 설정 범위

~/.claude.json                — 전역 (모든 프로젝트)
.mcp.json                     — 프로젝트 (git 공유)

## 고급 기능

- MCP 프롬프트 커맨드: /mcp__servername__promptname
- 리소스 참조: @server:protocol://path
- OAuth 2.0: /mcp 명령으로 인증
- Tool Search: MCP 도구 10% 초과 시 지연 로딩 자동 활성화

## 추천 MCP 서버

@modelcontextprotocol/server-github       — GitHub API
@modelcontextprotocol/server-filesystem   — 파일 시스템
@modelcontextprotocol/server-sqlite       — SQLite DB
@modelcontextprotocol/server-brave-search — 웹 검색
context7                                  — 최신 라이브러리 문서
@playwright/mcp                           — 브라우저 자동화
"""
        },
        new()
        {
            Title = "Statusline 설정 가이드",
            Icon = "📊",
            Category = "환경",
            Content = """
# Statusline (상태바) 설정 가이드

## settings.json 설정

{
  "statusLine": {
    "type": "command",
    "command": "~/.claude/my-statusline.sh",
    "padding": 2
  }
}

## 스크립트 입력 JSON (stdin으로 수신)

{
  "workspace": {
    "current_dir": "/path/to/project"
  },
  "model": {
    "display_name": "Claude Sonnet 4.6",
    "version": "claude-sonnet-4-6"
  },
  "context_window": {
    "used_percentage": 42.5,
    "remaining_percentage": 57.5
  },
  "cost": {
    "total_cost_usd": 0.0234,
    "total_duration_ms": 45000
  },
  "session_id": "uuid-string",
  "vim": {"mode": "normal"},
  "agent": {"name": "general-purpose"},
  "worktree": {"branch": "feature/my-branch"}
}

## 최소 Bash 스크립트 예시

#!/bin/bash
input=$(cat)
dir=$(echo "$input" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['workspace']['current_dir'])")
pct=$(echo "$input" | python3 -c "import sys,json; d=json.load(sys.stdin); print(int(d['context_window']['used_percentage']))")

if [ "$pct" -gt 90 ]; then color="\033[31m"
elif [ "$pct" -gt 70 ]; then color="\033[33m"
else color="\033[32m"; fi
echo -e "${color}🧠 ${pct}%\033[0m  📁 $(basename $dir)"

## 자동 생성 방법

Claude Code에서 실행:
/statusline describe what you want

예: /statusline show context usage and git branch

## 유용한 npm 도구

npm i -g @chongdashu/cc-statusline   — 테마 지원
npm i -g ccusage                      — 비용/토큰 추적

# cc-statusline 설치
npx @chongdashu/cc-statusline create
→ ~/.claude/statusline-command.sh 생성됨
"""
        },
        new()
        {
            Title = "settings.json 레퍼런스",
            Icon = "⚙️",
            Category = "환경",
            Content = """
# settings.json 완전 레퍼런스

## 4단계 우선순위 (높음 → 낮음)

1. 관리형 정책 (IT 배포, 재정의 불가)
2. 커맨드라인 인자
3. 로컬: .claude/settings.local.json (gitignore, 개인)
4. 프로젝트: .claude/settings.json (git 공유)
5. 전역: ~/.claude/settings.json

## 전체 구조

{
  "$schema": "https://json.schemastore.org/claude-code-settings.json",
  "statusLine": {
    "type": "command",
    "command": "~/.claude/statusline.sh"
  },
  "permissions": {
    "allow": ["Bash(npm run *)", "Read(src/**)"],
    "deny": ["Bash(curl *)", "Read(.env*)"],
    "ask": ["Bash(git push *)"]
  },
  "env": {
    "MY_CUSTOM_VAR": "value"
  },
  "model": "claude-sonnet-4-6",
  "hooks": { ... },
  "sandbox": {
    "enabled": true,
    "filesystem": {
      "allowWrite": ["//tmp/build"],
      "denyRead": ["~/.aws/credentials"]
    },
    "network": {"allowedDomains": ["github.com"]}
  },
  "outputStyle": "Default",
  "autoMemoryEnabled": true,
  "forceLoginMethod": "claudeai"
}

## permissions 규칙 문법

Tool          — 모든 사용
Tool(spec)    — 와일드카드 지정
Bash(git:*)   — git 명령만 허용
Read(src/**)  — src 하위 읽기만
WebFetch(domain:github.com)  — 도메인 제한

평가 순서: deny → ask → allow (첫 매치 우선)

## permissions 예시

"allow": [
  "Bash(git:*)",
  "Bash(npm run *)",
  "Read",
  "Write(src/**)",
  "Edit"
],
"deny": [
  "Bash(rm -rf:*)",
  "Bash(curl *)"
]

## outputStyle 옵션

"Default"      — 소프트웨어 엔지니어링 최적화
"Explanatory"  — 교육적 설명 추가
"Learning"     — TODO(human) 마커로 학습 모드
또는 사용자 정의 스타일 이름
"""
        },
        new()
        {
            Title = "키바인딩 커스터마이징",
            Icon = "⌨️",
            Category = "환경",
            Content = """
# 키바인딩 커스터마이징

## 파일 위치

~/.claude/keybindings.json

## 구조

{
  "$schema": "https://www.schemastore.org/claude-code-keybindings.json",
  "bindings": [
    {
      "context": "Chat",
      "bindings": {
        "ctrl+e": "chat:externalEditor",
        "ctrl+u": null
      }
    },
    {
      "context": "Global",
      "bindings": {
        "ctrl+k ctrl+s": "app:toggleTodos"
      }
    }
  ]
}

## 컨텍스트 목록

Chat, Global, Autocomplete, Settings, Confirmation,
Tabs, Help, Transcript, HistorySearch, Task,
ThemePicker, Attachments, Footer, MessageSelector,
DiffDialog, ModelPicker, Select, Plugin

## 키 문법

- 수식어: ctrl, alt, shift, meta/cmd
- 대문자는 shift 포함: K = shift+k
- 코드 시퀀스(chord): ctrl+k ctrl+s
- 특수키: escape, enter, tab, space, backspace,
          delete, up, down, left, right

## 주요 액션

### Chat 컨텍스트
chat:submit           — 메시지 전송
chat:cancel           — 요청 취소
chat:cycleMode        — 모드 순환
chat:modelPicker      — 모델 선택
chat:externalEditor   — 외부 에디터 열기
chat:stash            — 입력 임시 저장

### Global 컨텍스트
app:interrupt         — 중단
app:exit              — 종료
app:toggleTodos       — TODO 패널 토글
app:toggleTranscript  — 트랜스크립트 토글

### Autocomplete 컨텍스트
autocomplete:accept   — 자동완성 수락
autocomplete:dismiss  — 자동완성 닫기
autocomplete:previous/next — 이전/다음

## 특이사항

- null 로 설정하면 해당 키 언바인딩
- 파일 변경 시 자동 리로드
- 예약된 키: Ctrl+C, Ctrl+D (터미널 시그널)
- 주의: Ctrl+B (tmux), Ctrl+A (GNU screen) 충돌 가능

## 설정 방법

Claude Code에서 /keybindings-help 스킬 사용
또는 직접 JSON 파일 편집
"""
        },
        new()
        {
            Title = "Output Styles 가이드",
            Icon = "🎨",
            Category = "환경",
            Content = """
# Output Styles (출력 스타일) 가이드

## 개요

Claude의 시스템 프롬프트를 수정하여 비개발 용도에도 활용 가능.
세션 시작 시 적용되며 세션 내에서는 안정적.

## 내장 스타일

Default      — 소프트웨어 엔지니어링 최적화 (기본)
Explanatory  — 코드 작업과 함께 교육적 인사이트 제공
Learning     — TODO(human) 마커로 학습 모드 협업

## 커스텀 스타일 만들기

저장 위치:
- .claude/output-styles/<name>.md (프로젝트)
- ~/.claude/output-styles/<name>.md (전역)

파일 형식:

---
name: My Style
description: 스타일 설명 (/config 메뉴에 표시)
keep-coding-instructions: false
---

# 커스텀 지침

당신은 명확하게 설명하는 기술 작가입니다...

## Frontmatter 필드

name                    — 선택기에 표시될 이름
description             — /config 메뉴 설명
keep-coding-instructions — 소프트웨어 엔지니어링 지침 유지 (기본 false)

## 스타일 vs 다른 기능 비교

vs CLAUDE.md:
  - Output Styles → 시스템 프롬프트 수정
  - CLAUDE.md     → 사용자 메시지에 포함

vs Skills:
  - Styles → 전역 톤/형식 변경
  - Skills → 특정 작업용 지침

vs Agents:
  - Styles → 시스템 프롬프트만 수정
  - Agents → 격리된 도구/모델 환경

## 스타일 변경 방법

Claude Code에서: /config → Output style 선택
또는 settings.json:

{
  "outputStyle": "My Style"
}

변경 사항은 새 세션에 적용됩니다.

## 실용 예시: 한국어 응답 스타일

---
name: Korean
description: 항상 한국어로 응답
keep-coding-instructions: true
---

항상 한국어로 응답하세요. 코드와 기술 용어는 영어 유지.
"""
        },
        new()
        {
            Title = "IDE 통합 가이드",
            Icon = "💻",
            Category = "통합",
            Content = """
# IDE 통합 가이드

## VS Code 확장

설치: Cmd/Ctrl+Shift+X → "Claude Code" 검색

### 주요 기능
- 그래픽 채팅 패널 (사이드바/탭/새 창)
- 인라인 diff 뷰어 (accept/reject)
- @-멘션 파일 (퍼지 매칭, 줄 범위)
- 세션 히스토리 검색 및 재개
- 모델 선택기, 확장 사고 토글
- 컨텍스트 창 사용량 표시

### 키보드 단축키
Cmd/Ctrl+Esc       — 에디터/Claude 포커스 전환
Cmd/Ctrl+Shift+Esc — 새 탭에 새 대화 열기
Option/Alt+K       — 줄 번호 포함 파일 참조 삽입
Cmd/Ctrl+N         — 새 대화 시작

### VS Code 설정

설정 항목:
selectedModel     — 기본 모델 (default)
useTerminal       — CLI 인터페이스 사용 (false)
initialPermissionMode — plan|acceptEdits|bypassPermissions
preferredLocation — panel|sidebar (panel)
autosave          — 파일 읽기/쓰기 전 자동 저장 (true)
useCtrlEnterToSend — Ctrl+Enter로 전송 (false)

### CLI 연결 (외부 터미널 → IDE)
claude /ide 실행으로 현재 터미널을 VS Code에 연결

---

## JetBrains IDE 통합

지원: IntelliJ IDEA, PyCharm, Android Studio,
      WebStorm, PhpStorm, GoLand

설치: Settings → Plugins → "Claude Code" 검색
※ 설치 후 전체 IDE 재시작 필요

### 주요 기능
- 빠른 실행: Cmd+Esc (Mac) / Ctrl+Esc (Win/Linux)
- IDE diff 뷰어 통합
- 선택 영역 자동 공유
- 파일 참조: Cmd+Option+K (Mac) / Alt+Ctrl+K
- Lint 오류 자동 공유

### 원격 개발
- 로컬 클라이언트가 아닌 원격 호스트에 플러그인 설치
- Settings → Plugin (Host)

### WSL 설정
Claude command 경로: wsl -d Ubuntu -- bash -lic "claude"
"""
        },
    ];
}
