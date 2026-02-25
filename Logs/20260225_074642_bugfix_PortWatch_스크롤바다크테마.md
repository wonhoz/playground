# 20260225_074642 | bugfix | Port.Watch 스크롤바 다크 테마 수정

**경로**: `Applications/Tools/Port.Watch/`
**커밋**: `af79108`

## 문제

DataGridView 우측 스크롤바가 OS 기본 흰색으로 표시되어 다크 테마와 불일치.

### 시도했으나 실패한 방법들

| 방법 | 이유 |
|------|------|
| `SetWindowTheme(_grid.Handle, "DarkMode_Explorer", null)` | DataGridView 자체 Win32 핸들에만 적용, 자식 스크롤바 윈도우에 미전달 |
| `SetPreferredAppMode(2)` in Program.cs | 전역 앱 모드 설정이지만 WinForms DataGridView 내부 스크롤바에는 미반영 |

## 원인 분석

DataGridView는 내부적으로 `VScrollBar vertScrollBar` / `HScrollBar horizScrollBar` 필드를
private으로 갖고 있으며, 이들은 별도의 Win32 자식 윈도우 핸들을 가짐.

`SetWindowTheme(parent.Handle, ...)` 는 해당 핸들에만 적용되고,
자식 핸들에는 자동으로 전파되지 않음.

## 해결

**`System.Reflection`으로 내부 스크롤바 핸들에 직접 접근**

```csharp
private void ApplyGridDarkScrollbars()
{
    if (_scrollbarsDarked) return;

    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    var t  = typeof(DataGridView);
    var vSb = (t.GetField("vertScrollBar",  F) ?? t.GetField("_vertScrollBar",  F))?.GetValue(_grid) as ScrollBar;
    var hSb = (t.GetField("horizScrollBar", F) ?? t.GetField("_horizScrollBar", F))?.GetValue(_grid) as ScrollBar;

    if (vSb?.IsHandleCreated == true && hSb?.IsHandleCreated == true)
    {
        SetWindowTheme(vSb.Handle, "DarkMode_Explorer", null);
        SetWindowTheme(hSb.Handle, "DarkMode_Explorer", null);
        _scrollbarsDarked = true;
        return;
    }

    // 폴백: 자식 윈도우 전체 열거 (핸들 미생성 시)
    EnumChildProc cb = (hwnd, _) => { SetWindowTheme(hwnd, "DarkMode_Explorer", null); return true; };
    EnumChildWindows(_grid.Handle, cb, IntPtr.Zero);
    GC.KeepAlive(cb);
}
```

## 적용 시점

| 시점 | 이유 |
|------|------|
| `OnShown()` | 창 표시 직후 최초 시도 |
| `ApplyFilter()` 끝 | 데이터 로드 후 스크롤바 핸들 확실히 생성된 시점에 재시도 |

`_scrollbarsDarked` 플래그로 리플렉션 성공 후 중복 실행 방지.

## 메모

- .NET 8 WinForms DataGridView 내부 필드명: `vertScrollBar`, `horizScrollBar` (언더스코어 없음)
  - .NET Framework 구버전 대비 변경 가능성 → 양쪽 이름 모두 시도
- `EnumChildWindows` 폴백은 핸들 미생성 상태에서도 안전하게 동작
- `GC.KeepAlive(cb)` — 델리게이트가 열거 완료 전 GC 수집되지 않도록 필수
