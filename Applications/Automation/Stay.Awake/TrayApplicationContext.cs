using System.ComponentModel;

namespace StayAwake
{
    /// <summary>
    /// 시스템 트레이 애플리케이션 컨텍스트
    /// </summary>
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly System.Windows.Forms.Timer _activityTimer;
        private readonly System.Windows.Forms.Timer _scheduleTimer;
        private readonly System.Windows.Forms.Timer _tooltipTimer;
        private readonly ActivitySimulator _simulator;
        private readonly SlackUiAutomation _slackAutomation;

        private Font? _menuFont;

        private ToolStripMenuItem _startStopItem = null!;
        private ToolStripMenuItem _intervalItem = null!;
        private ToolStripMenuItem _distanceItem = null!;
        private ToolStripMenuItem _statusItem = null!;
        private ToolStripMenuItem _skipIfActiveItem = null!;
        private ToolStripMenuItem _slackAutoStatusItem = null!;

        private bool _isRunning = false;
        private int _intervalMinutes = 3; // 기본 3분 (Slack 10분 타임아웃의 1/3)
        private int _activityCount = 0;
        private DateTime _startTime;
        private DateTime _lastActivityTime;
        private readonly AppSettings _settings;

        // 일일 통계
        private DailyStats _dailyStats;
        private int _dailySimCount = 0;
        private int _dailySkipCount = 0;
        private DateTime _statsDate = DateTime.Today;
        private TimeSpan _todayActiveTime = TimeSpan.Zero;
        private DateTime _sessionRunStart;
        private DateTime _appStartTime = DateTime.Now;

