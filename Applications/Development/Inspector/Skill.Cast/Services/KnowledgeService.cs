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
- **전역**: `~/.claude/commands/*.md`
- **프로젝트**: `.claude/commands/*.md`
- **네임스페이스**: `~/.claude/commands/git/commit.md` → `/git:commit`

## Frontmatter 필드

```yaml
---
description: 명령어 설명 (60자 이내, /help에 표시)
allowed-tools: Read, Write, Edit, Bash(git:*)
model: haiku | sonnet | opus
argument-hint: [arg1] [arg2] [optional]
disable-model-invocation: true   # 사용자만 호출 가능
---
```

## 동적 컨텍스트 주입

```markdown
현재 Git 상태: !`git status --short`
변경 파일: !`git diff --name-only`
```

`!` 뒤의 백틱 명령은 Claude가 보기 전에 실행되어 결과로 대체됩니다.

## $1, $2 인자 사용

```markdown
---
argument-hint: [branch-name] [message]
---

`$1` 브랜치로 전환하고 `$2` 메시지로 커밋하세요.
```

## 예시: Git 커밋 커맨드

```markdown
---
description: Git 커밋 생성
allowed-tools: Bash(git:*)
argument-hint: [commit-message]
---

현재 변경사항: !`git diff --stat`

$1 메시지로 스테이징된 파일을 커밋하세요.
```

## 네임스페이스 구조

```
.claude/commands/
├── commit.md          → /commit
├── deploy.md          → /deploy
└── workflows/
    ├── feature.md     → /workflows:feature
    └── hotfix.md      → /workflows:hotfix
```
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
```
.claude/skills/
└── my-skill/
    ├── SKILL.md           ← 필수 (메인 지침)
    ├── templates/         ← 참조할 템플릿 파일들
    ├── scripts/           ← 실행할 스크립트
    └── examples/          ← 참조 예시
```

## SKILL.md Frontmatter

```yaml
---
name: skill-name
description: 이 스킬의 역할 (Claude가 언제 사용할지 판단)
disable-model-invocation: true   # 사용자만 호출 가능 (부작용 있을 때)
user-invocable: false            # Claude만 호출 (배경 지식으로만)
allowed-tools: Read, Grep, Glob
context: fork                    # 격리된 서브에이전트에서 실행
agent: Explore                   # fork 시 사용할 에이전트 타입
---
```

## 호출 제어

| 설정 | 사용자 | Claude | 용도 |
|------|--------|--------|------|
| (기본) | ✓ | ✓ | 범용 스킬 |
| `disable-model-invocation: true` | ✓ | ✗ | 배포/전송 등 부작용 |
| `user-invocable: false` | ✗ | ✓ | 배경 지식, 컨벤션 |

## $ARGUMENTS 사용

```markdown
---
name: api-doc
description: API 엔드포인트 문서 생성
---

$ARGUMENTS 경로의 엔드포인트를 분석하고 문서를 생성하세요.
```

## 동적 컨텍스트

```markdown
## 현재 Git 상태
- 브랜치: !`git branch --show-current`
- 마지막 커밋: !`git log -1 --oneline`
```

## 예시: 프로젝트 컨벤션 스킬

```markdown
---
name: project-conventions
description: 이 프로젝트의 코딩 스타일. 코드 작성/검토 시 자동 적용.
user-invocable: false
---

## 네이밍 규칙
- 클래스: PascalCase
- 변수: camelCase
- 상수: UPPER_SNAKE_CASE

## 금지 사항
- `any` 타입 사용 금지
- console.log 프로덕션 코드 금지
```
"""
        },
        new()
        {
            Title = "Memory 시스템 가이드",
            Icon = "💾",
            Category = "핵심",
            Content = """
# Memory (메모리) 시스템 가이드

## 메모리 타입

