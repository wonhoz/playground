using System.Drawing;
using System.Runtime.InteropServices;

namespace ToastCast;

/// <summary>
/// 루틴 알림 오버레이.
/// 설명 + 카운트다운 + 완료 버튼 통합 모드.
///   - 완료 버튼 클릭 → Completed (행동 함, 달성으로 카운트)
///   - 건너뛰기 클릭 / 시간 만료 → Skipped (행동 안 함, 미달성으로 카운트)
/// </summary>
public sealed class CountdownOverlay : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private int _remaining;
    private readonly string _routineId;

    private readonly Label  _lblTitle;
    private readonly Label  _lblDesc;
    private readonly Label  _lblCountdown;
    private readonly Label  _lblHint;
    private readonly Button _btnComplete;
    private readonly Button _btnSkip;

    public event EventHandler? Completed;
    public event EventHandler? Skipped;

    public CountdownOverlay(string icon, string title, string description,
                            int seconds, string routineId = "")
    {
        _routineId = routineId;
        _remaining = Math.Max(1, seconds);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        Opacity         = 0.92;
        BackColor       = Color.FromArgb(18, 18, 28);
        Size            = new Size(360, 320);
        StartPosition   = FormStartPosition.Manual;

        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(screen.Right - Width - 20, screen.Bottom - Height - 20);

        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddRoundedRectangle(new Rectangle(0, 0, Width, Height), 12);
        Region = new Region(path);

        // ── 제목 ──────────────────────────────────────────────────────────
        _lblTitle = new Label
        {
            Text      = $"{icon}  {title}",
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 230, 235),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(0, 14, Width, 34)
        };

        // ── 설명 ──────────────────────────────────────────────────────────
        _lblDesc = new Label
        {
            Text      = description,
            Font      = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 160, 180),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(16, 52, Width - 32, 44)
        };

        // ── 카운트다운 숫자 ───────────────────────────────────────────────
        _lblCountdown = new Label
        {
            Text      = _remaining.ToString(),
            Font      = new Font("Segoe UI", 48f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 150),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(0, 98, Width, 96)
        };

        // ── 힌트 ──────────────────────────────────────────────────────────
        _lblHint = new Label
        {
            Text      = "초 내에 완료를 누르세요",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(130, 130, 150),
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds    = new Rectangle(0, 196, Width, 22)
        };

        // ── 완료 버튼 ─────────────────────────────────────────────────────
        _btnComplete = new Button
        {
            Text      = "완료  ✓",
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(18, 18, 28),
            BackColor = Color.FromArgb(100, 220, 150),
            FlatStyle = FlatStyle.Flat,
            Bounds    = new Rectangle(Width / 2 - 80, 224, 160, 40),
            Cursor    = Cursors.Hand
        };
        _btnComplete.FlatAppearance.BorderSize           = 0;
        _btnComplete.FlatAppearance.MouseOverBackColor   = Color.FromArgb(130, 235, 175);
        _btnComplete.Click += (_, _) =>
        {
            _timer.Stop();
            Completed?.Invoke(this, EventArgs.Empty);
            Close();
        };

        // ── 건너뛰기 버튼 ─────────────────────────────────────────────────
        _btnSkip = new Button
        {
            Text      = "건너뛰기",
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(150, 150, 170),
            BackColor = Color.FromArgb(38, 38, 52),
            FlatStyle = FlatStyle.Flat,
            Bounds    = new Rectangle(Width / 2 - 70, 272, 140, 32),
            Cursor    = Cursors.Hand
        };
        _btnSkip.FlatAppearance.BorderColor        = Color.FromArgb(60, 60, 80);
        _btnSkip.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 72);
        _btnSkip.Click += (_, _) =>
        {
            _timer.Stop();
            Skipped?.Invoke(this, EventArgs.Empty);
            Close();
        };

        Controls.AddRange([_lblTitle, _lblDesc, _lblCountdown, _lblHint, _btnComplete, _btnSkip]);
        _timer.Tick += OnTick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        _timer.Start();
        Task.Run(() => PlayRoutineSound(_routineId));
    }

    private static void PlayRoutineSound(string routineId)
    {
        switch (routineId)
        {
            case "eye-rest":
                Console.Beep(880, 150); Thread.Sleep(60);  Console.Beep(659, 280); break;
            case "water":
                Console.Beep(523, 100); Thread.Sleep(40);  Console.Beep(784, 220); break;
            case "stretch":
                Console.Beep(392, 90);  Thread.Sleep(35);  Console.Beep(523, 90);
                Thread.Sleep(35);       Console.Beep(659, 220); break;
            case "posture":
                Console.Beep(880, 80);  Thread.Sleep(50);  Console.Beep(880, 80);  break;
            default:
                Console.Beep(740, 200); break;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;
        if (_remaining <= 0)
        {
            _timer.Stop();
            // 시간 만료 → 행동 안 함 (미달성)
            Skipped?.Invoke(this, EventArgs.Empty);
            Close();
            return;
        }
        _lblCountdown.Text      = _remaining.ToString();
        _lblCountdown.ForeColor = _remaining <= 5
            ? Color.FromArgb(255, 120, 100)
            : Color.FromArgb(100, 220, 150);
    }

    // 클릭으로 드래그 이동 지원
    private bool _dragging;
    private Point _dragStart;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
    }
    protected override void OnMouseUp(MouseEventArgs e) => _dragging = false;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

// RoundedRectangle 확장
internal static class GraphicsPathExtensions
{
    public static void AddRoundedRectangle(this System.Drawing.Drawing2D.GraphicsPath path, Rectangle rect, int radius)
    {
        var d = radius * 2;
        path.AddArc(rect.X,         rect.Y,          d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y,          d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d,   0, 90);
        path.AddArc(rect.X,         rect.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
    }
}
