# 작업 로그 — Dodge.Craft / Key.Test / Shortcut.Forge

| 항목 | 내용 |
|------|------|
| **일시** | 2026-03-11 ~ 2026-03-12 (KST) |
| **태그** | feature |
| **커밋** | ff7c1bc |
| **작업자** | Claude Sonnet 4.6 |

---

## 구현 프로젝트

### 1. Dodge.Craft (`Games/Shooter/Dodge.Craft`)

**장르**: 탄막 회피 + 방어 구조물 배치 하이브리드 슈터

| 파일 | 역할 |
|------|------|
| `Engine/GameLoop.cs` | 16ms DispatcherTimer 게임 루프 |
| `Engine/BulletPatterns.cs` | 4종 탄막 패턴 (Radial/Aimed/Wave/Spiral), 추적탄·각속도탄 |
| `Entities/Bullet.cs` | 탄환 물리 (추적, 각속도, 관통, HitTest) |
| `Entities/Structure.cs` | 4종 구조물 (Wall/Mirror/Fan/Bomb) + 상호작용 로직 |
| `Entities/Enemy.cs` | 순찰 이동, 벽 반사, 발사 쿨다운 |
| `Entities/Particle.cs` | 파티클 버스트, 알파 페이드, 마찰 감속 |
| `MainWindow.xaml` | 680×760 Canvas HUD, 팔레트 패널, Title/GameOver 오버레이 |
| `MainWindow.xaml.cs` | 마우스 이동 플레이어, 구조물 배치(코스트 시스템), 웨이브 관리 |

**핵심 기능**:
- 구조물 4종: Wall(차단/내구도 5), Mirror(반사/1회), Fan(편향/내구도 3), Bomb(흡수/폭발)
- 반사된 탄환이 적에게 역타격 (VY < 0 체크)
- 웨이브마다 탄막 패턴 강화
- 무적 타이머 2초, 구조물 슬롯 최대 5개

---

### 2. Key.Test (`Applications/System/Key.Test`)

**용도**: 저수준 키보드 훅 기반 진단 도구

| 파일 | 역할 |
|------|------|
| `MainWindow.xaml` | 640×280 키보드 Canvas + 우측 정보 패널 (현재 키/통계/이벤트 로그) |
| `MainWindow.xaml.cs` | WH_KEYBOARD_LL 훅, ANSI 104키 레이아웃, 채터링/고착/NKRO 감지 |

**핵심 기능**:
- `SetWindowsHookEx(WH_KEYBOARD_LL)` 전역 키보드 훅
- **채터링 감지**: 동일 키 < 50ms 재입력
- **고착 감지**: 키 누름 > 3000ms 유지
- **NKRO 측정**: `_pressedKeys` HashSet으로 동시 입력 수 추적
- 104키 ANSI 레이아웃 Canvas 시각화 (정상=녹색, 문제=빨간색)
- HTML 리포트 내보내기

---

### 3. Shortcut.Forge (`Applications/Files/Manager/Shortcut.Forge`)

**용도**: Windows .lnk 바로가기 일괄 생성·관리

| 파일 | 역할 |
|------|------|
| `Models/ShortcutEntry.cs` | 바로가기 모델 (INotifyPropertyChanged, StatusBrush/Text) |
| `Services/ShellLinkService.cs` | IShellLink COM (Create/Load/Save/Delete) |
| `Services/IconExtractor.cs` | Shell32.ExtractIconEx + SHGetFileInfo 폴백 |
| `Services/ShortcutScanner.cs` | 폴더 .lnk 재귀 스캔, 깨진 링크 감지 |
| `MainWindow.xaml` | 목록(좌) + 편집 폼(우) 레이아웃 |
| `MainWindow.xaml.cs` | CRUD, 일괄 생성 다이얼로그, 빠른 위치 ComboBox |

**핵심 기능**:
- IShellLink COM 인터페이스 직접 구현 (WshShell 대안)
- ExtractIconEx / SHGetFileInfo로 대상 파일 아이콘 추출
- 깨진 링크 자동 감지 (BrokenTarget / BrokenIcon / Missing)
- 일괄 생성: 파일 경로 목록 → .lnk 일괄 생성
- 빠른 위치: 바탕화면, 시작 메뉴, Programs 등 미리 등록

---

## 솔루션 변경

- `Playground.slnx` — Dodge.Craft, Key.Test, Shortcut.Forge 등록
- `+publish.cmd` — 3개 항목 추가 (총 83개, 번호 재정렬)
- `+publish-all.cmd` — 3개 항목 추가

## 빌드 결과

```
Dodge.Craft  → 경고 0, 오류 0 ✅
Key.Test     → 경고 0, 오류 0 ✅
Shortcut.Forge → 경고 1 (미사용 필드), 오류 0 ✅
```

## 해결한 오류

| 오류 | 원인 | 해결 |
|------|------|------|
| CS0426 `StructureType` not in `Structure` | enum이 클래스 밖에 선언됨 | enum을 `Structure` 클래스 내부로 이동 |
| CS7065 Win32 리소스 오류 (ICO) | ICO 바이너리 포맷 오류 | 기존 성공한 ICO 파일 복사로 대체 |