| 타입 | 설명 | 저장 시기 |
|------|------|----------|
| `user` | 사용자 역할, 목표, 전문성 | 사용자 정보를 알게 됐을 때 |
| `feedback` | 수정 요청, 행동 교정 지침 | 사용자가 접근 방식 수정 요청 시 |
| `project` | 진행 중 작업, 결정사항 | 작업 컨텍스트, 마감일 등 |
| `reference` | 외부 시스템 포인터 | 외부 리소스 위치 학습 시 |

## 파일 구조

```
~/.claude/projects/<encoded-path>/
└── memory/
    ├── user_role.md
    ├── feedback_terse_responses.md
    ├── project_merge_freeze.md
    └── reference_linear_pipeline.md
```

**MEMORY.md** — 인덱스 파일 (200줄 이내 유지)

## memory 파일 형식

```markdown
---
name: 메모리 이름
description: 한 줄 설명 (미래 대화에서 관련성 판단에 사용)
type: user | feedback | project | reference
---

메모리 내용

**Why:** 이유 (feedback/project 타입)
**How to apply:** 적용 방법
```

## feedback 메모리 구조

```markdown
---
name: no_db_mocks
description: 테스트에서 데이터베이스를 목(mock)으로 대체하지 말 것
type: feedback
---

통합 테스트는 실제 DB를 사용해야 함.

**Why:** 이전에 mock/prod 불일치로 마이그레이션 실패를 숨긴 적 있음.
**How to apply:** 테스트 코드 작성 시 항상 실제 DB 연결 사용.
```

## 저장하지 말아야 할 것
- 코드 패턴, 아키텍처 (코드 자체에서 파생 가능)
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

```
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
```

## plugin.json 필드

```json
{
  "name": "my-plugin",           // 필수, kebab-case
  "version": "1.0.0",            // 시맨틱 버전
  "description": "플러그인 설명",
  "author": {
    "name": "개발자 이름",
    "email": "email@example.com"
  },
  "license": "MIT",
  "keywords": ["tag1", "tag2"],
  "commands": "./commands",      // 기본값
  "agents": "./agents",          // 기본값
  "hooks": "./hooks/hooks.json", // 기본값
  "mcpServers": "./.mcp.json"   // 기본값
}
```

## 플러그인 설치

```bash
/plugin install <plugin-name>
/plugin list
/plugin remove <plugin-name>
```

## 마켓플레이스

공식 플러그인은 `~/.claude/plugins/marketplaces/claude-plugins-official/` 에 저장됩니다.

### 유용한 공식 플러그인
- `plugin-dev` — 플러그인 개발 도구 (스킬, 커맨드, 에이전트 개발 가이드)
- `commit-commands` — Git 커밋 워크플로우
- `feature-dev` — 기능 개발 엔드-투-엔드 워크플로우
- `hookify` — 자동화 규칙 생성 도구
- `code-review` — 코드 리뷰 자동화
- `pr-review-toolkit` — PR 리뷰 도구킷
- `claude-code-setup` — Claude Code 환경 설정 추천

## Path 규칙
- 상대 경로만 허용 (`./`로 시작)
- `../` 부모 디렉토리 불가
- 슬래시만 사용 (Windows도 동일)
"""
        },
        new()
        {
            Title = "Hooks 설정 가이드",
            Icon = "🪝",
            Category = "자동화",
            Content = """
# Hooks (훅) 설정 가이드

## 훅 타입

| 이벤트 | 설명 |
|--------|------|
| `PreToolUse` | 도구 실행 전 |
| `PostToolUse` | 도구 실행 후 |
| `Notification` | Claude 알림 시 |
| `Stop` | 세션 종료 시 |

## hooks.json 구조

```json
{
  "PreToolUse": [
    {
      "matcher": "Write",
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
          "command": "prettier --write $CLAUDE_TOOL_INPUT_FILE_PATH"
        }
      ]
    }
  ]
}
```

## 매처(Matcher)

- `"Write"` — Write 도구만
- `"Edit|Write"` — Edit 또는 Write
- `"Bash(git:*)"` — git 명령 Bash
- `"*"` — 모든 도구

## 환경 변수

