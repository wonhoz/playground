using System.Windows;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>
/// 분봉 시그널 백테스트 결과 분석 다이얼로그 — 등급·타임프레임·종목 필터와
/// 중요도/날짜 정렬로 여러 파일의 시그널을 한 화면에서 훑는다.
/// </summary>
public partial class SignalResultWindow : Window
{
    /// <summary>파일(종목·일자) 1개의 백테스트 결과.</summary>
    public sealed record FileSignals(string Stem, IReadOnlyList<MinuteSignal> Signals);

    private readonly List<Row> _all;
    private bool _sortByImportance;
    private bool _initialized;

    public SignalResultWindow(IReadOnlyList<FileSignals> files)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        _all = files
            .SelectMany(f => f.Signals.Select(s => new Row(ShortStem(f.Stem), s)))
            .OrderBy(r => r.Time).ThenBy(r => r.Tf)
            .ToList();

        GradeCombo.ItemsSource = new[]
        {
            "전체",
            "🔥 강력 확인만",
            "🔥+✅ 확인 (진입 후보)",
            "📈 바닥 반등 (1차)",
            "📉 고점 계열 (경고·데드)",
            "⚠ 약한 확인",
        };
        GradeCombo.SelectedIndex = 0;

        TfCombo.ItemsSource = new[] { "전체" }.Concat(
            _all.Select(r => r.Tf).Distinct().OrderBy(t => t).Select(t => $"{t}분")).ToList();
        TfCombo.SelectedIndex = 0;

        StockCombo.ItemsSource = new[] { "전체" }.Concat(
            _all.Select(r => r.Stock).Distinct().OrderBy(s => s)).ToList();
        StockCombo.SelectedIndex = 0;

        _initialized = true;
        Render();
    }

    /// <summary>파일 stem("이름(코드)_yyyyMMdd_1분봉")에서 종목 표시명만 추출.</summary>
    private static string ShortStem(string stem)
    {
        int i = stem.IndexOf(")_", StringComparison.Ordinal);
        return i > 0 ? stem[..(i + 1)] : stem;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialized) Render();
    }

    private void SortImportance_Click(object sender, RoutedEventArgs e)
    {
        _sortByImportance = !_sortByImportance;
        Render();
    }

    private void Render()
    {
        IEnumerable<Row> rows = _all;

        rows = GradeCombo.SelectedIndex switch
        {
            1 => rows.Where(r => r.Kind == MinuteSignalKind.StrongGoldenCross),
            2 => rows.Where(r => r.Kind is MinuteSignalKind.StrongGoldenCross or MinuteSignalKind.GoldenCross),
            3 => rows.Where(r => r.Kind is MinuteSignalKind.Rebound or MinuteSignalKind.FollowThrough),
            4 => rows.Where(r => r.Kind is MinuteSignalKind.TopWarn or MinuteSignalKind.DeadCross),
            5 => rows.Where(r => r.Kind == MinuteSignalKind.WeakGoldenCross),
            _ => rows,
        };
        if (TfCombo.SelectedIndex > 0 && TfCombo.SelectedItem is string tfText)
            rows = rows.Where(r => $"{r.Tf}분" == tfText);
        if (StockCombo.SelectedIndex > 0 && StockCombo.SelectedItem is string stock)
            rows = rows.Where(r => r.Stock == stock);

        var list = (_sortByImportance
                ? rows.OrderBy(r => r.Rank).ThenBy(r => r.Time).ThenBy(r => r.Tf)
                : rows.OrderBy(r => r.Time).ThenBy(r => r.Tf))
            .ToList();
        Grid.ItemsSource = list;

        int Count(params MinuteSignalKind[] kinds) => list.Count(r => kinds.Contains(r.Kind));
        StatText.Text = $"표시 {list.Count}건 — 🔥 {Count(MinuteSignalKind.StrongGoldenCross)} · " +
            $"✅ {Count(MinuteSignalKind.GoldenCross)} · ⚠ {Count(MinuteSignalKind.WeakGoldenCross)} · " +
            $"📈 {Count(MinuteSignalKind.Rebound)} · ↗ {Count(MinuteSignalKind.FollowThrough)} · " +
            $"📉 {Count(MinuteSignalKind.TopWarn)} · 🔻 {Count(MinuteSignalKind.DeadCross)}" +
            (_sortByImportance ? "  (중요도순)" : "  (날짜순)");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ───────────────────────── 행 뷰 모델 ─────────────────────────
    public sealed class Row(string stock, MinuteSignal s)
    {
        public MinuteSignalKind Kind => s.Kind;
        public DateTime Time => s.Time;
        public string Stock { get; } = stock;
        public int Tf => s.Timeframe;
        public string TfText => $"{s.Timeframe}분";
        public decimal Price => s.Price;
        public string Detail => s.Detail;
        public string Context => s.Context;

        /// <summary>중요도 순위(작을수록 중요) — 🔥 → ✅ → 📈 → ↗ → 📉 → 🔻 → ⚠ → ☀.</summary>
        public int Rank => s.Kind switch
        {
            MinuteSignalKind.StrongGoldenCross => 0,
            MinuteSignalKind.GoldenCross => 1,
            MinuteSignalKind.Rebound => 2,
            MinuteSignalKind.FollowThrough => 3,
            MinuteSignalKind.TopWarn => 4,
            MinuteSignalKind.DeadCross => 5,
            MinuteSignalKind.WeakGoldenCross => 6,
            _ => 7,
        };

        public string Icon => s.Kind switch
        {
            MinuteSignalKind.StrongGoldenCross => "🔥",
            MinuteSignalKind.GoldenCross => "✅",
            MinuteSignalKind.Rebound => "📈",
            MinuteSignalKind.FollowThrough => "↗",
            MinuteSignalKind.TopWarn => "📉",
            MinuteSignalKind.DeadCross => "🔻",
            MinuteSignalKind.WeakGoldenCross => "⚠",
            _ => "☀",
        };

        public string Label => s.Kind switch
        {
            MinuteSignalKind.StrongGoldenCross => "강력 확인",
            MinuteSignalKind.GoldenCross => "반등 확인",
            MinuteSignalKind.Rebound => "바닥 반등",
            MinuteSignalKind.FollowThrough => "반등 지속",
            MinuteSignalKind.TopWarn => "고점 경고",
            MinuteSignalKind.DeadCross => "데드크로스",
            MinuteSignalKind.WeakGoldenCross => "약한 확인",
            _ => "개장 브리핑",
        };
    }
}
