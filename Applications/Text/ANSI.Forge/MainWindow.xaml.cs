using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AnsiForge.Services;

namespace AnsiForge;

// 셀 하나
public class AnsiCell
{
    public char Char { get; set; } = ' ';
    public Color Fg { get; set; } = Color.FromRgb(0xCC, 0xCC, 0xCC);
    public Color Bg { get; set; } = Color.FromRgb(0, 0, 0);
    public bool Bold { get; set; }
    public bool Changed { get; set; } = true;
}

public enum DrawTool { Pen, Fill, Eraser }

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    const int Cols = 80, Rows = 30;
    const double CellW = 10, CellH = 18;

    private readonly AnsiCell[,] _grid = new AnsiCell[Rows, Cols];
    private Color _fgColor = Color.FromRgb(0xCC, 0xCC, 0xCC);
    private Color _bgColor = Color.FromRgb(0, 0, 0);
    private char _currentChar = '█';
    private DrawTool _tool = DrawTool.Pen;
    private bool _drawing = false;
    private bool _suppressRawUpdate = false;

    // 16색 팔레트
    static readonly Color[] PaletteColors16 = [
        Color.FromRgb(0,0,0),        Color.FromRgb(128,0,0),
        Color.FromRgb(0,128,0),      Color.FromRgb(128,128,0),
        Color.FromRgb(0,0,128),      Color.FromRgb(128,0,128),
        Color.FromRgb(0,128,128),    Color.FromRgb(192,192,192),
        Color.FromRgb(128,128,128),  Color.FromRgb(255,0,0),
        Color.FromRgb(0,255,0),      Color.FromRgb(255,255,0),
        Color.FromRgb(0,0,255),      Color.FromRgb(255,0,255),
        Color.FromRgb(0,255,255),    Color.FromRgb(255,255,255),
    ];

    // CP437 블록 문자
    static readonly string[] BlockCharsSet = [
        "█","▓","▒","░","▄","▀","▌","▐",
        "■","□","▪","▫","◼","◻","●","○",
        "►","◄","▲","▼","◆","◇","★","☆",
        "╔","╗","╚","╝","║","═","╠","╣",
        "╦","╩","╬","┌","┐","└","┘","│","─",
    ];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        InitGrid();
        BuildPalette16();
        BuildPalette256();
        BuildBlockChars();
        DrawGrid();
        UpdateColorBoxes();
        GridSizeText.Text = $"{Cols}×{Rows}";
        CurrentCharDisplay.Text = _currentChar.ToString();
        StatusBar.Text = "클릭하거나 드래그해 그립니다. Ctrl+Z: 실행 취소.";
    }

    void InitGrid()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                _grid[r, c] = new AnsiCell();
    }

    // ─── 팔레트 구성 ──────────────────────────────────────────────────
    void BuildPalette16()
    {
        foreach (var col in PaletteColors16)
        {
            var rect = new Rectangle { Width = 16, Height = 16, Fill = new SolidColorBrush(col), Margin = new Thickness(1), Cursor = Cursors.Hand };
            rect.MouseDown += (s, _) => SetFgColor(((Rectangle)s).Fill is SolidColorBrush b ? b.Color : _fgColor);
            rect.MouseRightButtonDown += (s, _) => SetBgColor(((Rectangle)s).Fill is SolidColorBrush b ? b.Color : _bgColor);
            Palette16.Children.Add(rect);
        }
    }

    void BuildPalette256()
    {
        // 256색: 16+216+24
        for (int i = 0; i < 256; i++)
        {
            var col = XtermColor(i);
            var rect = new Rectangle { Width = 10, Height = 10, Fill = new SolidColorBrush(col), Margin = new Thickness(0.5), Cursor = Cursors.Hand, ToolTip = $"색상 {i}" };
            int ci = i;
            rect.MouseDown += (_, _) => SetFgColor(XtermColor(ci));
            rect.MouseRightButtonDown += (_, _) => SetBgColor(XtermColor(ci));
            Palette256.Children.Add(rect);
        }
    }

    void BuildBlockChars()
    {
        foreach (var ch in BlockCharsSet)
        {
            var tb = new TextBlock
            {
                Text = ch, FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                Margin = new Thickness(2), Cursor = Cursors.Hand, ToolTip = $"U+{(int)ch[0]:X4}"
            };
            string c = ch;
            tb.MouseDown += (_, _) => { _currentChar = c[0]; CurrentCharDisplay.Text = c; };
            BlockChars.Children.Add(tb);
        }
    }

    Color XtermColor(int i)
    {
        if (i < 16) return PaletteColors16[i];
        if (i < 232)
        {
            int n = i - 16, b = n % 6, g = (n / 6) % 6, r = n / 36;
            return Color.FromRgb((byte)(r == 0 ? 0 : 55 + r * 40), (byte)(g == 0 ? 0 : 55 + g * 40), (byte)(b == 0 ? 0 : 55 + b * 40));
        }
        byte gray = (byte)(8 + (i - 232) * 10);
        return Color.FromRgb(gray, gray, gray);
    }

    // ─── 그리드 렌더링 ────────────────────────────────────────────────
    void DrawGrid()
    {
        EditCanvas.Width = Cols * CellW;
        EditCanvas.Height = Rows * CellH;
        EditCanvas.Children.Clear();

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                var cell = _grid[r, c];
                DrawCell(r, c, cell);
            }
        }
        UpdatePreview();
    }

    void DrawCell(int row, int col, AnsiCell cell)
    {
        // 배경
        var bg = new Rectangle
        {
            Width = CellW, Height = CellH,
            Fill = new SolidColorBrush(cell.Bg),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(bg, col * CellW);
        Canvas.SetTop(bg, row * CellH);
        EditCanvas.Children.Add(bg);

        // 문자
        if (cell.Char != ' ')
        {
            var tb = new TextBlock
            {
                Text = cell.Char.ToString(),
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                FontSize = CellH * 0.85,
                Foreground = new SolidColorBrush(cell.Fg),
                FontWeight = cell.Bold ? FontWeights.Bold : FontWeights.Normal,
                IsHitTestVisible = false,
                Width = CellW, Height = CellH,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, col * CellW);
            Canvas.SetTop(tb, row * CellH);
            EditCanvas.Children.Add(tb);
        }
    }

    // ─── 그리기 ───────────────────────────────────────────────────────
    void EditCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _drawing = true;
        EditCanvas.CaptureMouse();
        ApplyTool(e.GetPosition(EditCanvas));
    }

    void EditCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing) return;
        var pos = e.GetPosition(EditCanvas);
        ApplyTool(pos);
        int c = (int)(pos.X / CellW), r = (int)(pos.Y / CellH);
        PosText.Text = $"({c}, {r})";
    }

    void EditCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _drawing = false;
        EditCanvas.ReleaseMouseCapture();
        UpdatePreview();
        UpdateRawText();
    }

    void ApplyTool(Point pos)
    {
        int c = (int)(pos.X / CellW);
        int r = (int)(pos.Y / CellH);
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;

        var cell = _grid[r, c];
        switch (_tool)
        {
            case DrawTool.Pen:
                cell.Char = _currentChar;
                cell.Fg = _fgColor;
                cell.Bg = _bgColor;
                break;
            case DrawTool.Eraser:
                cell.Char = ' ';
                cell.Bg = Color.FromRgb(0, 0, 0);
                break;
            case DrawTool.Fill:
                FloodFill(r, c, cell.Char, cell.Bg);
                return;
        }

        // 해당 셀만 재렌더링
        // 간단하게 전체 다시 그림 (성능 최적화는 생략)
        DrawGrid();
    }

    void FloodFill(int startR, int startC, char targetChar, Color targetBg)
    {
        if (startR < 0 || startR >= Rows || startC < 0 || startC >= Cols) return;
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startR, startC));
        var visited = new bool[Rows, Cols];
        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            if (r < 0 || r >= Rows || c < 0 || c >= Cols || visited[r, c]) continue;
            var cell = _grid[r, c];
            if (cell.Char != targetChar || cell.Bg != targetBg) continue;
            visited[r, c] = true;
            cell.Char = _currentChar;
            cell.Fg = _fgColor;
            cell.Bg = _bgColor;
            queue.Enqueue((r - 1, c)); queue.Enqueue((r + 1, c));
            queue.Enqueue((r, c - 1)); queue.Enqueue((r, c + 1));
        }
        DrawGrid();
    }

    // ─── 도구 선택 ────────────────────────────────────────────────────
    void DrawTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            _tool = btn.Tag?.ToString() switch { "fill" => DrawTool.Fill, "eraser" => DrawTool.Eraser, _ => DrawTool.Pen };
            BtnPen.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            BtnFill.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            BtnEraser.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            btn.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x4A, 0x80));
        }
    }

    // ─── 색상 선택 ────────────────────────────────────────────────────
    void SetFgColor(Color c) { _fgColor = c; UpdateColorBoxes(); }
    void SetBgColor(Color c) { _bgColor = c; UpdateColorBoxes(); }
    void UpdateColorBoxes()
    {
        FgColorBox.Background = new SolidColorBrush(_fgColor);
        BgColorBox.Background = new SolidColorBrush(_bgColor);
    }
    void FgColorBox_MouseDown(object sender, MouseButtonEventArgs e) => PickColor(true);
    void BgColorBox_MouseDown(object sender, MouseButtonEventArgs e) => PickColor(false);

    void PickColor(bool isFg)
    {
        string defaultVal = isFg ? $"#{_fgColor.R:X2}{_fgColor.G:X2}{_fgColor.B:X2}" : $"#{_bgColor.R:X2}{_bgColor.G:X2}{_bgColor.B:X2}";
        var dlg = new Window
        {
            Title = isFg ? "전경색 선택" : "배경색 선택",
            Width = 300, Height = 130,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, Owner = this
        };
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

        var label = new TextBlock { Text = "HEX 색상 (#RRGGBB):", Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(label);

        var tb = new TextBox
        {
            Text = defaultVal, FontFamily = new FontFamily("Consolas"),
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            Padding = new Thickness(6, 4, 6, 4)
        };
        Grid.SetRow(tb, 2);
        grid.Children.Add(tb);
        dlg.Content = grid;
        dlg.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                try
                {
                    var col = (Color)ColorConverter.ConvertFromString(tb.Text.Trim());
                    if (isFg) SetFgColor(col); else SetBgColor(col);
                }
                catch { }
                dlg.Close();
            }
            else if (ke.Key == Key.Escape) dlg.Close();
        };
        dlg.Loaded += (_, _) => { tb.SelectAll(); tb.Focus(); };
        dlg.ShowDialog();
    }

    // ─── 미리보기 ─────────────────────────────────────────────────────
    void UpdatePreview()
    {
        PreviewPanel.Children.Clear();
        var ansi = GridToAnsi();
        var spans = AnsiParser.Parse(ansi);
        RenderSpansToPanel(spans, PreviewPanel);
    }

    void RenderSpansToPanel(AnsiSpan[] spans, StackPanel panel)
    {
        var lineParagraph = new WrapPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(lineParagraph);

        foreach (var span in spans)
        {
            foreach (char c in span.Text)
            {
                if (c == '\n')
                {
                    lineParagraph = new WrapPanel { Orientation = Orientation.Horizontal };
                    panel.Children.Add(lineParagraph);
                    continue;
                }

                var border = new Border
                {
                    Width = CellW, Height = CellH,
                    Background = new SolidColorBrush(span.Background)
                };
                if (c != ' ')
                {
                    border.Child = new TextBlock
                    {
                        Text = c.ToString(),
                        FontFamily = new FontFamily("Cascadia Code, Consolas"),
                        FontSize = CellH * 0.85,
                        Foreground = new SolidColorBrush(span.Foreground),
                        FontWeight = span.Bold ? FontWeights.Bold : FontWeights.Normal,
                        Width = CellW, Height = CellH,
                        TextAlignment = TextAlignment.Center
                    };
                }
                lineParagraph.Children.Add(border);
            }
        }
    }

    // ─── ANSI 생성 ────────────────────────────────────────────────────
    string GridToAnsi()
    {
        var sb = new StringBuilder();
        for (int r = 0; r < Rows; r++)
        {
            Color? curFg = null, curBg = null;
            for (int c = 0; c < Cols; c++)
            {
                var cell = _grid[r, c];
                var codes = new List<int>();
                if (curFg != cell.Fg)
                {
                    codes.Add(38); codes.Add(2);
                    codes.Add(cell.Fg.R); codes.Add(cell.Fg.G); codes.Add(cell.Fg.B);
                    curFg = cell.Fg;
                }
                if (curBg != cell.Bg)
                {
                    codes.Add(48); codes.Add(2);
                    codes.Add(cell.Bg.R); codes.Add(cell.Bg.G); codes.Add(cell.Bg.B);
                    curBg = cell.Bg;
                }
                if (codes.Count > 0)
                    sb.Append($"\x1b[{string.Join(";", codes)}m");
                sb.Append(cell.Char);
            }
            sb.Append("\x1b[0m\n");
        }
        return sb.ToString();
    }

    // ─── 원시 텍스트 ──────────────────────────────────────────────────
    void UpdateRawText()
    {
        _suppressRawUpdate = true;
        RawTextBox.Text = GridToAnsi();
        _suppressRawUpdate = false;
    }

    void RawTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressRawUpdate || !IsLoaded) return;
        // 원시 텍스트 편집 → 미리보기만 업데이트
        PreviewPanel.Children.Clear();
        var spans = AnsiParser.Parse(RawTextBox.Text);
        RenderSpansToPanel(spans, PreviewPanel);
    }

    // ─── 파일 I/O ─────────────────────────────────────────────────────
    void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ANSI 파일|*.ans;*.asc;*.nfo;*.txt|모든 파일|*.*" };
        if (dlg.ShowDialog() != true) return;
        var raw = File.ReadAllText(dlg.FileName, Encoding.Latin1);
        RawTextBox.Text = raw;
        PreviewPanel.Children.Clear();
        var spans = AnsiParser.Parse(raw);
        RenderSpansToPanel(spans, PreviewPanel);
        StatusBar.Text = $"열기: {dlg.FileName}";
    }

    void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "ANSI 파일|*.ans|텍스트|*.txt", DefaultExt = "ans" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, GridToAnsi(), Encoding.Latin1);
        StatusBar.Text = $"저장: {dlg.FileName}";
    }

    void BtnExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PNG 이미지|*.png", DefaultExt = "png" };
        if (dlg.ShowDialog() != true) return;

        var rtb = new RenderTargetBitmap((int)(Cols * CellW), (int)(Rows * CellH), 96, 96, PixelFormats.Pbgra32);
        var drawingVisual = new DrawingVisual();
        using (var dc = drawingVisual.RenderOpen())
        {
            var vb = new VisualBrush(EditCanvas);
            dc.DrawRectangle(vb, null, new Rect(0, 0, Cols * CellW, Rows * CellH));
        }
        rtb.Render(drawingVisual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(dlg.FileName);
        encoder.Save(fs);
        StatusBar.Text = $"PNG 저장: {dlg.FileName}";
    }

    void BtnExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "HTML|*.html", DefaultExt = "html" };
        if (dlg.ShowDialog() != true) return;
        var spans = AnsiParser.Parse(GridToAnsi());
        File.WriteAllText(dlg.FileName, AnsiParser.ToHtml(spans), Encoding.UTF8);
        StatusBar.Text = $"HTML 저장: {dlg.FileName}";
    }

    void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        InitGrid();
        DrawGrid();
        RawTextBox.Text = "";
        PreviewPanel.Children.Clear();
        StatusBar.Text = "그리드 초기화.";
    }
}
