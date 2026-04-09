using System.Diagnostics;
using System.Text;
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
        LoadCustomFolders();
        TargetList.ItemsSource = _targets;

        var last = _history.GetLast();
        TbStatus.Text = last != null
            ? $"마지막 청소: {CleanHistoryService.FormatRelativeTime(last.Time)}  ({CleanTarget.FormatSize(last.CleanedBytes)} 해제)"
            : "준비";
    }

    // ── 커스텀 폴더 로드 ──────────────────────────────────────────────
    private void LoadCustomFolders()
    {
        var folders = CustomFolderService.Load();
        if (folders.Count == 0) return;

        _targets.Add(new CleanTarget { IsGroup = true, Name = "커스텀 폴더", Category = "custom", CleanerId = "grp_custom" });
        foreach (var folder in folders)
        {
            _targets.Add(new CleanTarget
            {
                Name = Path.GetFileName(folder.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : folder,
                Description = folder,
                Category = "custom",
                CleanerId = $"custom_{folder.GetHashCode():X}",
                Paths = [folder]
            });
        }
    }

    // ── 커스텀 폴더 추가 버튼 ────────────────────────────────────────
    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "청소 대상 폴더를 선택하세요",
            Multiselect = false
        };

        if (dlg.ShowDialog() != true) return;

        var folder = dlg.FolderName;
        if (string.IsNullOrEmpty(folder)) return;

        if (_targets.Any(t => !t.IsGroup && t.Category == "custom" &&
                               t.Paths.Length > 0 &&
                               string.Equals(t.Paths[0], folder, StringComparison.OrdinalIgnoreCase)))
        {
            TbStatus.Text = "이미 추가된 폴더입니다.";
            return;
        }

        // 커스텀 그룹 헤더가 없으면 추가
        if (!_targets.Any(t => t.IsGroup && t.Category == "custom"))
            _targets.Add(new CleanTarget { IsGroup = true, Name = "커스텀 폴더", Category = "custom", CleanerId = "grp_custom" });

        _targets.Add(new CleanTarget
        {
            Name = Path.GetFileName(folder.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : folder,
            Description = folder,
            Category = "custom",
            CleanerId = $"custom_{folder.GetHashCode():X}",
            Paths = [folder]
        });

        CustomFolderService.Add(folder);
        TargetList.ItemsSource = null;
        TargetList.ItemsSource = _targets;
        _analyzed = false;
        TbStatus.Text = $"'{folder}' 추가됨. 분석을 다시 실행하세요.";
    }

    // ── 키보드 단축키 트리거 (MainWindow에서 호출) ─────────────────────
    internal void TriggerAnalyze() => BtnAnalyze_Click(BtnAnalyze, new RoutedEventArgs());
    internal void TriggerClean()   { if (BtnClean.IsEnabled) BtnClean_Click(BtnClean, new RoutedEventArgs()); }
    internal void TriggerSelectAll() => BtnSelectAll_Click(null!, new RoutedEventArgs());
    internal bool IsCleanEnabled => BtnClean.IsEnabled;

    // ── 프리셋 ────────────────────────────────────────────────────────
    // 프리셋 카테고리 배열 — null이면 모두 선택
    private static readonly string[]? QuickPreset   = ["system", "browser_cache"];
    private static readonly string[]? BrowserPreset = ["browser_cache", "browser_history"];
    private static readonly string[]? AllPreset      = null;

    internal void ApplyPreset(string[]? categories)
    {
        foreach (var t in _targets.Where(t => !t.IsGroup))
            t.IsSelected = categories == null || categories.Contains(t.Category);

        if (_analyzed) UpdateResults();
        TargetList.ItemsSource = null;
        TargetList.ItemsSource = _targets;
    }

    private void BtnPresetQuick_Click(object sender, RoutedEventArgs e)   => ApplyPreset(QuickPreset);
    private void BtnPresetBrowser_Click(object sender, RoutedEventArgs e) => ApplyPreset(BrowserPreset);
    private void BtnPresetAll_Click(object sender, RoutedEventArgs e)     => ApplyPreset(AllPreset);

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
        BtnCopy.IsEnabled = false;
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
            BtnCopy.IsEnabled = true;
            int selectedCount = _targets.Count(t => !t.IsGroup && t.IsSelected && t.Size > 0);
            TbResultHeader.Text = $"총 {_targets.Count(t => !t.IsGroup && t.Size > 0)}개 항목 발견 — {selectedCount}개 선택 ({CleanTarget.FormatSize(totalCleanable)} 정리 가능)";
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

    // ── 결과 카드 (우클릭 → 폴더 열기 / 지금 청소) ────────────────────
    private Border BuildResultCard(CleanTarget target)
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

        // 우클릭 → 폴더 열기 / 지금 청소
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

        var menuCleanNow = new MenuItem
        {
            Header = "🧹  지금 청소",
            IsEnabled = target.Size > 0
        };
        menuCleanNow.Click += async (_, _) => await CleanSingleTargetAsync(target);

        var sep = new Separator
        {
            Style = (Style)Application.Current.FindResource("MenuSeparator")
        };

        var ctx = new ContextMenu();
        ctx.Items.Add(menuOpen);
        ctx.Items.Add(sep);
        ctx.Items.Add(menuCleanNow);

        // 커스텀 폴더는 목록에서 제거 옵션 추가
        if (target.Category == "custom")
        {
            var sep2 = new Separator { Style = (Style)Application.Current.FindResource("MenuSeparator") };
            var menuRemove = new MenuItem { Header = "🗑  목록에서 제거" };
            menuRemove.Click += (_, _) =>
            {
                if (target.Paths.Length > 0)
                    CustomFolderService.Remove(target.Paths[0]);
                _targets.Remove(target);
                // 커스텀 폴더가 없으면 그룹 헤더도 제거
                if (!_targets.Any(t => !t.IsGroup && t.Category == "custom"))
                    _targets.RemoveAll(t => t.IsGroup && t.Category == "custom");
                TargetList.ItemsSource = null;
                TargetList.ItemsSource = _targets;
                _analyzed = false;
                ResultPanel.Children.Clear();
                TbTotalSize.Text = "";
                TbResultHeader.Text = "분석 버튼을 클릭하면 정리 가능한 파일을 검색합니다.";
                TbResultHeader.Foreground = (Brush)Application.Current.FindResource("BrFgSec");
                TbStatus.Text = "커스텀 폴더 제거됨.";
            };
            ctx.Items.Add(sep2);
            ctx.Items.Add(menuRemove);
        }

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 2, 0, 2),
            Child = grid,
            ToolTip = "클릭: 대상 파일 미리 보기  |  " + (target.Description ?? ""),
            ContextMenu = ctx,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        card.MouseLeftButtonUp += (_, _) => ShowFilePreview(target);
        return card;
    }

    // ── 파일 미리 보기 팝업 ───────────────────────────────────────────
    private void ShowFilePreview(CleanTarget target)
    {
        var files = _service.GetPreviewFiles(target);

        var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

        if (files.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "표시할 파일이 없습니다.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 12
            });
        }
        else
        {
            foreach (var f in files)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = f,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 1, 0, 1),
                    ToolTip = f
                });
            }
            if (files.Count >= 200)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "… (최대 200개 표시)",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 11, Margin = new Thickness(0, 4, 0, 0)
                });
            }
        }

        var scroll = new ScrollViewer
        {
            Content = panel,
            MaxHeight = 340,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(4, 0, 4, 0)
        };

        var win = new Window
        {
            Title = $"파일 미리 보기 — {target.Name}  ({CleanTarget.FormatSize(target.Size)}, {files.Count}개)",
            Width = 620, Height = 420,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.CanResize,
            ShowInTaskbar = false,
            Content = new Border { Padding = new Thickness(16), Child = scroll }
        };
        win.ShowDialog();
    }

    // ── 개별 항목 즉시 청소 ────────────────────────────────────────────
    private async Task CleanSingleTargetAsync(CleanTarget target)
    {
        if (_cts != null) return; // 이미 작업 중

        var result = MessageBox.Show(
            $"'{target.Name}' ({CleanTarget.FormatSize(target.Size)})을 지금 청소하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "청소 확인",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        BtnClean.IsEnabled = false;
        BtnAnalyze.IsEnabled = false;
        BtnCopy.IsEnabled = false;
        TbStatus.Text = $"청소 중: {target.Name}";

        _cts = new CancellationTokenSource();
        long cleaned = 0;
        int errors = 0;
        try
        {
            (cleaned, errors) = await _service.CleanTargetAsync(target, _cts.Token);
            target.Size = 0;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            BtnAnalyze.IsEnabled = true;
            BtnClean.IsEnabled = true;
            BtnCopy.IsEnabled = true;
        }

        if (cleaned > 0)
            _history.Append(new CleanHistoryEntry(DateTime.Now, 1, cleaned));

        var msg = errors > 0
            ? $"'{target.Name}' 청소 완료!\n해제된 공간: {CleanTarget.FormatSize(cleaned)}\n실패: {errors}개 (사용 중인 파일 등)"
            : $"'{target.Name}' 청소 완료!\n해제된 공간: {CleanTarget.FormatSize(cleaned)}";
        MessageBox.Show(msg, "청소 완료", MessageBoxButton.OK, MessageBoxImage.Information);

        TbStatus.Text = $"'{target.Name}' — {CleanTarget.FormatSize(cleaned)} 해제";
        UpdateResults();
        (Application.Current.MainWindow as MainWindow)?.UpdateDiskInfo();
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
        if (_cts != null)
        {
            _cts.Cancel();
            return;
        }

        if (!_analyzed) return;

        var toClean = _targets.Where(t => !t.IsGroup && t.IsSelected && t.Size > 0).ToList();
        if (toClean.Count == 0) return;

        var result = MessageBox.Show(
            $"선택한 {toClean.Count}개 항목 ({CleanTarget.FormatSize(toClean.Sum(t => t.Size))})을 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "청소 확인",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        // 브라우저 실행 중 경고
        var runningBrowserWarnings = toClean
            .Select(t => GetRunningBrowserName(t.CleanerId))
            .Where(n => n != null)
            .Distinct()
            .ToList();

        if (runningBrowserWarnings.Count > 0)
        {
            var warnResult = MessageBox.Show(
                $"다음 브라우저가 실행 중입니다: {string.Join(", ", runningBrowserWarnings)}\n\n" +
                "브라우저가 열려 있으면 캐시 파일 일부가 사용 중이어서 삭제되지 않을 수 있습니다.\n\n" +
                "브라우저를 먼저 닫고 청소하면 더 많은 공간을 확보할 수 있습니다.\n\n" +
                "그래도 계속하시겠습니까?",
                "브라우저 실행 중",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (warnResult != MessageBoxResult.Yes) return;
        }

        BtnClean.Content = "⏹  중지";
        BtnClean.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A));
        BtnClean.Foreground = (Brush)Application.Current.FindResource("BrDanger");
        BtnClean.BorderBrush = (Brush)Application.Current.FindResource("BrDanger");
        BtnAnalyze.IsEnabled = false;
        BtnCopy.IsEnabled = false;
        PbProgress.Visibility = Visibility.Visible;
        PbProgress.IsIndeterminate = false;
        PbProgress.Value = 0;
        PbProgress.Maximum = toClean.Count;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        long totalCleaned = 0;
        int totalErrors = 0;
        int done = 0;
        bool cancelled = false;

        try
        {
            foreach (var target in toClean)
            {
                if (ct.IsCancellationRequested) { cancelled = true; break; }
                TbStatus.Text = $"청소 중: {target.Name}";
                var (cleaned, errors) = await _service.CleanTargetAsync(target, ct);
                totalCleaned += cleaned;
                totalErrors += errors;
                target.Size = 0;
                done++;
                PbProgress.Value = done;
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            PbProgress.Visibility = Visibility.Collapsed;
            BtnClean.Content = "🧹  청소 실행";
            BtnClean.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x3A, 0x1F));
            BtnClean.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
            BtnClean.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x8E, 0x3C));
            BtnAnalyze.IsEnabled = true;
            BtnCopy.IsEnabled = true;
            _analyzed = false;
        }

        if (cancelled)
        {
            TbStatus.Text = totalCleaned > 0
                ? $"청소 취소됨 — 취소 전 {CleanTarget.FormatSize(totalCleaned)} 해제"
                : "청소 취소됨";
            if (totalCleaned > 0)
                _history.Append(new CleanHistoryEntry(DateTime.Now, done, totalCleaned));
            UpdateResults();
            (Application.Current.MainWindow as MainWindow)?.UpdateDiskInfo();
            return;
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

        // 청소 후 사이드바 디스크 정보 갱신
        (Application.Current.MainWindow as MainWindow)?.UpdateDiskInfo();
    }

    // ── 전체 선택 ─────────────────────────────────────────────────────
    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool allSelected = _targets.Where(t => !t.IsGroup).All(t => t.IsSelected);
        foreach (var t in _targets.Where(t => !t.IsGroup))
            t.IsSelected = !allSelected;
        if (_analyzed) UpdateResults();
    }

    // ── 브라우저 프로세스 감지 ───────────────────────────────────────
    private static string? GetRunningBrowserName(string cleanerId)
    {
        return cleanerId switch
        {
            "chrome_cache" or "chrome_history" or "chrome_cookies"
                when Process.GetProcessesByName("chrome").Length > 0 => "Chrome",
            "edge_cache" or "edge_history" or "edge_cookies"
                when Process.GetProcessesByName("msedge").Length > 0 => "Edge",
            "firefox_cache" or "firefox_history"
                when Process.GetProcessesByName("firefox").Length > 0 => "Firefox",
            "brave_cache"
                when Process.GetProcessesByName("brave").Length > 0 => "Brave",
            "vivaldi_cache"
                when Process.GetProcessesByName("vivaldi").Length > 0 => "Vivaldi",
            "opera_cache"
                when Process.GetProcessesByName("opera").Length > 0 => "Opera",
            _ => null
        };
    }

    // ── 결과 클립보드 복사 ────────────────────────────────────────────
    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Sys.Clean 분석 결과 ===");
        sb.AppendLine($"분석 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        string? currentGroup = null;
        long total = 0;
        foreach (var target in _targets)
        {
            if (target.IsGroup)
            {
                currentGroup = target.Name;
                continue;
            }
            if (target.Size <= 0) continue;

            if (currentGroup != null)
            {
                sb.AppendLine($"[{currentGroup}]");
                currentGroup = null;
            }
            sb.AppendLine($"  {target.Name,-30}  {target.SizeText,10}");
            total += target.Size;
        }

        sb.AppendLine();
        sb.AppendLine($"총 정리 가능: {CleanTarget.FormatSize(total)}");

        Clipboard.SetText(sb.ToString());
        TbStatus.Text = "분석 결과를 클립보드에 복사했습니다.";
    }
}
