using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IconMaker.Services;
using SkiaSharp;
using Svg.Skia;

namespace IconMaker;

enum Tool { Pencil, Eraser, Fill }

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    static readonly int[] Sizes = [16, 32, 48, 64, 128, 256];
    readonly SKBitmap?[] _bitmaps = new SKBitmap?[6];

    int _editIdx = 0;
    Tool _activeTool = Tool.Pencil;
    SKColor _drawColor = SKColors.White;
    bool _painting = false;
    bool _darkBg = true;

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
        RefreshEditor();
        RefreshPreviews();
    }

    // ─── 파일 열기 ──────────────────────────────────────────────────────────
    void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "이미지 파일|*.svg;*.png;*.jpg;*.jpeg;*.bmp;*.ico|모든 파일|*.*",
            Title = "이미지 파일 열기"
        };
        if (dlg.ShowDialog() != true) return;
        LoadFile(dlg.FileName);
    }

    void Window_DragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            LoadFile(files[0]);
    }

    void LoadFile(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ico") LoadIco(path);
            else if (ext == ".svg") LoadSvg(path);
            else LoadBitmap(path);

            SourceLabel.Text = Path.GetFileName(path);
            BtnExportIco.IsEnabled = true;
            BtnExportPng.IsEnabled = true;
            RefreshEditor();
            RefreshPreviews();
            StatusBar.Text = $"불러옴: {path}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 불러오기 실패: {ex.Message}", "Icon.Maker", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void LoadSvg(string path)
    {
        var svg = new SKSvg();
        svg.Load(path);
        if (svg.Picture is null) throw new InvalidOperationException("SVG 파싱 실패");

        for (int i = 0; i < Sizes.Length; i++)
        {
            int sz = Sizes[i];
            var bmp = new SKBitmap(sz, sz);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Transparent);
            float sx = sz / svg.Picture.CullRect.Width;
            float sy = sz / svg.Picture.CullRect.Height;
            canvas.Scale(sx, sy);
            canvas.DrawPicture(svg.Picture);
            _bitmaps[i]?.Dispose();
            _bitmaps[i] = bmp;
        }
        SourcePreview.Source = SkiaToBitmapSource(_bitmaps[5]!);
    }

    void LoadBitmap(string path)
    {
        using var original = SKBitmap.Decode(path);
        if (original is null) throw new InvalidDataException("이미지 디코드 실패");
        for (int i = 0; i < Sizes.Length; i++)
        {
            _bitmaps[i]?.Dispose();
            _bitmaps[i] = original.Resize(new SKImageInfo(Sizes[i], Sizes[i]), SKFilterQuality.High);
        }
        SourcePreview.Source = SkiaToBitmapSource(original);
    }

    void LoadIco(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);
        br.ReadUInt16(); br.ReadUInt16();
        int count = br.ReadUInt16();

        var entries = new (int w, uint size, uint offset)[count];
        for (int i = 0; i < count; i++)
        {
            int w = br.ReadByte(); if (w == 0) w = 256;
            br.ReadByte(); br.ReadByte(); br.ReadByte(); br.ReadUInt16(); br.ReadUInt16();
            uint sz = br.ReadUInt32();
            uint off = br.ReadUInt32();
            entries[i] = (w, sz, off);
        }

        for (int i = 0; i < _bitmaps.Length; i++) { _bitmaps[i]?.Dispose(); _bitmaps[i] = null; }

        foreach (var (w, sz, off) in entries)
        {
            fs.Seek(off, SeekOrigin.Begin);
            byte[] data = br.ReadBytes((int)sz);
            using var decoded = SKBitmap.Decode(data);
            if (decoded is null) continue;
            int best = FindBestSlot(w);
            _bitmaps[best]?.Dispose();
            _bitmaps[best] = decoded.Resize(new SKImageInfo(Sizes[best], Sizes[best]), SKFilterQuality.High);
        }

        var source = _bitmaps.LastOrDefault(b => b is not null);
        if (source is null) return;
        for (int i = 0; i < _bitmaps.Length; i++)
        {
            if (_bitmaps[i] is null)
                _bitmaps[i] = source.Resize(new SKImageInfo(Sizes[i], Sizes[i]), SKFilterQuality.High);
        }
        SourcePreview.Source = SkiaToBitmapSource(_bitmaps[5]!);
    }

    static int FindBestSlot(int w)
    {
        int best = 0, bestDiff = int.MaxValue;
        for (int i = 0; i < Sizes.Length; i++)
        {
            int diff = Math.Abs(Sizes[i] - w);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    // ─── 내보내기 ────────────────────────────────────────────────────────────
    void BtnExportIco_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "ICO 파일|*.ico", DefaultExt = "ico" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var images = new List<(int, byte[])>();
            for (int i = 0; i < Sizes.Length; i++)
                images.Add((Sizes[i], BitmapToPng(_bitmaps[i] ?? CreateEmpty(Sizes[i]))));
            File.WriteAllBytes(dlg.FileName, IcoEncoder.Encode(images));
            StatusBar.Text = $"ICO 저장됨: {dlg.FileName}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "저장 오류"); }
    }

    void BtnExportPng_Click(object sender, RoutedEventArgs e)
    {
        var bmp = _bitmaps[_editIdx];
        if (bmp is null) { StatusBar.Text = "편집 중인 이미지가 없습니다."; return; }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG 파일|*.png",
            FileName = $"icon_{Sizes[_editIdx]}x{Sizes[_editIdx]}.png"
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllBytes(dlg.FileName, BitmapToPng(bmp));
        StatusBar.Text = $"PNG 저장됨: {dlg.FileName}";
    }

    // ─── 픽셀 에디터 ─────────────────────────────────────────────────────────
    void SizeSelected(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string tagStr)
        {
            _editIdx = int.Parse(tagStr);
            EditorTitle.Text = $"픽셀 에디터 — {Sizes[_editIdx]}×{Sizes[_editIdx]}";
            RefreshEditor();
        }
    }

    void ToolChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _activeTool = sender == ToolPencil ? Tool.Pencil
                    : sender == ToolEraser ? Tool.Eraser
                    : Tool.Fill;
    }

    void BgChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _darkBg = RbDark.IsChecked == true;
        RefreshPreviews();
    }

    void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        // Hex 색상 입력 다이얼로그
        string current = $"#{_drawColor.Red:X2}{_drawColor.Green:X2}{_drawColor.Blue:X2}";
        string? input = ShowHexInput("색상 코드 입력 (예: #FF5733 또는 FF5733)", current);
        if (input is null) return;

        input = input.Trim().TrimStart('#');
        if (input.Length == 6 && uint.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
        {
            byte r = (byte)(rgb >> 16), g = (byte)(rgb >> 8), b = (byte)(rgb & 0xFF);
            _drawColor = new SKColor(r, g, b);
            UpdateColorUI();
        }
    }

    static string? ShowHexInput(string prompt, string defaultValue)
    {
        var win = new Window
        {
            Title = "색상 선택",
            Width = 340, Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
            Foreground = Brushes.White
        };
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = prompt, TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.LightGray, Margin = new Thickness(0, 0, 0, 8)
        });
        var tb = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x38)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
            Margin = new Thickness(0, 0, 0, 12),
            FontFamily = new FontFamily("Consolas"), Padding = new Thickness(4)
        };
        sp.Children.Add(tb);
        var btnRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnOk = new System.Windows.Controls.Button { Content = "확인", Width = 72, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
        var btnCancel = new System.Windows.Controls.Button { Content = "취소", Width = 72, Height = 28 };
        btnRow.Children.Add(btnOk); btnRow.Children.Add(btnCancel);
        sp.Children.Add(btnRow);
        win.Content = sp;

        string? result = null;
        btnOk.Click += (_, _) => { result = tb.Text; win.DialogResult = true; };
        btnCancel.Click += (_, _) => { win.DialogResult = false; };
        win.ShowDialog();
        return result;
    }

    void UpdateColorUI()
    {
        ColorSwatch.Background = new SolidColorBrush(
            Color.FromArgb(_drawColor.Alpha, _drawColor.Red, _drawColor.Green, _drawColor.Blue));
        ColorHex.Text = $"#{_drawColor.Red:X2}{_drawColor.Green:X2}{_drawColor.Blue:X2}";
    }

    int GetZoom() => Sizes[_editIdx] switch
    {
        16 => 22, 32 => 14, 48 => 10, 64 => 8, 128 => 4, _ => 2
    };

    void Editor_MouseDown(object sender, MouseButtonEventArgs e)
    {
        EnsureBitmap();
        _painting = true;
        ApplyTool(e.GetPosition(EditorOverlay));
    }

    void Editor_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(EditorOverlay);
        int zoom = GetZoom();
        int px = (int)(pos.X / zoom);
        int py = (int)(pos.Y / zoom);
        PosLabel.Text = $"({px}, {py})";
        if (_painting && e.LeftButton == MouseButtonState.Pressed)
            ApplyTool(pos);
    }

    void Editor_MouseUp(object sender, MouseButtonEventArgs e) => _painting = false;

    void Editor_RightDown(object sender, MouseButtonEventArgs e)
    {
        // 우클릭 = 색상 스포이드
        var bmp = _bitmaps[_editIdx];
        if (bmp is null) return;
        var pos = e.GetPosition(EditorOverlay);
        int zoom = GetZoom();
        int px = (int)(pos.X / zoom), py = (int)(pos.Y / zoom);
        if (px < 0 || py < 0 || px >= bmp.Width || py >= bmp.Height) return;
        _drawColor = bmp.GetPixel(px, py);
        UpdateColorUI();
    }

    void ApplyTool(Point pos)
    {
        var bmp = _bitmaps[_editIdx];
        if (bmp is null) return;
        int zoom = GetZoom();
        int px = (int)(pos.X / zoom), py = (int)(pos.Y / zoom);
        if (px < 0 || py < 0 || px >= bmp.Width || py >= bmp.Height) return;

        if (_activeTool == Tool.Fill)
        {
            FloodFill(bmp, px, py, _drawColor);
            _painting = false;
        }
        else
            bmp.SetPixel(px, py, _activeTool == Tool.Eraser ? SKColors.Transparent : _drawColor);

        RefreshEditor();
        RefreshPreviews();
    }

    static void FloodFill(SKBitmap bmp, int x, int y, SKColor fillColor)
    {
        SKColor target = bmp.GetPixel(x, y);
        if (target == fillColor) return;
        var queue = new Queue<(int, int)>();
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (cx < 0 || cy < 0 || cx >= bmp.Width || cy >= bmp.Height) continue;
            if (bmp.GetPixel(cx, cy) != target) continue;
            bmp.SetPixel(cx, cy, fillColor);
            queue.Enqueue((cx + 1, cy)); queue.Enqueue((cx - 1, cy));
            queue.Enqueue((cx, cy + 1)); queue.Enqueue((cx, cy - 1));
        }
    }

    void EnsureBitmap()
    {
        if (_bitmaps[_editIdx] is null)
            _bitmaps[_editIdx] = CreateEmpty(Sizes[_editIdx]);
    }

    static SKBitmap CreateEmpty(int sz)
    {
        var bmp = new SKBitmap(sz, sz);
        bmp.Erase(SKColors.Transparent);
        return bmp;
    }

    // ─── 렌더링 ──────────────────────────────────────────────────────────────
    void RefreshEditor()
    {
        var bmp = _bitmaps[_editIdx] ?? CreateEmpty(Sizes[_editIdx]);
        int zoom = GetZoom();
        int cw = bmp.Width * zoom, ch = bmp.Height * zoom;

        var wb = new WriteableBitmap(cw, ch, 96, 96, PixelFormats.Bgra32, null);
        wb.Lock();
        unsafe
        {
            byte* dst = (byte*)wb.BackBuffer;
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    for (int dy = 0; dy < zoom; dy++)
                    {
                        for (int dx = 0; dx < zoom; dx++)
                        {
                            int idx = ((y * zoom + dy) * cw + (x * zoom + dx)) * 4;
                            bool isGrid = dx == 0 || dy == 0;
                            if (c.Alpha == 0)
                            {
                                bool cb = ((x + (dx < zoom / 2 ? 0 : 1) + y + (dy < zoom / 2 ? 0 : 1)) & 1) == 0;
                                byte cv = (byte)(cb ? (isGrid ? 0x99 : 0xBB) : (isGrid ? 0x55 : 0x77));
                                dst[idx] = cv; dst[idx + 1] = cv; dst[idx + 2] = cv; dst[idx + 3] = 0xFF;
                            }
                            else
                            {
                                byte r = c.Red, g = c.Green, b = c.Blue;
                                if (isGrid) { r = (byte)(r * 0x80 / 0xFF); g = (byte)(g * 0x80 / 0xFF); b = (byte)(b * 0x80 / 0xFF); }
                                dst[idx] = b; dst[idx + 1] = g; dst[idx + 2] = r; dst[idx + 3] = 0xFF;
                            }
                        }
                    }
                }
            }
        }
        wb.AddDirtyRect(new Int32Rect(0, 0, cw, ch));
        wb.Unlock();

        EditorImage.Source = wb;
        EditorImage.Width = cw;
        EditorImage.Height = ch;
        EditorOverlay.Width = cw;
        EditorOverlay.Height = ch;
    }

    void RefreshPreviews()
    {
        PreviewPanel.Children.Clear();
        var bgColor = _darkBg ? Color.FromRgb(0x11, 0x11, 0x22) : Color.FromRgb(0xF0, 0xF0, 0xF0);

        for (int i = Sizes.Length - 1; i >= 0; i--)
        {
            var bmp = _bitmaps[i];
            if (bmp is null) continue;
            int displaySz = Math.Min(Sizes[i], 96);

            var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = $"{Sizes[i]}×{Sizes[i]}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xAA)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var border = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(bgColor),
                Width = displaySz, Height = displaySz,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var img = new System.Windows.Controls.Image
            {
                Source = SkiaToBitmapSource(bmp),
                Width = displaySz, Height = displaySz
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            border.Child = img;
            sp.Children.Add(border);
            PreviewPanel.Children.Add(sp);
        }
    }

    // ─── 유틸 ────────────────────────────────────────────────────────────────
    static BitmapSource SkiaToBitmapSource(SKBitmap bmp)
    {
        var info = bmp.Info;
        var wb = new WriteableBitmap(info.Width, info.Height, 96, 96, PixelFormats.Bgra32, null);
        wb.Lock();
        unsafe
        {
            byte* src = (byte*)bmp.GetPixels();
            byte* dst = (byte*)wb.BackBuffer;
            int pixels = info.Width * info.Height;
            for (int i = 0; i < pixels; i++, src += 4, dst += 4)
            { dst[0] = src[2]; dst[1] = src[1]; dst[2] = src[0]; dst[3] = src[3]; }
        }
        wb.AddDirtyRect(new Int32Rect(0, 0, info.Width, info.Height));
        wb.Unlock();
        return wb;
    }

    static byte[] BitmapToPng(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
