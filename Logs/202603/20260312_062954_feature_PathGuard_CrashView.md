# 작업 로그 — Path.Guard / Crash.View

| 항목 | 내용 |
|------|------|
| **일시** | 2026-03-12 (KST) |
| **태그** | feature |
| **커밋** | a167b3f |
| **작업자** | Claude Sonnet 4.6 |

---

## 구현 프로젝트

### 1. Path.Guard (`Applications/System/Path.Guard`)

**용도**: Windows PATH 환경변수 전용 시각적 관리자

| 파일 | 역할 |
|------|------|
| `Models/PathEntry.cs` | PATH 항목 모델 (INotifyPropertyChanged, 상태/스코프 색상 브러시) |
| `Models/PathSnapshot.cs` | 스냅샷 record (CreatedAt, SystemPath, UserPath, Label) |
| `Services/PathService.cs` | PATH 읽기/쓰기/진단, 실행파일 검색, SendMessageTimeout WM_SETTINGCHANGE |
| `Services/SnapshotService.cs` | 메모리 스냅샷 저장/복원/삭제 |
| `MainWindow.xaml` | 목록+진단 리스트(좌) + 편집/검색/스냅샷(우) |
| `MainWindow.xaml.cs` | 드래그&드롭 재정렬, 이동 버튼, 필터, 버전 충돌 경고 |

**핵심 기능**:
- 시스템 PATH / 사용자 PATH 분리 색상 표시 (보라 / 하늘색)
- 진단: 깨진 경로(빨강), 중복(노랑), 비활성(회색), 정상(초록)
- 드래그&드롭 + ▲▼ 버튼으로 실행 파일 검색 우선순위 제어
- `%USERPROFILE%` 등 환경변수 확장 미리보기
- 실행파일 검색: `python.exe` 입력 → 히트된 경로 강조
- 버전 충돌 감지: 2개 이상 경로에서 동일 파일명 발견 시 경고
- 스냅샷 저장/더블클릭 롤백
- 시스템 PATH 변경 시 관리자 권한 체크

---

### 2. Crash.View (`Applications/Development/Crash.View`)

**용도**: Windows 덤프 파일(.dmp/.mdmp) 경량 분석 뷰어

| 파일 | 역할 |
|------|------|
| `Models/DumpInfo.cs` | 덤프 분석 결과 (예외·모듈·스레드·GC힙 정보) |
| `Services/DumpAnalyzer.cs` | Microsoft.Diagnostics.Runtime (ClrMD) 기반 분석 엔진 |
| `MainWindow.xaml` | 좌(예외요약+GC힙+스레드) + 우(콜스택/모듈/분석로그 탭) |
| `MainWindow.xaml.cs` | 드래그&드롭, 비동기 분석, 스레드별 스택 전환, 리포트 내보내기 |

**핵심 기능**:
- `.dmp` / `.mdmp` 드래그&드롭 → 즉시 분석
- .NET 관리 예외 타입·메시지·주소 추출 (ClrMD)
- 관리 스택 프레임 역추적 (모듈!메서드 형식)
- GC 힙 세대별 크기 (Gen0/Gen1/Gen2/LOH) ProgressBar 시각화
- 스레드 목록 → 클릭하여 해당 스레드 스택 전환
- 모듈 목록 (이름·버전·크기·관리코드 여부)
- 분석 로그 실시간 표시 (Progress\<string\>)
- Markdown / HTML 리포트 내보내기

**사용 패키지**:
- `Microsoft.Diagnostics.Runtime` v3.1.512801 (ClrMD)

---

## 해결한 이슈

| 이슈 | 원인 | 해결 |
|------|------|------|
| `DataTarget.Architecture` 없음 | ClrMD 3.x에서 API 변경 | `dataTarget.DataReader.Architecture` 사용 |
| `ModuleInfo` 모호한 참조 | CrashView.Models + Microsoft.Diagnostics.Runtime 동일명 충돌 | `using ClrModuleInfo = Microsoft.Diagnostics.Runtime.ModuleInfo` 별칭 |
| XAML OpacityConverter 오류 | `FrameworkElement.OpacityConverter` 미존재 | Opacity 바인딩 제거 |

## 솔루션 변경

- `Playground.slnx` — Path.Guard, Crash.View 등록
- `+publish.cmd` — 4(Crash.View), 30(Path.Guard) 추가, 번호 85개로 재정렬
- `+publish-all.cmd` — 2개 항목 알파벳 순 추가

## 빌드 결과

```
Path.Guard  → 경고 0, 오류 0 ✅
Crash.View  → 경고 0, 오류 0 ✅
```
