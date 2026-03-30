# Wave.Surf — 물리 서핑 게임 개발

- **날짜**: 2026-03-10
- **태그**: feature
- **작업자**: Claude

---

## 개요

아이디어 브레인스토밍 7차(`idea_20260309_7차.md`)에서 제안된 **Wave.Surf** 게임을 신규 구현함.

- **카테고리**: Games / Casual / Simulation
- **프로젝트 경로**: `Games/Casual/Wave.Surf/`

---

## 구현 내용

### 핵심 시스템

| 파일 | 역할 |
|------|------|
| `Engine/WavePhysics.cs` | 4개 사인파 합성 파도 생성, 스크롤, 세션 진행에 따른 진폭 증가 |
| `Engine/SurferPhysics.cs` | COM(무게중심) 균형 물리, Verlet 공중 포물선, 공중 회전 묘기, 착지 판정 |
| `Engine/TrickSystem.cs` | 묘기 이름/점수(360°=1000, 720°=2800, 1080°=5500), 콤보 ×0.5 누적 |
| `Engine/GameLoop.cs` | 16ms DispatcherTimer 루프 |
| `Engine/WaveTheme.cs` | 4가지 색상 테마 (열대/폭풍/일몰/오로라) |
| `Engine/ScreenCapture.cs` | 묘기 착지 성공 시 PNG 자동 캡처 (Documents\WaveSurf\Captures\) |

### 게임플레이 메커닉

- **파도**: 주파수 다른 4개 사인파 합성 → 자연스러운 불규칙 파도. 세션 시간에 따라 진폭 55→130px 점진 증가
- **균형**: ← → 키로 COM 이동. 파도 기울기가 자연스럽게 균형에 영향. 균형 > 1.05 = 와이프아웃
- **공중 묘기**: 파도 상승 속도 > 160px/s 시 자동 발사. 공중에서 ← → 로 회전 누적
- **착지 판정**: 360° 배수 ±28° 내 착지 = 클린 랜딩. 실패 시 와이프아웃
- **콤보**: 클린 랜딩 연속 시 ×0.5씩 배율 증가 (2콤보=×1.5, 3콤보=×2.0...)
- **점수**: 묘기 점수 × 콤보 배율 + 생존 점수 10pt/s

### UI/렌더링

- **캔버스 기반 렌더링**: `StreamGeometry` 파도 Path + `Polyline` 거품선 매 프레임 갱신
- **서퍼 렌더링**: WPF Canvas 하위 도형(보드+몸통+팔다리+머리) + RotateTransform
- **균형 게이지**: 서퍼 하단 80px 바, 균형에 따라 초록→빨강 색상 전환
- **와이프아웃 파티클**: 16개 Ellipse 물보라 (Gravity 적용)
- **테마**: 4종 (T키로 순환) - 열대/폭풍/일몰/오로라
- **다크 타이틀바**: DwmSetWindowAttribute attr=20

---

## 빌드 오류 수정 이력

| 오류 | 원인 | 수정 |
|------|------|------|
| `Canvas is not found` | `System.Windows.Controls` using 누락 | `using System.Windows.Controls;` 추가 |
| `ScreenX const access error` | const를 인스턴스로 접근 | `const` → property `=> 380.0` 변경 |

---

## 파일 목록

```
Games/Casual/Wave.Surf/
├── Wave.Surf.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── make_icon.ps1
├── Engine/
│   ├── GameLoop.cs
│   ├── WavePhysics.cs
│   ├── SurferPhysics.cs
│   ├── TrickSystem.cs
│   ├── WaveTheme.cs
│   └── ScreenCapture.cs
└── Resources/
    └── app.ico (16/32/48/256px)
```

---

## 솔루션/스크립트 업데이트

- `Playground.slnx`: `/Games/Casual/` 폴더에 Wave.Surf 추가
- `+publish-all.cmd`: `Games/Casual` 섹션에 추가
- `+publish.cmd`: 번호 68 (알파벳 순, Tray.Stats↔Word.Cloud 사이), Word.Cloud→69, Zip.Peek→70

---

## 조작 방법

| 키 | 기능 |
|----|------|
| `←` / `→` | 균형 조절 (파도 위) / 회전 (공중) |
| `Space` | 타이틀/게임오버에서 시작 |
| `T` | 테마 순환 |
| `F1` | 캡처 폴더 열기 |