        public TrayApplicationContext()
        {
            // 저장된 설정 로드
            _settings = AppSettings.Load();

            // 오늘 통계 로드 (앱 재시작 후에도 이어서 누적)
            _dailyStats = DailyStats.Load();
            _dailySimCount = _dailyStats.SimCount;
            _dailySkipCount = _dailyStats.SkipCount;
            _todayActiveTime = _dailyStats.ActiveTime;
            _statsDate = _dailyStats.Date.Date;

            _simulator = new ActivitySimulator
            {
                MoveDistance = _settings.MoveDistance,
                ActivityType = Enum.TryParse<ActivityType>(_settings.ActivityType, out var at) ? at : ActivityType.MouseMove,
                PreventDisplaySleep = _settings.PreventDisplaySleep,
                SkipIfUserActive = _settings.SkipIfUserActive,
                IdleThresholdSeconds = _settings.IntervalMinutes * 60
            };
            _intervalMinutes = _settings.IntervalMinutes;

            // Slack UI 자동화 초기화
            _slackAutomation = new SlackUiAutomation
            {
                IsEnabled = _settings.SlackAutoStatusEnabled,
                WorkStartHour = _settings.WorkStartHour,
                WorkStartMinute = _settings.WorkStartMinute,
                WorkEndHour = _settings.WorkEndHour,
                WorkEndMinute = _settings.WorkEndMinute
            };

            // 활동 타이머 설정
            _activityTimer = new System.Windows.Forms.Timer
            {
                Interval = _intervalMinutes * 60 * 1000
            };
            _activityTimer.Tick += OnTimerTick;

            // Slack 상태 스케줄 타이머 (1분마다 체크)
            _scheduleTimer = new System.Windows.Forms.Timer { Interval = 60 * 1000 };
            _scheduleTimer.Tick += OnScheduleTimerTick;
            _scheduleTimer.Start();

            // 툴팁 카운트다운 타이머 (1초마다 갱신)
            _tooltipTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _tooltipTimer.Tick += OnTooltipTimerTick;

            // 컨텍스트 메뉴 생성
            _contextMenu = CreateContextMenu();

            // 트레이 아이콘 설정
            _trayIcon = new NotifyIcon
            {
                Icon = CreateIcon(false),
                Text = "StayAwake - 정지됨",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => ToggleRunning();

            // 앱 실행 시 자동 시작
            ToggleRunning();
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer(),
                ShowImageMargin = true,
                ShowCheckMargin = true,
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.FromArgb(240, 240, 240)
            };

            // 폰트 설정
            _menuFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            menu.Font = _menuFont;

            // 시작/정지
            _startStopItem = new ToolStripMenuItem("▶ 시작", null, (s, e) => ToggleRunning())
            {
                Font = new Font(_menuFont, FontStyle.Bold),
                ForeColor = Color.FromArgb(76, 175, 80) // Green for start
            };
            menu.Items.Add(_startStopItem);

            menu.Items.Add(new ToolStripSeparator());

            // 상태
            _statusItem = new ToolStripMenuItem("상태: 정지됨") { Enabled = false };
            menu.Items.Add(_statusItem);

            menu.Items.Add(new ToolStripSeparator());

            // 간격 설정
            _intervalItem = new ToolStripMenuItem($"간격: {_intervalMinutes}분");
            var intervals = new[] { 1, 2, 3, 5, 7 };
            foreach (var min in intervals)
            {
                var item = new ToolStripMenuItem($"{min}분", null, (s, e) => SetInterval(min))
                {
                    Checked = (min == _intervalMinutes)
                };
                _intervalItem.DropDownItems.Add(item);
            }
            menu.Items.Add(_intervalItem);

            // 이동 거리 설정
            _distanceItem = new ToolStripMenuItem($"이동 거리: {_simulator.MoveDistance}px");
            var distances = new[] { 10, 30, 50, 100, 200 };
            foreach (var px in distances)
            {
                var item = new ToolStripMenuItem($"{px}px", null, (s, e) => SetDistance(px))
                {
                    Checked = (px == _simulator.MoveDistance)
                };
                _distanceItem.DropDownItems.Add(item);
            }
            menu.Items.Add(_distanceItem);

            // 활동 유형
            var activityTypeItem = new ToolStripMenuItem("활동 유형");
            activityTypeItem.DropDownItems.Add(new ToolStripMenuItem("마우스 이동", null, (s, e) => SetActivityType(ActivityType.MouseMove)) { Checked = _simulator.ActivityType == ActivityType.MouseMove });
            activityTypeItem.DropDownItems.Add(new ToolStripMenuItem("마우스 + 키보드", null, (s, e) => SetActivityType(ActivityType.MouseAndKeyboard)) { Checked = _simulator.ActivityType == ActivityType.MouseAndKeyboard });
            menu.Items.Add(activityTypeItem);

            // 디스플레이 절전 방지
            var preventSleepItem = new ToolStripMenuItem("디스플레이 절전 방지", null, (s, e) => TogglePreventSleep())
            {
                Checked = _simulator.PreventDisplaySleep
            };
            menu.Items.Add(preventSleepItem);

            // 사용 중 건너뛰기
            _skipIfActiveItem = new ToolStripMenuItem("사용 중이면 건너뛰기", null, (s, e) => ToggleSkipIfActive())
            {
                Checked = _simulator.SkipIfUserActive
            };
            menu.Items.Add(_skipIfActiveItem);

            menu.Items.Add(new ToolStripSeparator());

            // 지금 실행
            menu.Items.Add(new ToolStripMenuItem("지금 활동 실행", null, (s, e) => SimulateNow()));

            menu.Items.Add(new ToolStripSeparator());

            // Slack 자동 상태 변경
            var slackItem = new ToolStripMenuItem("Slack 자동 상태 변경");

            _slackAutoStatusItem = new ToolStripMenuItem("자동 변경 활성화", null, (s, e) => ToggleSlackAutoStatus())
            {
                Checked = _settings.SlackAutoStatusEnabled
            };
            slackItem.DropDownItems.Add(_slackAutoStatusItem);
            slackItem.DropDownItems.Add(new ToolStripMenuItem("시간 설정...", null, (s, e) => OpenSlackSettings()));
            slackItem.DropDownItems.Add(new ToolStripSeparator());
            slackItem.DropDownItems.Add(new ToolStripMenuItem("지금 활성으로 변경", null, async (s, e) => await SetSlackActiveNowAsync()));
            slackItem.DropDownItems.Add(new ToolStripMenuItem("지금 자리비움으로 변경", null, async (s, e) => await SetSlackAwayNowAsync()));
            menu.Items.Add(slackItem);

            menu.Items.Add(new ToolStripSeparator());

            // 오늘 통계
            menu.Items.Add(new ToolStripMenuItem("오늘 통계", null, (s, e) => ShowStats()));

            // 정보
            menu.Items.Add(new ToolStripMenuItem("정보", null, (s, e) => ShowAbout()));

            // 종료
            menu.Items.Add(new ToolStripMenuItem("종료", null, (s, e) => ExitApplication()));

            return menu;
        }

