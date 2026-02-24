# 20260224_231219 | bugfix | Toast.Cast 여백 수정 + 카운트다운 팝업 레이아웃

## 수정 내용

### 1. SettingsWindow — 헤더~카드 여백 + 버튼 하단 여백

**증상**: "루틴 설정" 헤더와 첫 번째 루틴 카드 사이 여백 부족; 저장 버튼 하단 여백 부족

**수정**:
- `routinePanel.Bounds`: y=64 → **72** (+8px)
- `routinePanel.Height`: 568 → **560** (전체 창 높이 동일 유지)
- `idleLabel.Location.Y`: 648 → **656**
- `idlePanel.Location.Y`: 638 → **646**
- `btnSave Rectangle.Y`: 674 → **682**

### 2. StatsWindow — 패널 좌우 여백 대칭화

**증상**: 패널 폭이 하드코딩 518로, DPI/OS에 따라 우측 여백이 비대칭

**근본 원인**: `Bounds = new Rectangle(20, y, 518, 76)`에서 518은 Form.Width(560) 기반 계산이지만
실제 ClientSize.Width는 DPI/OS마다 다름. Windows 11에서 FixedSingle 테두리 폭 차이 발생.

**수정**:
```csharp
// 생성자에서 동적으로 계산
var panelW = ClientSize.Width - 40;  // 좌 20px = 우 20px 대칭
// CreateStatRow 호출 시 전달
Controls.Add(CreateStatRow(stat, y, panelW));
// 메서드 시그니처 변경
private Panel CreateStatRow(WeeklyRoutineStat stat, int y, int panelW)
{
    var panel = new Panel { Bounds = new Rectangle(20, y, panelW, 76), ... };
```

### 3. CountdownOverlay — 숫자/버튼 클리핑 수정

**증상**:
1. 카운트다운 숫자(52pt Bold)가 높이 80px에서 잘림
2. "건너뛰기" 버튼(120px)이 한글 텍스트 클리핑

**수정**:
| 요소 | 이전 | 이후 |
|------|------|------|
| Form Size | (360, 200) | **(360, 240)** |
| _lblCountdown Bounds | (0, 50, Width, 80) | **(0, 46, Width, 100)** |
| _lblHint Bounds | (0, 128, Width, 24) | **(0, 152, Width, 26)** |
| _btnSkip Bounds | (Width/2-60, 158, 120, 28) | **(Width/2-70, 184, 140, 34)** |

> Region 경로는 `Width`/`Height` 참조이므로 자동 반영됨.

## 커밋
- `3f85be4` [toast.cast] | 설정창·통계창 여백 + 카운트다운 팝업 레이아웃 수정

## 재발 방지 규칙

> `StatsWindow`처럼 동적으로 생성되는 패널 폭은 **`ClientSize.Width` 기반으로 계산**해야 DPI/OS 무관하게 좌우 여백 대칭 보장.
> Form.Width 기반 상수 계산은 비클라이언트 영역 폭 오차로 비대칭 발생.

> `CountdownOverlay` 같은 FormBorderStyle.None 팝업의 카운트다운 숫자 높이는 **폰트 크기(pt) × 1.4 이상** 여유 확보.
> 52pt → 최소 73px, 실제 100px 할당.
