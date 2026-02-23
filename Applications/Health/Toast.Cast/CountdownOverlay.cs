using System.Drawing;
using System.Runtime.InteropServices;

namespace ToastCast;

/// <summary>화면 중앙에 반투명 카운트다운 오버레이를 표시합니다.</summary>
public sealed class CountdownOverlay : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private int _remaining;
    private readonly string _icon;
    private readonly string _title;
    private readonly Label _lblCountdown;
    private readonly Label _lblTitle;
    private readonly Label _lblHint;
    private readonly Button _btnSkip;

    public event EventHandler? Completed;
    public event EventHandler? Skipped;

    public CountdownOverlay(string icon, string title, int seconds)
    {
        _icon = icon;
        _title = title;
        _remaining = seconds;

        // 창 기본 설정
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Opacity = 0.92;
        BackColor = Color.FromArgb(18, 18, 28);
        Size = new Size(360, 200);
        StartPosition = FormStartPosition.Manual;

        // 화면 우측 하단 배치
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(screen.Right - Width - 20, screen.Bottom - Height - 20);

        // 라운드 코너
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddRoundedRectangle(new Rectangle(0, 0, Width, Height), 12);
        Region = new Region(path);

        // 아이콘 + 제목
        _lblTitle = new Label
        {
            Text = $"{_icon}  {_title}",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.FromArgb(230, 230, 235),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 16, Width, 36)
        };

        // 카운트다운 숫자
        _lblCountdown = new Label
        {
            Text = _remaining.ToString(),
            Font = new Font("Segoe UI", 52f, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 220, 150),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 50, Width, 80)
        };

        // 안내 문구
        _lblHint = new Label
        {
            Text = "초 후 자동으로 닫힙니다",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(130, 130, 150),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(0, 128, Width, 24)
        };

        // 스킵 버튼
        _btnSkip = new Button
        {
            Text = "건너뛰기",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(150, 150, 170),
            BackColor = Color.FromArgb(38, 38, 52),
            FlatStyle = FlatStyle.Flat,
            Bounds = new Rectangle(Width / 2 - 60, 158, 120, 28),
            Cursor = Cursors.Hand
        };
        _btnSkip.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
        _btnSkip.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 72);
        _btnSkip.Click += (_, _) => { _timer.Stop(); Skipped?.Invoke(this, EventArgs.Empty); Close(); };

        Controls.AddRange([_lblTitle, _lblCountdown, _lblHint, _btnSkip]);

        _timer.Tick += OnTick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // 다크 타이틀바
        var dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining--;
        if (_remaining <= 0)
        {
            _timer.Stop();
            Completed?.Invoke(this, EventArgs.Empty);
            Close();
            return;
        }
        _lblCountdown.Text = _remaining.ToString();
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
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
    }
}