        private void ToggleRunning()
        {
            _isRunning = !_isRunning;

            if (_isRunning)
            {
                _activityCount = 0;
                _startTime = DateTime.Now;
                _lastActivityTime = DateTime.Now;
                _sessionRunStart = DateTime.Now;
                _activityTimer.Start();
                _tooltipTimer.Start();
                _startStopItem.Text = "⏹ 정지";
                _startStopItem.ForeColor = Color.FromArgb(234, 67, 53); // Red for stop
                _trayIcon.Icon = CreateIcon(true);
                UpdateTooltip();
                UpdateStatus();

                _trayIcon.ShowBalloonTip(1500, "StayAwake",
                    $"활동 시뮬레이션 시작됨 ({_intervalMinutes}분 간격, {_simulator.MoveDistance}px)", ToolTipIcon.Info);
            }
            else
            {
                _todayActiveTime += DateTime.Now - _sessionRunStart;
                _activityTimer.Stop();
                _tooltipTimer.Stop();
                _simulator.AllowSleep(); // 절전 방지 해제
                _startStopItem.Text = "▶ 시작";
                _startStopItem.ForeColor = Color.FromArgb(76, 175, 80); // Green for start
                _trayIcon.Icon = CreateIcon(false);
                _trayIcon.Text = "StayAwake - 정지됨";
                _statusItem.Text = "상태: 정지됨";
                SaveDailyStats(); // 정지 시 활성 시간 포함 저장

                _trayIcon.ShowBalloonTip(1500, "StayAwake",
                    "활동 시뮬레이션 정지됨", ToolTipIcon.Info);
            }
        }

        private void SetInterval(int minutes)
        {
            _intervalMinutes = minutes;
            _activityTimer.Interval = minutes * 60 * 1000;
            _simulator.IdleThresholdSeconds = minutes * 60; // 유휴 임계값을 타이머 간격과 동일하게
            _intervalItem.Text = $"간격: {minutes}분";

            // 실행 중이면 타이머 즉시 리셋 (새 간격이 바로 적용되도록)
            if (_isRunning)
            {
                _activityTimer.Stop();
                _lastActivityTime = DateTime.Now;
                _activityTimer.Start();
            }

            // 체크 상태 업데이트
            foreach (ToolStripMenuItem item in _intervalItem.DropDownItems)
            {
                item.Checked = item.Text == $"{minutes}분";
            }

            if (_isRunning)
            {
                _trayIcon.Text = $"StayAwake - 실행 중 ({_intervalMinutes}분 간격)";
            }

            SaveSettings();
        }

        private void SetDistance(int pixels)
        {
            _simulator.MoveDistance = pixels;
            _distanceItem.Text = $"이동 거리: {pixels}px";

            // 체크 상태 업데이트
            foreach (ToolStripMenuItem item in _distanceItem.DropDownItems)
            {
                item.Checked = item.Text == $"{pixels}px";
            }

            SaveSettings();
        }

        private void SetActivityType(ActivityType type)
        {
            _simulator.ActivityType = type;

            var activityItem = _contextMenu.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(x => x.Text == "활동 유형");

            if (activityItem != null)
            {
                foreach (ToolStripMenuItem item in activityItem.DropDownItems)
                {
                    item.Checked = (item.Text == "마우스 이동" && type == ActivityType.MouseMove) ||
                                   (item.Text == "마우스 + 키보드" && type == ActivityType.MouseAndKeyboard);
                }
            }

            SaveSettings();
        }

