using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BadgeForge.Services;
using SkiaSharp;
using SysIO = System.IO;

namespace BadgeForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    string _currentSvg = "";
    List<BadgeConfig> _favorites = [];
    readonly string _favPath = SysIO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BadgeForge", "favorites.json");

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        BuildPalette();
        LoadFavorites();
        UpdatePreview();
    }

    // ─── 팔레트 ───────────────────────────────────────────────────────────
    void BuildPalette()
    {
        foreach (var (name, hex) in SvgBadgeBuilder.Palette)
        {
            string color = "#" + (hex.Length == 3
                ? string.Concat(hex.Select(c => $"{c}{c}"))
                : hex);
            var btn = new Button
            {
                Width = 20, Height = 20,
                Margin = new Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderThickness = new Thickness(0),
                ToolTip = $"{name} (#{hex})",
                Padding = new Thickness(0),
            };
            string capturedHex = hex;
            btn.Click += (_, _) =>
            {
                ValueColorBox.Text = capturedHex;
                UpdatePreview();
            };
            PalettePanel.Children.Add(btn);
        }
    }

    // ─── 미리보기 ─────────────────────────────────────────────────────────
    void Preview_Changed(object sender, EventArgs e)
    {
        if (!IsLoaded) return;
        UpdatePreview();
    }

    void UpdatePreview()
    {
        try
        {
            var cfg = BuildConfig();
            _currentSvg = SvgBadgeBuilder.Build(cfg);
            SvgCodeBox.Text = _currentSvg;

            // 색상 스와치 업데이트
            UpdateSwatch(LabelColorSwatch, LabelColorBox.Text.Trim());
            UpdateSwatch(ValueColorSwatch, ValueColorBox.Text.Trim());

            // WebBrowser로 SVG 렌더링
            string html = $"""
                <html><body style="margin:0;padding:0;background:transparent">
                {_currentSvg}
                </body></html>
                """;
            PreviewBrowser.NavigateToString(html);
            PreviewBrowserDark.NavigateToString(html);

            StatusBar.Text = $"배지 생성됨 | SVG 크기: {_currentSvg.Length}자";
        }
        catch (Exception ex)
        {
            StatusBar.Text = $"오류: {ex.Message}";
        }
    }

    void UpdateSwatch(Border swatch, string hex)
    {
        try
        {
            string h = hex.TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
            if (h.Length == 6)
                swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + h));
        }
        catch { }
    }

    BadgeConfig BuildConfig()
    {
        string styleStr = (StyleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "flat";
        var style = styleStr switch
        {
            "flat-square" => BadgeStyle.FlatSquare,
            "for-the-badge" => BadgeStyle.ForTheBadge,
            "plastic" => BadgeStyle.Plastic,
            "social" => BadgeStyle.Social,
            _ => BadgeStyle.Flat
        };
        return new BadgeConfig(
            LabelBox.Text.Trim(),
            ValueBox.Text.Trim(),
            LabelColorBox.Text.Trim().TrimStart('#'),
            ValueColorBox.Text.Trim().TrimStart('#'),
            style
        );
    }

    // ─── 내보내기 ─────────────────────────────────────────────────────────
    void BtnCopySvg_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentSvg)) return;
        Clipboard.SetText(_currentSvg);
        StatusBar.Text = "SVG 코드가 클립보드에 복사되었습니다.";
    }

    void BtnCopyMd_Click(object sender, RoutedEventArgs e)
    {
        string label = LabelBox.Text.Trim();
        string value = ValueBox.Text.Trim();
        string md = $"![{label}-{value}](data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(_currentSvg))})";
        Clipboard.SetText(md);
        StatusBar.Text = "Markdown 코드가 클립보드에 복사되었습니다.";
    }

    void BtnSavePng_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentSvg)) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG 파일|*.png",
            FileName = $"badge_{LabelBox.Text}_{ValueBox.Text}.png"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            // SVG를 SkiaSharp으로 PNG 변환 (간단한 텍스트 렌더링)
            ExportPng(dlg.FileName, 2);
            StatusBar.Text = $"PNG 저장 완료: {dlg.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PNG 저장 실패: {ex.Message}", "Badge.Forge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ExportPng(string path, int scale)
    {
        // SVG 크기 파싱
        int svgW = 200, svgH = 20;
        var wMatch = System.Text.RegularExpressions.Regex.Match(_currentSvg, @"width=""(\d+)""");
        var hMatch = System.Text.RegularExpressions.Regex.Match(_currentSvg, @"height=""(\d+)""");
        if (wMatch.Success) svgW = int.Parse(wMatch.Groups[1].Value);
        if (hMatch.Success) svgH = int.Parse(hMatch.Groups[1].Value);

        int pw = svgW * scale, ph = svgH * scale;
        using var bmp = new SKBitmap(pw, ph);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);

        // 레이블/값 색상 파싱
        string lhex = LabelColorBox.Text.Trim().TrimStart('#');
        string vhex = ValueColorBox.Text.Trim().TrimStart('#');
        if (lhex.Length == 3) lhex = string.Concat(lhex.Select(c => $"{c}{c}"));
        if (vhex.Length == 3) vhex = string.Concat(vhex.Select(c => $"{c}{c}"));

        using var lPaint = new SKPaint { Color = SKColor.Parse("#" + lhex), IsAntialias = true };
        using var vPaint = new SKPaint { Color = SKColor.Parse("#" + vhex), IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true, TextSize = 11 * scale };

        float lw = svgW / 2f * scale;
        canvas.DrawRect(0, 0, lw, ph, lPaint);
        canvas.DrawRect(lw, 0, pw - lw, ph, vPaint);
        canvas.DrawText(LabelBox.Text, 10 * scale, ph * 0.67f, textPaint);
        canvas.DrawText(ValueBox.Text, lw + 10 * scale, ph * 0.67f, textPaint);

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        SysIO.File.WriteAllBytes(path, data.ToArray());
    }

    // ─── 즐겨찾기 ─────────────────────────────────────────────────────────
    void BtnFavorite_Click(object sender, RoutedEventArgs e)
    {
        _favorites.Add(BuildConfig());
        SaveFavorites();
        RefreshFavoritePanel();
        StatusBar.Text = "즐겨찾기에 저장되었습니다.";
    }

    void SaveFavorites()
    {
        SysIO.Directory.CreateDirectory(SysIO.Path.GetDirectoryName(_favPath)!);
        SysIO.File.WriteAllText(_favPath, JsonSerializer.Serialize(_favorites));
    }

    void LoadFavorites()
    {
        if (!SysIO.File.Exists(_favPath)) return;
        try
        {
            _favorites = JsonSerializer.Deserialize<List<BadgeConfig>>(SysIO.File.ReadAllText(_favPath)) ?? [];
            RefreshFavoritePanel();
        }
        catch { }
    }

    void RefreshFavoritePanel()
    {
        FavoritePanel.Children.Clear();
        foreach (var fav in _favorites)
        {
            string svg = SvgBadgeBuilder.Build(fav);
            var btn = new Button
            {
                Content = $"{fav.Label}: {fav.Value}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(2),
                Padding = new Thickness(6, 3, 6, 3),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
                ToolTip = $"클릭하면 이 배지를 로드합니다.",
            };
            var capturedFav = fav;
            btn.Click += (_, _) => LoadFav(capturedFav);
            FavoritePanel.Children.Add(btn);
        }
    }

    void LoadFav(BadgeConfig fav)
    {
        LabelBox.Text = fav.Label;
        ValueBox.Text = fav.Value;
        LabelColorBox.Text = fav.LabelColor;
        ValueColorBox.Text = fav.ValueColor;
        string styleName = fav.Style switch
        {
            BadgeStyle.FlatSquare => "flat-square",
            BadgeStyle.ForTheBadge => "for-the-badge",
            BadgeStyle.Plastic => "plastic",
            BadgeStyle.Social => "social",
            _ => "flat"
        };
        foreach (ComboBoxItem item in StyleCombo.Items)
            if (item.Content?.ToString() == styleName) { item.IsSelected = true; break; }
        UpdatePreview();
    }

    // ─── URL 가져오기 ─────────────────────────────────────────────────────
    void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        string url = ImportUrlBox.Text.Trim();
        // 예: https://img.shields.io/badge/build-passing-brightgreen
        var m = System.Text.RegularExpressions.Regex.Match(url, @"badge/([^/\?]+)-([^/\?]+)-([^/\?&]+)");
        if (m.Success)
        {
            LabelBox.Text = m.Groups[1].Value.Replace("_", " ").Replace("%20", " ");
            ValueBox.Text = m.Groups[2].Value.Replace("_", " ").Replace("%20", " ");
            var palette = SvgBadgeBuilder.Palette.FirstOrDefault(p => p.Name == m.Groups[3].Value);
            ValueColorBox.Text = palette.Hex ?? m.Groups[3].Value;
            UpdatePreview();
            StatusBar.Text = $"URL에서 배지를 가져왔습니다.";
        }
        else
        {
            StatusBar.Text = "올바른 shields.io URL 형식이 아닙니다.";
        }
    }
}
