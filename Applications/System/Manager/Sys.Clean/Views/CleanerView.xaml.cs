using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class CleanerView : UserControl
{
    private readonly CleanerService _service = new();
    private readonly CleanHistoryService _history = new();
    private List<CleanTarget> _targets = [];
    private CancellationTokenSource? _cts;
    private bool _analyzed;

    public CleanerView()
    {
        InitializeComponent();
        _targets = _service.GetTargets();
        TargetList.ItemsSource = _targets;

        var last = _history.GetLast();
        TbStatus.Text = last != null
            ? $"마지막 청소: {CleanHistoryService.FormatRelativeTime(last.Time)}  ({CleanTarget.FormatSize(last.CleanedBytes)} 해제)"
            : "준비";
    }

    // ── 키보드 단축키 트리거 (MainWindow에서 호출) ─────────────────────
    internal void TriggerAnalyze() => BtnAnalyze_Click(BtnAnalyze, new RoutedEventArgs());
    internal void TriggerClean()   { if (BtnClean.IsEnabled) BtnClean_Click(BtnClean, new RoutedEventArgs()); }
    internal void TriggerSelectAll() => BtnSelectAll_Click(BtnSelectAll, new RoutedEventArgs());
    internal bool IsCleanEnabled => BtnClean.IsEnabled;

    // ── 분석 ──────────────────────────────────────────────────────────
    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            return;
        }

        _analyzed = false;
        BtnClean.IsEnabled = false;
        BtnAnalyze.Content = "⏹  중지";
        BtnAnalyze.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A));
        BtnAnalyze.Foreground = (Brush)Application.Current.FindResource("BrDanger");
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.IsIndeterminate = false;
        PbProgress.Value = 0;
        PbProgress.Maximum = _targets.Count(t => !t.IsGroup);

        ResultPanel.Children.Clear();
        TbHint.Visibility = Visibility.Collapsed;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        int done = 0;
        try
        {
            var tasks = _targets.Where(t => !t.IsGroup).ToList();
            foreach (var target in tasks)
            {
                if (ct.IsCancellationRequested) break;
                TbStatus.Text = $"분석 중: {target.Name}";

                var size = await _service.ScanTargetAsync(target, ct);
                target.Size = size;
                done++;
                PbProgress.Value = done;
            }

            _analyzed = true;
            UpdateResults();
        }
        catch (OperationCanceledException)
        {
            TbStatus.Text = "분석 취소됨";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            BtnAnalyze.Content = "🔍  분석";
            BtnAnalyze.Background = (Brush)Application.Current.FindResource("BrAccentDim");
            BtnAnalyze.Foreground = (Brush)Application.Current.FindResource("BrAccent");
            PbProgress.Visibility = Visibility.Collapsed;
        }
    }

    // ── 결과 업데이트 (그룹 헤더 포함) ───────────────────────────────
    private void UpdateResults()
    {
        ResultPanel.Children.Clear();
        TbHint.Visibility = Visibility.Collapsed;

        string? currentGroup = null;
        string? lastRenderedGroup = null;

        foreach (var target in _targets)
        {
            if (target.IsGroup)
            {
                currentGroup = target.Category;
                continue;
            }

            if (target.Size <= 0 && !target.IsSelected) continue;

            var groupItems = _targets.Where(t => !t.IsGroup && t.Category == currentGroup && t.Size > 0).ToList();
            if (groupItems.Count == 0) continue;

            // 그룹이 바뀔 때 헤더 삽입
            if (currentGroup != lastRenderedGroup)
            {
                var groupName = _targets.FirstOrDefault(t => t.IsGroup && t.Category == currentGroup)?.Name;
                ResultPanel.Children.Add(BuildGroupHeader(groupName));
                lastRenderedGroup = currentGroup;
            }

            ResultPanel.Children.Add(BuildResultCard(target));
        }

        var totalCleanable = _targets.Where(t => !t.IsGroup && t.IsSelected).Sum(t => t.Size > 0 ? t.Size : 0);
        TbTotalSize.Text = totalCleanable > 0
            ? $"정리 가능: {CleanTarget.FormatSize(totalCleanable)}"
            : "";

        if (totalCleanable > 0)
        {
            BtnClean.IsEnabled = true;
            TbResultHeader.Text = $"총 {_targets.Count(t => !t.IsGroup && t.Size > 0)}개 항목 발견 — {CleanTarget.FormatSize(totalCleanable)} 정리 가능";
            TbResultHeader.Foreground = (Brush)Application.Current.FindResource("BrAccentGreen");
        }
        else
        {
            TbResultHeader.Text = "정리할 파일이 없습니다.";
            TbResultHeader.Foreground = (Brush)Application.Current.FindResource("BrFgSec");
        }

        TbStatus.Text = "분석 완료";

        if (ResultPanel.Children.Count == 0)
            TbHint.Visibility = Visibility.Visible;
    }

    // ── 결과 카드 (우클릭 → 폴더 열기) ───────────────────────────────
    private static Border BuildResultCard(CleanTarget target)
    {
        var accentBrush = (SolidColorBrush)Application.Current.FindResource("BrAccent");
        var sizeColor = target.Size > 0
            ? accentBrush.Color
            : Color.FromRgb(0x55, 0x55, 0x55);

        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBlock = new TextBlock
        {
            Text = target.Name,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameBlock, 0);

        var sizeBlock = new TextBlock
        {
            Text = target.SizeText,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(sizeColor),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(sizeBlock, 1);

        grid.Children.Add(nameBlock);
        grid.Children.Add(sizeBlock);

        // 우클릭 → 폴더 열기
        var menuOpen = new MenuItem { Header = "📂  폴더 열기" };
        menuOpen.Click += (_, _) =>
        {
            string? folder = null;
            foreach (var p in target.Paths)
            {
                if (Directory.Exists(p)) { folder = p; break; }
                if (File.Exists(p)) { folder = Path.GetDirectoryName(p); break; }
            }
            if (folder != null)
                Process.Start("explorer.exe", folder);
        };

        var ctx = new ContextMenu();
        ctx.Items.Add(menuOpen);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 2, 0, 2),
            Child = grid,
            ToolTip = target.Description,
            ContextMenu = ctx
        };
    }

    // ── 그룹 헤더 ────────────────────────────────────────────────────
    private static Border BuildGroupHeader(string? name)
    {
        return new Border
        {
            Margin = new Thickness(0, 10, 0, 2),
            Padding = new Thickness(4, 2, 0, 6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = name ?? "",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
            }
        };
    }

    // ── 청소 ─────────────────────────────────────────────────────────
    private async void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        if (!_analyzed) return;

        var toClean = _targets.Where(t => !t.IsGroup && t.IsSelected && t.Size > 0).ToList();
        if (toClean.Count == 0) return;

        var result = MessageBox.Show(
            $"선택한 {toClean.Count}개 항목 ({CleanTarget.FormatSize(toClean.Sum(t => t.Size))})을 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "청소 확인",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        BtnClean.IsEnabled = false;
        BtnAnalyze.IsEnabled = false;
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.IsIndeterminate = false;
        PbProgress.Value = 0;
        PbProgress.Maximum = toClean.Count;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        long totalCleaned = 0;
        int totalErrors = 0;
        int done = 0;

        try
        {
            foreach (var target in toClean)
            {
                TbStatus.Text = $"청소 중: {target.Name}";
                var (cleaned, errors) = await _service.CleanTargetAsync(target, ct);
                totalCleaned += cleaned;
                totalErrors += errors;
                target.Size = 0;
                done++;
                PbProgress.Value = done;
            }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            PbProgress.Visibility = Visibility.Collapsed;
            BtnAnalyze.IsEnabled = true;
            _analyzed = false;
        }

        // 이력 저장
        if (totalCleaned > 0)
            _history.Append(new CleanHistoryEntry(DateTime.Now, toClean.Count, totalCleaned));

        var msg = totalErrors > 0
            ? $"청소 완료!\n\n해제된 공간: {CleanTarget.FormatSize(totalCleaned)}\n실패 항목: {totalErrors}개 (사용 중인 파일 등)"
            : $"청소 완료!\n\n해제된 공간: {CleanTarget.FormatSize(totalCleaned)}";

        MessageBox.Show(msg, "청소 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        TbStatus.Text = $"청소 완료 — {CleanTarget.FormatSize(totalCleaned)} 해제";
        TbTotalSize.Text = "";
        TbResultHeader.Text = "청소가 완료되었습니다. 다시 분석하려면 분석 버튼을 클릭하세요.";
        TbResultHeader.Foreground = (Brush)Application.Current.FindResource("BrAccentGreen");
        ResultPanel.Children.Clear();
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool allSelected = _targets.Where(t => !t.IsGroup).All(t => t.IsSelected);
        foreach (var t in _targets.Where(t => !t.IsGroup))
            t.IsSelected = !allSelected;
    }
}
