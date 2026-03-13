using System.Drawing.Drawing2D;
using WiFiCast.Views;

namespace WiFiCast.Views;

/// <summary>Wi-Fi 채널 분석기 메인 창</summary>
public sealed class MainForm : Form
{
    private readonly System.Windows.Forms.Timer _scanTimer = new() { Interval = 15_000 };
    private List<WifiNetwork> _networks = [];

    // ── 컨트롤 참조 ──────────────────────────────────────────────────
    private readonly ChannelHeatmap _heatmap24   = new();
    private readonly ChannelHeatmap _heatmap5    = new();
    private readonly DataGridView   _grid        = new();
    private readonly Label          _lblStatus   = new();
    private readonly Label          _lblBest24   = new();
    private readonly Label          _lblBest5    = new();
    private readonly Label          _lblScanTime = new();

    public MainForm()
    {
        Text            = "📶 WiFi.Cast";
        Size            = new Size(900, 650);
        MinimumSize     = new Size(700, 500);
        BackColor       = Color.FromArgb(26, 26, 26);
        ForeColor       = Color.FromArgb(224, 224, 224);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9f);

        ApplyDarkTitleBar();
        BuildLayout();

        FormClosing += (_, e) => { e.Cancel = true; Hide(); };

        _scanTimer.Tick += (_, _) => Scan();
        _scanTimer.Start();

