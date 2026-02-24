# 20260224_184023 | bugfix | Log.Lens XamlParseException + Toast.Cast CheckBox 수정

## 문제 1: Log.Lens 시작 시 XamlParseException

**증상**: Log.Lens 실행 시 `System.Windows.Markup.XamlParseException` 발생으로 앱이 실행되지 않음.

**근본 원인**: 이전 세션에서 `Window.Resources`의 `x:Key="DarkCheckBox"` 스타일을 삭제했으나,
`MainWindow.xaml` 140번 줄의 `Style="{StaticResource DarkCheckBox}"` 참조를 제거하지 않음.
StaticResource는 XAML 파싱 시점에 리소스를 찾고, 없으면 즉시 XamlParseException 발생.

**수정**: `MainWindow.xaml` 140번 줄 ChkRegex에서 Style 속성 제거.
App.xaml 전역 CheckBox 스타일(ControlTemplate 포함)이 자동 적용됨.

**커밋**: `5b37d83`

---

## 문제 2: Toast.Cast SettingsWindow CheckBox 흰 박스 (다크 테마 불일치)

**증상**: 설정 창의 "활성", "카운트다운" CheckBox가 다크 카드 위에 OS 기본 흰색 사각형으로 표시.
`FlatStyle.Flat` + `FlatAppearance.CheckedBackColor` 설정에도 불구하고 흰 박스 지속.

**근본 원인**: WinForms CheckBox의 체크박스 지시자(사각형) 영역은 `FlatStyle.Flat` 여부와 무관하게
OS 시스템 렌더러(GDI)가 그림. `BackColor`/`FlatAppearance` 설정은 컨트롤 배경 전체에만 적용되며,
내부 체크박스 사각형의 흰색 배경을 제거할 수 없음.

**수정**: `Appearance = Appearance.Button` 추가.
- OS 체크박스 사각형 완전 제거
- 토글 버튼 스타일로 동작 (눌린 상태 = 체크됨)
- `FlatAppearance.CheckedBackColor` = 다크 그린 (#1C5A3A) — 활성 상태 시각적 구분
- `FlatAppearance.BorderSize = 1` — 명확한 버튼 경계

**커밋**: `3797da5`

---

## 재발 방지 규칙

> WinForms CheckBox에서 OS 사각형을 완전히 제거하려면 **반드시 `Appearance = Appearance.Button`** 사용.
> `FlatStyle.Flat`만으로는 체크박스 사각형 배경을 다크 테마로 바꿀 수 없음.

> XAML StaticResource 키를 삭제할 때 **모든 참조(Style="{StaticResource ...}")를 동시에 제거**할 것.
> 참조 누락 시 XamlParseException 발생 (XAML 파싱 시점에 키 없으면 즉시 예외).
