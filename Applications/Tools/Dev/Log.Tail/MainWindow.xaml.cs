using LogTail.Controls;

namespace LogTail;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainWindow()
    {
        InitializeComponent();

        // 다크 타이틀바
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        // 드래그 앤 드롭 지원
        AllowDrop = true;
        Drop      += MainWindow_Drop;
        DragOver  += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  파일 열기
    // ─────────────────────────────────────────────────────────────
    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title       = "로그 파일 열기",
            Filter      = "로그 파일|*.log;*.txt;*.json;*.out;*.err;*.nlog;*.syslog|모든 파일|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
            OpenFile(path);
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var f in files)
            OpenFile(f);
    }

    private void OpenFile(string filePath)
    {
        // 이미 열려 있는 파일이면 해당 탭 활성화
        foreach (TabItem existing in TcMain.Items)
        {
            if (existing.Tag is string p && p == filePath)
            {
                TcMain.SelectedItem = existing;
                return;
            }
        }

        var maxLines = GetMaxLines();
        var view     = new LogTabView();

        var tab = new TabItem { Tag = filePath };
        tab.Header  = BuildTabHeader(Path.GetFileName(filePath), tab, view);
        tab.Content = view;

        TcMain.Items.Add(tab);
        TcMain.SelectedItem = tab;

        view.Initialize(filePath, maxLines);
        RefreshEmptyHint();
    }

    // ─────────────────────────────────────────────────────────────
    //  탭 헤더 구성 (파일명 + × 닫기 버튼)
    // ─────────────────────────────────────────────────────────────
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
            ToolTip = "탭 닫기",
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

    // ─────────────────────────────────────────────────────────────
    //  최대 줄 수 설정
    // ─────────────────────────────────────────────────────────────
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
}
