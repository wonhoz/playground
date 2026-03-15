using System.Drawing.Drawing2D;

namespace WiFiCast.Views;

/// <summary>채널 간섭 히트맵 — GDI+로 네트워크를 가우시안 아치로 그립니다.</summary>
public sealed class ChannelHeatmap : Control
{
    private List<WifiNetwork> _networks = [];
    private int[] _channels = [];
    private int   _bestChannel;

    // 팔레트 (최대 12개 네트워크 색상)
    private static readonly Color[] Palette =
    [
        Color.FromArgb(33,  150, 243),   // Blue
        Color.FromArgb(244,  67,  54),   // Red
        Color.FromArgb(76,  175,  80),   // Green
        Color.FromArgb(255, 193,   7),   // Amber
        Color.FromArgb(156,  39, 176),   // Purple
        Color.FromArgb(255, 152,   0),   // Orange
        Color.FromArgb(0,   188, 212),   // Cyan
        Color.FromArgb(233,  30,  99),   // Pink
        Color.FromArgb(121,  85,  72),   // Brown
        Color.FromArgb(96,  125, 139),   // BlueGrey
        Color.FromArgb(139, 195,  74),   // LightGreen
        Color.FromArgb(63,  81,  181),   // Indigo
    ];

    public ChannelHeatmap()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(14, 20, 30);
    }

    public void Update(List<WifiNetwork> networks, int[] channels, int bestChannel)
    {
        _networks    = networks;
        _channels    = channels;
        _bestChannel = bestChannel;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        if (_channels.Length == 0) return;

        const int padL = 40, padR = 12, padT = 12, padB = 36;
        int areaW = Width  - padL - padR;
        int areaH = Height - padT - padB;
        if (areaW <= 0 || areaH <= 0) return;

        float chStep  = (float)areaW / _channels.Length;
        float baseY   = padT + areaH;

        // 그리드 (Y축: 신호 0~100)
        using var gridPen = new Pen(Color.FromArgb(40, 255, 255, 255));
        for (int sig = 0; sig <= 100; sig += 25)
        {
            float y = baseY - areaH * sig / 100f;
            g.DrawLine(gridPen, padL, y, padL + areaW, y);
            using var lbl = new SolidBrush(Color.FromArgb(80, 80, 80));
            g.DrawString($"{sig}%", Font, lbl, 2, y - 7);
        }

        // 최적 채널 강조
        int bestIdx = Array.IndexOf(_channels, _bestChannel);
        if (bestIdx >= 0)
        {
            float bx = padL + bestIdx * chStep;
            using var bestBrush = new SolidBrush(Color.FromArgb(25, 76, 175, 80));
            g.FillRectangle(bestBrush, bx, padT, chStep, areaH);
        }

        // 각 네트워크를 가우시안 아치로 그리기
        for (int ni = 0; ni < _networks.Count; ni++)
        {
            var net = _networks[ni];
            var col = Palette[ni % Palette.Length];

            int chIdx = Array.IndexOf(_channels, net.Channel);
            if (chIdx < 0) continue;

            // 아치 꼭대기 위치
            float cx = padL + (chIdx + 0.5f) * chStep;
            float topY = baseY - areaH * net.Signal / 100f;

            // 가우시안 근사 폴리라인 (±5채널 범위)
            var pts = new List<PointF>();
            int spread = _channels == GetChannels5() ? 3 : 5;

            for (float dx = -spread * chStep; dx <= spread * chStep; dx += 2)
            {
                float sigma = spread * chStep * 0.4f;
                float val = (float)Math.Exp(-(dx * dx) / (2 * sigma * sigma));
                float py = baseY - (baseY - topY) * val;
                pts.Add(new PointF(cx + dx, py));
            }

            // 닫힌 영역 (채우기용)
            var fill = new List<PointF>(pts) { new(pts[^1].X, baseY), new(pts[0].X, baseY) };

            using var alphaBrush = new SolidBrush(Color.FromArgb(50, col));
            g.FillPolygon(alphaBrush, fill.ToArray());

            using var linePen = new Pen(col, 1.5f);
            g.DrawLines(linePen, pts.ToArray());

            // 채널 레이블 (꼭대기에)
            string lbl = net.Ssid.Length > 8 ? net.Ssid[..8] + "…" : net.Ssid;
            using var txtB = new SolidBrush(col);
            var sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(lbl, Font, txtB, cx, topY - 14, sf);
        }

        // X축 채널 레이블
        using var axisB = new SolidBrush(Color.FromArgb(150, 150, 150));
        using var axisSf = new StringFormat { Alignment = StringAlignment.Center };
        for (int i = 0; i < _channels.Length; i++)
        {
            float x = padL + (i + 0.5f) * chStep;
            string txt = _channels[i].ToString();
            Color fg = _channels[i] == _bestChannel ? Color.FromArgb(76, 175, 80) : Color.FromArgb(120, 120, 120);
            using var b = new SolidBrush(fg);
            g.DrawString(txt, Font, b, x, baseY + 4, axisSf);
        }

        // 축선
        using var axisPen = new Pen(Color.FromArgb(60, 60, 60));
        g.DrawLine(axisPen, padL, padT, padL, baseY);
        g.DrawLine(axisPen, padL, baseY, padL + areaW, baseY);
    }

    // 5GHz 채널 배열 참조용 (비교에 사용)
    private static int[]? _channels5Cache;
    private static int[] GetChannels5()
    {
        _channels5Cache ??= [36, 40, 44, 48, 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 149, 153, 157, 161, 165];
        return _channels5Cache;
    }
}
