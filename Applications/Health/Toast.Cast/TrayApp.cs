using System.Drawing;
using System.Runtime.InteropServices;
using ToastCast.Models;
using ToastCast.Services;

namespace ToastCast;

public sealed class TrayApp : ApplicationContext
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private AppConfig _config;
    private readonly System.Windows.Forms.Timer _checkTimer;

    // 현재 표시 중인 카운트다운 오버레이
    private CountdownOverlay? _currentOverlay;

    public TrayApp()
    {
        SetProcessDPIAware();
        _config = AppConfig.Load();
        ScheduleAll();

        _menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            AutoSize = true,
            ShowImageMargin = false,
            Font = new Font("Segoe UI", 9.5f)
        };

        _tray = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "Toast.Cast",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _tray.ShowBalloonTip(2000, "Toast.Cast",
            "건강 루틴 알림 시작 💚\n트레이 아이콘을 우클릭하여 설정하세요.",
            ToolTipIcon.Info);

        // 1분마다 루틴 체크
        _checkTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _checkTimer.Tick += OnTimerTick;
        _checkTimer.Start();

        BuildMenu();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_config.AutoStart) return;

        var now = DateTime.Now;
        foreach (var routine in _config.Routines.Where(r => r.Enabled))
        {
            if (routine.NextFireAt > now) continue;

            // 다음 알림 시간 갱신 (먼저 스케줄링)
            routine.NextFireAt = now.AddMinutes(routine.IntervalMinutes);

            // 유휴 상태면 스킵
            if (IdleDetectionService.IsIdle(_config.IdleThresholdMinutes))
            {
                StatsService.AddRecord(new RoutineRecord
                {
                    RoutineId = routine.Id,
                    RoutineName = routine.Name,
                    Skipped = true
                });
                continue;
            }

            FireRoutine(routine);
        }

        BuildMenu();
    }

    private void FireRoutine(Routine routine)
    {
        if (routine.ShowCountdown && _currentOverlay == null)
        {
            ShowCountdownOverlay(routine);
        }
        else
        {
            // 카운트다운 없이 Toast 알림만
            _tray.ShowBalloonTip(5000, $"{routine.Icon} {routine.Name}", routine.Description, ToolTipIcon.Info);
            StatsService.AddRecord(new RoutineRecord
            {
                RoutineId = routine.Id,
                RoutineName = routine.Name,
                Dismissed = true
            });
        }
    }

    private void ShowCountdownOverlay(Routine routine)
    {
        _currentOverlay = new CountdownOverlay(routine.Icon, routine.Name, routine.CountdownSeconds, routine.Id);

        _currentOverlay.Completed += (_, _) =>
        {
            StatsService.AddRecord(new RoutineRecord
            {
                RoutineId = routine.Id,
                RoutineName = routine.Name,
                Dismissed = true
            });
            _currentOverlay = null;
        };

        _currentOverlay.Skipped += (_, _) =>
        {
            StatsService.AddRecord(new RoutineRecord
            {
                RoutineId = routine.Id,
                RoutineName = routine.Name,
                Skipped = false,
                Dismissed = false
            });
            _currentOverlay = null;
        };

        _currentOverlay.FormClosed += (_, _) => _currentOverlay = null;
        _currentOverlay.Show();
    }

    private void ScheduleAll()
    {
        var now = DateTime.Now;
        foreach (var routine in _config.Routines)
            if (routine.NextFireAt == DateTime.MinValue)
                routine.NextFireAt = now.AddMinutes(routine.IntervalMinutes);
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        // 헤더
        var header = new ToolStripMenuItem("💚 Toast.Cast") { Enabled = false };
        header.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _menu.Items.Add(header);
        _menu.Items.Add(new ToolStripSeparator());

        // 루틴 목록 (다음 알림까지 남은 시간)
        foreach (var routine in _config.Routines.Where(r => r.Enabled))
        {
            var remaining = routine.NextFireAt - DateTime.Now;
            var remainStr = remaining.TotalMinutes >= 1
                ? $"{(int)remaining.TotalMinutes}분 후"
                : "곧";
            var item = new ToolStripMenuItem($"{routine.Icon} {routine.Name}  —  {remainStr}")
            {
                Enabled = false
            };
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());

        // 일시정지 / 재개
        var pauseItem = new ToolStripMenuItem(_config.AutoStart ? "⏸ 일시정지" : "▶ 재개");
        pauseItem.Click += (_, _) =>
        {
            _config.AutoStart = !_config.AutoStart;
            _config.Save();
            _tray.ShowBalloonTip(1500, "Toast.Cast",
                _config.AutoStart ? "루틴 알림이 재개되었습니다." : "루틴 알림이 일시정지되었습니다.",
                ToolTipIcon.Info);
            BuildMenu();
        };
        _menu.Items.Add(pauseItem);

        // 지금 당장 테스트
        var testItem = new ToolStripMenuItem("🔔 지금 테스트");
        testItem.Click += (_, _) =>
        {
            var first = _config.Routines.FirstOrDefault(r => r.Enabled);
            if (first != null) FireRoutine(first);
        };
        _menu.Items.Add(testItem);

        _menu.Items.Add(new ToolStripSeparator());

        // 설정
        var settingsItem = new ToolStripMenuItem("⚙ 설정");
        settingsItem.Click += (_, _) =>
        {
            SettingsWindow.Show(_config, () =>
            {
                _config = AppConfig.Load();
                ScheduleAll();
                StatsService.InvalidateCache();
                BuildMenu();
            });
        };
        _menu.Items.Add(settingsItem);

        // 통계
        var statsItem = new ToolStripMenuItem("📊 주간 통계");
        statsItem.Click += (_, _) => StatsWindow.Show(_config);
        _menu.Items.Add(statsItem);

        _menu.Items.Add(new ToolStripSeparator());

        // 종료
        var exitItem = new ToolStripMenuItem("❌ 종료");
        exitItem.Click += (_, _) => { _tray.Visible = false; Application.Exit(); };
        _menu.Items.Add(exitItem);
    }

    private static Icon CreateIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(18, 18, 28));

        // 하트 (건강)
        using var heartBrush = new SolidBrush(Color.FromArgb(100, 220, 150));
        DrawHeart(g, heartBrush, 3, 4, 26, 24);

        // 알림 물결
        using var wavePen = new Pen(Color.FromArgb(255, 200, 80), 1.5f);
        g.DrawArc(wavePen, 22, 3, 8, 8, 200, 140);
        g.DrawArc(wavePen, 20, 1, 12, 12, 200, 140);

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    private static void DrawHeart(Graphics g, Brush brush, float x, float y, float w, float h)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float cx = x + w / 2f, cy = y + h / 2f;
        float r = w / 4f;

        // 왼쪽 원
        path.AddEllipse(x, y, r * 2, r * 2);
        // 오른쪽 원
        path.AddEllipse(cx, y, r * 2, r * 2);
        // 아래 삼각형
        path.AddPolygon([
            new PointF(x, y + r),
            new PointF(cx + r * 2, y + r),
            new PointF(cx, y + h * 0.85f)
        ]);

        g.FillPath(brush, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _checkTimer.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _currentOverlay?.Close();
        }
        base.Dispose(disposing);
    }
}
