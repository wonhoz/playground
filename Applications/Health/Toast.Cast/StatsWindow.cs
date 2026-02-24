using System.Drawing;
using System.Runtime.InteropServices;
using ToastCast.Models;
using ToastCast.Services;

namespace ToastCast;

/// <summary>주간 달성률 통계 창</summary>
public sealed class StatsWindow : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static StatsWindow? _instance;

    public static void Show(AppConfig config)
    {
        if (_instance != null && !_instance.IsDisposed) { _instance.Activate(); return; }
        _instance = new StatsWindow(config);
        _instance.Show();
    }

    private StatsWindow(AppConfig config)
    {
        Text = "Toast.Cast — 주간 통계";
        Size = new Size(480, 480);
        BackColor = Color.FromArgb(26, 26, 36);
        ForeColor = Color.FromArgb(230, 230, 235);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var header = new Label
        {
            Text = "📊 이번 주 건강 루틴 달성률",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 150),
            AutoSize = true,
            Location = new Point(20, 16)
        };

        var weekLabel = new Label
        {
            Text = GetWeekRange(),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(120, 120, 140),
            AutoSize = true,
            Location = new Point(20, 44)
        };

        Controls.Add(header);
        Controls.Add(weekLabel);

        var stats = StatsService.GetWeeklyStats(config.Routines);
        var y = 76;
        foreach (var (routineId, stat) in stats)
        {
            var routine = config.Routines.FirstOrDefault(r => r.Id == routineId);
            if (routine == null) continue;
            Controls.Add(CreateStatRow(stat, y));
            y += 72;
        }

        // 닫기 버튼
        var btnClose = new Button
        {
            Text = "닫기",
            Bounds = new Rectangle(Width / 2 - 60, y + 20, 120, 34),
            BackColor = Color.FromArgb(45, 45, 62),
            ForeColor = Color.FromArgb(200, 200, 215),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 80);
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        Height = y + 120;
    }

    private Panel CreateStatRow(WeeklyRoutineStat stat, int y)
    {
        var panel = new Panel
        {
            Bounds = new Rectangle(16, y, Width - 48, 64),
            BackColor = Color.FromArgb(32, 32, 46)
        };

        var pct = (int)(stat.Rate * 100);
        var color = pct >= 80 ? Color.FromArgb(100, 220, 150)
                  : pct >= 50 ? Color.FromArgb(240, 200, 80)
                  : Color.FromArgb(220, 100, 90);

        var lblName = new Label
        {
            Text = $"{stat.Icon}  {stat.RoutineName}",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 230),
            AutoSize = true,
            Location = new Point(12, 8)
        };

        var lblRate = new Label
        {
            Text = $"{pct}%  ({stat.Achieved}/{stat.Expected})",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = color,
            AutoSize = true,
            Location = new Point(panel.Width - 140, 8)
        };

        // 진행 바 배경
        var barBg = new Panel
        {
            Bounds = new Rectangle(12, 36, panel.Width - 24, 10),
            BackColor = Color.FromArgb(50, 50, 65)
        };

        // 진행 바
        var barFill = new Panel
        {
            Bounds = new Rectangle(0, 0, (int)((barBg.Width - 0) * Math.Min(stat.Rate, 1.0)), 10),
            BackColor = color
        };
        barBg.Controls.Add(barFill);

        panel.Controls.AddRange([lblName, lblRate, barBg]);

        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(48, 48, 65), 1f);
            e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 8);
        };

        return panel;
    }

    private static string GetWeekRange()
    {
        var now = DateTime.Now;
        var dow = (int)now.DayOfWeek;
        var weekStart = now.Date.AddDays(dow == 0 ? -6 : -(dow - 1));
        var weekEnd = weekStart.AddDays(6);
        return $"{weekStart:MM/dd} ~ {weekEnd:MM/dd}";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _instance = null;
    }
}
