using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Stock.Fetch.Models;
using Stock.Fetch.Services;

namespace Stock.Fetch;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private readonly PriceSourceRegistry _registry;
    private StockSeries? _series;

    /// <summary>출력 포맷 콤보용 항목.</summary>
    private sealed record FormatItem(string Label, ExportFormat Format)
    {
        public override string ToString() => Label;
    }

    public MainWindow()
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        _config = AppConfig.Load();
        _registry = new PriceSourceRegistry(_config, () => _config.Save());

        // 데이터 소스 콤보 채우기
        // 커스텀 ComboBox 템플릿과 DisplayMemberPath 충돌 → ToString() 오버라이드로 라벨 표시
        SourceCombo.ItemsSource = _registry.All;
        SourceCombo.SelectedItem = _registry.Get(_config.LastSource);
        SourceCombo.SelectionChanged += SourceCombo_SelectionChanged;

        // 출력 포맷 콤보
        FormatCombo.ItemsSource = new[]
        {
            new FormatItem("CSV (쉼표 구분)", ExportFormat.Csv),
            new FormatItem("TSV (탭 구분)", ExportFormat.Tsv),
            new FormatItem("JSON", ExportFormat.Json),
            new FormatItem("XML", ExportFormat.Xml),
            new FormatItem("Markdown 표", ExportFormat.Markdown),
        };

        // 즐겨찾기 콤보
        RefreshFavorites();

        // 마지막 선택값 복원
        ApplyPreset("3M");      // 저장된 기간이 없을 때의 기본값
        RestoreState();

        UpdateSourceNote();
        Closed += (_, _) => { SaveState(); _registry.Dispose(); };
    }

    // ────────────────────────────── 상태 저장/복원 ──────────────────────────────
    private void RestoreState()
    {
        CodeBox.Text = string.IsNullOrWhiteSpace(_config.LastCode) ? "005930" : _config.LastCode;
        NameText.Text = _config.LastName;
        if (TryParseDate(_config.LastFrom, out _)) FromBox.Text = _config.LastFrom;
        if (TryParseDate(_config.LastTo, out _)) ToBox.Text = _config.LastTo;

        SourceCombo.SelectedItem = _registry.Get(_config.LastSource);
        FormatCombo.SelectedItem = FormatCombo.Items.Cast<FormatItem>()
            .FirstOrDefault(f => f.Format == _config.LastFormat) ?? FormatCombo.Items[0];

        // 컬럼 선택(빈 목록이면 전체)
        var cols = _config.LastColumns.Count > 0 ? _config.LastColumns.ToHashSet() : null;
        ColDate.IsChecked = cols?.Contains(CandleColumn.Date) ?? true;
        ColOpen.IsChecked = cols?.Contains(CandleColumn.Open) ?? true;
        ColClose.IsChecked = cols?.Contains(CandleColumn.Close) ?? true;
        ColLow.IsChecked = cols?.Contains(CandleColumn.Low) ?? true;
        ColHigh.IsChecked = cols?.Contains(CandleColumn.High) ?? true;
        ColVolume.IsChecked = cols?.Contains(CandleColumn.Volume) ?? true;
        IncludeHeaderCheck.IsChecked = _config.LastIncludeHeader;
    }

    private void SaveState()
    {
        _config.LastCode = CodeBox.Text.Trim();
        _config.LastName = NameText.Text;
        _config.LastFrom = FromBox.Text.Trim();
        _config.LastTo = ToBox.Text.Trim();
        if (SourceCombo.SelectedItem is IPriceSource src) _config.LastSource = src.Kind;
        if (FormatCombo.SelectedItem is FormatItem fi) _config.LastFormat = fi.Format;
        _config.LastColumns = SelectedColumns();
        _config.LastIncludeHeader = IncludeHeaderCheck.IsChecked == true;
        _config.Save();
    }

    // ────────────────────────────── 기간 프리셋 ──────────────────────────────
    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag) ApplyPreset(tag);
    }

    private void ApplyPreset(string tag)
    {
        var to = DateTime.Today;
        var from = tag switch
        {
            "1M" => to.AddMonths(-1),
            "3M" => to.AddMonths(-3),
            "6M" => to.AddMonths(-6),
            "1Y" => to.AddYears(-1),
            "3Y" => to.AddYears(-3),
            "YTD" => new DateTime(to.Year, 1, 1),
            _ => to.AddMonths(-3)
        };
        FromBox.Text = from.ToString("yyyy-MM-dd");
        ToBox.Text = to.ToString("yyyy-MM-dd");
    }

    /// <summary>현재 입력된 기간(시작·종료) 전체를 ±N일 이동.</summary>
    private void DayShift_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag || !int.TryParse(tag, out int d)) return;
        if (!TryParseDate(FromBox.Text, out var from) || !TryParseDate(ToBox.Text, out var to))
        {
            ShowError("기간은 yyyy-MM-dd 형식으로 입력하세요.");
            return;
        }
        FromBox.Text = from.AddDays(d).ToString("yyyy-MM-dd");
        ToBox.Text = to.AddDays(d).ToString("yyyy-MM-dd");
    }

    // ────────────────────────────── 종목명 조회 ──────────────────────────────
    private async void Lookup_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        if (code.Length is < 5 or > 6 || !code.All(char.IsDigit))
        {
            ShowError("종목코드는 6자리 숫자입니다(예: 005930).");
            return;
        }
        LookupBtn.IsEnabled = false;
        NameText.Text = "조회 중…";
        try
        {
            var name = await _registry.LookupNameAsync(code);
            NameText.Text = string.IsNullOrEmpty(name) ? "(이름 못 찾음)" : name;
        }
        finally
        {
            LookupBtn.IsEnabled = true;
        }
    }

    // ────────────────────────────── 즐겨찾기 ──────────────────────────────
    private void RefreshFavorites()
    {
        FavCombo.ItemsSource = null;
        FavCombo.ItemsSource = _config.Favorites;
    }

    private void Fav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavCombo.SelectedItem is FavoriteStock f)
        {
            CodeBox.Text = f.Code;
            NameText.Text = f.Name;
        }
    }

    private void FavAdd_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        if (code.Length is < 5 or > 6 || !code.All(char.IsDigit))
        {
            ShowError("종목코드는 6자리 숫자입니다(예: 005930).");
            return;
        }
        // 미조회/실패 표시는 이름으로 저장하지 않음
        string name = NameText.Text is "조회 중…" or "(이름 못 찾음)" ? "" : NameText.Text.Trim();

        var existing = _config.Favorites.FirstOrDefault(f => f.Code == code);
        if (existing != null)
        {
            if (!string.IsNullOrEmpty(name)) existing.Name = name;
        }
        else
        {
            _config.Favorites.Add(new FavoriteStock { Code = code, Name = name });
        }
        _config.Save();
        RefreshFavorites();
        FavCombo.SelectedItem = _config.Favorites.FirstOrDefault(f => f.Code == code);
        SummaryText.Text = $"⭐ 즐겨찾기 추가: {code}{(string.IsNullOrEmpty(name) ? "" : "  " + name)}";
    }

    private void FavRemove_Click(object sender, RoutedEventArgs e)
    {
        if (FavCombo.SelectedItem is not FavoriteStock f)
        {
            ShowError("제거할 즐겨찾기를 콤보에서 선택하세요.");
            return;
        }
        _config.Favorites.RemoveAll(x => x.Code == f.Code);
        _config.Save();
        RefreshFavorites();
        SummaryText.Text = $"🗑 즐겨찾기 제거: {f.Code}  {f.Name}";
    }

    // ────────────────────────────── 조회 ──────────────────────────────
    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        string code = CodeBox.Text.Trim();
        if (code.Length is < 5 or > 6 || !code.All(char.IsDigit))
        {
            ShowError("종목코드는 6자리 숫자입니다(예: 005930).");
            return;
        }
        if (!TryParseDate(FromBox.Text, out var from) || !TryParseDate(ToBox.Text, out var to))
        {
            ShowError("기간은 yyyy-MM-dd 형식으로 입력하세요.");
            return;
        }
        if (from > to)
        {
            ShowError("시작일이 종료일보다 늦습니다.");
            return;
        }
        if (SourceCombo.SelectedItem is not IPriceSource source) return;

        if (source.RequiresApiKey && !_config.HasKisCredentials)
        {
            ShowError("KIS 소스는 API 키가 필요합니다. [⚙ KIS 키 설정]에서 입력하세요.");
            return;
        }

        SetBusy(true, $"{source.DisplayName}에서 조회 중…");
        try
        {
            var series = await source.FetchAsync(code, from, to);
            _series = series;
            Grid.ItemsSource = series.Candles;
            SaveBtn.IsEnabled = CopyBtn.IsEnabled = series.Candles.Count > 0;
            UpdateSummary(series);

            // 종목명 자동 표시: 소스가 주면 사용, 아니면 KRX finder로 조회
            if (!string.IsNullOrEmpty(series.Name)) NameText.Text = series.Name;
            else if (string.IsNullOrEmpty(NameText.Text) || NameText.Text.StartsWith('('))
            {
                var nm = await _registry.LookupNameAsync(code);
                if (!string.IsNullOrEmpty(nm)) NameText.Text = nm;
            }

            // 최근 사용값 저장
            _config.LastSource = source.Kind;
            _config.LastCode = code;
            _config.Save();
        }
        catch (PriceSourceException ex)
        {
            _series = null;
            Grid.ItemsSource = null;
            SaveBtn.IsEnabled = CopyBtn.IsEnabled = false;
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _series = null;
            Grid.ItemsSource = null;
            SaveBtn.IsEnabled = CopyBtn.IsEnabled = false;
            ShowError($"조회 중 오류: {ex.Message}");
        }
        finally
        {
            SetBusy(false, null);
        }
    }

    private void UpdateSummary(StockSeries s)
    {
        if (s.Candles.Count == 0) { SummaryText.Text = "데이터가 없습니다."; return; }
        decimal hi = s.Candles.Max(c => c.High);
        decimal lo = s.Candles.Min(c => c.Low);
        string name = string.IsNullOrEmpty(s.Name) ? s.Code : $"{s.Name} ({s.Code})";
        string market = string.IsNullOrEmpty(s.Market) ? "" : $" · {s.Market}";
        SummaryText.Text =
            $"{name}{market}  |  {s.SourceLabel}  |  {s.Candles[0].Date:yyyy-MM-dd} ~ {s.Candles[^1].Date:yyyy-MM-dd}  " +
            $"|  {s.Candles.Count}건  |  기간 고가 {hi:N0} · 저가 {lo:N0}";
    }

    // ────────────────────────────── 내보내기 ──────────────────────────────
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_series is null || FormatCombo.SelectedItem is not FormatItem fi) return;
        var cols = SelectedColumns();
        if (cols.Count == 0) { ShowError("내보낼 컬럼을 1개 이상 선택하세요."); return; }

        var dlg = new SaveFileDialog
        {
            Filter = DataExporter.FilterLabel(fi.Format) + "|모든 파일|*.*",
            FileName = BuildFileName(_series, fi.Format),
            InitialDirectory = Directory.Exists(_config.LastExportDir) ? _config.LastExportDir : null
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            await DataExporter.SaveAsync(_series, fi.Format, cols, IncludeHeaderCheck.IsChecked == true, dlg.FileName);
            _config.LastExportDir = Path.GetDirectoryName(dlg.FileName) ?? "";
            _config.Save();
            SummaryText.Text = $"저장 완료({cols.Count}개 컬럼): {dlg.FileName}";
        }
        catch (Exception ex)
        {
            ShowError($"저장 실패: {ex.Message}");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_series is null || FormatCombo.SelectedItem is not FormatItem fi) return;
        var cols = SelectedColumns();
        if (cols.Count == 0) { ShowError("복사할 컬럼을 1개 이상 선택하세요."); return; }
        try
        {
            Clipboard.SetText(DataExporter.Serialize(_series, fi.Format, cols, IncludeHeaderCheck.IsChecked == true));
            SummaryText.Text = $"클립보드에 복사됨 ({fi.Label}, {cols.Count}개 컬럼 · {_series.Candles.Count}건).";
        }
        catch (Exception ex)
        {
            ShowError($"복사 실패: {ex.Message}");
        }
    }

    /// <summary>체크된 컬럼을 표시 순서(날짜-시가-종가-저가-고가-거래량)대로 반환.</summary>
    private List<CandleColumn> SelectedColumns()
    {
        var list = new List<CandleColumn>();
        if (ColDate.IsChecked == true) list.Add(CandleColumn.Date);
        if (ColOpen.IsChecked == true) list.Add(CandleColumn.Open);
        if (ColClose.IsChecked == true) list.Add(CandleColumn.Close);
        if (ColLow.IsChecked == true) list.Add(CandleColumn.Low);
        if (ColHigh.IsChecked == true) list.Add(CandleColumn.High);
        if (ColVolume.IsChecked == true) list.Add(CandleColumn.Volume);
        return list;
    }

    private void ColAll_Click(object sender, RoutedEventArgs e) => SetAllColumns(true);
    private void ColNone_Click(object sender, RoutedEventArgs e) => SetAllColumns(false);

    private void SetAllColumns(bool on)
    {
        ColDate.IsChecked = ColOpen.IsChecked = ColClose.IsChecked =
            ColLow.IsChecked = ColHigh.IsChecked = ColVolume.IsChecked = on;
    }

    private static string BuildFileName(StockSeries s, ExportFormat fmt)
    {
        string namePart = string.IsNullOrEmpty(s.Name) ? s.Code : $"{s.Code}_{s.Name}";
        foreach (char c in Path.GetInvalidFileNameChars()) namePart = namePart.Replace(c, '_');
        string range = s.Candles.Count > 0
            ? $"_{s.Candles[0].Date:yyyyMMdd}-{s.Candles[^1].Date:yyyyMMdd}"
            : "";
        return $"{namePart}{range}{DataExporter.Extension(fmt)}";
    }

    // ────────────────────────────── 설정 ──────────────────────────────
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_config) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _config.Save();
            UpdateSourceNote();
        }
    }

    private void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSourceNote();

    private void UpdateSourceNote()
    {
        if (SourceCombo.SelectedItem is not IPriceSource src) return;
        SourceNote.Text = src.Kind switch
        {
            SourceKind.Naver => "무인증 · 가장 빠름",
            SourceKind.Yahoo => "무인증 · 글로벌(KST 보정)",
            SourceKind.Daum => "무인증 · KRX 원천 시세",
            SourceKind.Kis => _config.HasKisCredentials ? "API 키 설정됨 · 공식" : "⚠ API 키 필요 (⚙ 설정)",
            _ => ""
        };
    }

    // ────────────────────────────── 헬퍼 ──────────────────────────────
    private static bool TryParseDate(string s, out DateTime dt) =>
        DateTime.TryParseExact(s.Trim(), "yyyy-MM-dd", null,
            System.Globalization.DateTimeStyles.None, out dt);

    private void SetBusy(bool busy, string? message)
    {
        FetchBtn.IsEnabled = !busy;
        FetchBtn.Content = busy ? "조회 중…" : "조회";
        if (message != null) SummaryText.Text = message;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private void ShowError(string message) => SummaryText.Text = "⚠ " + message;
}
