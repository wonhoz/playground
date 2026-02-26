using System.Windows.Threading;

namespace MockServer;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private readonly ObservableCollection<RouteEntry> _routeItems = [];
    private readonly ObservableCollection<string>     _logItems   = [];
    private readonly MockServerService                _server     = new();
    private FileSystemWatcher?  _watcher;
    private string?             _currentFile;
    private bool                _isYaml = true;
    private readonly DispatcherTimer _validateTimer;
    private const int MaxLogItems = 500;

    public MainWindow()
    {
        InitializeComponent();

        // WPF 창 아이콘 (PNG-embedded ICO는 BitmapDecoder 미지원 → 32px PNG 사용)
        try
        {
            Icon = new BitmapImage(
                new Uri("pack://application:,,,/Resources/icon32.png", UriKind.Absolute));
        }
        catch { }

        // 다크 타이틀바
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        // 컬렉션 바인딩
        LbRoutes.ItemsSource = _routeItems;
        LbLog.ItemsSource    = _logItems;

        // 유효성 검사 타이머 (500ms 디바운스)
        _validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _validateTimer.Tick += ValidateTimer_Tick;

        // 서버 요청 콜백
        _server.OnRequest = OnServerRequest;

        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 기본 YAML 로드
        TxtEditor.Text = RouteLoader.DefaultYaml;
        ApplyEditorContent();
        UpdateValidationStatus(true, "");
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_server.IsRunning)
            await _server.StopAsync();
        _watcher?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    //  에디터 이벤트
    // ─────────────────────────────────────────────────────────────

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _validateTimer.Stop();
        _validateTimer.Start();
    }

    private void ValidateTimer_Tick(object? sender, EventArgs e)
    {
        _validateTimer.Stop();
        if (!IsLoaded) return;
        var ok = RouteLoader.Validate(TxtEditor.Text, _isYaml, out var err);
        UpdateValidationStatus(ok, err);
    }

    private void UpdateValidationStatus(bool ok, string error)
    {
        if (ok)
        {
            TxtValidation.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            TxtValidation.Text       = "✓ 유효한 형식";
        }
        else
        {
            TxtValidation.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            TxtValidation.Text       = $"✗ {error}";
        }
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyEditorContent();
    }

    private void ApplyEditorContent()
    {
        if (!RouteLoader.Validate(TxtEditor.Text, _isYaml, out var err))
        {
            UpdateValidationStatus(false, err);
            return;
        }
        var cfg = _isYaml
            ? RouteLoader.LoadYaml(TxtEditor.Text)
            : RouteLoader.LoadJson(TxtEditor.Text);

        _server.ApplyRoutes(cfg);
        RefreshRouteTable(cfg);
        UpdateValidationStatus(true, "");
    }

    private void RefreshRouteTable(RouteConfig cfg)
    {
        _routeItems.Clear();
        foreach (var r in cfg.Routes) _routeItems.Add(r);
        TxtRouteCount.Text = $"라우트 {cfg.Routes.Count}개";
    }

    // ─────────────────────────────────────────────────────────────
    //  포맷 전환
    // ─────────────────────────────────────────────────────────────

    private void CbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var newIsYaml = CbFormat.SelectedIndex == 0;
        if (newIsYaml == _isYaml) return;

        // 현재 에디터 내용을 다른 포맷으로 변환 시도
        if (RouteLoader.Validate(TxtEditor.Text, _isYaml, out _))
        {
            var cfg = _isYaml
                ? RouteLoader.LoadYaml(TxtEditor.Text)
                : RouteLoader.LoadJson(TxtEditor.Text);

            _isYaml = newIsYaml;
            TxtEditor.Text = _isYaml ? RouteLoader.ToYaml(cfg) : RouteLoader.ToJson(cfg);
        }
        else
        {
            _isYaml = newIsYaml;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  파일 열기 / 저장 / 새로 만들기
    // ─────────────────────────────────────────────────────────────

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "라우트 파일 열기",
            Filter = "YAML/JSON 파일|*.yaml;*.yml;*.json|모든 파일|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        LoadFile(dlg.FileName);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_currentFile != null)
        {
            SaveFile(_currentFile);
        }
        else
        {
            SaveAs();
        }
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _currentFile = null;
        SetWatcher(null);
        _isYaml = true;
        CbFormat.SelectedIndex = 0;
        TxtEditor.Text = RouteLoader.DefaultYaml;
        ApplyEditorContent();
    }

    private void LoadFile(string path)
    {
        try
        {
            var text    = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var isYaml  = path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                          path.EndsWith(".yml",  StringComparison.OrdinalIgnoreCase);

            _isYaml              = isYaml;
            CbFormat.SelectedIndex = isYaml ? 0 : 1;
            _currentFile         = path;
            TxtEditor.Text       = text;

            ApplyEditorContent();
            SetWatcher(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 열기 실패:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveFile(string path)
    {
        try { File.WriteAllText(path, TxtEditor.Text, System.Text.Encoding.UTF8); }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "라우트 파일 저장",
            Filter     = _isYaml ? "YAML 파일|*.yaml" : "JSON 파일|*.json",
            DefaultExt = _isYaml ? ".yaml" : ".json"
        };
        if (dlg.ShowDialog() != true) return;
        _currentFile = dlg.FileName;
        SaveFile(_currentFile);
        SetWatcher(_currentFile);
    }

    // ─────────────────────────────────────────────────────────────
    //  FileSystemWatcher (hot-reload)
    // ─────────────────────────────────────────────────────────────

    private void SetWatcher(string? path)
    {
        _watcher?.Dispose();
        _watcher = null;
        if (path == null) return;

        _watcher = new FileSystemWatcher(
            System.IO.Path.GetDirectoryName(path)!,
            System.IO.Path.GetFileName(path))
        {
            NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                var text = File.ReadAllText(e.FullPath, System.Text.Encoding.UTF8);
                TxtEditor.Text = text;
                ApplyEditorContent();
            }
            catch { }
        });
    }

    // ─────────────────────────────────────────────────────────────
    //  서버 시작 / 중지
    // ─────────────────────────────────────────────────────────────

    private async void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        BtnToggle.IsEnabled = false;

        try
        {
            if (_server.IsRunning)
            {
                await _server.StopAsync();
                BtnToggle.Content    = "▶ 서버 시작";
                BtnToggle.Background = new SolidColorBrush(Color.FromRgb(0x81, 0x8C, 0xF8));
                TxtServerStatus.Text = "";
            }
            else
            {
                if (!int.TryParse(TxtPort.Text.Trim(), out var port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("포트 번호가 올바르지 않습니다 (1~65535).", "오류",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // 현재 에디터 내용 먼저 적용
                ApplyEditorContent();
                await _server.StartAsync(port);
                BtnToggle.Content    = "■ 서버 중지";
                BtnToggle.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                TxtServerStatus.Text = $"● http://localhost:{port}";
                TxtServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"서버 오류:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnToggle.IsEnabled = true;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  요청 로그
    // ─────────────────────────────────────────────────────────────

    private void OnServerRequest(RequestLog log)
    {
        Dispatcher.Invoke(() =>
        {
            _logItems.Insert(0, log.Display);
            while (_logItems.Count > MaxLogItems)
                _logItems.RemoveAt(_logItems.Count - 1);
        });
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _logItems.Clear();
    }
}
