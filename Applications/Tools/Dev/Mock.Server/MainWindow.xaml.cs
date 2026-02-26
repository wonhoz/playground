using System.Windows.Threading;
using Microsoft.Win32;

namespace MockServer;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ── 데이터 ────────────────────────────────────────────────────
    private readonly ObservableCollection<MockRoute> _routes  = [];
    private readonly ObservableCollection<string>    _logItems = [];
    private readonly MockServerService               _server  = new();

    // ── 코드 편집 탭 상태 ────────────────────────────────────────
    private bool                _isYaml = true;
    private string?             _currentFile;
    private FileSystemWatcher?  _watcher;
    private readonly DispatcherTimer _validateTimer;

    // ── GUI 편집 탭 상태 ─────────────────────────────────────────
    private MockRoute? _selectedRoute;
    private bool       _guiUpdating;

    private const int MaxLogItems = 500;

    public MainWindow()
    {
        InitializeComponent();

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
        LbEndpoints.ItemsSource = _routes;
        LbRoutes.ItemsSource    = _routes;
        LbLog.ItemsSource       = _logItems;

        // 유효성 검사 타이머 (500ms 디바운스)
        _validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _validateTimer.Tick += ValidateTimer_Tick;

        _server.OnRequest = OnServerRequest;

        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 기본 YAML 로드 → 공통 _routes에 반영
        var defaults = RouteLoader.LoadRoutesYaml(RouteLoader.DefaultYaml);
        foreach (var r in defaults) _routes.Add(r);

        // 코드 탭 에디터 초기화
        TxtEditor.Text = RouteLoader.ToYaml(_routes);
        UpdateValidationStatus(true, "");

        // 서버에 반영
        _server.ApplyRoutes(_routes);
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_server.IsRunning)
            await _server.StopAsync();
        _watcher?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    //  탭 전환 동기화
    // ─────────────────────────────────────────────────────────────

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        try
        {
            if (Tabs.SelectedIndex == 1)  // → 코드 탭: GUI → 텍스트
            {
                TxtEditor.Text = _isYaml
                    ? RouteLoader.ToYaml(_routes)
                    : RouteLoader.ToJson(_routes);
            }
            else                          // → GUI 탭: 텍스트 → GUI
            {
                if (!RouteLoader.Validate(TxtEditor.Text, _isYaml, out _)) return;
                var loaded = _isYaml
                    ? RouteLoader.LoadRoutesYaml(TxtEditor.Text)
                    : RouteLoader.LoadRoutesJson(TxtEditor.Text);
                _routes.Clear();
                foreach (var r in loaded) _routes.Add(r);
                HideGuiEditor();
                _server.ApplyRoutes(_routes);
            }
        }
        catch { }
    }

    // ─────────────────────────────────────────────────────────────
    //  GUI 편집 탭
    // ─────────────────────────────────────────────────────────────

    private void LbEndpoints_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _selectedRoute = LbEndpoints.SelectedItem as MockRoute;
        if (_selectedRoute is null) { HideGuiEditor(); return; }

        _guiUpdating = true;
        ShowGuiEditor();

        ChkEnabled.IsChecked = _selectedRoute.Enabled;
        TxtEdDesc.Text       = _selectedRoute.Description;
        TxtEdPath.Text       = _selectedRoute.Path;
        TxtEdDelay.Text      = _selectedRoute.Delay.ToString();
        TxtEdBody.Text       = _selectedRoute.Response;

        // Method
        foreach (ComboBoxItem ci in CmbMethod.Items)
        {
            if ((string)ci.Content == _selectedRoute.Method)
            { CmbMethod.SelectedItem = ci; break; }
        }

        // Status (앞 3자리 매칭)
        var sc = _selectedRoute.Status.ToString();
        foreach (ComboBoxItem ci in CmbStatus.Items)
        {
            if (((string)ci.Content).StartsWith(sc))
            { CmbStatus.SelectedItem = ci; break; }
        }

        _guiUpdating = false;
    }

    private void GuiField_Changed(object sender, RoutedEventArgs e)
    {
        if (_guiUpdating || _selectedRoute is null || !IsLoaded) return;
        SaveGuiEditor();
    }

    private void GuiCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_guiUpdating || _selectedRoute is null || !IsLoaded) return;
        SaveGuiEditor();
    }

    private void SaveGuiEditor()
    {
        if (_selectedRoute is null) return;

        _selectedRoute.Enabled     = ChkEnabled.IsChecked == true;
        _selectedRoute.Description = TxtEdDesc.Text;
        _selectedRoute.Path        = TxtEdPath.Text.Trim();
        _selectedRoute.Response    = TxtEdBody.Text;
        _selectedRoute.Delay       = int.TryParse(TxtEdDelay.Text, out var d) ? Math.Max(0, d) : 0;

        if (CmbMethod.SelectedItem is ComboBoxItem mi)
            _selectedRoute.Method = (string)mi.Content;

        if (CmbStatus.SelectedItem is ComboBoxItem si)
        {
            var s = (string)si.Content;
            _selectedRoute.Status = int.TryParse(s[..3], out var sc) ? sc : 200;
        }

        // 목록 갱신 (ObservableCollection은 PropertyChanged 미지원이므로 인덱스 트릭)
        var idx = _routes.IndexOf(_selectedRoute);
        if (idx >= 0)
        {
            _routes.RemoveAt(idx);
            _routes.Insert(idx, _selectedRoute);
            LbEndpoints.SelectedIndex = idx;
        }

        _server.ApplyRoutes(_routes);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var route = new MockRoute();
        _routes.Add(route);
        LbEndpoints.SelectedItem = route;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRoute is null) return;
        if (MessageBox.Show(
                $"'{_selectedRoute.Method} {_selectedRoute.Path}' 엔드포인트를 삭제할까요?",
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _routes.Remove(_selectedRoute);
        _selectedRoute = null;
        HideGuiEditor();
        _server.ApplyRoutes(_routes);
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "JSON으로 내보내기",
            Filter     = "JSON 파일|*.json",
            FileName   = "mock-routes.json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, RouteLoader.ToJson(_routes), System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"내보내기 실패:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "JSON 가져오기",
            Filter = "JSON 파일|*.json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json   = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            var loaded = RouteLoader.LoadRoutesJson(json);
            _routes.Clear();
            foreach (var r in loaded) _routes.Add(r);
            HideGuiEditor();
            _server.ApplyRoutes(_routes);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 파싱 실패:\n{ex.Message}", "오류",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowGuiEditor()
    {
        TxtGuiPlaceholder.Visibility = Visibility.Collapsed;
        GuiEditor.Visibility         = Visibility.Visible;
    }

    private void HideGuiEditor()
    {
        TxtGuiPlaceholder.Visibility = Visibility.Visible;
        GuiEditor.Visibility         = Visibility.Collapsed;
    }

    // ─────────────────────────────────────────────────────────────
    //  코드 편집 탭 — 에디터 이벤트
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

        var loaded = _isYaml
            ? RouteLoader.LoadRoutesYaml(TxtEditor.Text)
            : RouteLoader.LoadRoutesJson(TxtEditor.Text);

        _routes.Clear();
        foreach (var r in loaded) _routes.Add(r);

        TxtRouteCount.Text = $"{_routes.Count}개";
        _server.ApplyRoutes(_routes);
        UpdateValidationStatus(true, "");
    }

    private void CbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var newIsYaml = CbFormat.SelectedIndex == 0;
        if (newIsYaml == _isYaml) return;

        if (RouteLoader.Validate(TxtEditor.Text, _isYaml, out _))
        {
            var loaded = _isYaml
                ? RouteLoader.LoadRoutesYaml(TxtEditor.Text)
                : RouteLoader.LoadRoutesJson(TxtEditor.Text);

            _isYaml = newIsYaml;
            TxtEditor.Text = _isYaml
                ? RouteLoader.ToYaml(loaded)
                : RouteLoader.ToJson(loaded);
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
        var dlg = new OpenFileDialog
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
        if (_currentFile != null) SaveFile(_currentFile);
        else                      SaveAs();
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
            var text   = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var isYaml = path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".yml",  StringComparison.OrdinalIgnoreCase);

            _isYaml               = isYaml;
            CbFormat.SelectedIndex = isYaml ? 0 : 1;
            _currentFile          = path;
            TxtEditor.Text        = text;

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
        var dlg = new SaveFileDialog
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
            Path.GetDirectoryName(path)!,
            Path.GetFileName(path))
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
                TxtEditor.Text = File.ReadAllText(e.FullPath, System.Text.Encoding.UTF8);
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
                // 현재 라우트 적용
                _server.ApplyRoutes(_routes);
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
