using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Tray.Stats.Services;

namespace Tray.Stats.UI;

sealed class PopupForm : Form
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    const uint SWP_NOSIZE   = 0x0001;
    const uint SWP_NOMOVE   = 0x0002;
    const uint SWP_NOACTIVATE = 0x0010;
    static readonly IntPtr HWND_TOPMOST = new(-1);

    // ── 색상 팔레트 ──────────────────────────────────────────────────────────
    static readonly Color BG     = Color.FromArgb(0xFF, 0x13, 0x13, 0x1F);
    static readonly Color BG2    = Color.FromArgb(0xFF, 0x1A, 0x1A, 0x2A);
    static readonly Color Border = Color.FromArgb(0xFF, 0x2A, 0x2A, 0x3E);
    static readonly Color TxtPri = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0);
    static readonly Color TxtSec = Color.FromArgb(0xFF, 0x88, 0x88, 0x99);

    // 지표별 컬러
    static Color CpuColor(float v)  => v < 50 ? Color.FromArgb(0xFF,0x4A,0xDE,0x80)
                                     : v < 80 ? Color.FromArgb(0xFF,0xFA,0xCC,0x15)
                                              : Color.FromArgb(0xFF,0xF8,0x71,0x71);
    static readonly Color RamCol  = Color.FromArgb(0xFF, 0x60, 0xA5, 0xFA);
    static readonly Color DiskCol = Color.FromArgb(0xFF, 0xFB, 0x92, 0x3C);
    static readonly Color NetCol  = Color.FromArgb(0xFF, 0x34, 0xD3, 0x99);
    static readonly Color GpuCol  = Color.FromArgb(0xFF, 0xC0, 0x84, 0xFC);

    const int W         = 300;
    const int H         = 410;
    const int Pad       = 16;
    const int GraphH    = 40;
    const int RowH      = 72;  // 각 지표 행 높이

    StatsSnapshot _snap = new();
    StatsCollector? _collector;

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        Size            = new Size(W, H);
        BackColor       = BG;
        ShowInTaskbar   = false;
        TopMost         = true;
        DoubleBuffered  = true;

        Deactivate += (_, _) => Hide();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int v = 1;
        DwmSetWindowAttribute(Handle, 20, ref v, sizeof(int));
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        ApplyRoundCorners();
    }

    void ApplyRoundCorners()
    {
        // 윈도우 11 라운드 코너 (DWM_WINDOW_CORNER_PREFERENCE = 2: ROUND)
        int corner = 2;
        DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
    }

    public void Bind(StatsCollector collector) => _collector = collector;

    public void UpdateSnapshot(StatsSnapshot snap)
    {
        _snap = snap;
        if (Visible) Invalidate();
    }

    public void ShowNear(NotifyIcon icon)
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        int x = screen.WorkingArea.Right  - W - 12;
        int y = screen.WorkingArea.Bottom - H - 12;
        Location = new Point(x, y);
        Show();
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // ── 배경 ─────────────────────────────────────────────────────────────
        g.Clear(BG);

        // ── 헤더 ─────────────────────────────────────────────────────────────
        using var hFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var sBrush = new SolidBrush(TxtPri);
        g.DrawString("Tray.Stats", hFont, sBrush, Pad, Pad);

        string timeStr = DateTime.Now.ToString("HH:mm:ss");
        using var tFont  = new Font("Segoe UI", 8.5f);
        using var tBrush = new SolidBrush(TxtSec);
        var tSize = g.MeasureString(timeStr, tFont);
        g.DrawString(timeStr, tFont, tBrush, W - Pad - tSize.Width, Pad + 2);

        // 구분선
        using var sepPen = new Pen(Border, 1);
        g.DrawLine(sepPen, Pad, 36, W - Pad, 36);

        // ── 지표 행들 ────────────────────────────────────────────────────────
        int y = 44;
        DrawMetric(g, "CPU",  $"{_snap.CpuPercent:F0}%",  _snap.CpuPercent, CpuColor(_snap.CpuPercent),
            _collector?.CpuHistory,  ref y);
        DrawMetric(g, "RAM",  $"{_snap.RamUsedGb:F1} / {_snap.RamTotalGb:F1} GB",
            _snap.RamPercent, RamCol, _collector?.RamHistory,  ref y);
        DrawMetric(g, "DISK", $"{_snap.DiskPercent:F0}%",  _snap.DiskPercent, DiskCol,
            _collector?.DiskHistory, ref y);
        DrawNetMetric(g, ref y);
        DrawMetric(g, "GPU",  $"{_snap.GpuPercent:F0}%",  _snap.GpuPercent, GpuCol,
            _collector?.GpuHistory,  ref y);

        // ── 하단 힌트 ────────────────────────────────────────────────────────
        using var hintFont  = new Font("Segoe UI", 7.5f);
        using var hintBrush = new SolidBrush(Color.FromArgb(0xFF, 0x55, 0x55, 0x66));
        g.DrawString("우클릭: 메뉴 | 클릭: 닫기", hintFont, hintBrush,
            Pad, H - 22);
    }

    void DrawMetric(Graphics g, string label, string valueText, float percent,
        Color barColor, CircularBuffer<float>? history, ref int y)
    {
        using var labelFont = new Font("Segoe UI", 8f);
        using var valFont   = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var labelBrush = new SolidBrush(TxtSec);
        using var valBrush   = new SolidBrush(TxtPri);

        g.DrawString(label, labelFont, labelBrush, Pad, y);

        var valSize = g.MeasureString(valueText, valFont);
        g.DrawString(valueText, valFont, valBrush, W - Pad - valSize.Width, y);

        y += 18;

        // 진행 바 배경
        int barW = W - Pad * 2;
        using var bgBrush = new SolidBrush(BG2);
        g.FillRectangle(bgBrush, Pad, y, barW, 6);

        // 진행 바
        int fillW = (int)(barW * Math.Clamp(percent / 100f, 0, 1));
        if (fillW > 0)
        {
            using var barBrush = new SolidBrush(barColor);
            g.FillRectangle(barBrush, Pad, y, fillW, 6);
        }

        y += 12;

        // 그래프
        if (history != null && history.Count > 1)
            DrawGraph(g, history, barColor, Pad, y, barW, GraphH - 12);

        y += GraphH - 8;
    }

    void DrawNetMetric(Graphics g, ref int y)
    {
        using var labelFont  = new Font("Segoe UI", 8f);
        using var valFont    = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var labelBrush = new SolidBrush(TxtSec);
        using var valBrush   = new SolidBrush(TxtPri);

        g.DrawString("NET", labelFont, labelBrush, Pad, y);
        string netStr = $"↑{FormatKBs(_snap.NetSendKBs)}  ↓{FormatKBs(_snap.NetRecvKBs)}";
        var valSize = g.MeasureString(netStr, valFont);
        g.DrawString(netStr, valFont, valBrush, W - Pad - valSize.Width, y);

        y += 18;

        int barW = W - Pad * 2;
        if (_collector != null && _collector.NetHistory.Count > 1)
            DrawGraph(g, _collector.NetHistory, NetCol, Pad, y, barW, GraphH - 12);

        y += GraphH - 8;
    }

    static void DrawGraph(Graphics g, CircularBuffer<float> history,
        Color color, int x, int y, int w, int h)
    {
        int cnt = Math.Min(history.Count, w);
        if (cnt < 2) return;

        float max = 0;
        for (int i = 0; i < cnt; i++)
            if (history[i] > max) max = history[i];
        if (max < 1f) max = 1f;

        var pts = new PointF[cnt];
        for (int i = 0; i < cnt; i++)
        {
            float val = history[i];
            float px  = x + w - 1 - i * (w / (float)(cnt - 1));
            float py  = y + h - (val / max * h);
            pts[i] = new PointF(px, py);
        }

        Array.Reverse(pts);

        // 채우기
        var fill = new PointF[pts.Length + 2];
        fill[0] = new PointF(pts[0].X, y + h);
        pts.CopyTo(fill, 1);
        fill[^1] = new PointF(pts[^1].X, y + h);

        using var fillBrush = new SolidBrush(Color.FromArgb(40, color));
        g.FillPolygon(fillBrush, fill);

        // 선
        using var pen = new Pen(color, 1.2f);
        g.DrawLines(pen, pts);
    }

    static string FormatKBs(float kbs)
    {
        if (kbs >= 1024f) return $"{kbs / 1024f:F1} MB/s";
        return $"{kbs:F0} KB/s";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;  // WS_EX_TOOLWINDOW (작업 표시줄 미표시)
            return cp;
        }
    }
}