        Shown += (_, _) => Scan();
    }

    // ── 레이아웃 ──────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // 툴바
        var toolbar = new Panel
        {
            Dock = DockStyle.Top, Height = 40,
            BackColor = Color.FromArgb(20, 20, 20),
            Padding = new Padding(8, 0, 8, 0),
        };

        var btnScan = DarkButton("🔍 지금 스캔", () => Scan());
        var btnExport = DarkButton("📄 CSV 내보내기", ExportCsv);
        btnScan.Location   = new Point(8, 6);
        btnExport.Location = new Point(116, 6);

        _lblScanTime.AutoSize  = true;
        _lblScanTime.ForeColor = Color.FromArgb(100, 100, 100);
        _lblScanTime.Location  = new Point(230, 12);

        toolbar.Controls.AddRange([btnScan, btnExport, _lblScanTime]);
        Controls.Add(toolbar);

        // 탭 패널 (상단)
        var tabs = new TabControl
        {
            Dock = DockStyle.Top, Height = 220,
            BackColor = Color.FromArgb(26, 26, 26),
        };
        StyleTabControl(tabs);

        var tab24 = new TabPage("2.4 GHz") { BackColor = Color.FromArgb(20, 20, 20) };
        var tab5  = new TabPage("5 GHz")   { BackColor = Color.FromArgb(20, 20, 20) };

        _heatmap24.Dock = DockStyle.Fill;
        _heatmap5 .Dock = DockStyle.Fill;
        tab24.Controls.Add(_heatmap24);
        tab5 .Controls.Add(_heatmap5);
        tabs.TabPages.AddRange([tab24, tab5]);
        Controls.Add(tabs);

        // 추천 채널 패널
        var recPanel = new Panel
        {
            Dock = DockStyle.Top, Height = 28,
            BackColor = Color.FromArgb(14, 30, 14),
            Padding = new Padding(10, 4, 10, 4),
        };
        _lblBest24.AutoSize = true;
        _lblBest24.ForeColor = Color.FromArgb(76, 175, 80);
        _lblBest24.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _lblBest24.Location = new Point(10, 5);

        _lblBest5.AutoSize = true;
        _lblBest5.ForeColor = Color.FromArgb(76, 175, 80);
        _lblBest5.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _lblBest5.Location = new Point(200, 5);

        recPanel.Controls.AddRange([_lblBest24, _lblBest5]);
        Controls.Add(recPanel);

        // 네트워크 목록 그리드 (하단)
        BuildGrid();
        Controls.Add(_grid);

        // 상태바
        _lblStatus.Dock      = DockStyle.Bottom;
        _lblStatus.Height    = 22;
        _lblStatus.BackColor = Color.FromArgb(18, 18, 18);
        _lblStatus.ForeColor = Color.FromArgb(100, 100, 100);
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        _lblStatus.Padding   = new Padding(8, 0, 0, 0);
        Controls.Add(_lblStatus);

        // 레이아웃 순서 조정 (Dock 순서)
        _grid.Dock = DockStyle.Fill;
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows    = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly             = true;
        _grid.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect          = false;
        _grid.RowHeadersVisible    = false;
        _grid.BorderStyle          = BorderStyle.None;
        _grid.AutoSizeColumnsMode  = DataGridViewAutoSizeColumnsMode.Fill;

        // 다크 테마
        _grid.BackgroundColor           = Color.FromArgb(22, 22, 22);
        _grid.DefaultCellStyle.BackColor = Color.FromArgb(26, 26, 26);
        _grid.DefaultCellStyle.ForeColor = Color.FromArgb(220, 220, 220);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 80, 120);
        _grid.DefaultCellStyle.SelectionForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(18, 18, 18);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(150, 150, 150);
        _grid.GridColor = Color.FromArgb(40, 40, 40);
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.ColumnHeadersHeight = 28;
        _grid.RowTemplate.Height = 24;
        _grid.EnableHeadersVisualStyles = false;

        string[] cols = ["SSID", "BSSID", "채널", "대역", "신호", "RSSI (dBm)", "주파수"];
        int[]  widths  = [3,      2,       1,     1,     1,     1,             1];
        foreach (var (name, w) in cols.Zip(widths))
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = name,
                FillWeight = w * 20,
                SortMode   = DataGridViewColumnSortMode.NotSortable,
            });
        }
    }

    // ── 스캔 ──────────────────────────────────────────────────────────

    private bool _scanning;

    public void Scan()
    {
        if (_scanning) return;
        _scanning = true;
        _lblStatus.Text = "스캔 중...";

        Task.Run(() =>
        {
            try   { return WlanScanner.Scan(); }
            catch { return new List<WifiNetwork>(); }
        }).ContinueWith(t =>
        {
            _networks = t.Result;
            _scanning = false;
            RefreshUi();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void RefreshUi()
    {
        // 추천 채널 계산
        int best24 = ChannelAnalyzer.BestChannel24(_networks);
        int best5  = ChannelAnalyzer.BestChannel5(_networks);

        int[] ch24 = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];
        int[] ch5  = [36, 40, 44, 48, 52, 56, 60, 64, 100, 104, 108, 112, 116, 120, 124, 128, 132, 136, 140, 149, 153, 157, 161, 165];

        var nets24 = _networks.Where(n => n.Band == "2.4GHz").ToList();
        var nets5  = _networks.Where(n => n.Band == "5GHz").ToList();

        _heatmap24.Update(nets24, ch24, best24);
        _heatmap5 .Update(nets5,  ch5,  best5);

        _lblBest24.Text = $"✨ 2.4GHz 추천: CH {best24}";
        _lblBest5 .Text = nets5.Count > 0 ? $"✨ 5GHz 추천: CH {best5}" : "5GHz 네트워크 없음";

        // 그리드 갱신
        _grid.Rows.Clear();
        foreach (var n in _networks)
        {
            _grid.Rows.Add(n.Ssid, n.Bssid, n.Channel, n.Band,
                $"{n.Signal}%", $"{n.Rssi} dBm", $"{n.FreqMhz} MHz");
        }

        // 신호 강도에 따라 행 색상
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Index < _networks.Count)
            {
                int sig = _networks[row.Index].Signal;
                row.DefaultCellStyle.ForeColor = sig >= 70
                    ? Color.FromArgb(76, 175, 80)
                    : sig >= 40
                        ? Color.FromArgb(255, 193, 7)
                        : Color.FromArgb(244, 67, 54);
            }
        }

        string time = DateTime.Now.ToString("HH:mm:ss");
        _lblStatus.Text = $"마지막 스캔: {time}  |  {_networks.Count}개 네트워크 감지";
        _lblScanTime.Text = $"마지막: {time}";
    }

    // ── CSV 내보내기 ──────────────────────────────────────────────────

    private void ExportCsv()
    {
        using var dlg = new SaveFileDialog
        {
            Filter   = "CSV 파일|*.csv",
            FileName = $"wifi_scan_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SSID,BSSID,Channel,Band,Signal%,RSSI_dBm,Freq_MHz");
        foreach (var n in _networks)
            sb.AppendLine($"\"{n.Ssid}\",{n.Bssid},{n.Channel},{n.Band},{n.Signal},{n.Rssi},{n.FreqMhz}");

        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"저장 완료:\n{dlg.FileName}", "내보내기", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private static Button DarkButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text      = text,
            Size      = new Size(100, 28),
            BackColor = Color.FromArgb(35, 55, 75),
            ForeColor = Color.FromArgb(220, 220, 220),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f),
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(55, 85, 110);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static void StyleTabControl(TabControl tc)
    {
        tc.DrawMode = TabDrawMode.OwnerDrawFixed;
        tc.DrawItem += (s, e) =>
        {
            var tab = (TabControl)s!;
            bool sel = e.Index == tab.SelectedIndex;
            Color bg  = sel ? Color.FromArgb(22, 22, 22) : Color.FromArgb(18, 18, 18);
            Color fg  = sel ? Color.FromArgb(220, 220, 220) : Color.FromArgb(120, 120, 120);
            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(tab.TabPages[e.Index].Text, tab.Font, new SolidBrush(fg), e.Bounds, sf);
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int val, int sz);

    private void ApplyDarkTitleBar()
    {
        var handle = Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
    }
}
