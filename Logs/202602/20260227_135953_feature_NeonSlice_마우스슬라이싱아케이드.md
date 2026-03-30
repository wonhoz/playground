# 20260227_135953 | feature | Neon.Slice 마우스 슬라이싱 아케이드 게임 개발

## 작업 개요
idea_20260227_121600.md G-5 스펙 기반으로 Neon.Slice (마우스 슬라이싱 아케이드 게임) 신규 개발.

## 생성된 파일 목록

### 프로젝트 구조
```
Games/Arcade/Neon.Slice/
├── Neon.Slice.csproj
├── App.xaml / App.xaml.cs
├── Engine/
│   └── GameEngine.cs       ← DrawingVisualHost + 게임 루프 + 슬라이스 판정
├── Models/
│   ├── NeonShape.cs         ← ShapeType enum, NeonShape (물리 기반 도형)
│   ├── Particle.cs          ← 파티클 이펙트
│   ├── SlicedHalf.cs        ← 슬라이스된 반쪽 도형
│   └── GameResult.cs        ← GameMode enum, GameResult, HighScoreData
├── Services/
│   └── HighScoreService.cs  ← 최고 점수 JSON 저장/로드
├── MainWindow.xaml          ← 메뉴/게임/일시정지/게임오버 화면
├── MainWindow.xaml.cs
└── Resources/
    └── app.ico              ← 네온 칼날 컨셉 사이버펑크 아이콘
```

### 수정된 기존 파일
- `Playground.slnx` — /Games/Arcade/에 Neon.Slice.csproj 추가
- `+publish-all.cmd` — Neon.Slice 배포 항목 추가

## 주요 구현 내용

### 게임 메커닉
- **마우스 슬라이스 판정**: `LineCircleIntersect()` — 선분-원 교차 감지 (지난 6프레임 평균 궤적)
- **물리 기반 도형**: 포물선 솟아오름 (Vy -= 480~660px/s, Gravity = 420px/s²)
- **슬로모션**: 슬로우팩터 0.25 적용, 콤보 5회 → 피버(1.5초), 얼음 도형 → 3초
- **도형 반쪽 분리**: SlicedHalf 2개 생성, 수평 ±60~100px/s 반발
- **파티클 폭발**: 8~16개 방사형, 비선형 페이드(Life²)

### 특수 도형
| 도형 | 효과 |
|------|------|
| Bomb | 감점 -15, 콤보 리셋 |
| Lightning | 화면 모든 도형 클리어 + 각 5점 |
| Ice | 슬로모션 3초 |
| Star | 보너스 +10점, 콤보 증가 |

### 게임 모드
| 모드 | 설명 |
|------|------|
| Classic | 목숨 3개, 도형 놓칠 때마다 감소 |
| TimeAttack | 60초 제한, 제한 시간 내 최대 점수 |
| Zen | 30개 슬라이스 내 최대 콤보/점수 |

### 렌더링 (DrawingVisual 기반)
- `DrawingVisualHost : FrameworkElement` — MeasureOverride/ArrangeOverride 크기 채움
- `CompositionTarget.Rendering` 게임 루프 — 실제 프레임 dt 계산 (max 50ms 클램핑)
- 격자 배경 (사이버펑크 느낌)
- 마우스 궤적 점진적 페이드 (6프레임, 알파 80→250)
- 도형 글로우: RadialGradientBrush 이중 레이어
- 슬로모션 오버레이: 핑크(피버) / 시안(얼음) 반투명 파동

### UI 화면
- 메뉴: 타이틀(네온 글로우), 모드 선택 버튼, 최고 점수 3종
- HUD: 점수/콤보/슬라이스/목숨-타이머-슬라이스잔여 + 최고점수
- 일시정지: ESC 키, 반투명 오버레이
- 게임오버: 통계 카드, NEW BEST 뱃지, 다시하기/메뉴

## 빌드 결과
- ✅ 빌드 성공 (경고 0, 오류 0)
- 수정 오류: LetterSpacing 미지원 (MC3072) → 제거

## 기술 스택
- .NET 8.0, WPF, DrawingVisual, CompositionTarget.Rendering
- System.Text.Json (최고 점수 저장)
- DwmSetWindowAttribute (다크 타이틀바)
