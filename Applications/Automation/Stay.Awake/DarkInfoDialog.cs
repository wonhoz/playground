using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// 다크 테마 정보 다이얼로그 (OS 기본 MessageBox 대체)
    /// </summary>
    public class DarkInfoDialog : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static readonly Color BgColor   = Color.FromArgb(30, 30, 30);
        private static readonly Color TextColor = Color.FromArgb(224, 224, 224);
        private static readonly Color ChartFillColor  = Color.FromArgb(67, 217, 123);  // █ — 아이콘 ACTIVE 색상
        private static readonly Color ChartTrackColor = Color.FromArgb(80, 80, 80);    // ░ — 차트 빈 영역

        /// <summary>다크 테마 정보 다이얼로그를 표시합니다.</summary>
        /// <param name="refresh">non-null이면 1초마다 호출되어 메시지를 갱신 (실시간 통계 등)</param>
        public static void Show(string title, string message, int width = 480, int height = 400, Func<string>? refresh = null)
        {
            using var form = new DarkInfoDialog(title, message, width, height, refresh);
            form.ShowDialog();
        }

        private DarkInfoDialog(string title, string message, int width, int height, Func<string>? refresh = null)
        {
            Text = title;
            Size = new Size(width, height);
            MinimumSize = new Size(300, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9.5f);

            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));

            // 슬림 스크롤바 너비·여백
            const int scrollW  = 8;
            const int rightPad = 14;
            const int gap      = 6;

            var textBox = new SlimScrollRichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = BgColor,
                ForeColor = TextColor,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical, // 핸들 생성 후 EM_SHOWSCROLLBAR 로 숨김
                Dock = DockStyle.None,
                Location = new Point(16, 16),
                Size = new Size(width - 16 - gap - scrollW - rightPad, height - 32),
                TabStop = false,
                DetectUrls = true
            };
            SetTextWithChartColors(textBox, message);
            textBox.LinkClicked += (s, e) =>
            {
                try { if (!string.IsNullOrEmpty(e.LinkText)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText) { UseShellExecute = true }); }
                catch { }
            };

            var scrollBar = new ModernVScrollBar(textBox)
            {
                Location = new Point(16 + textBox.Width + gap, 16),
                Size = new Size(scrollW, height - 32)
            };

            // 스크롤 발생 시에만 커스텀 스크롤바 갱신 (타이머 없음 → 깜빡임 없음)
            textBox.VScroll += (s, e) => scrollBar.Invalidate();

            // 1초마다 메시지 갱신 (실시간 통계용) — 스크롤 위치 보존
            if (refresh != null)
            {
                var refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                refreshTimer.Tick += (s, e) =>
                {
                    try
                    {
                        var newText = refresh();
                        if (!string.Equals(newText, textBox.Text, StringComparison.Ordinal))
                            UpdateTextPreservingScroll(textBox, scrollBar, newText);
                    }
                    catch (Exception ex) { Logger.LogException("DarkInfoDialog.Refresh", ex); }
                };
                refreshTimer.Start();
                FormClosed += (s, e) => { refreshTimer.Stop(); refreshTimer.Dispose(); };
            }

            // 확인 버튼 없음 — Esc 또는 X로 닫기
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close(); };

            Controls.Add(scrollBar);
            Controls.Add(textBox);
        }

        /// <summary>
        /// RichTextBox에 텍스트를 설정하고 ASCII 차트 문자(█/░)에 색상을 적용
        /// </summary>
        private static void SetTextWithChartColors(RichTextBox textBox, string text)
        {
            textBox.Text = text;
            ApplyChartHighlights(textBox);
        }

        /// <summary>
        /// █ → 녹색(#43D97B), ░ → 회색(#505050) 색상 적용 — 통계 활성시간 차트 가독성 향상
        /// </summary>
        private static void ApplyChartHighlights(RichTextBox textBox)
        {
            var content = textBox.Text;
            var i = 0;
            while (i < content.Length)
            {
                if (content[i] == '█')
                {
                    var start = i;
                    while (i < content.Length && content[i] == '█') i++;
                    textBox.Select(start, i - start);
                    textBox.SelectionColor = ChartFillColor;
                }
                else if (content[i] == '░')
                {
                    var start = i;
                    while (i < content.Length && content[i] == '░') i++;
                    textBox.Select(start, i - start);
                    textBox.SelectionColor = ChartTrackColor;
                }
                else
                {
                    i++;
                }
            }
            textBox.Select(0, 0);
        }

        /// <summary>
        /// 갱신 시 스크롤 위치를 보존하면서 텍스트와 차트 색상 재적용
        /// </summary>
        private static void UpdateTextPreservingScroll(RichTextBox textBox, Control scrollBar, string newText)
        {
            var firstVisibleChar = textBox.GetCharIndexFromPosition(new Point(1, 1));
            SetTextWithChartColors(textBox, newText);
            if (firstVisibleChar > 0 && firstVisibleChar < textBox.Text.Length)
            {
                textBox.SelectionStart = firstVisibleChar;
                textBox.ScrollToCaret();
            }
            textBox.Select(0, 0);
            scrollBar.Invalidate();
        }

        // ─────────────────────────────────────────────────────────────────
        // 네이티브 스크롤바를 RichEdit 레벨에서 숨기는 서브클래스
        // EM_SHOWSCROLLBAR 사용 — WndProc 인터셉션 없음 → 리페인트 루프 없음
        // ─────────────────────────────────────────────────────────────────
        private class SlimScrollRichTextBox : RichTextBox
        {
            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            private const int EM_SHOWSCROLLBAR = 0x0460;
            private const int SB_VERT          = 1;

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                // lParam = 0(FALSE) → 숨김, 스크롤 정보(pos/range/page)는 유지됨
                SendMessage(Handle, EM_SHOWSCROLLBAR, (IntPtr)SB_VERT, IntPtr.Zero);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 슬림 모던 수직 스크롤바
        // ─────────────────────────────────────────────────────────────────
        private class ModernVScrollBar : Control
        {
            [DllImport("user32.dll")]
            private static extern bool GetScrollInfo(IntPtr hwnd, int fnBar, ref SCROLLINFO lpsi);
            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            [StructLayout(LayoutKind.Sequential)]
            private struct SCROLLINFO
            {
                public uint cbSize;
                public uint fMask;
                public int  nMin;
                public int  nMax;
                public uint nPage;
                public int  nPos;
                public int  nTrackPos;
            }

            private const int SB_VERT          = 1;
            private const int WM_VSCROLL       = 0x0115;
            private const int SIF_ALL          = 0x17;
            private const int SB_LINEUP        = 0;
            private const int SB_LINEDOWN      = 1;
            private const int SB_PAGEUP        = 2;
            private const int SB_PAGEDOWN      = 3;
            private const int SB_THUMBPOSITION = 4;

            private static readonly Color TrackColor = Color.FromArgb(42, 42, 42);
            private static readonly Color ThumbColor = Color.FromArgb(88, 88, 88);
            private static readonly Color ThumbHover = Color.FromArgb(115, 115, 115);

            private readonly RichTextBox _rtb;
            private bool _dragging;
            private int  _dragStartY;
            private int  _dragStartPos;
            private bool _thumbHovered;

            public ModernVScrollBar(RichTextBox rtb)
            {
                _rtb = rtb;
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
                BackColor = TrackColor;
                Cursor = Cursors.Default;

                MouseDown  += OnMouseDown;
                MouseMove  += OnMouseMove;
                MouseUp    += (s, e) => { _dragging = false; Invalidate(); };
                MouseEnter += (s, e) => { _thumbHovered = true;  Invalidate(); };
                MouseLeave += (s, e) => { _thumbHovered = false; Invalidate(); };
                MouseWheel += OnMouseWheel;
            }

            private (int pos, int max, int page, int min) FetchScrollInfo()
            {
                var si = new SCROLLINFO
                {
                    cbSize = (uint)Marshal.SizeOf<SCROLLINFO>(),
                    fMask  = SIF_ALL
                };
                GetScrollInfo(_rtb.Handle, SB_VERT, ref si);
                return (si.nPos, si.nMax, (int)si.nPage, si.nMin);
            }

            private (int top, int height) CalcThumb(int pos, int max, int page, int min)
            {
                var range = max - min;
                if (range <= 0 || page >= range) return (0, Height);
                var scrollable = range - page;
                var thumbH = Math.Max(24, Height * page / range);
                var thumbTop = scrollable > 0
                    ? (int)((Height - thumbH) * (double)(pos - min) / scrollable)
                    : 0;
                return (thumbTop, thumbH);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);

                var (pos, max, page, min) = FetchScrollInfo();
                var range = max - min;
                if (range <= 0 || page >= range) return;

                var (thumbTop, thumbH) = CalcThumb(pos, max, page, min);
                var thumbRect = new Rectangle(2, thumbTop, Width - 4, thumbH);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                var color = (_dragging || _thumbHovered) ? ThumbHover : ThumbColor;
                using var brush = new SolidBrush(color);
                using var path  = RoundedRect(thumbRect, 4);
                g.FillPath(brush, path);
            }

            private void OnMouseDown(object? sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;

                var (pos, max, page, min) = FetchScrollInfo();
                var range = max - min;
                if (range <= 0) return;

                var (thumbTop, thumbH) = CalcThumb(pos, max, page, min);

                if (e.Y >= thumbTop && e.Y <= thumbTop + thumbH)
                {
                    _dragging     = true;
                    _dragStartY   = e.Y;
                    _dragStartPos = pos;
                }
                else
                {
                    var action = e.Y < thumbTop ? SB_PAGEUP : SB_PAGEDOWN;
                    SendMessage(_rtb.Handle, WM_VSCROLL, (IntPtr)action, IntPtr.Zero);
                    Invalidate();
                }
            }

            private void OnMouseMove(object? sender, MouseEventArgs e)
            {
                if (!_dragging) return;

                var (_, max, page, min) = FetchScrollInfo();
                var scrollable = max - min - page;
                if (scrollable <= 0) return;

                var (_, thumbH) = CalcThumb(_dragStartPos, max, page, min);
                var trackRange  = Height - thumbH;
                if (trackRange <= 0) return;

                var delta  = e.Y - _dragStartY;
                var newPos = (int)Math.Round(_dragStartPos + (double)delta * scrollable / trackRange);
                newPos = Math.Max(min, Math.Min(min + scrollable, newPos));

                var wp = (IntPtr)((uint)SB_THUMBPOSITION | ((uint)newPos << 16));
                SendMessage(_rtb.Handle, WM_VSCROLL, wp, IntPtr.Zero);
                Invalidate();
            }

            private void OnMouseWheel(object? sender, MouseEventArgs e)
            {
                var action = e.Delta > 0 ? SB_LINEUP : SB_LINEDOWN;
                for (int i = 0; i < 3; i++)
                    SendMessage(_rtb.Handle, WM_VSCROLL, (IntPtr)action, IntPtr.Zero);
                Invalidate();
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                var d    = radius * 2;
                var path = new GraphicsPath();
                path.AddArc(r.X,         r.Y,          d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
                path.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