        private void TogglePreventSleep()
        {
            _simulator.PreventDisplaySleep = !_simulator.PreventDisplaySleep;

            var preventSleepItem = _contextMenu.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(x => x.Text == "디스플레이 절전 방지");

            if (preventSleepItem != null)
            {
                preventSleepItem.Checked = _simulator.PreventDisplaySleep;
            }

            if (!_simulator.PreventDisplaySleep)
            {
                _simulator.AllowSleep();
            }

            SaveSettings();
        }

        private void ToggleSkipIfActive()
        {
            _simulator.SkipIfUserActive = !_simulator.SkipIfUserActive;
            _skipIfActiveItem.Checked = _simulator.SkipIfUserActive;
            SaveSettings();
        }

        private void SaveSettings()
        {
            _settings.IntervalMinutes = _intervalMinutes;
            _settings.MoveDistance = _simulator.MoveDistance;
            _settings.ActivityType = _simulator.ActivityType.ToString();
            _settings.PreventDisplaySleep = _simulator.PreventDisplaySleep;
            _settings.SkipIfUserActive = _simulator.SkipIfUserActive;
            _settings.SlackAutoStatusEnabled = _slackAutomation.IsEnabled;
            _settings.Save();
        }

        private async void OnTimerTick(object? sender, EventArgs e)
        {
            // 자정이 넘어가면 일일 통계 초기화 (새 날 기준으로 리셋)
            if (DateTime.Today != _statsDate)
            {
                _todayActiveTime = TimeSpan.Zero;
                _sessionRunStart = DateTime.Now;
                _dailySimCount = 0;
                _dailySkipCount = 0;
                _statsDate = DateTime.Today;
                _dailyStats = new DailyStats();
            }

            // UI 차단 방지: SimulateActivity()는 Thread.Sleep(110ms) 포함 → 배경 스레드에서 실행
            bool simulated = await Task.Run(() => _simulator.SimulateActivity());
            if (simulated)
            {
                _activityCount++;
                _dailySimCount++;
            }
            else
            {
                _dailySkipCount++;
            }
            _lastActivityTime = DateTime.Now;
            UpdateStatus(simulated);
            SaveDailyStats();
        }

        private async void SimulateNow()
        {
            // UI 차단 방지: SimulateActivity()는 Thread.Sleep(110ms) 포함 → 배경 스레드에서 실행
            // 수동 실행이므로 메뉴 클릭에 의한 사용자 활동 감지를 무시하고 강제 실행
            bool simulated = await Task.Run(() => _simulator.SimulateActivity(forceSimulate: true));
            if (simulated)
                _dailySimCount++;
            else
                _dailySkipCount++;

            if (_isRunning && simulated)
            {
                _activityCount++;
                UpdateStatus(simulated);
            }
            SaveDailyStats();
            var message = simulated
                ? $"활동 시뮬레이션 실행됨 ({_simulator.MoveDistance}px 이동)"
                : "사용자 활동 감지됨 - 시뮬레이션 건너뜀";
            _trayIcon.ShowBalloonTip(1000, "StayAwake", message, ToolTipIcon.Info);
        }

        private void UpdateStatus(bool? lastSimulated = null)
        {
            var elapsed = DateTime.Now - _startTime;
            var skipInfo = lastSimulated == false ? " (건너뜀)" : "";
            _statusItem.Text = $"상태: 실행 중 ({_activityCount}회{skipInfo}, {elapsed:hh\\:mm\\:ss})";
        }

        private void SaveDailyStats()
        {
            var currentActiveTime = _isRunning
                ? _todayActiveTime + (DateTime.Now - _sessionRunStart)
                : _todayActiveTime;
            _dailyStats.Date = DateTime.Today;
            _dailyStats.SimCount = _dailySimCount;
            _dailyStats.SkipCount = _dailySkipCount;
            _dailyStats.ActiveTime = currentActiveTime;
            _dailyStats.Save();
        }

