━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  📋 Stay.Awake 리뷰 보고서  (v1.3.0)
  분석 파일: 10개  |  앱 타입: WinForms 트레이 앱
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  [A. 기능/로직 개선]
    🟡 MED   (개선 권장)
      · SlackSettingsForm: lblHint(y=155)와 btnOk(x=130,y=155)가 같은 줄에 배치됨.
        "※ Slack 앱이 실행 중이어야 합니다" 텍스트(폭 ~170px)가 확인 버튼과 겹침 (SlackSettingsForm.cs:80,89)
    🟢 LOW   (선택적)
      · TogglePreventSleep/SetActivityType에서 메뉴 아이템을 Text 문자열로 검색 →
        멤버 변수로 저장하면 더 안전 (TrayApplicationContext.cs:302,323)
      · 자정 경계 통계 초기화가 타이머 틱에만 의존 → 자정 직후 ShowStats() 호출 시
        전날 카운터 노출 가능 (TrayApplicationContext.cs:360)

  [B. 추가 기능 후보]
    💡 Windows 자동 시작  |  난이도: Easy  |  임팩트: High
       → 레지스트리 Run 키 등록/해제. 매번 수동 실행 불편 해소.
         트레이 메뉴에 "Windows 시작 시 자동 실행" 체크 아이템 추가.
    💡 일시 정지 타이머  |  난이도: Medium  |  임팩트: High
       → "N분 후 자동 재시작" 옵션. 회의 중 임시 정지 후 자동 복귀.
    💡 커스텀 간격 직접 입력  |  난이도: Easy  |  임팩트: Medium
       → 현재 1/2/3/5/7분 고정. 사용자 임의값(예: 4분) 입력 불가.
    💡 다음 Slack 상태 변경까지 툴팁 표시  |  난이도: Easy  |  임팩트: Medium
       → Slack 자동 상태 활성화 시 툴팁에 "Slack ← N분 후" 추가 표시.
    💡 통계 히스토리 (과거 N일)  |  난이도: Medium  |  임팩트: Low
       → 현재 오늘 데이터만 유지. 주간 통계 추적 불가.

  [C. 아이콘 → UI 테마 일치도]
    상태: ✅ 일치
    아이콘 팔레트: app.ico 바이너리 직접 읽기 불가.
      코드 내 주석 "아이콘 배지 색상" → #43D97B(초록)이 아이콘 포인트 컬러임을 유추
    현재 UI 강조색: CheckMarkColor #43D97B, AccentColor(SlackForm) #43D97B
    판단 근거: DarkMenuRenderer.CheckMarkColor와 SlackSettingsForm.AccentColor 모두
      "아이콘 배지 색상"으로 명시. UI가 아이콘 팔레트와 이미 일치.
    조치: Skip

  [D. CLAUDE.md 규칙 준수]
    ✅ 준수:
      · DwmSetWindowAttribute 다크 타이틀바 (DarkInfoDialog, SlackSettingsForm 모두 적용)
      · DarkMenuRenderer 적용
      · ShowBalloonTip() 시작/정지/Slack 변경 시 모두 호출
      · AUMID 등록 (Program.cs RegisterAumid(), UI 생성 전 최우선 호출)
      · 중복 실행 방지 (Mutex)
      · UI 스레드 블로킹 방지 (Task.Run으로 SimulateActivity 백그라운드 실행)
      · ShowImageMargin=true, ShowCheckMargin=true (이모지 미사용, Unicode 특수문자 앱)
      · 다크 테마 색상 준수 (배경 #1E1E1E, 텍스트 #E0E0E0, 보더 #3C3C3C)
    ⚠️ 미준수: 없음
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

  ---
  개선 계획:

  ┌──────────┬────────────────────────────────────────────────┬───────────┐
  │ 우선순위 │                      항목                      │   처리    │
  ├──────────┼────────────────────────────────────────────────┼───────────┤
  │ 🟡 MED   │ SlackSettingsForm 레이아웃 수정 (lblHint 겹침) │ 즉시 실행 │
  ├──────────┼────────────────────────────────────────────────┼───────────┤
  │ 🟡 MED   │ 추가기능: Windows 자동 시작 (Easy/High)        │ 순차 실행 │
  └──────────┴────────────────────────────────────────────────┴───────────┘

  LOW는 보고서 기록만. 아이콘 테마 일치 → Skip.









