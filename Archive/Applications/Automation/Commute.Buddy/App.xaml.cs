using System.Windows;
using CommuteBuddy.Models;
using CommuteBuddy.Services;
using CommuteBuddy.Views;

namespace CommuteBuddy;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _tray;
    private WifiMonitor?       _wifiMonitor;
    private RoutineEngine?     _routineEngine;
    private CommuteLogger?     _commuteLogger;
    private SettingsService?   _settingsService;
    private AppSettings        _settings = new();
    private Location?          _currentLocation;
    private DateTime           _remoteWorkFiredDate = DateTime.MinValue;
    private System.Windows.Forms.Timer? _remoteWorkTimer;
    private SettingsWindow?    _settingsWindow;
    private StatsWindow?       _statsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 아이콘 생성
        var resDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        IconGenerator.Generate(resDir);

        // 서비스 초기화
        _settingsService = new SettingsService();
        _settings        = _settingsService.Load();
        _commuteLogger   = new CommuteLogger();
        _routineEngine   = new RoutineEngine(_settings);

        // WiFi 모니터 초기화 (이벤트 연결 후 Start)
        _wifiMonitor = new WifiMonitor();
        _wifiMonitor.SsidChanged += OnSsidChanged;
        _wifiMonitor.Start();

        // 시작 시 현재 SSID로 초기 위치 조용히 설정 (루틴 실행 없이)
        _currentLocation = FindLocation(_wifiMonitor.CurrentSsid);

        // 재택 모드 타이머 (1분마다 체크)
        _remoteWorkTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _remoteWorkTimer.Tick += OnRemoteWorkTick;
        _remoteWorkTimer.Start();

        // 트레이 초기화
        InitTray(resDir);

        // 시작 풍선 알림
        var locMsg = _currentLocation != null
            ? $"현재 위치: {_currentLocation.Emoji} {_currentLocation.Name}"
            : "WiFi SSID를 설정에서 등록해 주세요.";
        _tray?.ShowBalloonTip(3000, "Commute.Buddy",
            $"출퇴근 감지 시작! {locMsg}",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    // ── WiFi 변경 처리 ────────────────────────────────────────────────────

    private void OnSsidChanged(string ssid)
    {
        Dispatcher.Invoke(() =>
        {
            var newLocation = FindLocation(ssid);

            if (newLocation?.Name == _currentLocation?.Name) return; // 동일 위치

            if (newLocation != null)
            {
                SwitchLocation(newLocation);
            }
            else if (_currentLocation != null)
            {
                // 알려진 장소를 벗어났지만 새 장소를 인식 못함
                ExecuteDeparture(_currentLocation);
                _currentLocation = null;
                UpdateTrayTooltip();
                RebuildTrayMenu();
            }
        });
    }

    private void SwitchLocation(Location newLocation, string? logSuffix = null)
    {
        // 이전 장소 퇴근 루틴
        if (_currentLocation != null)
            ExecuteDeparture(_currentLocation);

        _currentLocation = newLocation;

        // 새 장소 도착 루틴
        _routineEngine!.Execute(newLocation.ArrivalRoutine);
        _commuteLogger!.Log(newLocation.Name,
            logSuffix != null ? $"arrived ({logSuffix})" : "arrived");

        if (newLocation.ArrivalRoutine.ShowNotification)
        {
            var msg = logSuffix != null
                ? $"{newLocation.Emoji} 재택 근무 시작! ({newLocation.Name})"
                : $"{newLocation.Emoji} {newLocation.Name}에 도착했습니다!";
            ShowBalloon(msg);
        }

        UpdateTrayTooltip();
        RebuildTrayMenu();
    }

    private void ExecuteDeparture(Location loc)
    {
        _routineEngine!.Execute(loc.DepartureRoutine);
        _commuteLogger!.Log(loc.Name, "left");

        if (loc.DepartureRoutine.ShowNotification)
            ShowBalloon($"{loc.Emoji} {loc.Name}에서 퇴근했습니다.");
    }

    // ── 재택 모드 타이머 ─────────────────────────────────────────────────

    private void OnRemoteWorkTick(object? sender, EventArgs e)
    {
        var rw = _settings.RemoteWork;
        if (!rw.Enabled) return;
        if (_remoteWorkFiredDate == DateTime.Today) return;

        var now = DateTime.Now;
        if (now.Hour != rw.StartHour || now.Minute != rw.StartMinute) return;

        var location = _settings.Locations.FirstOrDefault(l => l.Name == rw.LocationName);
        if (location == null) return;
        if (!location.Ssids.Contains(_wifiMonitor!.CurrentSsid)) return;
        if (_currentLocation?.Name == location.Name) return; // 이미 해당 위치

        _remoteWorkFiredDate = DateTime.Today;
        Dispatcher.Invoke(() => SwitchLocation(location, "재택"));
    }

    // ── 트레이 ───────────────────────────────────────────────────────────

    private void InitTray(string resDir)
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text    = "Commute.Buddy",
            Visible = true,
        };

        var icoPath = Path.Combine(resDir, IconGenerator.IconFileName);
        if (File.Exists(icoPath))
            _tray.Icon = new System.Drawing.Icon(icoPath);

        _tray.DoubleClick += (_, _) => ShowSettings();

        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Renderer = new DarkMenuRenderer();
        var font = new System.Drawing.Font("Segoe UI", 9.5f);
        menu.Font = font;

        // ── 현재 위치 표시 (비활성)
        var locText = _currentLocation != null
            ? $"📍 현재: {_currentLocation.Emoji} {_currentLocation.Name}"
            : "📍 현재 위치: 알 수 없음";
        var locItem = menu.Items.Add(locText);
        locItem.Enabled = false;

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── 수동 위치 전환
        var switchMenu = new System.Windows.Forms.ToolStripMenuItem("🔄 수동 전환");
        foreach (var loc in _settings.Locations)
        {
            var l    = loc;
            var item = new System.Windows.Forms.ToolStripMenuItem($"{l.Emoji} {l.Name}");
            item.Checked = (_currentLocation?.Name == l.Name);
            item.Click  += (_, _) => Dispatcher.Invoke(() => SwitchLocation(l, "수동"));
            switchMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(switchMenu);

        // ── 지금 WiFi 확인
        var checkItem = menu.Items.Add("🔍 WiFi 지금 확인");
        checkItem.Click += (_, _) => _wifiMonitor?.CheckNow();

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── 통계
        var statsItem = menu.Items.Add("📊 이번 달 통계");
        statsItem.Click += (_, _) => Dispatcher.Invoke(ShowStats);

        // ── 설정
        var settingsItem = menu.Items.Add("⚙️ 설정");
        settingsItem.Click += (_, _) => Dispatcher.Invoke(ShowSettings);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── 종료
        var exitItem = menu.Items.Add("종료");
        exitItem.Click += (_, _) => ExitApp();

        if (_tray != null)
            _tray.ContextMenuStrip = menu;
    }

    private void UpdateTrayTooltip()
    {
        if (_tray == null) return;
        _tray.Text = _currentLocation != null
            ? $"Commute.Buddy — {_currentLocation.Emoji} {_currentLocation.Name}"
            : "Commute.Buddy — 위치 미인식";
    }

    // ── 창 ───────────────────────────────────────────────────────────────

    private void ShowSettings()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.Closed += (_, _) =>
        {
            if (_settingsWindow.UpdatedSettings != null)
            {
                _settings = _settingsWindow.UpdatedSettings;
                _settingsService!.Save(_settings);
                _routineEngine!.UpdateSettings(_settings);
                RebuildTrayMenu();
            }
            _settingsWindow = null;
        };
        _settingsWindow.Show();
    }

    private void ShowStats()
    {
        if (_statsWindow != null)
        {
            _statsWindow.Activate();
            return;
        }

        _statsWindow = new StatsWindow(_commuteLogger!);
        _statsWindow.Closed += (_, _) => _statsWindow = null;
        _statsWindow.Show();
    }

    // ── 종료 ─────────────────────────────────────────────────────────────

    private void ExitApp()
    {
        _remoteWorkTimer?.Stop();
        _remoteWorkTimer?.Dispose();
        _wifiMonitor?.Dispose();
        _tray?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _remoteWorkTimer?.Dispose();
        _wifiMonitor?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }

    // ── 유틸 ──────────────────────────────────────────────────────────────

    private Location? FindLocation(string ssid)
    {
        if (string.IsNullOrEmpty(ssid)) return null;
        return _settings.Locations
            .FirstOrDefault(l => l.Ssids.Any(s =>
                s.Trim().Equals(ssid.Trim(), StringComparison.Ordinal)));
    }

    private void ShowBalloon(string msg)
    {
        _tray?.ShowBalloonTip(2500, "Commute.Buddy", msg,
            System.Windows.Forms.ToolTipIcon.Info);
    }
}