        private void OnTooltipTimerTick(object? sender, EventArgs e)
        {
            if (_isRunning) UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            var nextActivity = _lastActivityTime.AddMinutes(_intervalMinutes) - DateTime.Now;
            if (nextActivity < TimeSpan.Zero) nextActivity = TimeSpan.Zero;
            var activeTime = _todayActiveTime + (DateTime.Now - _sessionRunStart);
            _trayIcon.Text = $"StayAwake - 다음: {(int)nextActivity.TotalMinutes}분 {nextActivity.Seconds:D2}초 후 | {activeTime:hh\\:mm\\:ss} / {_dailySimCount}회";
        }

        private void ShowStats()
        {
            var activeTime = _isRunning
                ? _todayActiveTime + (DateTime.Now - _sessionRunStart)
                : _todayActiveTime;

            var total = _dailySimCount + _dailySkipCount;
            var skipRate = total > 0 ? (double)_dailySkipCount / total * 100 : 0;
            var totalElapsed = DateTime.Now - _appStartTime;
            var activeRate = totalElapsed.TotalSeconds > 0
                ? activeTime.TotalSeconds / totalElapsed.TotalSeconds * 100
                : 0;

            var message = $@"StayAwake 오늘의 통계 ({_statsDate:yyyy-MM-dd})

[활동 시뮬레이션]
• 시뮬레이션 실행: {_dailySimCount}회
• 사용자 활동으로 스킵: {_dailySkipCount}회
• 스킵율: {skipRate:F1}% (직접 사용 중이던 비율)

[활성 시간]
• 오늘 누적 활성 시간: {activeTime:hh\:mm\:ss}
• 앱 실행 중 활성 비율: {activeRate:F1}% ({activeTime:hh\:mm\:ss} / {totalElapsed:hh\:mm\:ss})";

            DarkInfoDialog.Show("오늘의 통계", message, 570, 460);
        }

        private void ShowAbout()
        {
            var slackStatusLine = _slackAutomation.IsEnabled
                ? $"켜짐 ({_slackAutomation.WorkStartHour:D2}:{_slackAutomation.WorkStartMinute:D2} 활성 / {_slackAutomation.WorkEndHour:D2}:{_slackAutomation.WorkEndMinute:D2} 자리비움)"
                : "꺼짐";

            var activityTypeLabel = _simulator.ActivityType == ActivityType.MouseAndKeyboard
                ? "마우스 + 키보드"
                : "마우스 이동";

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.3.0";
            var message = $@"StayAwake {versionStr}

Slack 자리 비움 상태 방지 도구

[Slack 자리 비움 감지 방식]
• 10분간 키보드/마우스 비활성 시 Away
• Slack이 백그라운드에 있어도 시스템 활동 감지
• API로도 강제 Active 불가, 자연스러운 활동만 인정

[동작 방식]
• 주기적으로 마우스를 이동 후 원위치
• SetThreadExecutionState로 디스플레이 절전 방지
• 사용자가 직접 활동 중이면 마우스 이동 자동 건너뜀
• Slack 상태 변경: 클립보드 방식으로 슬래시 커맨드 전송 (한글 IME 대응)

[현재 설정]
• 간격: {_intervalMinutes}분
• 이동 거리: {_simulator.MoveDistance}px
• 활동 유형: {activityTypeLabel}
• 디스플레이 절전 방지: {(_simulator.PreventDisplaySleep ? "켜짐" : "꺼짐")}
• 사용 중 건너뛰기: {(_simulator.SkipIfUserActive ? "켜짐" : "꺼짐")}
• Slack 자동 상태 변경: {slackStatusLine}

[사용법]
• 더블클릭: 시작/정지 토글
• 우클릭: 메뉴 열기
• 트레이 호버: 다음 활동까지 남은 시간 표시
• 오늘 통계 메뉴: 활성 시간 및 비율 확인
• Slack → 시간 설정: 출퇴근 시간 변경 (기본 08:55 / 18:55)
• 권장 간격: 3~5분

© 2026 https://github.com/wonhoz";

            DarkInfoDialog.Show("StayAwake 정보", message, 750, 1070);
        }

