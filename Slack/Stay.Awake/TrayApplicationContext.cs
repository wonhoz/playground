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
        private readonly ActivitySimulator _simulator;

        private ToolStripMenuItem _startStopItem = null!;
        private ToolStripMenuItem _intervalItem = null!;
        private ToolStripMenuItem _distanceItem = null!;
        private ToolStripMenuItem _statusItem = null!;
        private ToolStripMenuItem _skipIfActiveItem = null!;

        private bool _isRunning = false;
        private int _intervalMinutes = 3; // 기본 3분 (Slack 10분 타임아웃의 1/3)
        private int _activityCount = 0;
        private DateTime _startTime;

        public TrayApplicationContext()
        {
            _simulator = new ActivitySimulator();

            // 타이머 설정
            _activityTimer = new System.Windows.Forms.Timer
            {
                Interval = _intervalMinutes * 60 * 1000
            };
            _activityTimer.Tick += OnTimerTick;

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

            // 시작 알림
            _trayIcon.ShowBalloonTip(2000, "StayAwake",
                "더블클릭하여 시작/정지\n우클릭하여 메뉴 열기", ToolTipIcon.Info);
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
            var menuFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            menu.Font = menuFont;

            // 시작/정지
            _startStopItem = new ToolStripMenuItem("▶ 시작", null, (s, e) => ToggleRunning())
            {
                Font = new Font(menuFont, FontStyle.Bold),
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
            activityTypeItem.DropDownItems.Add(new ToolStripMenuItem("마우스 이동", null, (s, e) => SetActivityType(ActivityType.MouseMove)) { Checked = true });
            activityTypeItem.DropDownItems.Add(new ToolStripMenuItem("마우스 + 키보드", null, (s, e) => SetActivityType(ActivityType.MouseAndKeyboard)));
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
                _activityTimer.Start();
                _startStopItem.Text = "⏹ 정지";
                _startStopItem.ForeColor = Color.FromArgb(234, 67, 53); // Red for stop
                _trayIcon.Icon = CreateIcon(true);
                _trayIcon.Text = $"StayAwake - 실행 중 ({_intervalMinutes}분 간격)";
                UpdateStatus();

                _trayIcon.ShowBalloonTip(1500, "StayAwake",
                    $"활동 시뮬레이션 시작됨 ({_intervalMinutes}분 간격, {_simulator.MoveDistance}px)", ToolTipIcon.Info);
            }
            else
            {
                _activityTimer.Stop();
                _simulator.AllowSleep(); // 절전 방지 해제
                _startStopItem.Text = "▶ 시작";
                _startStopItem.ForeColor = Color.FromArgb(76, 175, 80); // Green for start
                _trayIcon.Icon = CreateIcon(false);
                _trayIcon.Text = "StayAwake - 정지됨";
                _statusItem.Text = "상태: 정지됨";

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

            // 체크 상태 업데이트
            foreach (ToolStripMenuItem item in _intervalItem.DropDownItems)
            {
                item.Checked = item.Text == $"{minutes}분";
            }

            if (_isRunning)
            {
                _trayIcon.Text = $"StayAwake - 실행 중 ({_intervalMinutes}분 간격)";
            }
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
        }

        private void ToggleSkipIfActive()
        {
            _simulator.SkipIfUserActive = !_simulator.SkipIfUserActive;
            _skipIfActiveItem.Checked = _simulator.SkipIfUserActive;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            bool simulated = _simulator.SimulateActivity();
            if (simulated)
            {
                _activityCount++;
            }
            UpdateStatus(simulated);
        }

        private void SimulateNow()
        {
            bool simulated = _simulator.SimulateActivity();
            if (_isRunning && simulated)
            {
                _activityCount++;
                UpdateStatus(simulated);
            }
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

        private void ShowAbout()
        {
            var message = $@"StayAwake v1.2

Slack 자리 비움 상태 방지 도구

[Slack 자리 비움 감지 방식]
• 10분간 키보드/마우스 비활성 시 Away
• Slack이 백그라운드에 있어도 시스템 활동 감지
• API로도 강제 Active 불가, 자연스러운 활동만 인정

[동작 방식]
• 주기적으로 마우스를 이동 후 원위치
• SetThreadExecutionState로 디스플레이 절전 방지
• 사용자가 직접 활동 중이면 마우스 이동 자동 건너뜀

[현재 설정]
• 간격: {_intervalMinutes}분
• 이동 거리: {_simulator.MoveDistance}px
• 디스플레이 절전 방지: {(_simulator.PreventDisplaySleep ? "켜짐" : "꺼짐")}
• 사용 중 건너뛰기: {(_simulator.SkipIfUserActive ? "켜짐" : "꺼짐")}

[사용법]
• 더블클릭: 시작/정지 토글
• 우클릭: 메뉴 열기
• 권장 간격: 3~5분

© 2026 SmartCareworks Inc.";

            MessageBox.Show(message, "StayAwake 정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApplication()
        {
            _activityTimer.Stop();
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
                _activityTimer.Dispose();
                _trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
