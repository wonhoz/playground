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
        Size = new Size(700, 820);
        MinimumSize = new Size(660, 740);
        BackColor = Color.FromArgb(26, 26, 36);
        ForeColor = Color.FromArgb(230, 230, 235);
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        // OS/DPI마다 FixedSingle 창 테두리 폭이 다름 → ClientSize로 실제 클라이언트 폭 확인
        var cw = ClientSize.Width;
        var margin = 18;
        var panelW = cw - margin * 2;  // 좌 18px = 우 18px 대칭

        var header = new Label
        {
            Text = "\U0001F49A 루틴 설정",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 150),
            AutoSize = true,
            Location = new Point(margin + 4, 18)
        };

        // AutoScroll = false: 스크롤바 트랙 공간 예약 없이 panelW 전체 사용 가능
        _routinePanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Bounds = new Rectangle(margin, 72, panelW, 560),
            BackColor = Color.FromArgb(26, 26, 36)
        };

        var idleLabel = new Label
        {
            Text = "유휴 감지 기준 (분):",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 160, 180),
            AutoSize = true,
            Location = new Point(margin + 4, 656)
        };
        var (idlePanel, getIdleValue) = CreateDarkSpinner(1, 30, _config.IdleThresholdMinutes, _ => { });
        idlePanel.Location = new Point(252, 646);

        // 저장 버튼: 우측 22px, 하단 22px 여백 (ClientSize 기준)
        var btnSaveX = cw - 192 - 22;
        var btnSave = CreateButton("\U0001F4BE  저장", new Rectangle(btnSaveX, 682, 192, 52), Color.FromArgb(60, 150, 100));
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
        // cardW: 실제 패널 폭 기반 → DPI/OS 무관하게 좌우 여백 대칭
        var cardW = _routinePanel.Width - 8;  // 4px 카드 마진 양쪽
        var card = new Panel
        {
            Size = new Size(cardW, 128),
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
            Size = new Size(Math.Max(200, cardW - 130), 36),  // 우측 활성 버튼 영역 확보
            Location = new Point(16, 46)
        };

        // 활성 버튼: 카드 우측 12px 여백 (고정 x 아닌 cardW 기준 우정렬)
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
            Location = new Point(cardW - 102, 10),  // 우측 12px 여백
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

    private static (Panel panel, Func<int> getValue) CreateDarkSpinner(int min, int max, int initial, Action<int> onChange)
    {
        var val = initial;
        var btnBg  = Color.FromArgb(40, 40, 60);
        var btnHov = Color.FromArgb(64, 64, 90);
        var divCol = Color.FromArgb(58, 58, 80);

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
            AutoSize = false,
            Bounds = new Rectangle(39, 1, 36, 34)
        };

        pMinus.Click += (_, _) => { if (val > min) { val--; lblVal.Text = val.ToString(); onChange(val); } };
        pPlus.Click  += (_, _) => { if (val < max) { val++; lblVal.Text = val.ToString(); onChange(val); } };

        container.Controls.AddRange(new Control[] { pMinus, lblVal, pPlus });
        return (container, () => val);
    }

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
                LineAlignment = StringAlignment.Center
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
