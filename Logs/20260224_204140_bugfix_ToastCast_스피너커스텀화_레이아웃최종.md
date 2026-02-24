# 20260224_204140 | bugfix | Toast.Cast 스피너 커스텀화 + 카드 레이아웃 최종 수정

## 문제: NumericUpDown 다크 테마 불일치 + "카운트다운" 텍스트 클리핑

**증상**:
1. 설정창 루틴 카드의 간격(분) 스피너(NumericUpDown)의 ▲▼ 화살표 버튼이 OS 시스템 테마(밝은 색)로 렌더링되어 다크 UI와 이질감
2. "카운트다운" CheckBox 버튼이 Size(100,28)에서도 여전히 텍스트 클리핑 ("카우트다" 표시)

**근본 원인**:
1. WinForms `NumericUpDown`의 텍스트 입력 영역은 `BackColor`/`ForeColor`로 스타일 가능하지만,
   Up/Down 스핀 버튼(▲▼)은 OS GDI 렌더러가 직접 그림 → 어떤 속성 설정으로도 색상 변경 불가
2. `Appearance.Button` CheckBox에서 명시적 Font 없이 폼 기본 폰트(9.5f)를 상속받을 때
   Size(100,28)이 "카운트다운" 텍스트에 여전히 부족 (폰트 크기 + 패딩 계산 오차)

## 해결

### 1. CreateDarkSpinner — 커스텀 다크 스피너
`NumericUpDown` 완전 제거, `[ − | val | + ]` 구성의 커스텀 Panel 컨트롤로 대체:

```csharp
private static (Panel panel, Func<int> getValue) CreateDarkSpinner(
    int min, int max, int initial, Action<int> onChange)
{
    // Panel(92×26) + btnMinus(26×24) + lblVal(38×26) + btnPlus(26×24)
    // panel.Paint: 외곽 보더 + 구분선 2개 직접 그림
}
```

- **적용 위치**: 루틴 카드 "간격(분)" 스피너 + 하단 "유휴 감지 기준(분)" 스피너
- OS 의존 요소 완전 제거, 다크 배경(#181826) + 버튼(#282838) + 구분선 Paint 구현
- 값 변경은 `Action<int> onChange` 콜백으로 즉시 모델 반영
- `btnSave.Click`에서 `getIdleValue()` 클로저로 최종값 읽기

### 2. 카드 레이아웃 조정
- 카드 높이: 92 → **104px** (하단 행 수용 여백 확보)
- 하단 행 (간격 + 카운트다운) y: 62 → **74~76**
- `chkCountdown`: Size(100,28) → **Size(120,28)** + `Font("Segoe UI", 9f)` 명시
- `chkEnabled`: `Font("Segoe UI", 9f)` 명시
- `lblDesc`: Size(370,36) → **Size(376,34)**, y: 34 → 38

### 3. 창 하단 여백 조정
- 유휴 설정 행 y: 518 → 530 (idleLabel), 514 → 526 (idlePanel)
- btnSave: y=550 → 568, 높이 36 → 38

## 커밋
- `c75a575` [toast.cast] - 스피너 커스텀화(NumericUpDown → 다크 스피너) + 카드 레이아웃 최종 수정

## 재발 방지 규칙

> WinForms `NumericUpDown`의 스핀 버튼은 OS 렌더링 → 다크 테마 불가.
> 다크 스피너가 필요하면 **`[ − | val | + ]` 커스텀 Panel + Button + Label** 조합으로 구현할 것.

> `Appearance.Button` CheckBox의 텍스트 크기 측정은 폰트에 민감.
> **명시적 `Font` + 충분한 `Size`** 지정 필수 (자동 계산 신뢰 금지).
