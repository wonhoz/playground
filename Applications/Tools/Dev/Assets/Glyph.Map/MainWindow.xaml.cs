using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GlyphMap.Models;
using GlyphMap.Services;

namespace GlyphMap;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 서비스 ──────────────────────────────────────────────────────────
    private readonly UnicodeDataService _unicode = new();
    private AppSettings _settings = SettingsService.Load();

    // ── 상태 ────────────────────────────────────────────────────────────
    private GlyphEntry? _selected;
    private string _currentBlockName = "";
    private const int Cols = 14; // 격자 열 수

    // ── 검색 디바운스 타이머 ─────────────────────────────────────────────
    private readonly DispatcherTimer _searchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(280)
    };

    public MainWindow()
    {
        InitializeComponent();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); RunSearch(); };
        Loaded += OnWindowLoaded;
    }

    // ──────────────────────────────────────────────────────────────────────
    // 초기 로드
    // ──────────────────────────────────────────────────────────────────────
    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var dark = 1;
        DwmSetWindowAttribute(
            new System.Windows.Interop.WindowInteropHelper(this).Handle,
            20, ref dark, sizeof(int));

        LoadingOverlay.Visibility = Visibility.Visible;
        await _unicode.LoadAsync();

        Dispatcher.Invoke(() =>
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            BuildCategoryTree();

            // 마지막 선택 블록 복원
            var last = _settings.LastBlock;
            if (!string.IsNullOrEmpty(last))
                SelectBlockByName(last);
            else
                ShowBlock(_unicode.Blocks.FirstOrDefault(b => b.Name == "Basic Latin").Name ?? "Basic Latin");
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // 카테고리 트리 빌드
    // ──────────────────────────────────────────────────────────────────────
    private void BuildCategoryTree()
    {
        CategoryTree.Items.Clear();

        var groups = _unicode.Blocks
            .GroupBy(b => UnicodeDataService.GetCategoryGroup(b.Name))
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            var parent = new TreeViewItem
            {
                Header     = group.Key,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)FindResource("AccentGreen"),
                IsExpanded = group.Key is "기본 라틴" or "동아시아" or "이모지 & 기호"
            };

            foreach (var block in group.OrderBy(b => b.Start))
            {
                var count = _unicode.GetByBlock(block.Name).Count;
                if (count == 0) continue;

                var child = new TreeViewItem
                {
                    Header = $"{block.Name}  ({count:N0})",
                    Tag    = block.Name,
                    FontWeight = FontWeights.Normal,
                    Foreground = (SolidColorBrush)FindResource("TextMain")
                };
                parent.Items.Add(child);
            }

            if (parent.Items.Count > 0)
                CategoryTree.Items.Add(parent);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // 블록명으로 트리 항목 선택
    // ──────────────────────────────────────────────────────────────────────
    private void SelectBlockByName(string blockName)
    {
        foreach (TreeViewItem parent in CategoryTree.Items)
        {
            foreach (TreeViewItem child in parent.Items)
            {
                if (child.Tag is string tag && tag == blockName)
                {
                    parent.IsExpanded = true;
                    child.IsSelected  = true;
                    child.BringIntoView();
                    return;
                }
            }
        }
        ShowBlock(blockName);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 블록 표시
    // ──────────────────────────────────────────────────────────────────────
    private void ShowBlock(string blockName)
    {
        _currentBlockName = blockName;
        var glyphs = _unicode.GetByBlock(blockName);
        ShowGlyphs(glyphs, blockName, $"{glyphs.Count:N0}자");
        _settings.LastBlock = blockName;
        SettingsService.Save(_settings);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 격자에 GlyphEntry 목록 표시 (행 단위 가상화)
    // ──────────────────────────────────────────────────────────────────────
    private void ShowGlyphs(IReadOnlyList<GlyphEntry> glyphs, string title, string countLabel)
    {
        TxtCurrentBlock.Text = title;
        TxtGlyphCount.Text   = $"  {countLabel}";

        // Cols(14)개씩 행으로 나눠 WrapPanel 행으로 묶음
        var rows = new List<GlyphRow>();
        for (int i = 0; i < glyphs.Count; i += Cols)
        {
            var rowItems = glyphs.Skip(i).Take(Cols).ToList();
            rows.Add(new GlyphRow(rowItems));
        }

        GlyphGrid.ItemsSource  = rows;
        GlyphGrid.ItemTemplate = BuildRowTemplate();
        GlyphGrid.ScrollIntoView(GlyphGrid.Items.Count > 0 ? GlyphGrid.Items[0] : null!);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 행 DataTemplate 동적 생성
    // ──────────────────────────────────────────────────────────────────────
    private DataTemplate BuildRowTemplate()
    {
        var dt = new DataTemplate(typeof(GlyphRow));
        var spFactory = new FrameworkElementFactory(typeof(StackPanel));
        spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        spFactory.SetValue(StackPanel.MarginProperty, new Thickness(4, 2, 4, 2));

        // 실제 아이템은 코드비하인드에서 ItemsControl로 처리
        // → 간단히 ItemsControl 사용
        var icFactory = new FrameworkElementFactory(typeof(ItemsControl));
        icFactory.SetBinding(ItemsControl.ItemsSourceProperty,
            new System.Windows.Data.Binding("Items"));
        icFactory.SetValue(ItemsControl.HorizontalAlignmentProperty, HorizontalAlignment.Left);

        var panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
        panelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
        icFactory.SetValue(ItemsControl.ItemsPanelProperty,
            new ItemsPanelTemplate(panelFactory));

        // 각 셀 DataTemplate
        var cellDt = new DataTemplate(typeof(GlyphEntry));
        var cellFactory = new FrameworkElementFactory(typeof(Border));
        cellFactory.SetValue(Border.WidthProperty, 48.0);
        cellFactory.SetValue(Border.HeightProperty, 48.0);
        cellFactory.SetValue(Border.MarginProperty, new Thickness(1));
        cellFactory.SetValue(Border.CursorProperty, Cursors.Hand);
        cellFactory.SetValue(Border.ToolTipProperty, new System.Windows.Data.Binding("Name"));
        cellFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        cellFactory.SetValue(Border.BackgroundProperty, (SolidColorBrush)FindResource("BgSurface"));

        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Char"));
        tbFactory.SetValue(TextBlock.FontSizeProperty, 22.0);
        tbFactory.SetValue(TextBlock.FontFamilyProperty,
            new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Segoe UI"));
        tbFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        tbFactory.SetValue(TextBlock.ForegroundProperty, (SolidColorBrush)FindResource("TextMain"));

        cellFactory.AppendChild(tbFactory);
        cellDt.VisualTree = cellFactory;
        cellDt.Seal();

        icFactory.SetValue(ItemsControl.ItemTemplateProperty, cellDt);

        // 마우스 이벤트 → 셀 선택
        icFactory.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(GlyphCell_Click));

        spFactory.AppendChild(icFactory);
        dt.VisualTree = spFactory;
        dt.Seal();
        return dt;
    }

    // ──────────────────────────────────────────────────────────────────────
    // 격자 셀 클릭
    // ──────────────────────────────────────────────────────────────────────
    private void GlyphCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBlock tb &&
            tb.DataContext is GlyphEntry entry)
        {
            SelectGlyph(entry);
        }
    }

    private void GlyphGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    // ──────────────────────────────────────────────────────────────────────
    // 문자 선택 → 상세 패널 갱신
    // ──────────────────────────────────────────────────────────────────────
    private void SelectGlyph(GlyphEntry entry)
    {
        _selected = entry;
        _settings.AddRecent(entry.CodePoint);
        SettingsService.Save(_settings);

        TxtBigChar.Text    = entry.IsRenderable ? entry.Char : "?";
        TxtCodePoint.Text  = entry.CodePointHex;
        TxtGlyphName.Text  = entry.Name;
        TxtGlyphBlock.Text = $"{entry.Block}  ·  {entry.CategoryLabel}";

        BtnFav.Content = _settings.IsFavorite(entry.CodePoint) ? "★" : "☆";

        RefreshCopyPreview();
        RefreshFormatTable(entry);
    }

    private void RefreshCopyPreview()
    {
        if (_selected == null) return;
        TxtCopyPreview.Text = GetCopyText(_selected);
    }

    private string GetCopyText(GlyphEntry g) =>
        RbChar.IsChecked  == true ? g.Char       :
        RbHex.IsChecked   == true ? g.CodePointHex :
        RbHtml.IsChecked  == true ? g.HtmlEntity :
        RbCs.IsChecked    == true ? g.CsEscape   :
        RbCss.IsChecked   == true ? g.CssContent :
        RbUrl.IsChecked   == true ? g.UrlEncoded :
        g.Char;

    private void RefreshFormatTable(GlyphEntry g)
    {
        FormatTable.ItemsSource = new[]
        {
            new FormatRow("문자",    g.Char),
            new FormatRow("U+",      g.CodePointHex),
            new FormatRow("HTML",    g.HtmlEntity),
            new FormatRow("C#/Java", g.CsEscape),
            new FormatRow("CSS",     g.CssContent),
            new FormatRow("URL",     g.UrlEncoded),
            new FormatRow("카테고리",g.CategoryLabel),
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // 복사 형식 라디오 변경 시 미리보기 갱신
    // ──────────────────────────────────────────────────────────────────────
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        foreach (var rb in new[] { RbChar, RbHex, RbHtml, RbCs, RbCss, RbUrl })
            rb.Checked += (_, _) => RefreshCopyPreview();
    }

    // ──────────────────────────────────────────────────────────────────────
    // 복사 버튼
    // ──────────────────────────────────────────────────────────────────────
    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        CopyToClipboard(GetCopyText(_selected));
        FlashCopyButton();
    }

    private void FormatValue_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.DataContext is FormatRow row)
            CopyToClipboard(row.Value);
    }

    private static void CopyToClipboard(string text)
    {
        try { Clipboard.SetText(text); } catch { }
    }

    private async void FlashCopyButton()
    {
        BtnCopy.Content = "✓  복사됨";
        await Task.Delay(1000);
        BtnCopy.Content = "📋  복사";
    }

    // ──────────────────────────────────────────────────────────────────────
    // 즐겨찾기 버튼
    // ──────────────────────────────────────────────────────────────────────
    private void BtnFav_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        bool added = _settings.ToggleFavorite(_selected.CodePoint);
        BtnFav.Content = _settings.IsFavorite(_selected.CodePoint) ? "★" : "☆";
        if (!added && !_settings.IsFavorite(_selected.CodePoint))
        {
            // 한도 초과 시 메시지 (이미 즐겨찾기 가득 찬 경우)
            if (_settings.Favorites.Count >= AppSettings.MaxFavoritesFree)
                MessageBox.Show(
                    $"무료 버전은 즐겨찾기를 최대 {AppSettings.MaxFavoritesFree}개까지 저장할 수 있습니다.",
                    "즐겨찾기 한도", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        SettingsService.Save(_settings);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 즐겨찾기 보기
    // ──────────────────────────────────────────────────────────────────────
    private void BtnFavorites_Click(object sender, RoutedEventArgs e)
    {
        var glyphs = _settings.Favorites
            .Select(cp => _unicode.GetByCodePoint(cp))
            .OfType<GlyphEntry>()
            .ToList();
        ShowGlyphs(glyphs, "★ 즐겨찾기", $"{glyphs.Count}자");
        ClearTreeSelection();
    }

    // ──────────────────────────────────────────────────────────────────────
    // 최근 사용 보기
    // ──────────────────────────────────────────────────────────────────────
    private void BtnRecent_Click(object sender, RoutedEventArgs e)
    {
        var glyphs = _settings.Recent
            .Select(cp => _unicode.GetByCodePoint(cp))
            .OfType<GlyphEntry>()
            .ToList();
        ShowGlyphs(glyphs, "🕐 최근 사용", $"{glyphs.Count}자");
        ClearTreeSelection();
    }

    private void ClearTreeSelection()
    {
        foreach (TreeViewItem parent in CategoryTree.Items)
        {
            parent.IsSelected = false;
            foreach (TreeViewItem child in parent.Items)
                child.IsSelected = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // 카테고리 트리 선택
    // ──────────────────────────────────────────────────────────────────────
    private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem { Tag: string blockName })
            ShowBlock(blockName);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 검색
    // ──────────────────────────────────────────────────────────────────────
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        _searchTimer.Stop();
        if (string.IsNullOrWhiteSpace(TxtSearch.Text))
        {
            // 검색 지우면 마지막 블록 복원
            if (!string.IsNullOrEmpty(_currentBlockName))
                ShowBlock(_currentBlockName);
            return;
        }
        _searchTimer.Start();
    }

    private void RunSearch()
    {
        var query = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        var results = _unicode.Search(query, 500).ToList();
        ShowGlyphs(results, $"🔍 \"{query}\" 검색 결과", $"{results.Count}자");
    }

    // ──────────────────────────────────────────────────────────────────────
    // 격자 스크롤
    // ──────────────────────────────────────────────────────────────────────
    private void GridScroll_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }
}

// ── 보조 레코드 ─────────────────────────────────────────────────────────────
public sealed class GlyphRow
{
    public List<GlyphEntry> Items { get; }
    public GlyphRow(List<GlyphEntry> items) => Items = items;
}

public sealed record FormatRow(string Label, string Value);
