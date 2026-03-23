using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPrompt.Models;
using DiffPrompt.Services;

namespace DiffPrompt;

// ─── API 키 입력 다이얼로그 ─────────────────────────────────────────────
public class ApiKeyDialog : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public string ApiKey { get; private set; } = "";
    private readonly TextBox _keyBox;

    public ApiKeyDialog(string current)
    {
        Title = "API 키 설정";
        Width = 480; Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };

        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock
        {
            Text = "Anthropic API 키 (sk-ant-...)",
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            FontSize = 12, Margin = new Thickness(0, 0, 0, 8)
        });

        _keyBox = new TextBox
        {
            Text = current, FontFamily = new FontFamily("Consolas"),
            FontSize = 12, Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 16)
        };
        stack.Children.Add(_keyBox);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnOk = MakeBtn("저장", true);
        var btnCancel = MakeBtn("취소", false);
        btnOk.Margin = new Thickness(0, 0, 8, 0);
        btnOk.Click += (_, _) => { ApiKey = _keyBox.Text.Trim(); DialogResult = true; };
        btnCancel.Click += (_, _) => DialogResult = false;
        btnPanel.Children.Add(btnOk);
        btnPanel.Children.Add(btnCancel);
        stack.Children.Add(btnPanel);
        Content = stack;
    }

    Button MakeBtn(string text, bool accent)
    {
        var btn = new Button
        {
            Content = text, Padding = new Thickness(20, 8, 20, 8),
            Background = accent
                ? new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF0))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
        };
        var tpl = new ControlTemplate(typeof(Button));
        var f = new FrameworkElementFactory(typeof(Border));
        f.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
        f.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
        f.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        f.AppendChild(cp);
        tpl.VisualTree = f;
        btn.Template = tpl;
        return btn;
    }
}

// ─── Diff 뷰어 ────────────────────────────────────────────────────────────
public class DiffWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public DiffWindow(string textA, string textB)
    {
        Title = "Diff 비교 — A vs B";
        Width = 1000; Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };

        var diff = InlineDiffBuilder.Diff(textA, textB);
        var rtb = new RichTextBox
        {
            IsReadOnly = true, Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            BorderThickness = new Thickness(0), Padding = new Thickness(16),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var doc = new FlowDocument();
        var para = new Paragraph { LineHeight = 20 };
        foreach (var line in diff.Lines)
        {
            Brush bg = line.Type switch
            {
                ChangeType.Inserted => new SolidColorBrush(Color.FromArgb(80, 0x66, 0xBB, 0x6A)),
                ChangeType.Deleted => new SolidColorBrush(Color.FromArgb(80, 0xEF, 0x53, 0x50)),
                ChangeType.Modified => new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xCC, 0x00)),
                _ => Brushes.Transparent
            };
            string prefix = line.Type switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                ChangeType.Modified => "~ ",
                _ => "  "
            };
            var run = new Run(prefix + line.Text + "\n") { Background = bg };
            para.Inlines.Add(run);
        }
        doc.Blocks.Add(para);
        rtb.Document = doc;

        var sv = new ScrollViewer { Content = rtb };
        Content = sv;
    }
}

// ─── 이력 뷰어 ────────────────────────────────────────────────────────────
public class HistoryWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public Experiment? SelectedExperiment { get; private set; }
    private readonly DbService _db;
    private readonly ListBox _list;

    public HistoryWindow(DbService db)
    {
        _db = db;
        Title = "실험 이력";
        Width = 800; Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

        Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
            Refresh();
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _list = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderThickness = new Thickness(0),
            FontSize = 12
        };
        _list.MouseDoubleClick += (_, _) => LoadSelected();
        Grid.SetRow(_list, 0);
        grid.Children.Add(_list);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 8, 16, 8) };
        var btnLoad = MakeBtn("불러오기");
        var btnDelete = MakeBtn("삭제");
        btnLoad.Margin = new Thickness(0, 0, 8, 0);
        btnLoad.Click += (_, _) => LoadSelected();
        btnDelete.Click += (_, _) => DeleteSelected();
        btnPanel.Children.Add(btnLoad);
        btnPanel.Children.Add(btnDelete);
        Grid.SetRow(btnPanel, 1);
        grid.Children.Add(btnPanel);

        Content = grid;
    }

    void Refresh()
    {
        _list.Items.Clear();
        foreach (var e in _db.GetAll())
        {
            string winner = e.WinnerVote switch { 1 => "🔵A", 2 => "🟠B", 0 => "🤝", _ => "?" };
            var lbi = new ListBoxItem
            {
                Tag = e.Id,
                Content = $"[{e.Id}] {e.CreatedAt:MM-dd HH:mm} | {e.ModelA} vs {e.ModelB} | {winner} | ${e.CostA + e.CostB:F4}",
                Padding = new Thickness(12, 6, 12, 6)
            };
            _list.Items.Add(lbi);
        }
    }

    void LoadSelected()
    {
        if (_list.SelectedItem is not ListBoxItem lbi) return;
        int id = (int)lbi.Tag!;
        SelectedExperiment = _db.GetById(id);
        DialogResult = true;
    }

    void DeleteSelected()
    {
        if (_list.SelectedItem is not ListBoxItem lbi) return;
        if (MessageBox.Show("삭제하시겠습니까?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _db.Delete((int)lbi.Tag!);
        Refresh();
    }

    Button MakeBtn(string text)
    {
        return new Button
        {
            Content = text, Padding = new Thickness(16, 6, 16, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand
        };
    }
}
