# 20260225_000459 | feature | Port.Watch + Hash.Forge 신규 앱 개발

## Port.Watch — 포트 & 프로세스 모니터

**경로**: `Applications/Tools/Port.Watch/`
**커밋**: `50299c7`

### 기능
| 기능 | 구현 방법 |
|------|-----------|
| 포트 전체 목록 | `netstat -ano` 파싱 → PID 매핑 → `Process.GetProcessById()` |
| 포트·프로세스 검색 | `TextBox.TextChanged` → `ApplyFilter()` |
| 자동 갱신 (5초) | `System.Windows.Forms.Timer` 토글 |
| 프로세스 종료 | `Process.GetProcessById(pid).Kill()` + 확인 다이얼로그 |
| 즐겨찾기 | `FavoritesService` — JSON 영속, 기본 11개 포트 |
| 포트 해제 알림 | 이전 `_prevOccupied` HashSet과 비교 |
| 다크 테마 DataGridView | `EnableHeadersVisualStyles=false` + 셀/헤더/대체행 스타일 수동 설정 |

### 파일 구조
```
Port.Watch.csproj
Program.cs
MainWindow.cs
DarkMenuRenderer.cs
Models/PortEntry.cs
Services/PortScanService.cs
Services/FavoritesService.cs
```

---

## Hash.Forge — 해시·인코딩 올인원 유틸리티

**경로**: `Applications/Tools/Hash.Forge/`
**커밋**: `3f499d8`

### 탭별 기능
| 탭 | 기능 |
|----|------|
| **해시** | MD5 / SHA-1 / SHA-256 / SHA-512 / HMAC-SHA256 실시간 계산 (TextChanged 이벤트) |
| **인코딩** | Base64 / URL / Hex / HTML Entity 인코딩·디코딩 |
| **JWT** | Base64Url 디코딩, exp/iat/sub/iss 파싱 및 만료 여부 표시 |
| **생성기** | UUID v4 / ULID 생성, 비밀번호 생성기 (슬라이더·옵션) + 강도 4단계 바 |

### WPF 다크 테마 — ControlTemplate 적용 컨트롤
- `TextBox`: CornerRadius=5, IsFocused → 보라 테두리
- `Button`: CornerRadius=5, 호버/클릭 배경 전환
- `TabControl` + `TabItem`: 하단 보더 강조 (선택 탭 = #7C69EF)
- `ComboBox`: PART_Popup 커스텀, ItemContainerStyle 포함
- `CheckBox`: Border+Path 틱 마크 직접 구현
- `Slider`: 진행 구간 #7C69EF, 썸 Ellipse
- `ScrollBar`: 슬림(6px) 썸

### 파일 구조
```
Hash.Forge.csproj
App.xaml (전역 다크 테마)
App.xaml.cs
MainWindow.xaml
MainWindow.xaml.cs
Services/CryptoService.cs  (해시 + 인코딩 + 생성기)
Services/JwtService.cs     (JWT 디코딩)
```

### 빌드 이슈 해결
1. `StackPanel.Spacing` → WinUI3 전용, WPF에서 사용 불가 → 각 CheckBox에 `Margin` 설정
2. Slider `ValueChanged` 이벤트 → `RoutedPropertyChangedEventArgs<double>` (WPF 타입)

---

## 메모
- `netstat -ano` 파싱 시 IPv6 주소 (`[::]:port`) 처리: `LastIndexOf(':')` 기반 포트 추출로 대응
- `Console.Beep` (Toast.Cast): 백그라운드 Thread 사용으로 UI 블로킹 없음
