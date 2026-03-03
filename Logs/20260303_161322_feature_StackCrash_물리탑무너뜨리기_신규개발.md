# Stack.Crash — 물리 탑 무너뜨리기 퍼즐 게임 신규 개발

- **날짜**: 2026-03-03
- **태그**: feature
- **상태**: 완료

---

## 개요

블록으로 쌓인 탑을 제한된 횟수 내에 클릭·제거하여 물리 시뮬레이션으로 무너뜨리는 퍼즐 게임.
Angry Birds + Bridge Constructor 스타일. nkast.Aether.Physics2D (Box2D 포트) 기반.

---

## 구현 항목

### 인프라
| 파일 | 설명 |
|------|------|
| `Stack.Crash.csproj` | net10.0-windows, WPF, nkast.Aether.Physics2D 2.0.0 |
| `GlobalUsings.cs` | WPF + Aether 전역 using, AetherVector2 별칭 |
| `Models/BlockMaterial.cs` | 6개 재질 정의 (나무/돌/금속/얼음/유리/폭발물) + 밀도/마찰/반발 |
| `Models/LevelDef.cs` | BlockDef record, LevelDef record |
| `Levels/LevelData.cs` | 8개 레벨 정의 (단순탑 → 성채) |
| `Game/GameBlock.cs` | Body + WPF Grid 비주얼 + SyncVisual() |
| `Game/GameEngine.cs` | World, Step, RemoveBlock, TriggerExplosion, CheckWin |

### UI
| 파일 | 설명 |
|------|------|
| `App.xaml` | 다크 테마 (GitHub Dark 계열, 산호 액센트 #F78166) |
| `App.xaml.cs` | Application 엔트리 |
| `MainWindow.xaml` | 툴바 + Canvas + 우측 패널 (레벨 정보·별 조건·재질 범례·결과) |
| `MainWindow.xaml.cs` | 게임 루프 (DispatcherTimer 60fps) + 블록 클릭 + 승패 판정 |
| `gen-icon.ps1` + `Resources/app.ico` | 무너지는 탑 + 폭발 아이콘 |

### 등록
- `Playground.slnx` 등록 (`/Games/Puzzle/`)
- `+publish.cmd` 메뉴 #42, 선택, PUBALL 섹션 추가
- `+publish-all.cmd` 항목 추가

---

## 핵심 기술 결정

### 물리 좌표 변환
```csharp
// Physics Y-up → Screen Y-down
screenX = physX * PPM + canvasCenterX   // PPM = 60
screenY = groundScreenY - physY * PPM
// 회전: Physics CCW + Y-flip → WPF CW (부호 그대로)
wpfAngle = body.Rotation * (180 / π)
```

### 폭발물 연쇄
```csharp
void TriggerExplosion(AetherVector2 center, ...) {
    // 반경 2.5m 내 블록에 방사형 임펄스
    // 맞은 폭발물 블록 → 재귀 호출 (연쇄 폭발)
}
```

### 승리 조건
- 전체 블록의 75% 이상이 Y < 0.3m (지면 근처)로 붕괴
- 이동 수 초과 시 패배

### nkast.Aether.Physics2D 2.0.0 주의사항
- 네임스페이스: `nkast.Aether.Physics2D.Dynamics` (tainicom 아님)
- `World.Step(float dt)` — 인수 1개 (3개 오버로드 없음)
- `Body.CreateRectangle(w, h, density, offset)` — 전체 크기 기준

---

## 레벨 구성 (8개)

| # | 이름 | 특징 | 최대 이동 |
|---|------|------|----------|
| 1 | 단순 탑 | 나무 5층 | 5 |
| 2 | 두 탑 | 나무+돌 연결 빔 | 4 |
| 3 | 피라미드 | 돌 10블록 | 5 |
| 4 | 얼음 탑 | 미끄러운 얼음 | 4 |
| 5 | 폭발물 코어 | 연쇄 폭발 | 3 |
| 6 | 혼합 구조 | 재질 혼합 + 유리 | 5 |
| 7 | 기울어진 탑 | 불안정 배치 | 3 |
| 8 | 성채 | 금속+폭발물 숨김 | 6 |

---

## 빌드 결과
```
경고 0개, 오류 0개 ✅
```

---

## 조작법
| 입력 | 기능 |
|------|------|
| 블록 클릭 | 블록 제거 |
| ▶ 시뮬레이션 | 물리 시뮬레이션 시작/일시정지 |
| ↺ 리셋 | 현재 레벨 재시작 |
| 레벨 콤보박스 | 레벨 선택 |
