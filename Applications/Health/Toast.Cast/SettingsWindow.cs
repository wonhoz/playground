using System.Drawing;
using System.Runtime.InteropServices;
using ToastCast.Models;

namespace ToastCast;

/// <summary>루틴 설정 창</summary>
public sealed class SettingsWindow : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly AppConfig _config;
    private readonly Action _onSave;
    private readonly FlowLayoutPanel _routinePanel;

    private static SettingsWindow? _instance;

    public static void Show(AppConfig config, Action onSave)
    {
        if (_instance != null && !_instance.IsDisposed) { _instance.Activate(); return; }
        _instance = new SettingsWindow(config, onSave);
        _instance.Show();
    }

    private SettingsWindow(AppConfig config, Action onSave)
    {
        _config = config;
        _onSave = onSave;

        Text = "Toast.Cast — 루틴 설정";
        Size = new Size(560, 640);
        MinimumSize = new Size(560, 560);
        BackColor = Color.FromArgb(26, 26, 36);
        ForeColor = Color.FromArgb(230, 230, 235);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        // 헤더
        var header = new Label
        {
            Text = "💚 루틴 설정",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 150),
            AutoSize = true,
            Location = new Point(20, 16)
        };

        // 루틴 목록 패널
        _routinePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Bounds = new Rectangle(16, 56, 528, 450),
            BackColor = Color.FromArgb(26, 26, 36)
        };

        // 유휴 설정 행
        var idleLabel = new Label
        {
            Text = "유휴 감지 기준 (분):",
            ForeColor = Color.FromArgb(160, 160, 180),
            AutoSize = true,
            Location = new Point(20, 518)
        };
        var idleSpinner = new NumericUpDown
        {
            Minimum = 1, Maximum = 30, Value = _config.IdleThresholdMinutes,
            BackColor = Color.FromArgb(38, 38, 52),
            ForeColor = Color.FromArgb(230, 230, 235),
            BorderStyle = BorderStyle.FixedSingle,
            Bounds = new Rectangle(220, 514, 60, 24)
        };

        // 저장 버튼
        var btnSave = CreateButton("💾  저장", new Rectangle(380, 550, 160, 36), Color.FromArgb(60, 150, 100));
        btnSave.Click += (_, _) =>
        {
            _config.IdleThresholdMinutes = (int)idleSpinner.Value;
            _config.Save();
            _onSave();
            Close();
        };

        Controls.AddRange([header, _routinePanel, idleLabel, idleSpinner, btnSave]);
        BuildRoutineCards();
    }

    private void BuildRoutineCards()
    {
        _routinePanel.Controls.Clear();
        foreach (var routine in _config.Routines)
            _routinePanel.Controls.Add(CreateRoutineCard(routine));
    }

    private Panel CreateRoutineCard(Routine routine)
    {
        var card = new Panel
        {
            Size = new Size(508, 92),
            BackColor = Color.FromArgb(34, 34, 48),
            Margin = new Padding(0, 0, 0, 8),
            Cursor = Cursors.Default
        };

        // 아이콘 + 이름
        var lblName = new Label
        {
            Text = $"{routine.Icon}  {routine.Name}",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 230, 235),
            AutoSize = true,
            Location = new Point(12, 10)
        };

        // 설명
        var lblDesc = new Label
        {
            Text = routine.Description,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(130, 130, 150),
            AutoSize = false,
            Size = new Size(370, 36),
            Location = new Point(12, 34)
        };

        // 활성화 토글 — Appearance.Button + 명시적 Size (AutoSize는 Korean 텍스트 측정 오류 있음)
        var chkEnabled = new CheckBox
        {
            Text = "활성",
            Checked = routine.Enabled,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(38, 38, 54),
            Size = new Size(68, 28),
            Location = new Point(428, 8),
            Cursor = Cursors.Hand
        };
        chkEnabled.FlatAppearance.CheckedBackColor = Color.FromArgb(28, 90, 58);
        chkEnabled.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 95);
        chkEnabled.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 72);
        chkEnabled.FlatAppearance.BorderSize = 1;
        chkEnabled.CheckedChanged += (_, _) => { routine.Enabled = chkEnabled.Checked; };

        // 간격 입력
        var lblInterval = new Label
        {
            Text = "간격(분):",
            ForeColor = Color.FromArgb(150, 150, 170),
            AutoSize = true,
            Location = new Point(12, 64)
        };
        var spInterval = new NumericUpDown
        {
            Minimum = 1, Maximum = 480, Value = routine.IntervalMinutes,
            BackColor = Color.FromArgb(38, 38, 52),
            ForeColor = Color.FromArgb(230, 230, 235),
            BorderStyle = BorderStyle.FixedSingle,
            Bounds = new Rectangle(88, 62, 72, 24)
        };
        spInterval.ValueChanged += (_, _) => { routine.IntervalMinutes = (int)spInterval.Value; };

        // 카운트다운 토글 — Appearance.Button + 명시적 Size
        var chkCountdown = new CheckBox
        {
            Text = "카운트다운",
            Checked = routine.ShowCountdown,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(38, 38, 54),
            Size = new Size(100, 28),
            Location = new Point(172, 62),
            Cursor = Cursors.Hand
        };
        chkCountdown.FlatAppearance.CheckedBackColor = Color.FromArgb(28, 90, 58);
        chkCountdown.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 95);
        chkCountdown.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 72);
        chkCountdown.FlatAppearance.BorderSize = 1;
        chkCountdown.CheckedChanged += (_, _) => { routine.ShowCountdown = chkCountdown.Checked; };

        card.Controls.AddRange([lblName, lblDesc, chkEnabled, lblInterval, spInterval, chkCountdown]);

        // 라운드 코너 페인트
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(50, 50, 70), 1f);
            e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
        };

        return card;
    }

    private static Button CreateButton(string text, Rectangle bounds, Color backColor)
    {
        var btn = new Button
        {
            Text = text,
            Bounds = bounds,
            BackColor = backColor,
            ForeColor = Color.FromArgb(230, 230, 235),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.1f);
        return btn;
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

// Graphics 확장
internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddRoundedRectangle(rect, radius);
        g.DrawPath(pen, path);
    }
}
