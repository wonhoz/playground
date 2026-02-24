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

        Text = "Toast.Cast \u2014 루틴 설정";
        Size = new Size(660, 780);
        MinimumSize = new Size(620, 700);
        BackColor = Color.FromArgb(26, 26, 36);
        ForeColor = Color.FromArgb(230, 230, 235);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        var header = new Label
        {
            Text = "\U0001F49A 루틴 설정",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 150),
            AutoSize = true,
            Location = new Point(22, 18)
        };

        _routinePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Bounds = new Rectangle(18, 64, 624, 568),
            BackColor = Color.FromArgb(26, 26, 36)
        };

        var idleLabel = new Label
        {
            Text = "유휴 감지 기준 (분):",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 160, 180),
            AutoSize = true,
            Location = new Point(22, 648)
        };
        var (idlePanel, getIdleValue) = CreateDarkSpinner(1, 30, _config.IdleThresholdMinutes, _ => { });
        idlePanel.Location = new Point(254, 640);

        var btnSave = CreateButton("\U0001F4BE  저장", new Rectangle(452, 688, 190, 52), Color.FromArgb(60, 150, 100));
        btnSave.Font = new Font("Segoe UI", 10.5f);
        btnSave.Click += (_, _) =>
        {
            _config.IdleThresholdMinutes = getIdleValue();
            _config.Save();
            _onSave();
            Close();
        };

        Controls.AddRange([header, _routinePanel, idleLabel, idlePanel, btnSave]);
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
        // 카드: 패널 폭 624 - 좌우 margin 4*2 = 616
        var card = new Panel
        {
            Size = new Size(612, 140),
            BackColor = Color.FromArgb(34, 34, 48),
            Margin = new Padding(4, 0, 4, 12),
            Cursor = Cursors.Default
        };

        var lblName = new Label
        {
            Text = $"{routine.Icon}  {routine.Name}",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 230, 235),
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var lblDesc = new Label
        {
            Text = routine.Description,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(130, 130, 150),
            AutoSize = false,
            Size = new Size(430, 40),
            Location = new Point(16, 52)
        };

        // 활성 토글 — Appearance.Button + 명시적 Size/Font
        var chkEnabled = new CheckBox
        {
            Text = "활성",
            Checked = routine.Enabled,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(38, 38, 54),
            Size = new Size(90, 36),
            Location = new Point(508, 10),
            Cursor = Cursors.Hand
        };
        chkEnabled.FlatAppearance.CheckedBackColor = Color.FromArgb(28, 90, 58);
        chkEnabled.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 95);
        chkEnabled.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 72);
        chkEnabled.FlatAppearance.BorderSize = 1;
        chkEnabled.CheckedChanged += (_, _) => { routine.Enabled = chkEnabled.Checked; };

        // 하단 행: 간격 스피너 + 카운트다운
        var lblInterval = new Label
        {
            Text = "간격(분):",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(150, 150, 170),
            AutoSize = true,
            Location = new Point(16, 102)
        };
        var (spPanel, _) = CreateDarkSpinner(1, 480, routine.IntervalMinutes, v => { routine.IntervalMinutes = v; });
        spPanel.Location = new Point(100, 96);

        var chkCountdown = new CheckBox
        {
            Text = "카운트다운",
            Checked = routine.ShowCountdown,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(38, 38, 54),
            Size = new Size(170, 36),
            Location = new Point(230, 96),
            Cursor = Cursors.Hand
        };
        chkCountdown.FlatAppearance.CheckedBackColor = Color.FromArgb(28, 90, 58);
        chkCountdown.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 95);
        chkCountdown.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 72);
        chkCountdown.FlatAppearance.BorderSize = 1;
        chkCountdown.CheckedChanged += (_, _) => { routine.ShowCountdown = chkCountdown.Checked; };

        card.Controls.AddRange([lblName, lblDesc, chkEnabled, lblInterval, spPanel, chkCountdown]);

        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(50, 50, 70), 1f);
            e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
        };

        return card;
    }

    /// <summary>
    /// 다크 테마 커스텀 스피너 [ - | val | + ]
    /// Label 기반: AutoSize=false 필수 (기본값 true이면 Bounds의 Size가 무시됨)
    /// </summary>
    private static (Panel panel, Func<int> getValue) CreateDarkSpinner(int min, int max, int initial, Action<int> onChange)
    {
        var val = initial;
        var btnBg  = Color.FromArgb(40, 40, 60);
        var btnHov = Color.FromArgb(64, 64, 90);
        var divCol = Color.FromArgb(58, 58, 80);   // 컨테이너 BackColor = 1px 갭 → 보더+구분선

        var container = new Panel { Size = new Size(114, 36), BackColor = divCol };

        // AutoSize = false 필수: 설정 안 하면 WinForms가 Bounds의 크기를 무시하고
        // 텍스트 크기에 맞게 Label을 축소시켜 "-"/"+"가 1~2px 크기로 렌더링됨
        var lblMinus = new Label
        {
            Text = "-",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(215, 215, 245),
            BackColor = btnBg,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Bounds = new Rectangle(1, 1, 38, 34),
            Cursor = Cursors.Hand
        };

        var lblVal = new Label
        {
            Text = val.ToString(),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(215, 215, 235),
            BackColor = Color.FromArgb(24, 24, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Bounds = new Rectangle(40, 1, 34, 34)
        };

        var lblPlus = new Label
        {
            Text = "+",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(215, 215, 245),
            BackColor = btnBg,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Bounds = new Rectangle(75, 1, 38, 34),
            Cursor = Cursors.Hand
        };

        lblMinus.Click += (_, _) => { if (val > min) { val--; lblVal.Text = val.ToString(); onChange(val); } };
        lblPlus.Click  += (_, _) => { if (val < max) { val++; lblVal.Text = val.ToString(); onChange(val); } };

        lblMinus.MouseEnter += (_, _) => lblMinus.BackColor = btnHov;
        lblMinus.MouseLeave += (_, _) => lblMinus.BackColor = btnBg;
        lblPlus.MouseEnter  += (_, _) => lblPlus.BackColor  = btnHov;
        lblPlus.MouseLeave  += (_, _) => lblPlus.BackColor  = btnBg;

        container.Controls.AddRange(new Control[] { lblMinus, lblVal, lblPlus });
        return (container, () => val);
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