| 변수 | 설명 |
|------|------|
| `$CLAUDE_TOOL_INPUT_FILE_PATH` | 편집/생성 중인 파일 경로 |
| `$CLAUDE_TOOL_INPUT_COMMAND` | 실행 중인 bash 명령 |
| `$CLAUDE_PLUGIN_ROOT` | 플러그인 루트 디렉토리 |

## 실용 예시

### 파일 저장 후 자동 포맷

```json
{
  "PostToolUse": [{
    "matcher": "Edit|Write",
    "hooks": [{
      "type": "command",
      "command": "prettier --write \"$CLAUDE_TOOL_INPUT_FILE_PATH\" 2>/dev/null || true"
    }]
  }]
}
```

### 민감 파일 보호

```json
{
  "PreToolUse": [{
    "matcher": "Write|Edit",
    "hooks": [{
      "type": "command",
      "command": "bash -c 'if [[ \"$CLAUDE_TOOL_INPUT_FILE_PATH\" == *\".env\"* ]]; then echo \"BLOCKED: .env 파일 수정 금지\" >&2; exit 1; fi'"
    }]
  }]
}
```

### 알림 스크립트 자동 실행 (Notification 이벤트)

```json
{
  "Notification": [{
    "matcher": "*",
    "hooks": [{
      "type": "command",
      "command": "powershell -File notify.ps1"
    }]
  }]
}
```
"""
        },
        new()
        {
            Title = "MCP Servers 가이드",
            Icon = "🌐",
            Category = "통합",
            Content = """
# MCP (Model Context Protocol) Servers 가이드

## .mcp.json 구조

```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_TOKEN": "${GITHUB_TOKEN}"
      }
    },
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path/to/files"]
    },
    "sqlite": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sqlite", "--db-path", "database.db"]
    }
  }
}
```

## 서버 타입

| 타입 | 설명 |
|------|------|
| `stdio` | 표준 I/O (기본) |
| `sse` | Server-Sent Events |
| `http` | HTTP 엔드포인트 |

## 추천 MCP 서버

| 서버 | 설치 | 기능 |
|------|------|------|
| `@modelcontextprotocol/server-github` | npx | GitHub API |
| `@modelcontextprotocol/server-filesystem` | npx | 파일 시스템 접근 |
| `@modelcontextprotocol/server-sqlite` | npx | SQLite DB |
| `@modelcontextprotocol/server-brave-search` | npx | 웹 검색 |
| `context7` | npx | 최신 라이브러리 문서 |
| `@playwright/mcp` | npx | 브라우저 자동화 |

## 설정 위치

```
~/.claude/.mcp.json           ← 전역 (모든 프로젝트)
프로젝트/.claude/.mcp.json   ← 프로젝트별
```

## 인증

```json
{
  "mcpServers": {
    "my-api": {
      "command": "node",
      "args": ["server.js"],
      "env": {
        "API_KEY": "${MY_API_KEY}"
      }
    }
  }
}
```

환경 변수는 `${VAR_NAME}` 형식으로 참조합니다.
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

```json
{
  "statusLine": {
    "type": "command",
    "command": "~/.claude/my-statusline.sh"
  }
}
```

## 입력 JSON 구조

Claude Code가 스크립트에 stdin으로 전달하는 데이터:

```json
{
  "workspace": {
    "current_dir": "/path/to/project"
  },
  "model": {
    "display_name": "Claude Sonnet 4.6",
    "version": "claude-sonnet-4-6"
  },
  "session_id": "uuid-string",
  "version": "1.x.x",
  "output_style": {
    "name": "default"
  }
}
```

## 최소 statusline 스크립트

```bash
#!/bin/bash
input=$(cat)

dir=$(echo "$input" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('workspace',{}).get('current_dir','?'))")
model=$(echo "$input" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('model',{}).get('display_name','Claude'))")

echo "📁 $dir  🤖 $model"
```

## 컨텍스트 사용량 표시 (세션 파일 파싱)

