# 20260224_200438 | bugfix | Toast.Cast 설정창/통계창 크기 확대 + 레이아웃 수정

## 문제 1: SettingsWindow 토글버튼 클리핑 ("활성" → "활", "카운트다운" → "카우트다")

**증상**: Appearance.Button CheckBox에 `AutoSize = true` 설정 시 Korean 텍스트 측정 오류로
버튼이 1글자 폭으로만 렌더링되어 텍스트 클리핑 발생.

**근본 원인**: WinForms CheckBox의 `PreferredSize` 계산이 Appearance.Button 모드에서
Korean 텍스트(한글)를 제대로 측정하지 못함. 결과적으로 AutoSize가 최소 크기로 수렴.

**수정**: `AutoSize = true` 제거 → `Size = new Size(68, 28)` (활성), `Size = new Size(100, 28)` (카운트다운) 명시.

## 문제 2: SettingsWindow 전반적으로 너무 좁음

**수정**:
- 폼: 480×560 → 560×640
- MinimumSize: 480×480 → 560×560
- 패널: Bounds(16,56,432,400) → Bounds(16,56,528,450)
- 카드: Size(420,88) → Size(508,92)
- 활성 토글 버튼 위치: x=340 → x=428 (카드 우측 정렬)
- 카운트다운 버튼 위치: x=160,y=64 → x=172,y=62
- NumericUpDown: Bounds(80,60,64,22) → Bounds(88,62,72,24)
- 유휴 설정 행: y=468 → y=518
- 저장 버튼: Bounds(310,494,140,32) → Bounds(380,550,160,36)

## 문제 3: StatsWindow 닫기 버튼 클리핑

**증상**: 통계창 하단 "닫기" 버튼이 잘려서 표시되지 않음.

**근본 원인**: `Height = y + 80` 공식에서 +80이 DPI 차이나 OS 비클라이언트 영역(타이틀바+테두리)
높이 계산 오차로 인해 닫기 버튼이 클라이언트 영역을 벗어남.

**수정**:
- 폼 폭: 420 → 480 (여유 추가)
- Height 공식: `y + 80` → `y + 120` (40px 추가 하단 여백)
- 닫기 버튼 y: `y + 12` → `y + 20`
- stat 패널 폭: `Width - 44` → `Width - 48`
- 달성률 텍스트 위치: `panel.Width - 130` → `panel.Width - 140`

## 커밋
- `110c3bd` [toast.cast] - 설정창/통계창 크기 확대 + 레이아웃 수정

## 재발 방지 규칙

> WinForms CheckBox (Appearance.Button 모드)에서 `AutoSize = true`는 Korean 텍스트에서
> 잘못된 PreferredSize를 반환할 수 있음. **항상 `Size`를 명시적으로 지정할 것.**

> 동적 Height 계산 (`Height = y + N`) 시 하단 여백은 **최소 +100 이상** 확보할 것.
> 비클라이언트 영역(타이틀바+테두리) 크기가 DPI/OS 버전마다 다름.
