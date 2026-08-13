# Stay.Awake v1.10.0 — 업무시간 스케줄 옵션 추가

- **일시**: 2026-08-14 08:46 (KST)
- **태그**: feature
- **대상**: `Applications/Automation/Stay.Awake`
- **버전**: 1.9.0 → 1.10.0

---

## 요청 사항

주중 업무시간을 따로 설정해서(예: 평일 09:00~17:00) 해당 시간에만 동작하는 옵션 추가.
→ 업무시간에만 절전 방지가 동작하고, 그 외 시간에는 PC가 정상적으로 절전에 들어감.

---

## 구현 내용

### 1. `WorkSchedule.cs` (신규) — 스케줄 판정 로직

| 멤버 | 설명 |
|------|------|
| `DaysMask` | 요일 비트마스크 (bit N = `(int)DayOfWeek`), 기본 `MaskWeekdays`(월~금, 62) |
| `IsWithin(now)` | 현재 시각이 업무시간 구간인지 판정 |
| `NextStart(now)` | 다음 업무시간 시작 시각 (최대 8일 탐색) |
| `CurrentEnd(now)` | 현재 구간의 종료 시각 |
| `Summary()` | `"평일 09:00~17:00"` 형태 요약 |

**경계 규칙**
- 시작 시각 **포함**, 종료 시각 **미포함** (09:00 시작 / 17:00 정지)
- 시작 == 종료 → 해당 요일 **종일** 동작
- 시작 > 종료 → **자정을 넘는 야간 구간**(예: 22:00~06:00)으로 해석하며,
  요일 판정은 **구간이 시작된 날짜** 기준 (금 22:00~토 06:00은 "금요일" 구간)

### 2. `WorkScheduleForm.cs` (신규) — 설정 다이얼로그

- 시작/종료 시각 `ModernSpinner` (시:분)
- 요일 토글 버튼 7개 (선택 시 액센트 그린, 미선택 시 일=적색·토=청색 텍스트)
- 프리셋 버튼: 평일 / 매일 / 주말
- 실시간 요약 라벨 (`→ 평일 09:00~17:00`) — 요일·시각 변경 즉시 반영
- 요일 미선택 상태로는 확인 불가 (스케줄이 영영 동작하지 않는 설정 방지)
- 다크 타이틀바(`DwmSetWindowAttribute` attr=20) + 다크 테마 적용

### 3. `ModernSpinner.cs` (분리) — DRY

`SlackSettingsForm`의 private nested 클래스였던 스피너를 **공용 internal 컨트롤로 추출**.
`ValueChanged` 이벤트를 추가하여 스케줄 폼의 실시간 요약 갱신에 사용.

### 4. `AppSettings.cs` — 설정 영속화

`ScheduleEnabled` / `ScheduleStartHour·Minute` / `ScheduleEndHour·Minute` / `ScheduleDaysMask`
(기본값: 꺼짐, 평일 09:00~17:00)

### 5. `TrayApplicationContext.cs` — 통합

- **메뉴**: `업무시간 스케줄: 평일 09:00~17:00` 서브메뉴
  → `스케줄 사용`(체크) · `요일·시간 설정...` · 현재 상태 표시(비활성 항목)
- **자동 전환**: 기존 Slack 스케줄 타이머(1분 주기)에 `ApplySchedule()` 추가
  - **구간 경계에서만** 시작/정지 → 구간 안에서의 **수동 조작은 그대로 유지**
    (`_lastScheduleInWindow` 이전 판정값과 비교)
  - 스케줄 켜기 / 설정 변경 시에는 `_lastScheduleInWindow = null`로 즉시 재평가
- **앱 시작 시**: 스케줄이 켜져 있으면 업무시간일 때만 자동 시작, 아니면 '대기' 상태로 시작
- **툴팁**: `StayAwake - 업무시간 외 대기 (다음 시작: 월 09:00 | 오늘 HH:MM:SS)`
- **풍선 알림**: 전환 사유 표시 (`업무시간 시작 — … (17:00까지)` / `업무시간 종료 — … 다음 시작: 월 09:00`)
- `ToggleRunning(string? balloonOverride = null)` — 전환 사유 문구 주입용 파라미터 추가
- `ShowStartupBalloon()` / `UpdateStoppedTooltip()` 헬퍼 추출 (중복 제거)
- 도움말에 `[메뉴 — 업무시간 스케줄]` 섹션, 정보 창에 스케줄 상태 라인 추가

---

## 검증

### 빌드
```
dotnet build Applications/Automation/Stay.Awake/Stay.Awake.csproj -c Release
→ 경고 0개, 오류 0개
```
> 솔루션 전체 빌드 시 오류 2건은 `Claude.Shell.Native` C++ vcxproj — dotnet CLI로 빌드 불가한
> 기존 환경 이슈이며 이번 변경과 무관.

### 스케줄 경계 로직 단위 검증 (임시 콘솔 프로젝트, 27 케이스 전부 통과)

| 케이스 | 결과 |
|--------|------|
| 금 08:59 밖 / 09:00 안 / 16:59 안 / 17:00 밖 | PASS |
| 토 12:00 밖 (평일 마스크) | PASS |
| 야간 22:00~06:00 — 금 23:00 안, 토 03:00 안(금 시작 구간), 토 23:00 밖, 일 03:00 밖 | PASS |
| 야간 다음 시작 (토 12:00 → 월 22:00) | PASS |
| 종일 모드(시작==종료) 주말 마스크 | PASS |
| 요일 0개 → 항상 밖 + 다음 시작 null | PASS |
| 요약 문자열 (`평일` / `주말` / `매일` / `월·수·금`) | PASS |

---

## 변경 파일

| 파일 | 구분 |
|------|------|
| `WorkSchedule.cs` | 신규 |
| `WorkScheduleForm.cs` | 신규 |
| `ModernSpinner.cs` | 신규 (SlackSettingsForm에서 분리) |
| `SlackSettingsForm.cs` | 수정 (nested 스피너 제거) |
| `AppSettings.cs` | 수정 (스케줄 설정 6개 추가) |
| `TrayApplicationContext.cs` | 수정 (메뉴·자동 전환·툴팁·도움말·정보) |
| `Stay.Awake.csproj` | 수정 (Version 1.9.0 → 1.10.0) |

---

## 사용 방법

1. 트레이 아이콘 우클릭 → **업무시간 스케줄** → **요일·시간 설정...**
2. 적용 요일(기본 평일) + 시작 09:00 / 종료 17:00 지정 후 확인
3. **스케줄 사용** 체크 → 즉시 현재 시각 기준으로 상태 적용

업무시간 외에는 시뮬레이션이 정지되어 PC가 정상적으로 절전에 들어간다.