```bash
session_file=$(find ~/.claude/projects -name "${session_id}.jsonl" | head -1)
latest_tokens=$(tail -20 "$session_file" | jq -r '.message.usage | (.input_tokens // 0)' | tail -1)
pct=$(( latest_tokens * 100 / 200000 ))
echo "🧠 Context: ${pct}%"
```

## 인기 도구

| 도구 | 설명 | 설치 |
|------|------|------|
| `cc-statusline` | npm 패키지, 테마 지원 | `npm i -g @chongdashu/cc-statusline` |
| `ccusage` | 비용/토큰 추적 | `npm i -g ccusage` |

### cc-statusline 설치

```bash
npx @chongdashu/cc-statusline create
# → ~/.claude/statusline-command.sh 생성됨
```

settings.json에 다음 추가:
```json
{
  "statusLine": {
    "type": "command",
    "command": "~/.claude/statusline-command.sh"
  }
}
```
"""
        },
        new()
        {
            Title = "settings.json 레퍼런스",
            Icon = "⚙️",
            Category = "환경",
            Content = """
# settings.json 완전 레퍼런스

## 파일 위치

```
~/.claude/settings.json           ← 전역 (사용자 설정)
~/.claude/settings.local.json     ← 로컬 개인 설정 (git 제외)
프로젝트/.claude/settings.json   ← 프로젝트 공유 설정
```

## 전체 구조

```json
{
  "statusLine": {
    "type": "command",
    "command": "~/.claude/my-statusline.sh"
  },
  "permissions": {
    "allow": ["Bash(git:*)", "Read", "Write"],
    "deny": ["Bash(rm:*)"]
  },
  "hooks": {
    "PostToolUse": [{
      "matcher": "Edit|Write",
      "hooks": [{
        "type": "command",
        "command": "prettier --write \"$CLAUDE_TOOL_INPUT_FILE_PATH\""
      }]
    }]
  },
  "env": {
    "MY_CUSTOM_VAR": "value"
  }
}
```

## statusLine 옵션

```json
"statusLine": {
  "type": "command",       // "command" | "text"
  "command": "~/.claude/statusline.sh",
  "text": "정적 텍스트"   // type이 "text"일 때
}
```

## permissions 예시

```json
"permissions": {
  "allow": [
    "Bash(git:*)",
    "Bash(npm:*)",
    "Read",
    "Write",
    "Edit"
  ],
  "deny": [
    "Bash(rm -rf:*)"
  ]
}
```

## 우선순위 (낮음 → 높음)

1. `~/.claude/settings.json` (전역)
2. 프로젝트 `CLAUDE.md` 지침
3. `.claude/settings.json` (프로젝트)
4. `~/.claude/settings.local.json` (로컬 개인)
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

```
~/.claude/keybindings.json
```

## 기본 구조

```json
[
  {
    "key": "ctrl+shift+r",
    "command": "workbench.action.reloadWindow"
  }
]
```

## 현재 keybindings.json 형식

```json
[
  {
    "key": "ctrl+enter",
    "command": "editor.action.insertNewlineAbove"
  },
  {
    "key": "shift+enter",
    "command": "send-message"
  }
]
```

## 주요 커맨드 ID

| 커맨드 | 설명 |
|--------|------|
| `send-message` | 메시지 전송 |
| `cancel-request` | 요청 취소 |
| `clear-chat` | 채팅 초기화 |
| `new-session` | 새 세션 시작 |
| `toggle-auto-accept` | 자동 승인 토글 |

## 코드 키 수식어

- `ctrl` — Control
- `shift` — Shift
- `alt` — Alt/Option
- `meta` — Windows/Command 키

## 코드 키 조합 예시

```json
[
  {
    "key": "ctrl+shift+enter",
    "command": "send-message"
  },
  {
    "key": "escape",
    "command": "cancel-request"
  }
]
```

## 코드 키 수정 방법

`/keybindings-help` 스킬을 사용하거나 직접 JSON 파일을 수정합니다.
"""
        },
    ];
}
