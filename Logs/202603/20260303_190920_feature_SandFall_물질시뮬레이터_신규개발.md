# Sand.Fall — Falling Sand 물질 시뮬레이터 신규 개발

- **날짜**: 2026-03-03
- **태그**: feature
- **상태**: 완료

---

## 개요

셀룰러 오토마타 기반 Falling Sand 물질 시뮬레이터.
모래·물·불·기름·증기·얼음·씨앗·식물·산 등 12종 물질의 상호 반응을 실시간 시뮬레이션.
WPF WriteableBitmap + unsafe 픽셀 직접 조작으로 고성능 렌더링.

---

## 구현 항목

| 파일 | 설명 |
|------|------|
| `Sand.Fall.csproj` | net10.0-windows, WPF, AllowUnsafeBlocks |
| `GlobalUsings.cs` | WPF 전역 using |
| `Sim/Material.cs` | 12종 물질 열거형 + 색상 + 분류 + 반응 정의 |
| `Sim/SimGrid.cs` | 320×200 셀룰러 오토마타 그리드 + 물질별 업데이트 규칙 |
| `App.xaml/cs` | 다크 테마 (GitHub Dark) |
| `MainWindow.xaml` | 시뮬레이션 Image + 우측 제어 패널 |
| `MainWindow.xaml.cs` | WriteableBitmap 렌더링 + 마우스 브러시 입력 |
| `gen-icon.ps1` | 모래/물/불/얼음 복합 아이콘 생성 |

---

## 핵심 기술

### 셀룰러 오토마타 업데이트 순서
```
아래에서 위로 행 순회 (중력 핵심)
+ 각 행에서 랜덤 좌-우 방향 (모래 편향 제거)
+ Updated[] 플래그로 이중 처리 방지
```

### 물질별 행동 규칙
| 물질 | 행동 |
|------|------|
| 모래/재 | Powder: 아래·대각선 낙하, 액체 위에 뜸 |
| 물 | Liquid: 아래+수평 흐름 3방향 |
| 기름 | Liquid: 물보다 가벼워 물 위로 뜸 |
| 불 | 수명 감소→재, 인근 가연성 점화, 물→증기, 얼음→물 |
| 증기 | Gas: 위로 상승, 수명 감소 시 물 or Empty |
| 씨앗 | Powder + 물에 닿으면 식물 성장 |
| 식물 | 물 흡수해서 상단으로 성장 |
| 산 | Liquid + 비-산 고체 용해 |
| 얼음 | 인근 물을 얼림, 불에 녹으면 물 |
| 돌/나무 | Solid (정적, 나무는 가연성) |

### WriteableBitmap 렌더링 (unsafe)
```csharp
_bitmap.Lock();
unsafe {
    for (int y = 0; y < H; y++)
    for (int x = 0; x < W; x++)
        *((uint*)_bitmap.BackBuffer + y * stride + x) = pixels[...];
}
_bitmap.AddDirtyRect(new Int32Rect(0, 0, W, H));
_bitmap.Unlock();
```

### 성능
- 그리드: 320 × 200 = 64,000 셀
- 렌더링: CompositionTarget.Rendering (모니터 주사율)
- 시뮬레이션 속도: ×1~×4 배속 슬라이더
- NearestNeighbor 스케일링으로 픽셀아트 스타일

---

## 물질 반응 체계
- 🔥 기름 + 불 → 불
- 🔥 나무 + 불 → 불
- 🔥 식물 + 불 → 불
- 💧 불 + 물 → 증기
- ❄️ 불 + 얼음 → 물
- 🌱 씨앗 + 물 → 식물
- 🧪 산 + 돌/나무/얼음 → Empty (용해)
- ❄️ 얼음 → 주변 물 동결

---

## 빌드 결과
```
경고 0개, 오류 0개 ✅
```
