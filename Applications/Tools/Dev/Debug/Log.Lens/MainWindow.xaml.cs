using LogLens.Controls;
using Microsoft.Win32;

namespace LogLens;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainWindow()
    {
        InitializeComponent();

        // 아이콘 로드
        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute));
        }
        catch { }

        // 다크 타이틀바
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        // 드래그 앤 드롭 + 단축키
        PreviewKeyDown += OnPreviewKeyDown;
    }

    // ── 파일/폴더 열기 ─────────────────────────────────────────────

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title       = "로그 파일 열기",
            Filter      = "로그 파일|*.log;*.txt;*.json;*.xml;*.yml;*.yaml;*.csv;*.out;*.err|모든 파일|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
            OpenFile(path);
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "로그 폴더 선택" };
        if (dlg.ShowDialog() != true) return;

        var files = Directory.GetFiles(dlg.FolderName)
            .Where(IsTextFile)
            .OrderBy(f => f)
            .Take(20);
        foreach (var f in files)
            OpenFile(f);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
            if (File.Exists(f)) OpenFile(f);
    }

    private void OpenFile(string filePath)
    {
        // 이미 열려 있으면 해당 탭 활성화
        foreach (TabItem existing in TcMain.Items)
        {
            if (existing.Tag is string p && p == filePath)
            {
                TcMain.SelectedItem = existing;
                return;
            }
        }

        var view = new LogTabView();
        var tab  = new TabItem { Tag = filePath };
        tab.Header  = BuildTabHeader(Path.GetFileName(filePath), tab, view);
        tab.Content = view;

        TcMain.Items.Add(tab);
        TcMain.SelectedItem = tab;
        view.Initialize(filePath, GetMaxLines());

        RefreshEmptyHint();
    }

    // ── 탭 헤더 구성 ───────────────────────────────────────────────

    private object BuildTabHeader(string fileName, TabItem tab, LogTabView view)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        sp.Children.Add(new TextBlock
        {
            Text              = fileName,
            FontSize          = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0),
            ToolTip           = tab.Tag?.ToString(),
        });

        var close = new Button
        {
            Content = "×",
            Style   = Application.Current.FindResource("CloseTabBtn") as Style,
            ToolTip = "탭 닫기 (Ctrl+W)",
        };
        close.Click += (_, _) =>
        {
            TcMain.Items.Remove(tab);
            view.Dispose();
            RefreshEmptyHint();
        };
        sp.Children.Add(close);

        return sp;
    }

    private void RefreshEmptyHint()
    {
        EmptyHint.Visibility = TcMain.Items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── 최대 줄 수 ─────────────────────────────────────────────────

    private int GetMaxLines()
    {
        if (CbMaxLines.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr &&
            int.TryParse(tagStr, out var val))
        {
            return val == 0 ? int.MaxValue : val;
        }
        return 50_000;
    }

    // ── 단축키 ─────────────────────────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+O: 파일 열기
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            BtnOpenFile_Click(this, e);
            e.Handled = true;
        }
        // Ctrl+F: 현재 탭 필터 포커스
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (TcMain.SelectedContent is LogTabView view)
                view.FocusFilter();
            e.Handled = true;
        }
        // Ctrl+W: 현재 탭 닫기
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (TcMain.SelectedItem is TabItem tab)
            {
                if (tab.Content is LogTabView view)
                    view.Dispose();
                TcMain.Items.Remove(tab);
                RefreshEmptyHint();
            }
            e.Handled = true;
        }
    }

    // ── 유틸 ───────────────────────────────────────────────────────

    private static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".log" or ".txt" or ".json" or ".xml"
                   or ".yml" or ".yaml" or ".csv"
                   or ".out" or ".err" or ".conf" or ".cfg" or "";
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (TabItem tab in TcMain.Items)
            if (tab.Content is LogTabView view) view.Dispose();
        base.OnClosed(e);
    }
}
