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
    private readonly Button       _btnInterval;
    private readonly Button       _btnHelp;
    private readonly CheckBox     _chkAuto;
    private readonly CheckBox     _chkFavOnly;
    private readonly System.Windows.Forms.Timer _autoTimer = new();

    private static readonly int[] _intervals  = [3, 5, 10, 30];
    private int                    _intervalIdx = 1;  // 기본 5초

    private List<PortEntry> _allEntries   = [];
    private HashSet<int>    _prevOccupied = [];
    private Panel?          _sbCorner;
    private bool            _fixingCorner;
    private bool            _initialized;

    public MainWindow()
    {
        Text = "Port.Watch \u2014 포트 & 프로세스 모니터";
        Size = new Size(1120, 700);
        MinimumSize = new Size(940, 520);
        BackColor = Color.FromArgb(16, 22, 16);
        ForeColor = Color.FromArgb(215, 235, 218);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        Icon = File.Exists(icoPath) ? new Icon(icoPath) : CreateAppIcon();

        // ── 상단 툴바 ────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            BackColor = Color.FromArgb(18, 26, 18),
            Padding = new Padding(12, 10, 12, 10)
        };

        var lblIcon = new Label
        {
            Text = "🔍",
            Font = new Font("Segoe UI", 12f),
            ForeColor = Color.FromArgb(120, 120, 150),
            AutoSize = false,
            Size = new Size(26, 26),
            Location = new Point(14, 14),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _searchBox = new TextBox
        {
            PlaceholderText = "포트 번호 또는 프로세스 이름...",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(22, 34, 22),
            ForeColor = Color.FromArgb(215, 235, 218),
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();
        var txH = _searchBox.PreferredHeight;  // 폰트 기반 자연 높이
        var searchBorder = new Panel
        {
            Location = new Point(44, (62 - txH - 2) / 2),  // 툴바 내 수직 중앙 정렬
            Size = new Size(252, txH + 2),  // 정확한 높이: 내부 텍스트 + 위아래 1px 보더
            BackColor = Color.FromArgb(44, 68, 44),
            Padding = new Padding(1)
        };
        searchBorder.Controls.Add(_searchBox);

        _btnRefresh = MakeButton("↺  새로고침", new Point(308, 15), 148);
        _btnRefresh.Click += async (_, _) => await RefreshAsync();

        _chkAuto = MakeToggle("⏱ 자동 갱신", new Point(460, 15), 162);
        _chkAuto.CheckedChanged += (_, _) =>
        {
            if (!_initialized) return;
            _autoTimer!.Enabled = _chkAuto.Checked;
            _chkAuto.Text = _chkAuto.Checked ? "⏱ 자동 갱신 ●" : "⏱ 자동 갱신";
        };

        _btnInterval = MakeButton($"{_intervals[_intervalIdx]}s", new Point(636, 15), 50);
        _btnInterval.Click += (_, _) =>
        {
            _intervalIdx = (_intervalIdx + 1) % _intervals.Length;
            _autoTimer.Interval = _intervals[_intervalIdx] * 1000;
            _btnInterval.Text = $"{_intervals[_intervalIdx]}s";
        };

        _chkFavOnly = MakeToggle("★ 즐겨찾기만", new Point(690, 15), 160);
        _chkFavOnly.CheckedChanged += (_, _) =>
        {
            if (!_initialized) return;
            ApplyFilter();
        };

        _btnHelp = MakeButton("?", new Point(854, 15), 40);
        _btnHelp.ForeColor = Color.FromArgb(130, 200, 155);
        _btnHelp.Click += (_, _) => ShowHelp();

        toolbar.Controls.AddRange([lblIcon, searchBorder, _btnRefresh, _chkAuto, _btnInterval, _chkFavOnly, _btnHelp]);

        // 검색창 가변 폭: 창 리사이즈 시 버튼은 오른쪽 고정, 검색창이 남은 공간을 채움
        void UpdateToolbarLayout()
        {
            const int rightMargin = 12, gap = 6;
            var right = toolbar.ClientSize.Width - rightMargin;
            _btnHelp.Left     = right - _btnHelp.Width;    right = _btnHelp.Left     - gap;
            _chkFavOnly.Left  = right - _chkFavOnly.Width; right = _chkFavOnly.Left  - gap;
            _btnInterval.Left = right - _btnInterval.Width; right = _btnInterval.Left - gap;
            _chkAuto.Left     = right - _chkAuto.Width;    right = _chkAuto.Left     - gap;
            _btnRefresh.Left  = right - _btnRefresh.Width;  right = _btnRefresh.Left  - 8;
            if (right > searchBorder.Left + 60)  // 최소 검색창 폭 60px
                searchBorder.Width = right - searchBorder.Left;
        }
        toolbar.SizeChanged += (_, _) => UpdateToolbarLayout();
        UpdateToolbarLayout();

        // ── DataGridView ─────────────────────────────────────────
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(14, 22, 14),
            GridColor = Color.FromArgb(28, 46, 28),
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
            ColumnHeadersHeight = 30,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ScrollBars = ScrollBars.Both
        };

        var cellStyle = _grid.DefaultCellStyle;
        cellStyle.BackColor = Color.FromArgb(16, 24, 16);
        cellStyle.ForeColor = Color.FromArgb(212, 232, 216);
        cellStyle.SelectionBackColor = Color.FromArgb(26, 76, 44);
        cellStyle.SelectionForeColor = Color.White;
        cellStyle.Padding = new Padding(6, 0, 4, 0);

        var altStyle = _grid.AlternatingRowsDefaultCellStyle;
        altStyle.BackColor = Color.FromArgb(20, 30, 20);
        altStyle.ForeColor = Color.FromArgb(212, 232, 216);
        altStyle.SelectionBackColor = Color.FromArgb(26, 76, 44);
        altStyle.SelectionForeColor = Color.White;

        var hdrStyle = _grid.ColumnHeadersDefaultCellStyle;
        hdrStyle.BackColor = Color.FromArgb(20, 32, 20);
        hdrStyle.ForeColor = Color.FromArgb(120, 180, 135);
        hdrStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        hdrStyle.SelectionBackColor = Color.FromArgb(20, 32, 20);
        hdrStyle.Padding = new Padding(6, 0, 4, 0);
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        AddCol("fav",     "",         28,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("proto",   "프로토콜", 70,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("port",    "포트",     72,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("process", "프로세스", 190, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("pid",     "PID",      65,  DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleCenter);
        AddCol("state",   "상태",     120, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("remote",  "원격 주소",155, DataGridViewAutoSizeColumnMode.None,  DataGridViewContentAlignment.MiddleLeft);
        AddCol("path",    "경로",     200, DataGridViewAutoSizeColumnMode.Fill,  DataGridViewContentAlignment.MiddleLeft);
        _grid.Columns["path"].MinimumWidth = 200;

        // 컨텍스트 메뉴
        var ctx = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, Font = new Font("Segoe UI", 9.5f) };
        var miKill   = new ToolStripMenuItem("⛔  프로세스 종료");
        var miPort   = new ToolStripMenuItem("📋  포트 번호 복사");
        var miPath   = new ToolStripMenuItem("📋  경로 복사");
        var miOpen   = new ToolStripMenuItem("📂  파일 탐색기로 열기");
        var miFav    = new ToolStripMenuItem("★  즐겨찾기 토글");
        miKill.Click  += KillSelected;
        miPort.Click  += (_, _) => CopyCell("port");
        miPath.Click  += (_, _) => CopyCell("path");
        miOpen.Click  += (_, _) =>
        {
            if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is PortEntry oe)
                OpenInExplorer(oe.ProcessPath);
        };
        miFav.Click   += ToggleFavorite;
        ctx.Items.AddRange([miKill, new ToolStripSeparator(), miPort, miPath, miOpen, new ToolStripSeparator(), miFav]);
        _grid.ContextMenuStrip = ctx;
        _grid.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C) { CopyCell("path"); e.Handled = true; }
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (_grid.Rows[e.RowIndex].Tag is PortEntry entry && !string.IsNullOrEmpty(entry.ProcessPath))
                OpenInExplorer(entry.ProcessPath);
        };

        // ── 상태바 ──────────────────────────────────────────────
        var statusBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = Color.FromArgb(16, 26, 16)
        };

        _statusLabel = new Label
        {
            Text = "로딩 중...",
            ForeColor = Color.FromArgb(90, 140, 100),
            Font = new Font("Segoe UI", 9f),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 0, 0, 0)
        };

        var btnKill = new Button
        {
            Text = "⛔ 선택 프로세스 종료",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(220, 90, 80),
            BackColor = Color.FromArgb(16, 26, 16),
            Dock = DockStyle.Right,
            Width = 220,
            Cursor = Cursors.Hand
        };
        btnKill.FlatAppearance.BorderColor = Color.FromArgb(44, 68, 44);
        btnKill.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 48, 30);
        btnKill.Click += KillSelected;

        // _statusLabel(index 0) → Fill, btnKill(index 1) → Right
        // WinForms는 Controls 역순(index 1 먼저)으로 도킹 처리 → btnKill이 오른쪽 차지 후 _statusLabel이 나머지 채움
        statusBar.Controls.AddRange([_statusLabel, btnKill]);

        // ── 자동 갱신 타이머 ─────────────────────────────────────
        _autoTimer.Interval = _intervals[_intervalIdx] * 1000;
        _autoTimer.Tick += async (_, _) => await RefreshAsync();

        // WinForms docking: 역순(back→front)으로 처리 → Fill(_grid)이 index 0(front)이어야 나머지 후 채움
        Resize += (_, _) => { ApplyGridDarkScrollbars(); FixScrollbarCorner(); };
        _grid.Layout += (_, _) => FixScrollbarCorner();
        FormClosing += (_, _) => SaveWindowState();
        Controls.AddRange([_grid, statusBar, toolbar]);
        RestoreWindowState();
        _initialized = true;
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
        if (freed.Count > 0)
        {
            var ports = string.Join(", ", freed.Select(p => $":{p}"));
            BeginInvoke(() => ShowNotice($"⚠ 즐겨찾기 포트 해제됨 — {ports}", 5000));
        }

        _prevOccupied = nowOccupied;
    }

    // 상태바에 일시 알림 표시 후 ms 경과 시 원래 상태 복원
    private System.Windows.Forms.Timer? _noticeTimer;
    private void ShowNotice(string message, int durationMs)
    {
        _statusLabel.ForeColor = Color.FromArgb(220, 170, 60);
        _statusLabel.Text = message;

        _noticeTimer?.Stop();
        _noticeTimer?.Dispose();
        _noticeTimer = new System.Windows.Forms.Timer { Interval = durationMs };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            _noticeTimer.Dispose();
            _noticeTimer = null;
            _statusLabel.ForeColor = Color.FromArgb(90, 140, 100);
            ApplyFilter();  // 상태 텍스트 복원
        };
        _noticeTimer.Start();
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
                e.State.ToLower().Contains(search) ||
                e.RemoteAddr.ToLower().Contains(search));

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
        var tcpCount  = list.Count(e => e.Protocol == "TCP");
        var udpCount  = list.Count(e => e.Protocol == "UDP");
        var procCount = list.Select(e => e.Pid).Distinct().Count(p => p > 0);
        _statusLabel.ForeColor = Color.FromArgb(90, 140, 100);
        _statusLabel.Text =
            $"총 {list.Count}개  |  TCP {tcpCount} · UDP {udpCount}  |  프로세스 {procCount}개  |  즐겨찾기 {list.Count(e => e.IsFavorite)}개  |  {DateTime.Now:HH:mm:ss}";

        // 데이터 로드 후 스크롤바 다크 테마 재적용
        ApplyGridDarkScrollbars();
        FixScrollbarCorner();
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

    // ── 파일 탐색기로 열기 ───────────────────────────────────────
    private static void OpenInExplorer(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path))
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path))
                Process.Start("explorer.exe", $"\"{path}\"");
        }
        catch { }
    }

    // ── 도움말 팝업 ──────────────────────────────────────────────
    private void ShowHelp()
    {
        var help = """
            Port.Watch — 사용 방법

            ── 기본 조작 ──────────────────
            Ctrl+C          선택 행 경로 복사
            더블 클릭       파일 탐색기로 위치 열기

            ── 툴바 ────────────────────────
            새로고침        포트 목록 수동 갱신
            자동 갱신       켜면 자동 주기 갱신
            3s / 5s / 10s / 30s  갱신 간격 순환 선택
            즐겨찾기만      즐겨찾기 포트만 표시

            ── 검색 ────────────────────────
            포트 번호, 프로세스명, 프로토콜,
            상태, 원격 주소로 실시간 필터링

            ── 우클릭 메뉴 ─────────────────
            프로세스 종료   선택 프로세스 Kill
            포트 번호 복사  클립보드에 포트 복사
            경로 복사       실행 파일 경로 복사
            파일 탐색기     실행 위치 탐색기 열기
            즐겨찾기 토글   즐겨찾기 추가/제거

            ── 즐겨찾기 포트 해제 알림 ─────
            즐겨찾기 포트가 닫히면 상태바에
            5초간 알림 표시
            """;
        MessageBox.Show(help, "Port.Watch 도움말", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── 창 크기/위치 영속성 ──────────────────────────────────────
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PortWatch", "settings.json");

    private record WindowBounds(int X, int Y, int Width, int Height);

    private void SaveWindowState()
    {
        if (WindowState == FormWindowState.Minimized) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            System.IO.File.WriteAllText(_settingsPath,
                System.Text.Json.JsonSerializer.Serialize(new WindowBounds(Left, Top, Width, Height)));
        }
        catch { }
    }

    private void RestoreWindowState()
    {
        try
        {
            if (!System.IO.File.Exists(_settingsPath)) return;
            var s = System.Text.Json.JsonSerializer.Deserialize<WindowBounds>(
                System.IO.File.ReadAllText(_settingsPath));
            if (s is null) return;
            var screen = Screen.GetWorkingArea(new System.Drawing.Point(s.X, s.Y));
            if (s.Width  >= MinimumSize.Width && s.Height >= MinimumSize.Height
                && s.X   >= screen.Left - 200  && s.Y    >= screen.Top - 100
                && s.X   <= screen.Right - 100)
            {
                StartPosition = FormStartPosition.Manual;
                SetBounds(s.X, s.Y, s.Width, s.Height);
            }
        }
        catch { }
    }

    // ── UI 팩토리 ─────────────────────────────────────────────────
    private static Button MakeButton(string text, Point loc, int width)
    {
        var btn = new Button
        {
            Text = text, Location = loc, Size = new Size(width, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 40, 24),
            ForeColor = Color.FromArgb(190, 225, 198),
            Font = new Font("Segoe UI", 9.5f), Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(44, 72, 46);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 58, 36);
        return btn;
    }

    private static CheckBox MakeToggle(string text, Point loc, int width)
    {
        var chk = new CheckBox
        {
            Text = text, Location = loc, Size = new Size(width, 32),
            Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(24, 40, 24),
            ForeColor = Color.FromArgb(155, 200, 165),
            Font = new Font("Segoe UI", 9.5f),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        chk.FlatAppearance.CheckedBackColor = Color.FromArgb(20, 74, 40);
        chk.FlatAppearance.BorderColor = Color.FromArgb(44, 72, 46);
        chk.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 58, 36);
        return chk;
    }

    // ── 아이콘 ───────────────────────────────────────────────────
    private static Icon CreateAppIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(10, 22, 10));

        // 소켓 모양: 두 핀
        using var pinBrush = new SolidBrush(Color.FromArgb(0, 210, 120));
        g.FillRectangle(pinBrush, 8, 5, 5, 12);
        g.FillRectangle(pinBrush, 19, 5, 5, 12);

        // 소켓 본체
        using var bodyBrush = new SolidBrush(Color.FromArgb(0, 160, 80));
        g.FillRoundedRect(bodyBrush, new RectangleF(4, 14, 24, 14), 4);

        // 신호선 (에메랄드 펄스)
        using var dotBrush = new SolidBrush(Color.FromArgb(0, 230, 118));
        g.FillEllipse(dotBrush, 13, 18, 6, 6);

        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        ApplyGridDarkScrollbars();
        FixScrollbarCorner();
    }

    // DataGridView 내부 스크롤바에 개별로 다크 테마 적용
    // 창 크기 변경 시 hSb가 새로 생성될 수 있으므로 매번 재적용 (idempotent)
    private void ApplyGridDarkScrollbars()
    {
        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        var t  = typeof(DataGridView);
        var vSb = (t.GetField("vertScrollBar",  F) ?? t.GetField("_vertScrollBar",  F))?.GetValue(_grid) as ScrollBar;
        var hSb = (t.GetField("horizScrollBar", F) ?? t.GetField("_horizScrollBar", F))?.GetValue(_grid) as ScrollBar;

        if (vSb?.IsHandleCreated == true) SetWindowTheme(vSb.Handle, "DarkMode_Explorer", null);
        if (hSb?.IsHandleCreated == true) SetWindowTheme(hSb.Handle, "DarkMode_Explorer", null);
    }

    // DataGridView가 vSb·hSb 코너 영역을 SystemColors.Control(밝은 색)으로 직접 페인트하므로
    // 리플렉션으로 두 스크롤바 위치를 계산해 다크 Panel로 덮음
    private void FixScrollbarCorner()
    {
        if (_fixingCorner) return;  // Controls.Add가 Layout 이벤트 재진입 방지
        _fixingCorner = true;
        try
        {
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            var t   = typeof(DataGridView);
            var vSb = (t.GetField("vertScrollBar",  F) ?? t.GetField("_vertScrollBar",  F))?.GetValue(_grid) as ScrollBar;
            var hSb = (t.GetField("horizScrollBar", F) ?? t.GetField("_horizScrollBar", F))?.GetValue(_grid) as ScrollBar;

            if (vSb is { Visible: true } && hSb is { Visible: true })
            {
                if (_sbCorner == null)
                {
                    _sbCorner = new Panel { BackColor = Color.FromArgb(16, 24, 16), BorderStyle = BorderStyle.None };
                    _grid.Controls.Add(_sbCorner);
                }
                _sbCorner.Bounds  = new Rectangle(vSb.Left, hSb.Top, vSb.Width, hSb.Height);
                _sbCorner.Visible = true;
                _sbCorner.BringToFront();
            }
            else if (_sbCorner != null)
            {
                _sbCorner.Visible = false;
            }
        }
        finally { _fixingCorner = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoTimer.Dispose();
            _noticeTimer?.Dispose();
        }
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
