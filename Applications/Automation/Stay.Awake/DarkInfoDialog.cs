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

        private static readonly Color BgColor     = Color.FromArgb(30, 30, 30);
        private static readonly Color TextColor   = Color.FromArgb(224, 224, 224);
        private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
        private static readonly Color BtnColor    = Color.FromArgb(55, 55, 55);

        /// <summary>다크 테마 정보 다이얼로그를 표시합니다.</summary>
        public static void Show(string title, string message, int width = 480, int height = 400)
        {
            using var form = new DarkInfoDialog(title, message, width, height);
            form.ShowDialog();
        }

        private DarkInfoDialog(string title, string message, int width, int height)
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
            const int scrollW = 8;
            const int rightPad = 14;   // 스크롤바 우측 여백
            const int gap = 6;         // 텍스트박스 ↔ 스크롤바 간격

            var textBox = new SlimScrollRichTextBox
            {
                Text = message,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = BgColor,
                ForeColor = TextColor,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical,   // 네이티브 스크롤바 (핸들 생성 후 숨김)
                Dock = DockStyle.None,
                Location = new Point(16, 16),
                Size = new Size(width - 16 - gap - scrollW - rightPad, height - 80),
                TabStop = false,
                DetectUrls = true
            };
            textBox.LinkClicked += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText) { UseShellExecute = true }); }
                catch { }
            };

            var scrollBar = new ModernVScrollBar(textBox)
            {
                Location = new Point(16 + textBox.Width + gap, 16),
                Size = new Size(scrollW, height - 80)
            };

            // 30ms 폴링으로 스크롤 위치 동기화 (키보드·휠·클릭 모두 커버)
            var syncTimer = new System.Windows.Forms.Timer { Interval = 30 };
            syncTimer.Tick += (s, e) => { if (!scrollBar.IsDisposed) scrollBar.Invalidate(); };
            Load += (s, e) => syncTimer.Start();
            FormClosed += (s, e) => { syncTimer.Stop(); syncTimer.Dispose(); };

            var btnOk = new Button
            {
                Text = "확인",
                Size = new Size(90, 30),
                BackColor = BtnColor,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9.5f)
            };
            btnOk.FlatAppearance.BorderColor = BorderColor;
            btnOk.FlatAppearance.BorderSize = 1;
            btnOk.Location = new Point(width - btnOk.Width - 45, ClientSize.Height - btnOk.Height - 25);

            AcceptButton = btnOk;
            Controls.Add(scrollBar);
            Controls.Add(btnOk);
            Controls.Add(textBox);
        }

        // ─────────────────────────────────────────────────────────────────
        // 네이티브 수직 스크롤바를 숨기는 RichTextBox 서브클래스
        // ─────────────────────────────────────────────────────────────────
        private class SlimScrollRichTextBox : RichTextBox
        {
            [DllImport("user32.dll")]
            private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

            private const int SB_VERT  = 1;
            private const int WM_PAINT = 0x000F;
            private const int WM_SIZE  = 0x0005;

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                ShowScrollBar(Handle, SB_VERT, false);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                // WM_PAINT / WM_SIZE 이후 OS가 스크롤바를 복원할 수 있으므로 재숨김
                if (m.Msg == WM_PAINT || m.Msg == WM_SIZE)
                    ShowScrollBar(Handle, SB_VERT, false);
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
                MouseUp    += (s, e) => _dragging = false;
                MouseEnter += (s, e) => { _thumbHovered = true;  Invalidate(); };
                MouseLeave += (s, e) => { _thumbHovered = false; Invalidate(); };

                // 커서가 스크롤바 위에 있을 때 휠 이벤트를 RichTextBox로 전달
                MouseWheel += OnMouseWheel;
            }

            // 스크롤 정보 조회
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

            // 썸 위치·크기 계산
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
                using var path = RoundedRect(thumbRect, 4);
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
                    // 썸 드래그 시작
                    _dragging     = true;
                    _dragStartY   = e.Y;
                    _dragStartPos = pos;
                }
                else
                {
                    // 트랙 클릭 → 페이지 단위 이동
                    var action = e.Y < thumbTop ? SB_LINEUP - 2 + 2 : 3; // SB_PAGEUP=2 / SB_PAGEDOWN=3
                    if (e.Y < thumbTop) action = 2; else action = 3;
                    SendMessage(_rtb.Handle, WM_VSCROLL, (IntPtr)action, IntPtr.Zero);
                    Invalidate();
                }
            }

            private void OnMouseMove(object? sender, MouseEventArgs e)
            {
                if (!_dragging) return;

                var (_, max, page, min) = FetchScrollInfo();
                var range = max - min;
                if (range <= 0) return;

                var scrollable = range - page;
                var (_, thumbH) = CalcThumb(_dragStartPos, max, page, min);
                var trackRange = Height - thumbH;
                if (trackRange <= 0) return;

                var delta    = e.Y - _dragStartY;
                var newPos   = (int)Math.Round(_dragStartPos + (double)delta * scrollable / trackRange);
                newPos = Math.Max(min, Math.Min(min + scrollable, newPos));

                var wp = (IntPtr)((uint)SB_THUMBPOSITION | ((uint)newPos << 16));
                SendMessage(_rtb.Handle, WM_VSCROLL, wp, IntPtr.Zero);
                Invalidate();
            }

            private void OnMouseWheel(object? sender, MouseEventArgs e)
            {
                var lines = e.Delta > 0 ? SB_LINEUP : SB_LINEDOWN;
                for (int i = 0; i < 3; i++)
                    SendMessage(_rtb.Handle, WM_VSCROLL, (IntPtr)lines, IntPtr.Zero);
                Invalidate();
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                var d = radius * 2;
                var path = new GraphicsPath();
                path.AddArc(r.X,             r.Y,              d, d, 180, 90);
                path.AddArc(r.Right - d,     r.Y,              d, d, 270, 90);
                path.AddArc(r.Right - d,     r.Bottom - d,     d, d,   0, 90);
                path.AddArc(r.X,             r.Bottom - d,     d, d,  90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
