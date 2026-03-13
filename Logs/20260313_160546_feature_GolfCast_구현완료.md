# Golf.Cast — 2D 미니골프 게임 구현

**시간**: 2026-03-13 16:05 KST
**태그**: feature
**커밋**: `3fec426`

---

## 수행 작업

### 빌드 오류 수정
| 파일 | 오류 | 수정 내용 |
|------|------|-----------|
| `Views/GameCanvas.cs` | CS1537: WpfColor alias 중복 | GlobalUsings.cs에 이미 정의 → 로컬 alias 제거 |
| `Models/CourseModels.cs` | CS8858: `with` 식 불가 | `class HoleData` → `record HoleData` |
| `Services/CourseData.cs` | CS0029: `((int,int),(int,int))` → `(double,double,double,double)` 불일치 | 파라미터 타입을 `((double,double),(double,double))[]?`로 변경, foreach 분해 `((ax,ay),(bx,by))`로 수정 |
| `Views/ScoreCardWindow.xaml.cs` | CS8510: unreachable switch arm `_ => ""` | `> 0 => ...` + `_ => ...` 분기를 `_ => ...`로 통합 |

### 솔루션 등록
- `Playground.slnx`: `/Games/Sports/` 폴더에 `Golf.Cast.csproj` 추가
- `+publish.cmd`: 메뉴 #93, 선택 배포, PUBALL 섹션 모두 추가

---

## 최종 빌드 결과

```
빌드했습니다.
    경고 0개
    오류 0개
```

---

## 프로젝트 구조

```
Games/Sports/Golf.Cast/
├── Golf.Cast.csproj
├── GlobalUsings.cs
├── App.xaml / App.xaml.cs
├── Models/
│   ├── Vec2.cs              — 2D 벡터 (readonly struct)
│   └── CourseModels.cs      — HoleData(record), WallSegment, Obstacle, Ball, ScoreCard
├── Core/
│   ├── PhysicsEngine.cs     — 원/선분 충돌, 마찰, 경사, 홀 흡입
│   └── RelayCommand.cs
├── Services/
│   ├── CourseData.cs        — 18홀 × 3세트 (쉬움/보통/어려움)
│   └── GameService.cs       — DispatcherTimer 60fps 게임 루프
├── Views/
│   ├── DarkTheme.xaml       — 골프 녹색 계열 다크 테마
│   ├── GameCanvas.cs        — DrawingContext 렌더러
│   ├── MainWindow.xaml/.cs  — 마우스 드래그 조준·발사, HUD
│   ├── CourseSelectWindow.xaml/.cs
│   └── ScoreCardWindow.xaml/.cs
└── Resources/app.ico
```
