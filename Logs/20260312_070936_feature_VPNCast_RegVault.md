---
시간: 2026-03-12 07:09:36 KST
태그: feature
작업명: VPN.Cast + Reg.Vault 최초 구현
---

# VPN.Cast / Reg.Vault 구현 작업 로그

## 개요

idea_20260312_064119.md (15차 브레인스토밍)에서 채택된 2개 프로젝트를 최초 구현.

---

## VPN.Cast (Applications/Network/VPN.Cast)

### 구현 파일

| 파일 | 설명 |
|------|------|
| `VPN.Cast.csproj` | WinForms 트레이 앱, net10.0-windows, System.ServiceProcess.ServiceController 패키지 |
| `Program.cs` | 단일 인스턴스 Mutex 가드, TrayApp 실행 |
| `Models/TunnelProfile.cs` | TunnelType(WireGuard/OpenVPN), TunnelStatus, TunnelProfile |
| `Models/ProfileStore.cs` | JSON 영속성 (LocalApplicationData/VpnCast/profiles.json) |
| `Models/ConnectionLog.cs` | 연결 이력 기록, 최대 100개 유지 |
| `Services/TunnelService.cs` | WireGuard 서비스 연동 (wireguard.exe /installtunnel), OpenVPN 프로세스 관리 |
| `Services/KillSwitchService.cs` | netsh advfirewall 규칙으로 킬 스위치 구현 |
| `DarkMenuRenderer.cs` | ToolStripProfessionalRenderer 기반 다크 트레이 메뉴 |
| `TrayApp.cs` | ApplicationContext, NotifyIcon, 5초 상태 갱신 타이머 |
| `Resources/app.ico` | 앱 아이콘 (방패+자물쇠 모티브) |

### 주요 기능
- WireGuard (.conf) / OpenVPN (.ovpn) 프로파일 가져오기
- 연결/해제 → WireGuard 서비스 설치/제거, OpenVPN 프로세스 관리
- 킬 스위치: 모든 아웃바운드 차단 + WG UDP 51820/DNS 53/루프백 허용
- 연결 이력 로그 (최근 30개 표시)
- 트레이 아이콘 색상: 연결됨=녹색, 끊김=회색

### 수정 사항
- `System.ServiceProcess.ServiceController` 8.0.0 NuGet 패키지 추가 (별도 어셈블리 필요)

---

## Reg.Vault (Applications/System/Reg.Vault)

### 구현 파일

| 파일 | 설명 |
|------|------|
| `Reg.Vault.csproj` | WPF 앱, net10.0-windows |
| `App.xaml` | 다크 테마 전체 스타일 (Button, TextBox, ComboBox, CheckBox, ScrollBar, TreeViewItem, DataGrid, TabControl 등) |
| `App.xaml.cs` | DwmSetWindowAttribute 다크 타이틀바 헬퍼 |
| `MainWindow.xaml` | 3패널 레이아웃: 트리뷰+북마크 / 값목록+검색+스냅샷비교 탭 / 상세패널 |
| `MainWindow.xaml.cs` | 트리 lazy-load, 값 편집/삭제, 정규식 검색, 북마크, 스냅샷, .reg/.json 내보내기 |
| `ValueEditDialog.xaml` + `.cs` | 레지스트리 값 편집 다이얼로그 |
| `Models/RegNode.cs` | ObservableCollection 기반 TreeView 노드, lazy-load |
| `Models/RegValue.cs` | 레지스트리 값 표시 형식 변환 (REG_SZ/DWORD/QWORD/BINARY/MULTI_SZ) |
| `Models/RegSnapshot.cs` | 스냅샷 데이터 구조, DiffEntry (Added/Removed/Modified) |
| `Models/BookmarkStore.cs` | JSON 북마크 영속성 |
| `Services/RegistryService.cs` | GetValues, SearchAsync (정규식/깊이제한), ExportToReg, ExportToJson, TakeSnapshot, CompareSnapshots, BackupKeyToReg |
| `Resources/app.ico` | 앱 아이콘 (금고+레지스트리 키 모티브) |

### 주요 기능
- 5개 하이브 (HKLM/HKCU/HKCR/HKU/HKCC) 트리 브라우저 (lazy-load)
- 정규식 검색: 키 이름/값 이름/데이터, 범위 선택 (현재 키/HKLM/HKCU/전체)
- 북마크: 자주 접근하는 경로 즐겨찾기 저장
- 스냅샷 비교: 두 시점 스냅샷 diff (추가/삭제/변경 시각화)
- .reg / JSON 내보내기
- HKLM 쓰기 시 관리자 확인 + 자동 .reg 백업 (LocalApplicationData/RegVault/backups/)
- 주소 표시줄 직접 입력 및 탐색 이력 (← 뒤로가기)

### 수정 사항
- `using System.IO;` 명시 추가 (BookmarkStore, RegistryService, MainWindow)
- `Models.SearchResult` → `SearchResult` 네임스페이스 수정 (Services에 정의됨)
- `Program.cs` 제거 (WPF App.xaml이 진입점 자동 생성)

---

## 공통 작업

- `Playground.slnx` — VPN.Cast, Reg.Vault 등록
- `+publish-all.cmd` — 두 프로젝트 추가 (알파벳 순)
- `+publish.cmd` — 메뉴 86/87번, 선택 섹션, PUBALL 섹션 추가

## 빌드 결과

```
Reg.Vault  → 경고 0개 / 오류 0개 ✅
VPN.Cast   → 경고 0개 / 오류 0개 ✅
```
