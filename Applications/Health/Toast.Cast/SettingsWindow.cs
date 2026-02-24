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

        // 카드 4개 × (128 + 8) = 544px < 568px → 스크롤바 없음
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
        idlePanel.Location = new Point(252, 638);  // 스피너 중심 = 638+18=656, 레이블 중심 = 648+8=656 ✓

        var btnSave = CreateButton("\U0001F4BE  저장", new Rectangle(450, 680, 192, 52), Color.FromArgb(60, 150, 100));
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
        var card = new Panel
        {
            Size = new Size(612, 128),
            BackColor = Color.FromArgb(34, 34, 48),
            Margin = new Padding(4, 0, 4, 8),
            Cursor = Cursors.Default
        };

        var lblName = new Label
        {
            Text = $"{routine.Icon}  {routine.Name}",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 230, 235),
            AutoSize = true,
            Location = new Point(16, 12)
        };

        var lblDesc = new Label
        {
            Text = routine.Description,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(130, 130, 150),
            AutoSize = false,
            Size = new Size(420, 36),
            Location = new Point(16, 46)
        };

        // TextAlign = MiddleCenter: 텍스트 버튼 가로/세로 정중앙
        var chkEnabled = new CheckBox
        {
            Text = "활성",
            Checked = routine.Enabled,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(38, 38, 54),
            Size = new Size(90, 34),
            Location = new Point(508, 10),
            Cursor = Cursors.Hand
        };
        chkEnabled.FlatAppearance.CheckedBackColor = Color.FromArgb(28, 90, 58);
        chkEnabled.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 95);
        chkEnabled.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 72);
        chkEnabled.FlatAppearance.BorderSize = 1;
        chkEnabled.CheckedChanged += (_, _) => { routine.Enabled = chkEnabled.Checked; };

        var lblInterval = new Label
        {
            Text = "간격(분):",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(150, 150, 170),
            AutoSize = true,
            Location = new Point(16, 88)
        };
        var (spPanel, _) = CreateDarkSpinner(1, 480, routine.IntervalMinutes, v => { routine.IntervalMinutes = v; });
        spPanel.Location = new Point(100, 82);

        var chkCountdown = new CheckBox
        {
            Text = "카운트다운",
            Checked = routine.ShowCountdown,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(180, 180, 200),
            BackColor = Color.FromArgb(38, 38, 54),
            Size = new Size(170, 36),
            Location = new Point(228, 82),
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
    /// Panel+GDI+ DrawString: StringAlignment.Center + LineAlignment.Center → 픽셀 완벽 정중앙
    /// </summary>
    private static (Panel panel, Func<int> getValue) CreateDarkSpinner(int min, int max, int initial, Action<int> onChange)
    {
        var val = initial;
        var btnBg  = Color.FromArgb(40, 40, 60);
        var btnHov = Color.FromArgb(64, 64, 90);
        var divCol = Color.FromArgb(58, 58, 80);  // 컨테이너 BackColor: 1px 갭 = 보더+구분선

        // 컨테이너(114×36): BackColor가 1px 갭에 노출되어 보더+구분선 형성
        // pMinus(37px) | gap(1px) | lblVal(36px) | gap(1px) | pPlus(37px) = 1+37+1+36+1+37+1=114
        var container = new Panel { Size = new Size(114, 36), BackColor = divCol };

        var pMinus = CreateSpinButton("-", btnBg, btnHov, new Rectangle(1, 1, 37, 34));
        var pPlus  = CreateSpinButton("+", btnBg, btnHov, new Rectangle(76, 1, 37, 34));

        var lblVal = new Label
        {
            Text = val.ToString(),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(215, 215, 235),
            BackColor = Color.FromArgb(24, 24, 40),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,    // 필수: false 아니면 Bounds.Size 무시됨
            Bounds = new Rectangle(39, 1, 36, 34)
        };

        pMinus.Click += (_, _) => { if (val > min) { val--; lblVal.Text = val.ToString(); onChange(val); } };
        pPlus.Click  += (_, _) => { if (val < max) { val++; lblVal.Text = val.ToString(); onChange(val); } };

        container.Controls.AddRange(new Control[] { pMinus, lblVal, pPlus });
        return (container, () => val);
    }

    /// <summary>스피너 버튼 패널: GDI+ DrawString으로 텍스트 정중앙 보장</summary>
    private static Panel CreateSpinButton(string text, Color bg, Color hov, Rectangle bounds)
    {
        var panel = new Panel { Bounds = bounds, BackColor = bg, Cursor = Cursors.Hand };
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var font  = new Font("Segoe UI", 15f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(215, 215, 245));
            using var sf    = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center   // 세로 정중앙 보장
            };
            e.Graphics.DrawString(text, font, brush, new RectangleF(0, 0, panel.Width, panel.Height), sf);
        };
        panel.MouseEnter += (_, _) => { panel.BackColor = hov; panel.Invalidate(); };
        panel.MouseLeave += (_, _) => { panel.BackColor = bg;  panel.Invalidate(); };
        return panel;
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
