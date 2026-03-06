using System.Drawing.Drawing2D;

namespace MouseFlick.Forms;

/// <summary>
/// 제스처 궤적을 표시하는 투명 클릭스루 전체화면 오버레이
/// BackColor = TransparencyKey = Color.Black → 검정은 완전 투명
/// 비검정 색상으로만 그리기
/// </summary>
internal sealed class GestureOverlay : Form
{
    private readonly List<Point> _points = [];
    private string _gestureText = "";

    // 궤적 그라디언트: 시작(보라) → 끝(시안)
    private static readonly Color _colorStart = Color.FromArgb(220, 120,  80, 255);
    private static readonly Color _colorEnd   = Color.FromArgb(220,   0, 220, 180);

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
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.PixelOffsetMode   = PixelOffsetMode.HighQuality;

        if (_points.Count < 1) return;

        // 스크린 좌표 → 클라이언트 좌표 (멀티모니터 대응)
        var vr = SystemInformation.VirtualScreen;
        int ox = -vr.X, oy = -vr.Y;
        var pts = _points.Select(p => new Point(p.X + ox, p.Y + oy)).ToArray();

        if (pts.Length >= 2)
        {
            // 스무딩용 카디널 스플라인 경로 (글로우에 사용)
            using var smoothPath = BuildSmoothPath(pts);

            // ── 글로우 레이어 (외곽 → 중간 → 내부) ──────────────────────────
            using (var pen = new Pen(Color.FromArgb(18, 110, 150, 255), 24f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, smoothPath);

            using (var pen = new Pen(Color.FromArgb(45, 130, 170, 255), 12f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, smoothPath);

            using (var pen = new Pen(Color.FromArgb(80, 150, 190, 255), 6f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, smoothPath);

            // ── 코어 라인: 세그먼트별 보라→시안 그라디언트 ──────────────────
            int n = pts.Length - 1;
            for (int i = 0; i < n; i++)
            {
                float t   = n == 1 ? 0f : (float)i / (n - 1);
                var   col = Blend(_colorStart, _colorEnd, t);
                using var pen = new Pen(col, 3f)
                {
                    StartCap = LineCap.Round,
                    EndCap   = LineCap.Round,
                };
                g.DrawLine(pen, pts[i], pts[i + 1]);
            }
        }

        // ── 시작점 글로우 원 (보라) ──────────────────────────────────────────
        DrawGlowDot(g, pts[0], Color.FromArgb(180, 110, 70, 255), 10);

        // ── 현재 끝점 글로우 원 (시안) ───────────────────────────────────────
        if (pts.Length > 1)
            DrawGlowDot(g, pts[^1], Color.FromArgb(180, 0, 210, 170), 7);

        // ── 제스처 텍스트 (라운드 배경) ──────────────────────────────────────
        if (!string.IsNullOrEmpty(_gestureText))
        {
            var lp = pts[^1];
            using var font     = new Font("Segoe UI", 20f, FontStyle.Bold);
            using var bgBrush  = new SolidBrush(Color.FromArgb(175, 12, 12, 32));
            using var txtBrush = new SolidBrush(Color.FromArgb(245, 255, 228, 72));

            var  sz = g.MeasureString(_gestureText, font);
            float rx = lp.X + 18, ry = lp.Y - 36;
            float rw = sz.Width + 20, rh = sz.Height + 10;

            FillRoundRect(g, bgBrush, rx, ry, rw, rh, 10);
            g.DrawString(_gestureText, font, txtBrush, rx + 10, ry + 5);
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    /// <summary>포인트 배열을 카디널 스플라인 GraphicsPath로 변환</summary>
    private static GraphicsPath BuildSmoothPath(Point[] pts)
    {
        var path = new GraphicsPath();
        if (pts.Length == 2)
            path.AddLine(pts[0], pts[1]);
        else
            path.AddCurve(pts, 0.4f);   // tension 0.4 — 자연스러운 곡률
        return path;
    }

    /// <summary>3겹 글로우 원 (외곽 희미 → 중간 → 중심 밝음)</summary>
    private static void DrawGlowDot(Graphics g, Point c, Color color, int r)
    {
        using var b1 = new SolidBrush(Color.FromArgb(30,  color.R, color.G, color.B));
        using var b2 = new SolidBrush(Color.FromArgb(90,  color.R, color.G, color.B));
        using var b3 = new SolidBrush(Color.FromArgb(210, color.R, color.G, color.B));
        g.FillEllipse(b1, c.X - r * 2, c.Y - r * 2, r * 4, r * 4);
        g.FillEllipse(b2, c.X - r,     c.Y - r,     r * 2, r * 2);
        g.FillEllipse(b3, c.X - r / 2, c.Y - r / 2, r,     r);
    }

    /// <summary>라운드 사각형 채우기</summary>
    private static void FillRoundRect(Graphics g, Brush brush,
                                      float x, float y, float w, float h, float radius)
    {
        using var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(x,         y,         d, d, 180, 90);
        path.AddArc(x + w - d, y,         d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d,   0, 90);
        path.AddArc(x,         y + h - d, d, d,  90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    /// <summary>두 색상 보간</summary>
    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.A + (b.A - a.A) * t),
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
}
