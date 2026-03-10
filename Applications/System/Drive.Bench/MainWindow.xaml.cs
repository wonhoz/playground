using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Shapes;

namespace DriveBench;

public partial class MainWindow : Window
{
    private readonly DriveDetectService _driveSvc  = new();
    private readonly BenchmarkService   _benchSvc  = new();
    private readonly SmartService       _smartSvc  = new();
    private readonly HistoryService     _histSvc   = new();

    private List<DriveItem>        _drives     = [];
    private List<BenchmarkResult>  _results    = [];
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── 초기화 ───────────────────────────────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1; DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        int round = 2; DwmSetWindowAttribute(hwnd, 33, ref round, sizeof(int));

        LoadDrives();
        UpdateStatus("드라이브를 선택하고 벤치마크를 시작하세요.");
    }

    private void LoadDrives()
    {
        _drives = _driveSvc.GetDrives();
        CboDrive.Items.Clear();
        CboHistDrive.Items.Clear();

        foreach (var d in _drives)
        {
            CboDrive.Items.Add(d);
            CboHistDrive.Items.Add(d);
        }

        if (_drives.Count > 0)
        {
            CboDrive.SelectedIndex     = 0;
            CboHistDrive.SelectedIndex = 0;
        }
    }

    // ── 드라이브 선택 ─────────────────────────────────────────────────

    private void CboDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CboDrive.SelectedItem is not DriveItem d) return;

        var info = new System.IO.DriveInfo(d.RootPath);
        long free = info.AvailableFreeSpace;
        TxtDriveInfo.Text = $"여유: {FormatSize(free)}  /  전체: {d.TotalText}";
    }

    // ── 벤치마크 실행 ─────────────────────────────────────────────────

    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (CboDrive.SelectedItem is not DriveItem drive) return;

        // 파일 크기 파싱
        long fileSize = 536870912L; // 512MB 기본
        if (CboSize.SelectedItem is ComboBoxItem ci && ci.Tag is string ts)
            long.TryParse(ts, out fileSize);

        // 여유 공간 체크
        var driveInfo = new System.IO.DriveInfo(drive.RootPath);
        if (driveInfo.AvailableFreeSpace < fileSize + 100 * 1024 * 1024)
        {
            MessageBox.Show($"여유 공간이 부족합니다.\n필요: {FormatSize(fileSize + 100 * 1024 * 1024)}\n여유: {FormatSize(driveInfo.AvailableFreeSpace)}",
                "Drive.Bench", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var opts = new BenchmarkOptions(
            SeqRead  : true,
            SeqWrite : ChkSeq1M.IsChecked == true,
            Seq128K  : ChkSeq128K.IsChecked == true,
            Rnd4KQ1T1: ChkRnd4KQ1.IsChecked == true,
            Rnd4KQ8T8: ChkRnd4KQ8.IsChecked == true);

        _cts = new CancellationTokenSource();
        BtnRun.IsEnabled    = false;
        BtnCancel.IsEnabled = true;
        PanelProgress.Visibility = Visibility.Visible;

        _results.Clear();
        ResultList.DataContext = null;

        var progress = new Progress<BenchmarkProgress>(p =>
        {
            TxtPhase.Text   = p.Phase;
            PbProgress.Value = p.Percent;
            TxtSpeed.Text   = p.SpeedMBps > 0 ? $"{p.SpeedMBps:F0} MB/s" : "";
        });

        try
        {
            UpdateStatus($"{drive.DisplayName} 벤치마크 진행 중...");
            _results = await _benchSvc.RunAsync(drive.RootPath, fileSize, opts, progress, _cts.Token);

            // 결과 바인딩 및 바 크기 업데이트
            ResultList.DataContext = _results;
            ResultList.UpdateLayout();
            UpdateResultBars();

            // 히스토리 저장
            _histSvc.Save(new HistoryEntry
            {
                DriveLetter   = drive.Letter,
                DriveLabel    = drive.Label,
                MediaType     = drive.MediaType,
                FileSizeBytes = fileSize,
                Results       = _results
            });
            UpdateStatus($"완료 — {DateTime.Now:HH:mm:ss}  |  {drive.DisplayName}");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("벤치마크 중단됨.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"오류: {ex.Message}", "Drive.Bench", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateStatus("오류 발생.");
        }
        finally
        {
            BtnRun.IsEnabled    = true;
            BtnCancel.IsEnabled = false;
            PbProgress.Value    = 0;
            TxtPhase.Text       = "";
            TxtSpeed.Text       = "";
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnCancel.IsEnabled = false;
        UpdateStatus("중단 중...");
    }

    // ── 결과 바 너비 계산 ──────────────────────────────────────────────

    private void UpdateResultBars()
    {
        if (_results.Count == 0) return;

        double maxRead  = _results.Max(r => r.ReadMBps);
        double maxWrite = _results.Max(r => r.WriteMBps);
        double maxVal   = Math.Max(maxRead, Math.Max(maxWrite, 1.0));

        // ItemsControl의 각 아이템 컨테이너 접근
        var panel = GetVisualChild<StackPanel>(ResultList) ?? GetVisualChild<VirtualizingStackPanel>(ResultList) as Panel;

        // ItemsPresenter 하위의 실제 아이템들에서 Border 찾기
        for (int i = 0; i < ResultList.Items.Count; i++)
        {
            var container = ResultList.ItemContainerGenerator.ContainerFromIndex(i) as ContentPresenter;
            if (container == null) continue;

            var rowBorder = GetVisualChild<Border>(container);
            if (rowBorder == null) continue;

            var readFill  = FindNamedBorder(container, "ReadFill");
            var writeFill = FindNamedBorder(container, "WriteFill");
            var readTrack = FindNamedBorder(container, "ReadTrack");
            var writeTrack = FindNamedBorder(container, "WriteTrack");

            if (readFill != null && readTrack != null && _results[i].ReadMBps > 0)
            {
                readTrack.UpdateLayout();
                readFill.Width = readTrack.ActualWidth * (_results[i].ReadMBps / maxVal);
            }
            if (writeFill != null && writeTrack != null && _results[i].WriteMBps > 0)
            {
                writeTrack.UpdateLayout();
                writeFill.Width = writeTrack.ActualWidth * (_results[i].WriteMBps / maxVal);
            }
        }
    }

    private static T? GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = GetVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private static Border? FindNamedBorder(DependencyObject parent, string name)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Border b && b.Name == name) return b;
            var result = FindNamedBorder(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // ── 히스토리 탭 ──────────────────────────────────────────────────

    private void CboHistDrive_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        DrawHistoryChart();
    }

    private void CboHistTest_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        DrawHistoryChart();
    }

    private void HistChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawHistoryChart();
    }

    private void DrawHistoryChart()
    {
        HistChart.Children.Clear();

        if (CboHistDrive.SelectedItem is not DriveItem drive) return;
        if (CboHistTest.SelectedItem is not ComboBoxItem ci) return;

        var testTag = ci.Tag?.ToString() ?? "";
        var history = _histSvc.GetByDrive(drive.Letter);

        // 데이터 포인트 추출
        var points = history
            .Select(h =>
            {
                var r = h.Results.FirstOrDefault(x => x.TestKey == testTag.Replace("_r", "").Replace("_w", ""));
                if (r == null) return (double?)null;
                double val = testTag.EndsWith("_w") ? r.WriteMBps : r.ReadMBps;
                return val > 0 ? (double?)val : null;
            })
            .Select((v, i) => (v, history[i].Timestamp))
            .Where(x => x.v.HasValue)
            .Select(x => (x.v!.Value, x.Timestamp))
            .ToList();

        if (points.Count < 2)
        {
            TxtHistEmpty.Visibility = Visibility.Visible;
            return;
        }
        TxtHistEmpty.Visibility = Visibility.Collapsed;

        double w = HistChart.ActualWidth;
        double h = HistChart.ActualHeight;
        if (w < 100 || h < 100) return;

        double padL = 60, padR = 20, padT = 20, padB = 40;
        double chartW = w - padL - padR;
        double chartH = h - padT - padB;

        double maxVal = points.Max(p => p.Item1) * 1.1;
        double minVal = 0;

        // 배경 그리드 라인
        for (int row = 0; row <= 4; row++)
        {
            double y = padT + chartH * row / 4;
            double val = maxVal * (1 - row / 4.0);

            var line = new Line
            {
                X1 = padL, X2 = padL + chartW,
                Y1 = y, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                StrokeThickness = 1
            };
            HistChart.Children.Add(line);

            var label = new TextBlock
            {
                Text = val >= 1000 ? $"{val / 1000:F1}G" : $"{val:F0}",
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                FontSize = 10
            };
            Canvas.SetLeft(label, 4);
            Canvas.SetTop(label, y - 7);
            HistChart.Children.Add(label);
        }

        // 데이터 선
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            for (int i = 0; i < points.Count; i++)
            {
                double x = padL + chartW * i / (points.Count - 1);
                double y = padT + chartH * (1 - (points[i].Item1 - minVal) / (maxVal - minVal));
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false);
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geom.Freeze();

        var path = new System.Windows.Shapes.Path
        {
            Data = geom,
            Stroke = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        HistChart.Children.Add(path);

        // 데이터 포인트 원 + X축 레이블
        for (int i = 0; i < points.Count; i++)
        {
            double x = padL + chartW * i / (points.Count - 1);
            double y = padT + chartH * (1 - (points[i].Item1 - minVal) / (maxVal - minVal));

            var dot = new Ellipse
            {
                Width = 7, Height = 7,
                Fill = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
            };
            Canvas.SetLeft(dot, x - 3.5);
            Canvas.SetTop(dot, y - 3.5);
            HistChart.Children.Add(dot);

            // X축 날짜 레이블 (4개 표시)
            if (i == 0 || i == points.Count - 1 || i % Math.Max(1, points.Count / 3) == 0)
            {
                var lbl = new TextBlock
                {
                    Text = points[i].Timestamp.ToString("MM/dd"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                    FontSize = 10
                };
                Canvas.SetLeft(lbl, x - 15);
                Canvas.SetTop(lbl, h - padB + 6);
                HistChart.Children.Add(lbl);
            }
        }
    }

    // ── 비교 탭 ───────────────────────────────────────────────────────

    private void BtnRefreshCompare_Click(object sender, RoutedEventArgs e) => DrawCompareChart();

    private void DrawCompareChart()
    {
        ComparePanel.Children.Clear();

        var testInfos = new[]
        {
            ("SEQ 1M 읽기",    "seq1m",       true),
            ("SEQ 1M 쓰기",    "seq1m",       false),
            ("SEQ 128K 읽기",  "seq128k",     true),
            ("RND 4K Q1T1 읽기", "rnd4k_q1t1", true),
        };

        foreach (var (label, key, isRead) in testInfos)
        {
            var driveData = new List<(string Drive, double Val)>();
            foreach (var d in _drives)
            {
                var history = _histSvc.GetByDrive(d.Letter);
                var last    = history.LastOrDefault();
                if (last == null) continue;
                var r = last.Results.FirstOrDefault(x => x.TestKey == key);
                if (r == null) continue;
                double val = isRead ? r.ReadMBps : r.WriteMBps;
                if (val > 0) driveData.Add((d.Letter, val));
            }

            if (driveData.Count == 0) continue;

            double maxVal = driveData.Max(d => d.Val);

            var header = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 8)
            };
            ComparePanel.Children.Add(header);

            foreach (var (driveLetter, val) in driveData.OrderByDescending(d => d.Val))
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                var driveLbl = new TextBlock
                {
                    Text = driveLetter,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                    FontSize = 12, VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(driveLbl, 0);

                var track = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                    Height = 20, CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                var fill = new Border
                {
                    Background = isRead
                        ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                        : new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(3),
                    Width = 0  // will be set after layout
                };
                track.Child = fill;
                Grid.SetColumn(track, 1);

                var valLbl = new TextBlock
                {
                    Text = $"{val:F1} MB/s",
                    Foreground = isRead
                        ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
                        : new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)),
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(valLbl, 2);

                row.Children.Add(driveLbl);
                row.Children.Add(track);
                row.Children.Add(valLbl);
                ComparePanel.Children.Add(row);

                // 바 너비 업데이트
                double capVal = val;
                double capMax = maxVal;
                track.SizeChanged += (s, _) =>
                {
                    fill.Width = track.ActualWidth * (capVal / capMax);
                };
            }
        }

        if (ComparePanel.Children.Count == 0)
        {
            ComparePanel.Children.Add(new TextBlock
            {
                Text = "벤치마크를 실행하면 드라이브 간 비교가 표시됩니다.",
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                FontSize = 13, Margin = new Thickness(0, 20, 0, 0)
            });
        }
    }

    // ── S.M.A.R.T ─────────────────────────────────────────────────────

    private void BtnSmartRefresh_Click(object sender, RoutedEventArgs e) => LoadSmart();

    private void LoadSmart()
    {
        if (CboDrive.SelectedItem is not DriveItem drive) return;

        var (attrs, isOk, model, serial) = _smartSvc.GetSmart(drive.Letter[0]);

        TxtSmartModel.Text  = model.Length > 0 ? model : "—";
        TxtSmartSerial.Text = serial.Length > 0 ? serial : "—";
        TxtSmartStatus.Text = isOk ? "✓ 정상" : "⚠ 불량 예측";
        TxtSmartStatus.Foreground = isOk
            ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
            : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));

        SmartList.ItemsSource = attrs.OrderBy(a => a.Id).ToList();

        if (attrs.Count == 0)
            UpdateStatus("S.M.A.R.T 데이터를 가져올 수 없습니다 (관리자 권한 필요).");
        else
            UpdateStatus($"S.M.A.R.T 로드 완료 — {attrs.Count}개 속성");
    }

    // ── 탭 전환 ───────────────────────────────────────────────────────

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var idx = Tabs.SelectedIndex;
        if (idx == 1) DrawHistoryChart();
        if (idx == 2) DrawCompareChart();
        if (idx == 3) LoadSmart();
    }

    // ── 타이틀바 ──────────────────────────────────────────────────────

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────

    private void UpdateStatus(string msg) => TxtStatus.Text = msg;

    private static string FormatSize(long b) => b switch
    {
        >= 1099511627776L => $"{b / 1099511627776.0:F1} TB",
        >= 1073741824L    => $"{b / 1073741824.0:F1} GB",
        >= 1048576L       => $"{b / 1048576.0:F1} MB",
        _                 => $"{b} B"
    };
}
