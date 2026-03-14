using System.Data;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.Sqlite;
using SqlLens.Services;

namespace SqlLens;

record HistoryItem(string Sql, string Info);

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    string? _dbPath;
    List<HistoryItem> _history = [];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
    }

    // ─── DB 연결 ──────────────────────────────────────────────────────────
    void BtnOpenDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "SQLite DB|*.db;*.sqlite;*.sqlite3;*.s3db|모든 파일|*.*",
            Title = "SQLite 데이터베이스 열기"
        };
        if (dlg.ShowDialog() != true) return;
        ConnectDb(dlg.FileName);
    }

    void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ConnectDb(files[0]);
    }

    void ConnectDb(string path)
    {
        try
        {
            _dbPath = path;
            DbLabel.Text = System.IO.Path.GetFileName(path);
            LoadSchema();
            LoadIndexInfo();
            StatusBar.Text = $"연결됨: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"DB 연결 실패: {ex.Message}", "SQL.Lens", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void LoadSchema()
    {
        if (_dbPath == null) return;
        SchemaTree.Items.Clear();
        using var con = OpenConnection();

        // 테이블 목록
        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string tableName = r.GetString(0);
            var treeItem = new TreeViewItem
            {
                Header = $"📋 {tableName}",
                IsExpanded = false,
                Tag = tableName
            };
            treeItem.Selected += TableItem_Selected;

            // 컬럼 로드
            var colCmd = con.CreateCommand();
            colCmd.CommandText = $"PRAGMA table_info([{tableName}])";
            using var cr = colCmd.ExecuteReader();
            while (cr.Read())
            {
                string col = cr.GetString(1);
                string type = cr.GetString(2);
                bool pk = cr.GetBoolean(5);
                treeItem.Items.Add(new TreeViewItem
                {
                    Header = $"  {(pk ? "🔑" : "·")} {col} ({type})",
                    Foreground = pk ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x4F))
                                    : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11
                });
            }
            SchemaTree.Items.Add(treeItem);
        }
    }

    void TableItem_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem item && item.Tag is string table)
        {
            SqlEditor.Text = $"SELECT * FROM [{table}] LIMIT 100;";
            e.Handled = true;
        }
    }

    void LoadIndexInfo()
    {
        if (_dbPath == null) return;
        using var con = OpenConnection();
        var sb = new StringBuilder();

        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT name, tbl_name FROM sqlite_master WHERE type='index' ORDER BY tbl_name, name";
        using var r = cmd.ExecuteReader();

        string? lastTable = null;
        while (r.Read())
        {
            string idxName = r.GetString(0);
            string tableName = r.GetString(1);
            if (tableName != lastTable)
            {
                sb.AppendLine($"[{tableName}]");
                lastTable = tableName;
            }
            sb.AppendLine($"  · {idxName}");
        }
        IndexInfo.Text = sb.Length > 0 ? sb.ToString() : "인덱스 없음";
    }

    // ─── SQL 실행 ─────────────────────────────────────────────────────────
    void SqlEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5) ExecuteQuery();
    }

    void BtnRun_Click(object sender, RoutedEventArgs e) => ExecuteQuery();

    void ExecuteQuery()
    {
        if (_dbPath == null) { StatusBar.Text = "DB를 먼저 연결하세요."; return; }
        string sql = GetActiveSql();
        if (string.IsNullOrWhiteSpace(sql)) return;

        try
        {
            using var con = OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var r = cmd.ExecuteReader();
            sw.Stop();

            var dt = new DataTable();
            dt.Load(r);
            ResultGrid.ItemsSource = dt.DefaultView;

            string info = $"{dt.Rows.Count}행 · {dt.Columns.Count}열 · {sw.ElapsedMilliseconds}ms";
            ResultInfo.Text = info;

            AddHistory(sql, info);
            StatusBar.Text = $"실행 완료: {info}";
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"오류: {ex.Message}";
            MessageBox.Show(ex.Message, "실행 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ─── EXPLAIN QUERY PLAN ────────────────────────────────────────────────
    void BtnExplain_Click(object sender, RoutedEventArgs e)
    {
        if (_dbPath == null) { StatusBar.Text = "DB를 먼저 연결하세요."; return; }
        string sql = GetActiveSql();
        if (string.IsNullOrWhiteSpace(sql)) return;

        try
        {
            using var con = OpenConnection();
            var cmd = con.CreateCommand();
            cmd.CommandText = $"EXPLAIN QUERY PLAN {sql}";

            var rows = new List<(int id, int parent, int notused, string detail)>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    rows.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3)));
            }

            var nodes = QueryPlanParser.BuildTree(rows);
            DrawPlanTree(nodes);

            var issues = QueryPlanParser.DetectIssues(nodes);
            IssueList.ItemsSource = issues.Count > 0 ? issues
                : [new PlanIssue("✅", "성능 이슈 없음", "이 쿼리는 효율적으로 실행됩니다.")];

            AddHistory($"EXPLAIN: {sql}", $"{nodes.Count}개 노드");
            StatusBar.Text = $"실행 계획 분석 완료 | {issues.Count(i => !i.Severity.StartsWith('✅'))}개 이슈 발견";
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"EXPLAIN 오류: {ex.Message}";
        }
    }

    // ─── 트리 다이어그램 그리기 ────────────────────────────────────────────
    void DrawPlanTree(List<PlanNode> nodes)
    {
        PlanCanvas.Children.Clear();
        if (nodes.Count == 0) return;

        const double nodeW = 280, nodeH = 36, hGap = 30, vGap = 20;

        // depth별 y 위치 계산
        var nodeRects = new Dictionary<int, Rect>();
        int maxDepth = nodes.Max(n => n.Depth);

        var byDepth = nodes.GroupBy(n => n.Depth).OrderBy(g => g.Key);
        foreach (var group in byDepth)
        {
            double cx = 16 + group.Key * (nodeW + hGap);
            double cy = 16;
            foreach (var node in group)
            {
                nodeRects[node.Id] = new Rect(cx, cy, nodeW, nodeH);
                cy += nodeH + vGap;
            }
        }

        // 선 먼저
        foreach (var node in nodes)
        {
            if (node.ParentId != 0 && nodeRects.TryGetValue(node.ParentId, out var pr) && nodeRects.TryGetValue(node.Id, out var nr))
            {
                var line = new System.Windows.Shapes.Line
                {
                    X1 = pr.Left + pr.Width,
                    Y1 = pr.Top + pr.Height / 2,
                    X2 = nr.Left,
                    Y2 = nr.Top + nr.Height / 2,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x6A)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection([4, 3])
                };
                PlanCanvas.Children.Add(line);
            }
        }

        // 노드 박스
        foreach (var node in nodes)
        {
            if (!nodeRects.TryGetValue(node.Id, out var rect)) continue;

            bool hasScan = node.Detail.ToUpper().Contains("SCAN TABLE");
            var bg = hasScan
                ? new SolidColorBrush(Color.FromArgb(180, 0x3A, 0x15, 0x10))
                : new SolidColorBrush(Color.FromArgb(180, 0x14, 0x14, 0x30));

            var border = new Border
            {
                Width = rect.Width, Height = rect.Height,
                Background = bg,
                BorderBrush = hasScan
                    ? new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
                    : new SolidColorBrush(Color.FromRgb(0x4A, 0x7F, 0xD6)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ToolTip = node.Detail
            };
            string icon = node.Detail.ToUpper() switch
            {
                var d when d.Contains("SCAN TABLE") => "⚠️ ",
                var d when d.Contains("USING INDEX") => "✅ ",
                var d when d.Contains("SEARCH") => "🔍 ",
                _ => "▶ "
            };
            var tb = new TextBlock
            {
                Text = icon + TruncateDetail(node.Detail, 34),
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xEE)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            border.Child = tb;
            System.Windows.Controls.Canvas.SetLeft(border, rect.Left);
            System.Windows.Controls.Canvas.SetTop(border, rect.Top);
            PlanCanvas.Children.Add(border);
        }

        // Canvas 크기 조정
        double maxRight = nodeRects.Values.Max(r => r.Right) + 20;
        double maxBottom = nodeRects.Values.Max(r => r.Bottom) + 20;
        PlanCanvas.Width = maxRight;
        PlanCanvas.Height = maxBottom;
    }

    static string TruncateDetail(string s, int max) =>
        s.Length > max ? s[..max] + "…" : s;

    // ─── 포맷터 ───────────────────────────────────────────────────────────
    void BtnFormat_Click(object sender, RoutedEventArgs e)
    {
        string sql = SqlEditor.Text;
        // 간단한 포맷팅: 키워드 앞에 줄바꿈
        string[] keywords = ["SELECT", "FROM", "WHERE", "JOIN", "LEFT JOIN", "RIGHT JOIN",
            "INNER JOIN", "ORDER BY", "GROUP BY", "HAVING", "LIMIT", "OFFSET", "UNION"];
        foreach (var kw in keywords)
            sql = System.Text.RegularExpressions.Regex.Replace(
                sql, @"\b" + kw + @"\b", "\n" + kw,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        SqlEditor.Text = sql.TrimStart();
    }

    // ─── 히스토리 ─────────────────────────────────────────────────────────
    void AddHistory(string sql, string info)
    {
        _history.Insert(0, new HistoryItem(sql, info));
        if (_history.Count > 50) _history.RemoveAt(50);
        HistoryList.ItemsSource = null;
        HistoryList.ItemsSource = _history;
    }

    void HistoryList_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item)
            SqlEditor.Text = item.Sql.StartsWith("EXPLAIN: ")
                ? item.Sql[9..]
                : item.Sql;
    }

    // ─── 유틸 ─────────────────────────────────────────────────────────────
    string GetActiveSql()
    {
        string sel = SqlEditor.SelectedText.Trim();
        return string.IsNullOrWhiteSpace(sel) ? SqlEditor.Text.Trim() : sel;
    }

    SqliteConnection OpenConnection()
    {
        var con = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
        con.Open();
        return con;
    }
}
