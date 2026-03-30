# Screen.Recorder 버그 수정 + Photo.Video.Organizer EXIF 회전

- **날짜**: 2026-02-20
- **태그**: bugfix / feature
- **상태**: 완료

## 개요

3개 작업 완료:
1. Screen.Recorder — 선택 영역 DPI 버그 수정
2. Screen.Recorder — 마우스 포인터 녹화 기능 추가
3. Photo.Video.Organizer — EXIF Orientation 자동 회전 기능 추가

---

## 1. Screen.Recorder DPI 버그 수정

### 원인

`RegionSelectWindow.xaml.cs`의 `OnMouseUp` 메서드에서 스크린 좌표 변환 시:

```csharp
// 버그 (논리 픽셀 그대로 사용)
var screenX = (int)Left + x;
var screenY = (int)Top + y;
```

`Left`, `Top`, `x`, `y` 모두 WPF 논리 픽셀 단위인데, `Graphics.CopyFromScreen`은 물리 픽셀 기준. 125% DPI 환경에서 녹화 영역이 완전히 어긋남.

### 수정

```csharp
var dpi     = VisualTreeHelper.GetDpi(this);
var screenX = (int)Math.Round((Left + x) * dpi.DpiScaleX);
var screenY = (int)Math.Round((Top  + y) * dpi.DpiScaleY);
var physW   = (int)Math.Round(w * dpi.DpiScaleX);
var physH   = (int)Math.Round(h * dpi.DpiScaleY);
```

크기 표시 레이블도 물리 픽셀 기준으로 변경.

### 수정 파일
- `Tools/Screen.Recorder/RegionSelectWindow.xaml.cs`

---

## 2. Screen.Recorder 마우스 포인터 녹화

### 구현

`ScreenCaptureService.cs`에 Win32 P/Invoke 추가:
- `GetCursorInfo` — 현재 커서 위치 및 핸들 가져오기
- `DrawIconEx` — 커서를 Graphics에 그리기

```csharp
private void CaptureFrame(string savePath)
{
    // ... CopyFromScreen ...
    if (_captureMouse)
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (GetCursorInfo(ref ci) && (ci.flags & CURSOR_SHOWING) != 0)
        {
            var curX = ci.ptScreenPos.X - _region.X;
            var curY = ci.ptScreenPos.Y - _region.Y;
            // 캡처 영역 내에 있을 때만 그리기
            if (curX >= -32 && curX < _region.Width + 32 && ...)
            {
                var hdc = g.GetHdc();
                DrawIconEx(hdc, curX, curY, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL);
                g.ReleaseHdc(hdc);
            }
        }
    }
}
```

### UI 변경
- `MainWindow.xaml` 설정 패널에 "마우스 포인터 포함" 체크박스 추가 (기본값: ON)
- 다크 테마 CheckBox 스타일 추가

### 수정 파일
- `Tools/Screen.Recorder/Services/ScreenCaptureService.cs`
- `Tools/Screen.Recorder/MainWindow.xaml`
- `Tools/Screen.Recorder/MainWindow.xaml.cs`

---

## 3. Photo.Video.Organizer EXIF 회전

### 구현

**신규 파일: `Services/ExifRotationHelper.cs`**

| 포맷 | 저장 방식 |
|------|---------|
| JPEG (.jpg, .jpeg) | Quality=100 재인코딩 (실질적 무손실) |
| TIFF (.tiff, .tif) | 원본 포맷 무손실 저장 |
| PNG (.png) | 원본 포맷 무손실 저장 |
| BMP (.bmp) | 원본 포맷 무손실 저장 |
| RAW (.cr2, .nef 등) | System.Drawing 미지원 → 단순 File.Copy |

- EXIF PropertyItem 전체 보존 (System.Drawing이 자동으로 처리)
- 저장 후 Orientation 태그를 1(정상)로 리셋 → 뷰어 이중 회전 방지

**FileOrganizer.cs 변경**:
- `OrganizeFilesAsync`에 `bool autoRotate = false` 파라미터 추가
- `OrganizeSummary.RotatedCount` 필드 추가
- `OrganizeResult.AutoRotated` 필드 추가

**UI 변경**:
- "EXIF 방향 자동 회전 적용" 체크박스 추가 (기본값: OFF)
- 결과 요약에 회전 적용 개수 표시: `(회전 적용 N개)`

### 수정 파일
- `Media/Photo.Video.Organizer/Services/ExifRotationHelper.cs` (신규)
- `Media/Photo.Video.Organizer/Services/FileOrganizer.cs`
- `Media/Photo.Video.Organizer/MainWindow.xaml`
- `Media/Photo.Video.Organizer/MainWindow.xaml.cs`

---

## 빌드 결과

| 프로젝트 | 결과 |
|---------|------|
| Screen.Recorder | 경고 0 / 오류 0 |
| Photo.Video.Organizer | 경고 0 / 오류 0 |

## 커밋

- `3495177` — `[screen.recorder] | DPI 스케일 버그 수정 및 마우스 포인터 녹화 기능 추가`
- `ed9d13d` — `[photo.video] | EXIF Orientation 회전 자동 적용 기능 추가`
