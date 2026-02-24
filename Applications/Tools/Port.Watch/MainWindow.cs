using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using PortWatch.Models;
using PortWatch.Services;

namespace PortWatch;

public sealed class MainWindow : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hwnd, EnumChildProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

    private readonly DataGridView _grid;
    private readonly TextBox      _searchBox;
    private readonly Label        _statusLabel;
    private readonly Button       _btnRefresh;
    private readonly CheckBox     _chkAuto;
    private readonly CheckBox     _chkFavOnly;
    private readonly System.Windows.Forms.Timer _autoTimer;

    private List<PortEntry> _allEntries   = [];
    private HashSet<int>    _prevOccupied = [];
    private bool            _scrollbarsDarked;

    public MainWindow()
    {
        Text = "Port.Watch \u2014 포트 & 프로세스 모니터";
        Size = new Size(1120, 700);
        MinimumSize = new Size(820, 520);
        BackColor = Color.FromArgb(18, 18, 28);
        ForeColor = Color.FromArgb(220, 220, 230);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = CreateAppIcon();

        // ── 상단 툴바 ────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.FromArgb(22, 22, 34),
            Padding = new Padding(12, 10, 12, 10)
        };

        var lblIcon = new Label
        {
            Text = "🔍",
            Font = new Font("Segoe UI", 12f),
            ForeColor = Color.FromArgb(120, 120, 150),
            AutoSize = true,
            Location = new Point(14, 14)
        };

        _searchBox = new TextBox
        {
            PlaceholderText = "포트 번호 또는 프로세스 이름...",
            Location = new Point(44, 12),
            Size = new Size(300, 28),
            BackColor = Color.FromArgb(30, 30, 46),
            ForeColor = Color.FromArgb(220, 220, 230),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        _btnRefresh = MakeButton("↺  새로고침", new Point(364, 12), 120);
        _btnRefresh.Click += async (_, _) => await RefreshAsync();

        _chkAuto = MakeToggle("⏱ 자동 갱신", new Point(500, 12), 118);
        _chkAuto.CheckedChanged += (_, _) =>
        {
            _autoTimer.Enabled = _chkAuto.Checked;
            _chkAuto.Text = _chkAuto.Checked ? "⏱ 자동 갱신 ●" : "⏱ 자동 갱신";
        };

        _chkFavOnly = MakeToggle("★ 즐겨찾기만", new Point(632, 12), 118);
        _chkFavOnly.CheckedChanged += (_, _) => ApplyFilter();

        toolbar.Controls.AddRange([lblIcon, _searchBox, _btnRefresh, _chkAuto, _chkFavOnly]);

        // ── DataGridView ─────────────────────────────────────────
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(16, 16, 26),
            GridColor = Color.FromArgb(36, 36, 52),
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = true,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            Font = new Font("Segoe UI", 9.5f),
            RowTemplate = { Height = 34 },
            ColumnHeadersHeight = 38,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ScrollBars = ScrollBars.Both
        };

        var cellStyle = _grid.DefaultCellStyle;
        cellStyle.BackColor = Color.FromArgb(20, 20, 32);
        cellStyle.ForeColor = Color.FromArgb(218, 218, 230);
        cellStyle.SelectionBackColor = Color.FromArgb(38, 78, 140);
        cellStyle.SelectionForeColor = Color.White;
        cellStyle.Padding = new Padding(6, 0, 4, 0);

        var altStyle = _grid.AlternatingRowsDefaultCellStyle;
        altStyle.BackColor = Color.FromArgb(24, 24, 38);
        altStyle.ForeColor = Color.FromArgb(218, 218, 230);
        altStyle.SelectionBackColor = Color.FromArgb(38, 78, 140);
        altStyle.SelectionForeColor = Color.White;

        var hdrStyle = _grid.ColumnHeadersDefaultCellStyle;
        hdrStyle.BackColor = Color.FromArgb(26, 26, 42);
        hdrStyle.ForeColor = Color.FromArgb(155, 155, 190);
        hdrStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        hdrStyle.SelectionBackColor = Color.FromArgb(26, 26, 42);
        hdrStyle.Padding = new Padding(6, 0, 4, 0);

        AddCol("fav",     "",         28,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("proto",   "프로토콜", 70,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("port",    "포트",     72,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("process", "프로세스", 190, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("pid",     "PID",      65,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("state",   "상태",     120, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("remote",  "원격 주소",155, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("path",    "경로",     340, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);

        // 컨텍스트 메뉴
        var ctx = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, Font = new Font("Segoe UI", 9.5f) };
        var miKill   = new ToolStripMenuItem("⛔  프로세스 종료");
        var miPort   = new ToolStripMenuItem("📋  포트 번호 복사");
        var miPath   = new ToolStripMenuItem("📋  경로 복사");
        var miFav    = new ToolStripMenuItem("★  즐겨찾기 토글");
        miKill.Click  += KillSelected;
        miPort.Click  += (_, _) => CopyCell("port");
        miPath.Click  += (_, _) => CopyCell("path");
        miFav.Click   += ToggleFavorite;
        ctx.Items.AddRange([miKill, new ToolStripSeparator(), miPort, miPath, new ToolStripSeparator(), miFav]);
        _grid.ContextMenuStrip = ctx;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) CopyCell("path"); };

        // ── 상태바 ──────────────────────────────────────────────
        var statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = Color.FromArgb(20, 20, 34)
        };

        _statusLabel = new Label
        {
            Text = "로딩 중...",
            ForeColor = Color.FromArgb(110, 110, 140),
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(14, 7)
        };

        var btnKill = new Button
        {
            Text = "⛔ 선택 프로세스 종료",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(220, 90, 80),
            BackColor = Color.FromArgb(20, 20, 34),
            AutoSize = true,
            Location = new Point(320, 4),
            Cursor = Cursors.Hand
        };
        btnKill.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 75);
        btnKill.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 40, 56);
        btnKill.Click += KillSelected;

        statusBar.Controls.AddRange([_statusLabel, btnKill]);

        // ── 자동 갱신 타이머 ─────────────────────────────────────
        _autoTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _autoTimer.Tick += async (_, _) => await RefreshAsync();

        Controls.AddRange([toolbar, _grid, statusBar]);
        _ = RefreshAsync();
    }

    // ── 컬럼 정의 ────────────────────────────────────────────────
    private void AddCol(string name, string header, int width,
        DataGridViewAutoSizeColumnMode auto, DataGridViewContentAlignment align)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = name, HeaderText = header, Width = width, AutoSizeMode = auto,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = { Alignment = align }
        });
    }

    // ── 스캔 & 갱신 ──────────────────────────────────────────────
    private async Task RefreshAsync()
    {
        void SetBusy(bool busy)
        {
            _btnRefresh.Enabled = !busy;
            _btnRefresh.Text = busy ? "↺  갱신 중..." : "↺  새로고침";
        }

        if (InvokeRequired) Invoke(() => SetBusy(true));
        else SetBusy(true);

        try
        {
            var entries = await PortScanService.ScanAsync();
            foreach (var e in entries)
                e.IsFavorite = FavoritesService.IsFavorite(e.LocalPort);

            // 즐겨찾기 포트가 해제되면 알림
            CheckFreedFavorites(entries);

            _allEntries = entries;

            if (InvokeRequired) Invoke(ApplyFilter);
            else ApplyFilter();
        }
        finally
        {
            if (InvokeRequired) Invoke(() => SetBusy(false));
            else SetBusy(false);
        }
    }

    private void CheckFreedFavorites(List<PortEntry> current)
    {
        var nowOccupied = current
            .Where(e => e.IsFavorite)
            .Select(e => e.LocalPort)
            .ToHashSet();

        var freed = _prevOccupied.Except(nowOccupied).ToList();
        foreach (var port in freed)
            BeginInvoke(() => MessageBox.Show(
                $"포트 {port} 이(가) 해제되었습니다!",
                "Port.Watch — 포트 해제 알림",
                MessageBoxButtons.OK, MessageBoxIcon.Information));

        _prevOccupied = nowOccupied;
    }

    private void ApplyFilter()
    {
        var search  = _searchBox.Text.Trim().ToLower();
        var favOnly = _chkFavOnly.Checked;

        var rows = _allEntries.AsEnumerable();
        if (favOnly)  rows = rows.Where(e => e.IsFavorite);
        if (!string.IsNullOrEmpty(search))
            rows = rows.Where(e =>
                e.LocalPort.ToString().Contains(search) ||
                e.ProcessName.ToLower().Contains(search) ||
                e.Protocol.ToLower().Contains(search) ||
                e.State.ToLower().Contains(search));

        var list = rows.ToList();

        _grid.SuspendLayout();
        _grid.Rows.Clear();

        foreach (var e in list)
        {
            var stateLabel = e.State switch
            {
                "LISTENING"   => "대기",
                "ESTABLISHED" => "연결됨",
                "CLOSE_WAIT"  => "종료 대기",
                "TIME_WAIT"   => "TIME_WAIT",
                "" => "—",
                _  => e.State
            };

            var i = _grid.Rows.Add(
                e.IsFavorite ? "★" : "",
                e.Protocol,
                e.LocalPort,
                e.ProcessName,
                e.Pid > 0 ? e.Pid.ToString() : "—",
                stateLabel,
                e.RemoteAddr,
                e.ProcessPath);

            var row = _grid.Rows[i];
            row.Tag = e;

            if (e.IsFavorite)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(22, 44, 30);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(130, 215, 155);
                row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 68, 44);
                row.DefaultCellStyle.SelectionForeColor = Color.White;
                row.Cells["fav"].Style.ForeColor = Color.FromArgb(200, 170, 50);
            }
        }

        _grid.ResumeLayout();
        _statusLabel.Text =
            $"총 {list.Count}개 항목  |  즐겨찾기 {list.Count(e => e.IsFavorite)}개  |  마지막 갱신: {DateTime.Now:HH:mm:ss}";

        // 데이터 로드 후 스크롤바 핸들이 생성되어 있으면 다크 테마 재적용
        if (!_scrollbarsDarked) ApplyGridDarkScrollbars();
    }

    // ── 액션 ─────────────────────────────────────────────────────
    private void KillSelected(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        if (_grid.SelectedRows[0].Tag is not PortEntry entry) return;
        if (entry.Pid <= 0) return;

        var answer = MessageBox.Show(
            $"프로세스 '{entry.ProcessName}' (PID {entry.Pid}) 를 종료하시겠습니까?\n\n경로: {entry.ProcessPath}",
            "프로세스 종료 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes) return;

        try
        {
            Process.GetProcessById(entry.Pid).Kill();
            _ = RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"종료 실패: {ex.Message}\n(관리자 권한이 필요할 수 있습니다.)",
                "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyCell(string colName)
    {
        if (_grid.SelectedRows.Count == 0) return;
        var val = _grid.SelectedRows[0].Cells[colName].Value?.ToString() ?? "";
        if (!string.IsNullOrEmpty(val)) Clipboard.SetText(val);
    }

    private void ToggleFavorite(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        if (_grid.SelectedRows[0].Tag is not PortEntry entry) return;
        FavoritesService.Toggle(entry.LocalPort);
        foreach (var en in _allEntries)
            en.IsFavorite = FavoritesService.IsFavorite(en.LocalPort);
        ApplyFilter();
    }

    // ── UI 팩토리 ─────────────────────────────────────────────────
    private static Button MakeButton(string text, Point loc, int width)
    {
        var btn = new Button
        {
            Text = text, Location = loc, Size = new Size(width, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 56),
            ForeColor = Color.FromArgb(200, 200, 222),
            Font = new Font("Segoe UI", 9.5f), Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(54, 54, 76);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 74);
        return btn;
    }

    private static CheckBox MakeToggle(string text, Point loc, int width)
    {
        var chk = new CheckBox
        {
            Text = text, Location = loc, Size = new Size(width, 30),
            Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 56),
            ForeColor = Color.FromArgb(170, 170, 200),
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        chk.FlatAppearance.CheckedBackColor = Color.FromArgb(24, 74, 50);
        chk.FlatAppearance.BorderColor = Color.FromArgb(54, 54, 76);
        chk.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 74);
        return chk;
    }

    // ── 아이콘 ───────────────────────────────────────────────────
    private static Icon CreateAppIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(16, 16, 28));

        // 소켓 모양: 두 핀
        using var pinBrush = new SolidBrush(Color.FromArgb(100, 210, 255));
        g.FillRectangle(pinBrush, 8, 5, 5, 12);
        g.FillRectangle(pinBrush, 19, 5, 5, 12);

        // 소켓 본체
        using var bodyBrush = new SolidBrush(Color.FromArgb(70, 140, 220));
        g.FillRoundedRect(bodyBrush, new RectangleF(4, 14, 24, 14), 4);

        // 신호선 (녹색 펄스)
        using var dotBrush = new SolidBrush(Color.FromArgb(80, 220, 140));
        g.FillEllipse(dotBrush, 13, 18, 6, 6);

        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        ApplyGridDarkScrollbars();
    }

    // DataGridView 내부 스크롤바 자식 윈도우에 직접 다크 테마 적용
    // (SetWindowTheme(_grid.Handle, ...) 은 DataGridView 자체에만 적용되고 자식 스크롤바 윈도우에는 미전달)
    private void ApplyGridDarkScrollbars()
    {
        if (_scrollbarsDarked) return;

        // 리플렉션으로 VScrollBar / HScrollBar 내부 필드 접근 (.NET 8: 언더스코어 없음)
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        var t  = typeof(DataGridView);
        var vSb = (t.GetField("vertScrollBar",  F) ?? t.GetField("_vertScrollBar",  F))?.GetValue(_grid) as ScrollBar;
        var hSb = (t.GetField("horizScrollBar", F) ?? t.GetField("_horizScrollBar", F))?.GetValue(_grid) as ScrollBar;

        if (vSb?.IsHandleCreated == true && hSb?.IsHandleCreated == true)
        {
            SetWindowTheme(vSb.Handle, "DarkMode_Explorer", null);
            SetWindowTheme(hSb.Handle, "DarkMode_Explorer", null);
            _scrollbarsDarked = true;
            return;
        }

        // 폴백: 자식 윈도우 전체 열거 (핸들이 아직 생성되지 않은 경우)
        EnumChildProc cb = (hwnd, _) => { SetWindowTheme(hwnd, "DarkMode_Explorer", null); return true; };
        EnumChildWindows(_grid.Handle, cb, IntPtr.Zero);
        GC.KeepAlive(cb);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _autoTimer.Dispose();
        base.Dispose(disposing);
    }
}

// Graphics 확장
internal static class GfxExt
{
    public static void FillRoundedRect(this Graphics g, Brush brush, RectangleF rect, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