        private async void OnScheduleTimerTick(object? sender, EventArgs e)
        {
            var result = await _slackAutomation.CheckAndSetPresenceAsync();
            if (result == null) return;

            if (result.Success)
            {
                var label = result.Status == "active" ? "활성" : "자리 비움";
                _trayIcon.ShowBalloonTip(2000, "StayAwake",
                    $"Slack 상태를 '{label}'으로 변경했습니다.", ToolTipIcon.Info);
            }
            else
            {
                _trayIcon.ShowBalloonTip(2500, "StayAwake",
                    $"Slack 상태 변경 실패: {result.ErrorMessage}", ToolTipIcon.Warning);
            }
        }

        private void OpenSlackSettings()
        {
            using var form = new SlackSettingsForm(
                _slackAutomation.WorkStartHour,
                _slackAutomation.WorkStartMinute,
                _slackAutomation.WorkEndHour,
                _slackAutomation.WorkEndMinute);

            if (form.ShowDialog() != DialogResult.OK) return;

            _slackAutomation.WorkStartHour = form.WorkStartHour;
            _slackAutomation.WorkStartMinute = form.WorkStartMinute;
            _slackAutomation.WorkEndHour = form.WorkEndHour;
            _slackAutomation.WorkEndMinute = form.WorkEndMinute;

            _settings.WorkStartHour = form.WorkStartHour;
            _settings.WorkStartMinute = form.WorkStartMinute;
            _settings.WorkEndHour = form.WorkEndHour;
            _settings.WorkEndMinute = form.WorkEndMinute;
            _settings.Save();
        }

        private void ToggleSlackAutoStatus()
        {
            _slackAutomation.IsEnabled = !_slackAutomation.IsEnabled;
            _slackAutoStatusItem.Checked = _slackAutomation.IsEnabled;
            SaveSettings();

            var msg = _slackAutomation.IsEnabled
                ? $"Slack 자동 상태 변경 활성화\n({_slackAutomation.WorkStartHour:D2}:{_slackAutomation.WorkStartMinute:D2} 활성, {_slackAutomation.WorkEndHour:D2}:{_slackAutomation.WorkEndMinute:D2} 자리비움)\nSlack 앱이 실행 중이어야 합니다."
                : "Slack 자동 상태 변경 비활성화";
            _trayIcon.ShowBalloonTip(2000, "StayAwake", msg, ToolTipIcon.Info);
        }

        private async Task SetSlackActiveNowAsync()
        {
            var result = await _slackAutomation.SetActiveAsync();
            _trayIcon.ShowBalloonTip(1500, "StayAwake",
                result.Success ? "Slack 상태를 '활성'으로 변경했습니다." : $"실패: {result.ErrorMessage}",
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        private async Task SetSlackAwayNowAsync()
        {
            var result = await _slackAutomation.SetAwayAsync();
            _trayIcon.ShowBalloonTip(1500, "StayAwake",
                result.Success ? "Slack 상태를 '자리비움'으로 변경했습니다." : $"실패: {result.ErrorMessage}",
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        private void ExitApplication()
        {
            if (_isRunning)
                _todayActiveTime += DateTime.Now - _sessionRunStart;
            SaveDailyStats(); // 종료 시 최종 통계 저장
            _scheduleTimer.Stop();
            _activityTimer.Stop();
            _tooltipTimer.Stop();
            _simulator.AllowSleep(); // 절전 방지 해제
            _trayIcon.Visible = false;
            Application.Exit();
        }

        private static Icon CreateIcon(bool isRunning)
        {
            return isRunning
                ? IconGenerator.LoadRunningIcon()
                : IconGenerator.LoadStoppedIcon();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _simulator.AllowSleep(); // 절전 방지 해제
                _scheduleTimer.Dispose();
                _activityTimer.Dispose();
                _tooltipTimer.Dispose();
                _trayIcon.Dispose();
                _contextMenu.Dispose();
                _menuFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
