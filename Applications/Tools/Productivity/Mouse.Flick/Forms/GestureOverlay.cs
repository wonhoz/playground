using System.Drawing.Drawing2D;

namespace MouseFlick.Forms;

/// <summary>
/// 제스처 궤적을 표시하는 투명 클릭스루 전체화면 오버레이
/// BackColor = TransparencyKey = Color.Black → 검정은 완전 투명
/// 비검정 색상으로만 그리기 (흰 선, 파란 원, 노란 텍스트)
/// </summary>
internal sealed class GestureOverlay : Form
{
    private readonly List<Point> _points = [];
    private string _gestureText = "";

    public GestureOverlay()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint  |
                 ControlStyles.UserPaint, true);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        BackColor       = Color.Black;      // 검정 = 투명
        TransparencyKey = Color.Black;
        Opacity         = 1.0;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_TRANSPARENT: 클릭 이벤트 하위 창으로 통과
            // WS_EX_TOOLWINDOW: 작업표시줄/Alt-Tab 미노출
            // WS_EX_LAYERED: 투명도 지원
            // WS_EX_NOACTIVATE: 포커스 비활성화
            cp.ExStyle |= 0x20 | 0x80 | 0x80000 | 0x8000000;
            return cp;
        }
    }

    public void BeginGesture(IEnumerable<Point> initialPoints)
    {
        _points.Clear();
        _points.AddRange(initialPoints);
        _gestureText = "";
        Bounds = SystemInformation.VirtualScreen;
        Show();
        Refresh();
    }

    public void AddPoint(Point pt)
    {
        _points.Add(pt);
        Invalidate();
    }

    public void UpdateGestureText(string text)
    {
        _gestureText = text;
        Invalidate();
        Update();
    }

    public void EndGesture()
    {
        Hide();
        _points.Clear();
        _gestureText = "";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g   = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_points.Count < 1) return;

        // 스크린 좌표 → 클라이언트 좌표 오프셋 (멀티모니터 대응)
        var vr = SystemInformation.VirtualScreen;
        int ox = -vr.X, oy = -vr.Y;

        var pts = _points.Select(p => new Point(p.X + ox, p.Y + oy)).ToArray();

        // 궤적 선: 반투명 흰색 2.5px
        if (pts.Length >= 2)
        {
            using var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 2.5f);
            pen.StartCap = LineCap.Round;
            pen.EndCap   = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, pts);
        }

        // 시작점 원: 파란색
        using (var b = new SolidBrush(Color.FromArgb(220, 60, 130, 255)))
            g.FillEllipse(b, pts[0].X - 7, pts[0].Y - 7, 14, 14);

        // 현재 제스처 텍스트: 마지막 포인트 근처 (노란색)
        if (!string.IsNullOrEmpty(_gestureText))
        {
            var lp = pts[^1];
            using var font  = new Font("Segoe UI", 18f, FontStyle.Bold);
            using var bgBrush = new SolidBrush(Color.FromArgb(160, 20, 20, 40));
            using var brush   = new SolidBrush(Color.FromArgb(230, 255, 220, 60));
            var sz    = g.MeasureString(_gestureText, font);
            var rect  = new RectangleF(lp.X + 12, lp.Y - 28, sz.Width + 8, sz.Height + 4);
            g.FillRectangle(bgBrush, rect);
            g.DrawString(_gestureText, font, brush, lp.X + 16, lp.Y - 26);
        }
    }
}
