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
        private readonly System.Windows.Forms.Timer _singleClickTimer; // 더블클릭 구분용
        // private readonly System.Windows.Forms.Timer _pauseTimer;
        private readonly ActivitySimulator _simulator;
        private readonly SlackUiAutomation _slackAutomation;

        private Font? _menuFont;
        private Font? _startStopFont;
        private Icon? _cachedRunningIcon;
        private Icon? _cachedStoppedIcon;

        private ToolStripMenuItem _startStopItem = null!;
        private ToolStripMenuItem _intervalItem = null!;
        private ToolStripMenuItem _distanceItem = null!;
        private ToolStripMenuItem _statusItem = null!;
        private ToolStripMenuItem _skipIfActiveItem = null!;
        private ToolStripMenuItem _slackAutoStatusItem = null!;
        private ToolStripMenuItem _preventSleepItem = null!;
        private ToolStripMenuItem _activityTypeItem = null!;
        // private ToolStripMenuItem _pauseItem = null!;
        // private ToolStripMenuItem _pauseCancelItem = null!;

        // private bool _isPaused = false;
        // private DateTime _pauseEndTime;

        private bool _isRunning = false;
        private bool _isFirstStart = true;
        private int _intervalMinutes = 3; // 기본 3분 (Slack 10분 타임아웃의 1/3)
        private int _activityCount = 0;
        private DateTime _startTime;
        private DateTime _lastActivityTime;
        private readonly AppSettings _settings;

        // 일일 통계
        private DailyStats _dailyStats;
        private int _dailySimCount = 0;
        private int _dailySkipCount = 0;
        private int _dailySlackSuccessCount = 0;
        private int _dailySlackFailCount = 0;
        private DateTime _statsDate = DateTime.Today;
        private TimeSpan _todayActiveTime = TimeSpan.Zero;
        private DateTime _sessionRunStart;

        public TrayApplicationContext()
        {
            // 저장된 설정 로드
            _settings = AppSettings.Load();

            // 오늘 통계 로드 (앱 재시작 후에도 이어서 누적)
            // 날짜가 바뀐 채 재시작된 경우 — 이전 날 데이터를 히스토리에 저장 후 새 통계 시작
            var previousStats = DailyStats.LoadRaw();
            if (previousStats != null && previousStats.Date.Date < DateTime.Today)
                StatsHistory.Append(previousStats);

            _dailyStats = DailyStats.Load();
            _dailySimCount = _dailyStats.SimCount;
            _dailySkipCount = _dailyStats.SkipCount;
            _dailySlackSuccessCount = _dailyStats.SlackSuccessCount;
            _dailySlackFailCount = _dailyStats.SlackFailCount;
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

            // Slack UI 자동화 초기화 (마지막 변경 날짜 복원 → 재시작 시 중복 전송 방지)
            _slackAutomation = new SlackUiAutomation
            {
                IsEnabled = _settings.SlackAutoStatusEnabled,
                WorkStartHour = _settings.WorkStartHour,
                WorkStartMinute = _settings.WorkStartMinute,
                WorkEndHour = _settings.WorkEndHour,
                WorkEndMinute = _settings.WorkEndMinute,
                LastActiveSet = _settings.LastSlackActiveSetDate,
                LastAwaySet = _settings.LastSlackAwaySetDate
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

            // 싱글클릭/더블클릭 구분용 타이머 (OS DoubleClickTime 기준)
            _singleClickTimer = new System.Windows.Forms.Timer { Interval = SystemInformation.DoubleClickTime };
            _singleClickTimer.Tick += (s, e) => { _singleClickTimer.Stop(); ToggleRunning(); };

            // 일시 중지 타이머 (비활성화)
            // _pauseTimer = new System.Windows.Forms.Timer();
            // _pauseTimer.Tick += OnPauseTimerTick;

            // 컨텍스트 메뉴 생성
            _contextMenu = CreateContextMenu();

            // 트레이 아이콘 캐싱 (매 토글마다 새 Icon 생성 → 리소스 누수 방지)
            _cachedRunningIcon = IconGenerator.LoadRunningIcon();
            _cachedStoppedIcon = IconGenerator.LoadStoppedIcon();

            // 트레이 아이콘 설정
            _trayIcon = new NotifyIcon
            {
                Icon = CreateIcon(false),
                Text = "StayAwake - 정지됨",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            // 싱글클릭 vs 더블클릭 구분: DoubleClickTime 이후에도 DoubleClick 이벤트가 없으면 ToggleRunning 실행
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _singleClickTimer.Stop();
                    _singleClickTimer.Start();
                }
            };
            _trayIcon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    _singleClickTimer.Stop(); // 싱글클릭 액션 취소
                    ShowStats();
                }
            };

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
            _startStopFont = new Font(_menuFont, FontStyle.Bold);
            _startStopItem = new ToolStripMenuItem("▶ 시작", null, (s, e) => ToggleRunning())
            {
                Font = _startStopFont,
                ForeColor = Color.FromArgb(67, 217, 123) // Green for start
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
                    Checked = (min == _intervalMinutes),
                    Tag = min
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
                    Checked = (px == _simulator.MoveDistance),
                    Tag = px
                };
                _distanceItem.DropDownItems.Add(item);
            }
            menu.Items.Add(_distanceItem);

            // 활동 유형
            var activityLabel = _simulator.ActivityType == ActivityType.MouseAndKeyboard ? "마우스 + 키보드" : "마우스 이동";
            _activityTypeItem = new ToolStripMenuItem($"활동 유형: {activityLabel}");
            _activityTypeItem.DropDownItems.Add(new ToolStripMenuItem("마우스 이동", null, (s, e) => SetActivityType(ActivityType.MouseMove)) { Checked = _simulator.ActivityType == ActivityType.MouseMove, Tag = ActivityType.MouseMove });
            _activityTypeItem.DropDownItems.Add(new ToolStripMenuItem("마우스 + 키보드", null, (s, e) => SetActivityType(ActivityType.MouseAndKeyboard)) { Checked = _simulator.ActivityType == ActivityType.MouseAndKeyboard, Tag = ActivityType.MouseAndKeyboard });
            menu.Items.Add(_activityTypeItem);

            // 디스플레이 절전 방지
            _preventSleepItem = new ToolStripMenuItem("디스플레이 절전 방지", null, (s, e) => TogglePreventSleep())
            {
                Checked = _simulator.PreventDisplaySleep
            };
            menu.Items.Add(_preventSleepItem);

            // 사용 중 건너뛰기
            _skipIfActiveItem = new ToolStripMenuItem("사용 중이면 건너뛰기", null, (s, e) => ToggleSkipIfActive())
            {
                Checked = _simulator.SkipIfUserActive
            };
            menu.Items.Add(_skipIfActiveItem);

            menu.Items.Add(new ToolStripSeparator());

            // 지금 실행
            menu.Items.Add(new ToolStripMenuItem("지금 활동 실행", null, (s, e) => SimulateNow()));

            // 일시 중지 (비활성화)
            // _pauseItem = new ToolStripMenuItem("일시 중지");
            // _pauseItem.DropDownItems.Add(new ToolStripMenuItem("15분 후 재시작", null, (s, e) => StartPause(15)));
            // _pauseItem.DropDownItems.Add(new ToolStripMenuItem("30분 후 재시작", null, (s, e) => StartPause(30)));
            // _pauseItem.DropDownItems.Add(new ToolStripMenuItem("1시간 후 재시작", null, (s, e) => StartPause(60)));
            // _pauseItem.DropDownItems.Add(new ToolStripSeparator());
            // _pauseCancelItem = new ToolStripMenuItem("취소", null, (s, e) => CancelPause()) { Enabled = false };
            // _pauseItem.DropDownItems.Add(_pauseCancelItem);
            // menu.Items.Add(_pauseItem);

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
            slackItem.DropDownItems.Add(new ToolStripSeparator());

            // 방해 금지 (DND) 프리셋
            var dndItem = new ToolStripMenuItem("방해 금지 (DND)");
            dndItem.DropDownItems.Add(new ToolStripMenuItem("30분", null, async (s, e) => await SetSlackDndAsync(30)));
            dndItem.DropDownItems.Add(new ToolStripMenuItem("1시간", null, async (s, e) => await SetSlackDndAsync(60)));
            dndItem.DropDownItems.Add(new ToolStripMenuItem("2시간", null, async (s, e) => await SetSlackDndAsync(120)));
            dndItem.DropDownItems.Add(new ToolStripSeparator());
            dndItem.DropDownItems.Add(new ToolStripMenuItem("해제", null, async (s, e) => await SetSlackDndAsync(0)));
            slackItem.DropDownItems.Add(dndItem);

            menu.Items.Add(slackItem);

            // 메뉴 열 때 상태 아이템 즉시 갱신
            menu.Opening += OnMenuOpening;

            menu.Items.Add(new ToolStripSeparator());

            // 오늘 통계
            menu.Items.Add(new ToolStripMenuItem("오늘 통계", null, (s, e) => ShowStats()));

            // 통계 CSV 내보내기
            menu.Items.Add(new ToolStripMenuItem("통계 CSV 내보내기", null, (s, e) => ExportStatsCsv()));

            menu.Items.Add(new ToolStripSeparator());

            // 도움말
            menu.Items.Add(new ToolStripMenuItem("도움말", null, (s, e) => ShowHelp()));

            // 정보
            menu.Items.Add(new ToolStripMenuItem("정보", null, (s, e) => ShowAbout()));

            menu.Items.Add(new ToolStripSeparator());

            // 종료
            menu.Items.Add(new ToolStripMenuItem("종료", null, (s, e) => ExitApplication()));

            return menu;
        }

        private void ToggleRunning()
        {
            // 수동 토글 시 일시 중지 상태 해제 (비활성화)
            // if (_isPaused)
            // {
            //     _isPaused = false;
            //     _pauseTimer.Stop();
            //     _pauseCancelItem.Enabled = false;
            //     _pauseCancelItem.Text = "취소";
            // }

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

                if (_isFirstStart)
                {
                    _isFirstStart = false;
                    _trayIcon.ShowBalloonTip(2000, "StayAwake",
                        $"StayAwake 실행됨 — {_intervalMinutes}분 간격으로 활동 시뮬레이션을 시작합니다.", ToolTipIcon.Info);
                }
                else
                {
                    _trayIcon.ShowBalloonTip(1500, "StayAwake",
                        $"활동 시뮬레이션 시작됨 ({_intervalMinutes}분 간격, {_simulator.MoveDistance}px)", ToolTipIcon.Info);
                }
            }
            else
            {
                _todayActiveTime += DateTime.Now - _sessionRunStart;
                _activityTimer.Stop();
                _tooltipTimer.Stop();
                _simulator.AllowSleep(); // 절전 방지 해제
                _startStopItem.Text = "▶ 시작";
                _startStopItem.ForeColor = Color.FromArgb(67, 217, 123); // Green for start
                _trayIcon.Icon = CreateIcon(false);
                var stoppedTime = _todayActiveTime;
                var stoppedText = $"StayAwake - 정지됨 (오늘 {(int)stoppedTime.TotalHours:D2}:{stoppedTime:mm\\:ss})";
                _trayIcon.Text = stoppedText.Length > 127 ? stoppedText[..127] : stoppedText;
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
                UpdateTooltip(); // 간격 변경 즉시 카운트다운 갱신
            }

            // 체크 상태 업데이트 (Tag 기반)
            foreach (ToolStripMenuItem item in _intervalItem.DropDownItems)
            {
                item.Checked = item.Tag is int val && val == minutes;
            }

            SaveSettings();
        }

        private void SetDistance(int pixels)
        {
            _simulator.MoveDistance = pixels;
            _distanceItem.Text = $"이동 거리: {pixels}px";

            // 체크 상태 업데이트 (Tag 기반)
            foreach (ToolStripMenuItem item in _distanceItem.DropDownItems)
            {
                item.Checked = item.Tag is int val && val == pixels;
            }

            SaveSettings();
        }

        private void SetActivityType(ActivityType type)
        {
            _simulator.ActivityType = type;

            // 부모 레이블 갱신
            var label = type == ActivityType.MouseAndKeyboard ? "마우스 + 키보드" : "마우스 이동";
            _activityTypeItem.Text = $"활동 유형: {label}";

            // 체크 상태 업데이트 (Tag 기반)
            foreach (ToolStripMenuItem item in _activityTypeItem.DropDownItems)
            {
                item.Checked = item.Tag is ActivityType t && t == type;
            }

            SaveSettings();
        }

        private void TogglePreventSleep()
        {
            _simulator.PreventDisplaySleep = !_simulator.PreventDisplaySleep;
            _preventSleepItem.Checked = _simulator.PreventDisplaySleep;

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

        /// <summary>
        /// 자정 경계 체크 — 날짜가 바뀐 경우 전날 통계를 히스토리에 저장하고 초기화
        /// </summary>
        private void CheckMidnightReset()
        {
            if (DateTime.Today == _statsDate) return;

            // 전날 최종 통계를 히스토리에 저장
            SaveDailyStats();
            StatsHistory.Append(_dailyStats);

            // 새 날짜로 리셋
            _todayActiveTime = TimeSpan.Zero;
            _sessionRunStart = DateTime.Now;
            _dailySimCount = 0;
            _dailySkipCount = 0;
            _dailySlackSuccessCount = 0;
            _dailySlackFailCount = 0;
            _statsDate = DateTime.Today;
            _dailyStats = new DailyStats();
        }

        private async void OnTimerTick(object? sender, EventArgs e)
        {
            try
            {
                // 자정이 넘어가면 일일 통계 초기화 (히스토리 저장 후 리셋)
                CheckMidnightReset();

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
            catch { /* 타이머 틱 예외 무시 — 다음 틱에서 재시도 */ }
        }

        private async void SimulateNow()
        {
            try
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
                    // 수동 실행 후 타이머 리셋 — 직후 자동 실행 방지
                    _activityTimer.Stop();
                    _lastActivityTime = DateTime.Now;
                    _activityTimer.Start();
                    UpdateStatus(simulated);
                    UpdateTooltip();
                }
                SaveDailyStats();
                var message = simulated
                    ? $"활동 시뮬레이션 실행됨 ({_simulator.MoveDistance}px 이동)"
                    : "사용자 활동 감지됨 - 시뮬레이션 건너뜀";
                _trayIcon.ShowBalloonTip(1000, "StayAwake", message, ToolTipIcon.Info);
            }
            catch { /* 수동 실행 예외 무시 */ }
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
            _dailyStats.SlackSuccessCount = _dailySlackSuccessCount;
            _dailyStats.SlackFailCount = _dailySlackFailCount;
            _dailyStats.ActiveTime = currentActiveTime;
            _dailyStats.Save();
        }

        private void OnTooltipTimerTick(object? sender, EventArgs e)
        {
            if (_isRunning) UpdateTooltip();
            // else if (_isPaused) UpdatePauseTooltip();
        }

        // private void UpdatePauseTooltip()
        // {
        //     var remaining = _pauseEndTime - DateTime.Now;
        //     if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        //     var text = $"StayAwake - 일시 중지 ({(int)remaining.TotalMinutes}분 {remaining.Seconds:D2}초 후 재시작)";
        //     _trayIcon.Text = text.Length > 127 ? text[..127] : text;
        //     _pauseCancelItem.Text = $"취소 ({(int)remaining.TotalMinutes}분 {remaining.Seconds:D2}초 남음)";
        // }

        private void UpdateTooltip()
        {
            var nextActivity = _lastActivityTime.AddMinutes(_intervalMinutes) - DateTime.Now;
            if (nextActivity < TimeSpan.Zero) nextActivity = TimeSpan.Zero;
            var activeTime = _todayActiveTime + (DateTime.Now - _sessionRunStart);
            var sessionElapsed = DateTime.Now - _sessionRunStart;
            var text = $"StayAwake - 다음: {(int)nextActivity.TotalMinutes}분 {nextActivity.Seconds:D2}초 후 | 오늘 {(int)activeTime.TotalHours:D2}:{activeTime:mm\\:ss} / {_dailySimCount}회 | 세션 {(int)sessionElapsed.TotalHours:D2}:{sessionElapsed:mm\\:ss}";
            _trayIcon.Text = text.Length > 127 ? text[..127] : text;
        }

        private void ShowStats()
        {
            // 자정이 넘어간 경우 히스토리 저장 후 리셋 (ShowStats 직접 호출 시에도 최신 데이터 보장)
            CheckMidnightReset();

            var activeTime = _isRunning
                ? _todayActiveTime + (DateTime.Now - _sessionRunStart)
                : _todayActiveTime;

            var total = _dailySimCount + _dailySkipCount;
            var skipRate = total > 0 ? (double)_dailySkipCount / total * 100 : 0;
            var todayElapsed = DateTime.Now - DateTime.Today;
            var activeRate = todayElapsed.TotalSeconds > 0
                ? activeTime.TotalSeconds / todayElapsed.TotalSeconds * 100
                : 0;

            var dayNames = new[] { "일", "월", "화", "수", "목", "금", "토" };
            var todayDayName = dayNames[(int)DateTime.Today.DayOfWeek];

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[오늘의 활동]  {_statsDate:yyyy-MM-dd} ({todayDayName})");
            sb.AppendLine();
            sb.AppendLine("[활동 시뮬레이션]");
            sb.AppendLine($"• 시뮬레이션 실행: {_dailySimCount}회");
            sb.AppendLine($"• 사용자 활동으로 스킵: {_dailySkipCount}회");
            sb.AppendLine($"• 스킵율: {skipRate:F1}%  (직접 사용 중이던 비율)");
            sb.AppendLine();
            sb.AppendLine("[활성 시간]");
            sb.AppendLine($"• 누적 활성 시간: {(int)activeTime.TotalHours:D2}:{activeTime:mm\\:ss}");
            sb.AppendLine($"• 활성 비율: {activeRate:F1}%  ({(int)activeTime.TotalHours:D2}:{activeTime:mm\\:ss} / {(int)todayElapsed.TotalHours:D2}:{todayElapsed:mm\\:ss})");

            // Slack 자동 상태 변경 통계 (오늘 변경이 1건 이상일 때만 표시)
            if (_dailySlackSuccessCount > 0 || _dailySlackFailCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("[Slack 자동 상태 변경]");
                sb.AppendLine($"• 성공: {_dailySlackSuccessCount}회  /  실패: {_dailySlackFailCount}회");
            }

            // 과거 히스토리 (오늘 제외 최대 6일)
            var history = StatsHistory.Load(6)
                .Where(x => x.Date.Date != DateTime.Today)
                .Take(6)
                .ToList();

            if (history.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[최근 {history.Count}일 히스토리]");
                sb.AppendLine($"{"날짜",-16} {"시뮬",-5} {"스킵",-5} {"활성시간",-10}");
                sb.AppendLine(new string('─', 42));
                foreach (var day in history)
                {
                    var d = day.Date;
                    var dName = dayNames[(int)d.DayOfWeek];
                    var at = day.ActiveTime;
                    sb.AppendLine($"{d:yyyy-MM-dd} ({dName})  {day.SimCount,4}회  {day.SkipCount,4}회   {(int)at.TotalHours:D2}:{at:mm\\:ss}");
                }

                // 주간 평균
                var avgSim = history.Average(x => x.SimCount);
                var avgSkip = history.Average(x => x.SkipCount);
                var avgActive = TimeSpan.FromSeconds(history.Average(x => x.ActiveSeconds));
                sb.AppendLine(new string('─', 42));
                sb.AppendLine($"{"평균",-13} {avgSim,5:F0}회 {avgSkip,5:F0}회   {(int)avgActive.TotalHours:D2}:{avgActive:mm\\:ss}");

                // ASCII 활성시간 차트
                var chartData = history.Take(7).Reverse().ToList();
                if (chartData.Count > 0)
                {
                    var maxSeconds = Math.Max(chartData.Max(x => x.ActiveSeconds), 1L);
                    sb.AppendLine();
                    sb.AppendLine("[활성시간 차트]");
                    const int barWidth = 18;
                    foreach (var day in chartData)
                    {
                        var d = day.Date;
                        var dName = dayNames[(int)d.DayOfWeek];
                        var bars = (int)Math.Round((double)day.ActiveSeconds / maxSeconds * barWidth);
                        var bar = new string('█', bars) + new string('░', barWidth - bars);
                        var at = day.ActiveTime;
                        sb.AppendLine($"{d:MM-dd} ({dName}) {bar} {(int)at.TotalHours:D2}:{at:mm\\:ss}");
                    }
                }
            }

            const int statsBaseHeight = 450;
            const int historyBaseHeight = 605;
            const int historyRowHeight = 30;
            var dialogHeight = history.Count > 0
                ? historyBaseHeight + historyRowHeight * (history.Count - 1)
                : statsBaseHeight;
            DarkInfoDialog.Show("통계", sb.ToString(), 500, dialogHeight);
        }

        private void ShowHelp()
        {
            var message = @"[트레이 아이콘 조작]
• 좌클릭              시작 / 정지 토글
• 더블클릭            오늘 통계 바로 보기
• 우클릭              컨텍스트 메뉴 열기
• 마우스 호버         다음 활동 카운트다운 + 오늘 활성 시간 + 세션 경과 시간
                      (정지 시: 오늘 누적 활성 시간)

[메뉴 — 시뮬레이션]
• ▶ 시작 / ⏹ 정지     시작 / 정지 토글
• 지금 활동 실행       즉시 강제 시뮬레이션 (타이머 리셋)

[메뉴 — 설정]
• 간격                 1 / 2 / 3 / 5 / 7분 선택 (권장: 3~5분)
• 이동 거리            10 / 30 / 50 / 100 / 200px 선택
• 활동 유형            마우스 이동 / 마우스 + 키보드 (F15)
• 디스플레이 절전 방지  화면 꺼짐 방지 On/Off
• 사용 중이면 건너뛰기  직접 입력 중 시뮬레이션 스킵

[메뉴 — Slack 자동 상태 변경]
• 자동 변경 활성화     출퇴근 시각에 Active/Away 자동 전환
• 시간 설정            출퇴근 시각 변경 (기본 08:55 / 18:55)
• 지금 활성/자리비움   즉시 수동 전환 (진행 상태 풍선 알림 표시)
• 방해 금지 (DND)      30분 / 1시간 / 2시간 설정 또는 해제
                       → /dnd 슬래시 커맨드를 Slack 앱에 자동 전송

[메뉴 — 통계]
• 오늘 통계            시뮬레이션·스킵·활성 시간·히스토리·차트
• 통계 CSV 내보내기    오늘 + 최근 30일 히스토리를 CSV 저장

[상태 표시 읽는 법]
  메뉴 열기 → '상태: 실행 중 (N회 · 다음 X분 XX초 후)' 즉시 확인
  트레이 호버 → '다음: X분 XX초 후 | 오늘 HH:MM:SS / N회 | 세션 HH:MM:SS'

[참고]
• 앱 실행 시 자동으로 시뮬레이션이 시작됩니다
• Slack 기능은 Slack 데스크탑 앱이 실행 중이어야 합니다
• 설정은 자동 저장되며 앱 재시작 후에도 유지됩니다";

            DarkInfoDialog.Show("도움말", message, 720, 780);
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
            var versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v?";
            var message = $@"StayAwake {versionStr}
Slack 자리 비움 상태 방지 도구
© 2026 https://github.com/wonhoz

[Slack 자리 비움 감지 방식]
• 10분간 키보드/마우스 비활성 시 Away
• Slack이 백그라운드에 있어도 시스템 활동 감지
• API로도 강제 Active 불가, 자연스러운 활동만 인정

[동작 방식]
• 주기적으로 마우스를 이동 후 원위치 (티 안 나게)
• SetThreadExecutionState로 디스플레이 절전 방지
• 사용자가 직접 활동 중이면 마우스 이동 자동 건너뜀
• Slack 상태 변경: 클립보드 방식으로 슬래시 커맨드 전송 (한글 IME 대응)

[현재 설정]
• 간격: {_intervalMinutes}분
• 이동 거리: {_simulator.MoveDistance}px
• 활동 유형: {activityTypeLabel}
• 디스플레이 절전 방지: {(_simulator.PreventDisplaySleep ? "켜짐" : "꺼짐")}
• 사용 중 건너뛰기: {(_simulator.SkipIfUserActive ? "켜짐" : "꺼짐")}
• Slack 자동 상태 변경: {slackStatusLine}";

            DarkInfoDialog.Show($"StayAwake {versionStr}", message, 750, 770);
        }

        private async void OnScheduleTimerTick(object? sender, EventArgs e)
        {
            try
            {
                var result = await _slackAutomation.CheckAndSetPresenceAsync();
                if (result == null) return;

                if (result.Success)
                {
                    // 변경 날짜 저장 — 재시작 시 중복 전송 방지
                    _settings.LastSlackActiveSetDate = _slackAutomation.LastActiveSet;
                    _settings.LastSlackAwaySetDate = _slackAutomation.LastAwaySet;
                    _settings.Save();
                    _dailySlackSuccessCount++;
                    SaveDailyStats();

                    var label = result.Status == "active" ? "활성" : "자리 비움";
                    _trayIcon.ShowBalloonTip(2000, "StayAwake",
                        $"Slack 상태를 '{label}'으로 변경했습니다.", ToolTipIcon.Info);
                }
                else
                {
                    _dailySlackFailCount++;
                    SaveDailyStats();
                    _trayIcon.ShowBalloonTip(2500, "StayAwake",
                        $"Slack 상태 변경 실패: {result.ErrorMessage}", ToolTipIcon.Warning);
                }
            }
            catch { /* 스케줄 타이머 예외 무시 */ }
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
            _trayIcon.ShowBalloonTip(1000, "StayAwake", "Slack 상태를 '활성'으로 변경 중...", ToolTipIcon.None);
            var result = await _slackAutomation.SetActiveAsync();
            if (result.Success) _dailySlackSuccessCount++; else _dailySlackFailCount++;
            SaveDailyStats();
            _trayIcon.ShowBalloonTip(1500, "StayAwake",
                result.Success ? "Slack 상태를 '활성'으로 변경했습니다." : $"실패: {result.ErrorMessage}",
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        private async Task SetSlackAwayNowAsync()
        {
            _trayIcon.ShowBalloonTip(1000, "StayAwake", "Slack 상태를 '자리비움'으로 변경 중...", ToolTipIcon.None);
            var result = await _slackAutomation.SetAwayAsync();
            if (result.Success) _dailySlackSuccessCount++; else _dailySlackFailCount++;
            SaveDailyStats();
            _trayIcon.ShowBalloonTip(1500, "StayAwake",
                result.Success ? "Slack 상태를 '자리비움'으로 변경했습니다." : $"실패: {result.ErrorMessage}",
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        private void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isRunning)
            {
                var nextActivity = _lastActivityTime.AddMinutes(_intervalMinutes) - DateTime.Now;
                if (nextActivity < TimeSpan.Zero) nextActivity = TimeSpan.Zero;
                _statusItem.Text = $"상태: 실행 중 ({_activityCount}회 · 다음 {(int)nextActivity.TotalMinutes}분 {nextActivity.Seconds:D2}초 후)";
            }
        }

        private async Task SetSlackDndAsync(int minutes)
        {
            var label = minutes == 0 ? "방해 금지 해제"
                : minutes < 60 ? $"방해 금지 {minutes}분"
                : $"방해 금지 {minutes / 60}시간";
            _trayIcon.ShowBalloonTip(1000, "StayAwake", $"Slack {label} 설정 중...", ToolTipIcon.None);
            var result = await _slackAutomation.SetDndAsync(minutes);
            if (result.Success) _dailySlackSuccessCount++; else _dailySlackFailCount++;
            SaveDailyStats();
            _trayIcon.ShowBalloonTip(1500, "StayAwake",
                result.Success ? $"Slack {label} 완료" : $"실패: {result.ErrorMessage}",
                result.Success ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }

        private void ExportStatsCsv()
        {
            try
            {
                using var dialog = new SaveFileDialog
                {
                    Title = "통계 CSV 내보내기",
                    Filter = "CSV 파일 (*.csv)|*.csv",
                    FileName = $"StayAwake_Stats_{DateTime.Today:yyyyMMdd}.csv",
                    DefaultExt = "csv"
                };
                if (dialog.ShowDialog() != DialogResult.OK) return;

                var activeTime = _isRunning
                    ? _todayActiveTime + (DateTime.Now - _sessionRunStart)
                    : _todayActiveTime;
                var history = StatsHistory.Load(30).Where(x => x.Date.Date != DateTime.Today).ToList();

                var lines = new System.Text.StringBuilder();
                lines.AppendLine("날짜,시뮬레이션,스킵,활성시간(초),Slack성공,Slack실패");
                lines.AppendLine($"{_statsDate:yyyy-MM-dd},{_dailySimCount},{_dailySkipCount},{(long)activeTime.TotalSeconds},{_dailySlackSuccessCount},{_dailySlackFailCount}");
                foreach (var day in history)
                    lines.AppendLine($"{day.Date:yyyy-MM-dd},{day.SimCount},{day.SkipCount},{day.ActiveSeconds},{day.SlackSuccessCount},{day.SlackFailCount}");

                File.WriteAllText(dialog.FileName, lines.ToString(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                _trayIcon.ShowBalloonTip(2000, "StayAwake", "통계 CSV 내보내기 완료", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                DarkInfoDialog.Show("내보내기 실패", $"CSV 내보내기 실패:\n{ex.Message}", 400, 200);
            }
        }

        // private void StartPause(int minutes)
        // {
        //     if (_isRunning) ToggleRunning(); // 실행 중이면 정지 (일시 중지 상태는 ToggleRunning에서 처리)
        //     _isPaused = true;
        //     _pauseEndTime = DateTime.Now.AddMinutes(minutes);
        //     _pauseTimer.Interval = minutes * 60 * 1000;
        //     _pauseTimer.Start();
        //     _tooltipTimer.Start(); // 카운트다운 표시
        //     _pauseCancelItem.Enabled = true;
        //     UpdatePauseTooltip();
        //     _trayIcon.ShowBalloonTip(1500, "StayAwake", $"일시 중지 — {minutes}분 후 자동 재시작", ToolTipIcon.Info);
        // }

        // private void CancelPause()
        // {
        //     _isPaused = false;
        //     _pauseTimer.Stop();
        //     _tooltipTimer.Stop();
        //     _pauseCancelItem.Enabled = false;
        //     _pauseCancelItem.Text = "취소";
        //     var stoppedTime = _todayActiveTime;
        //     var text = $"StayAwake - 정지됨 (오늘 {(int)stoppedTime.TotalHours:D2}:{stoppedTime:mm\\:ss})";
        //     _trayIcon.Text = text.Length > 127 ? text[..127] : text;
        //     _trayIcon.ShowBalloonTip(1500, "StayAwake", "일시 중지 취소됨", ToolTipIcon.Info);
        // }

        // private void OnPauseTimerTick(object? sender, EventArgs e)
        // {
        //     _isPaused = false;
        //     _pauseTimer.Stop();
        //     _pauseCancelItem.Enabled = false;
        //     _pauseCancelItem.Text = "취소";
        //     if (!_isRunning) ToggleRunning(); // 자동 재시작
        // }

        private void ExitApplication()
        {
            if (_isRunning)
                _todayActiveTime += DateTime.Now - _sessionRunStart;
            _isRunning = false; // SaveDailyStats에서 이중 계산 방지
            SaveDailyStats(); // 종료 시 최종 통계 저장
            _scheduleTimer.Stop();
            _activityTimer.Stop();
            _tooltipTimer.Stop();
            // _pauseTimer.Stop();
            _simulator.AllowSleep(); // 절전 방지 해제
            _trayIcon.Visible = false;
            Application.Exit();
        }

        private Icon CreateIcon(bool isRunning)
        {
            return isRunning ? _cachedRunningIcon! : _cachedStoppedIcon!;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _simulator.AllowSleep(); // 절전 방지 해제
                _scheduleTimer.Dispose();
                _activityTimer.Dispose();
                _tooltipTimer.Dispose();
                _singleClickTimer.Dispose();
                // _pauseTimer.Dispose();
                _trayIcon.Dispose();
                _contextMenu.Dispose();
                _menuFont?.Dispose();
                _startStopFont?.Dispose();
                _cachedRunningIcon?.Dispose();
                _cachedStoppedIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
