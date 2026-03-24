using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ApiProbe.Models;
using ApiProbe.Services;
using Microsoft.Win32;

namespace ApiProbe;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 상태 ────────────────────────────────────────────────────
    private ObservableCollection<ApiCollection> _collections = [];
    private ApiRequest?    _activeRequest;
    private ApiCollection? _activeCollection;
    private Guid?          _renamingCollectionId;
    private Guid?          _renamingRequestId;
    private bool           _loading;

    private List<EnvPreset> _envPresets = [];
    private DispatcherTimer? _saveTimer;

    private const string RespPlaceholder = "← 요청을 전송하면 응답이 여기에 표시됩니다.";

    private record HistoryEntry(string Summary, string Timestamp, string Body, string Headers);
    private readonly List<HistoryEntry> _history = [];
    private const int MaxHistory = 20;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _envPresets = EnvService.Load();
        ReloadEnvCombo();

        _collections = CollectionService.Load();
        if (_collections.Count == 0)
        {
            var demo = new ApiCollection { Name = "예시 컬렉션" };
            demo.Requests.Add(new ApiRequest
            {
                Name   = "GET httpbin",
                Method = "GET",
                Url    = "https://httpbin.org/get"
            });
            _collections.Add(demo);
        }
        RefreshSidebar();
    }

    // ── 사이드바 ─────────────────────────────────────────────────
    private void RefreshSidebar()
    {
        CollectionPanel.Children.Clear();

        foreach (var col in _collections)
        {
            var colRef         = col;
            bool colRenaming   = _renamingCollectionId == col.Id;

            // ── 컬렉션 헤더 ──────────────────────────────────────
            var hGrid = new Grid();
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (colRenaming)
            {
                var tb = MakeRenameBox(col.Name, (SolidColorBrush)FindResource("AccentBrush"));
                Grid.SetColumn(tb, 0);
                hGrid.Children.Add(tb);

                bool committed = false;
                void Commit()
                {
                    if (committed) return;
                    committed = true;
                    _renamingCollectionId = null;
                    var name = tb.Text.Trim();
                    if (!string.IsNullOrEmpty(name)) colRef.Name = name;
                    CollectionService.Save(_collections);
                    RefreshSidebar();
                }
                tb.KeyDown  += (_, e) =>
                {
                    if (e.Key == Key.Enter)  { e.Handled = true; Commit(); }
                    if (e.Key == Key.Escape) { _renamingCollectionId = null; RefreshSidebar(); }
                };
                tb.LostFocus += (_, _) => Commit();
                Dispatcher.BeginInvoke(() => { tb.Focus(); tb.SelectAll(); },
                    System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                var label = new TextBlock
                {
                    Text              = $"▸ {col.Name}",
                    Foreground        = (SolidColorBrush)FindResource("AccentBrush"),
                    FontWeight        = FontWeights.SemiBold,
                    FontSize          = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(label, 0);
                hGrid.Children.Add(label);
            }

            var addReqBtn = new Button
            {
                Content           = "+",
                Width             = 22,
                FontSize          = 15,
                Padding           = new Thickness(0),
                BorderThickness   = new Thickness(0),
                Background        = Brushes.Transparent,
                Foreground        = (SolidColorBrush)FindResource("FgDimBrush"),
                Cursor            = Cursors.Hand,
                ToolTip           = "이 컬렉션에 요청 추가",
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 4, 0),
            };
            Grid.SetColumn(addReqBtn, 1);
            addReqBtn.Click += (_, _) => AddRequestToCollection(colRef);
            hGrid.Children.Add(addReqBtn);

            var colBorder = new Border
            {
                Background = Brushes.Transparent,
                Padding    = new Thickness(10, 6, 4, 6),
                Cursor     = Cursors.Hand,
                Child      = hGrid,
            };

            colBorder.MouseEnter += (_, _) =>
                colBorder.Background = (SolidColorBrush)FindResource("HoverBrush");
            colBorder.MouseLeave += (_, _) =>
                colBorder.Background = Brushes.Transparent;

            colBorder.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 1)  _activeCollection = colRef;
                if (e.ClickCount == 2 && !colRenaming)
                {
                    _renamingCollectionId = colRef.Id;
                    RefreshSidebar();
                }
            };

            // 우클릭 컨텍스트 메뉴
            colBorder.ContextMenu = MakeContextMenu(
                ("이름 변경", () => { _renamingCollectionId = colRef.Id; RefreshSidebar(); }),
                (null, null), // 구분선
                ("컬렉션 삭제", () => DeleteCollection(colRef))
            );

            CollectionPanel.Children.Add(colBorder);

            // ── 요청 목록 ────────────────────────────────────────
            var reqPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 4) };

            foreach (var req in col.Requests)
            {
                var r           = req;
                bool reqRenaming = _renamingRequestId == r.Id;
                bool isActive   = ReferenceEquals(_activeRequest, r);

                var btn = new Border
                {
                    Background   = isActive
                        ? (SolidColorBrush)FindResource("Bg3Brush")
                        : Brushes.Transparent,
                    Padding      = new Thickness(8, 5, 8, 5),
                    Cursor       = Cursors.Hand,
                    CornerRadius = new CornerRadius(4),
                    Margin       = new Thickness(0, 1, 0, 1),
                };

                // 요청 항목 내부: 메서드 라벨 + 이름
                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var methodLabel = new TextBlock
                {
                    Text              = MethodLabel(r.Method),
                    Foreground        = MethodBrush(r.Method),
                    FontSize          = 11,
                    FontWeight        = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(methodLabel, 0);
                itemGrid.Children.Add(methodLabel);

                if (reqRenaming)
                {
                    var tb = MakeRenameBox(r.Name, (SolidColorBrush)FindResource("FgBrush"));
                    Grid.SetColumn(tb, 1);
                    itemGrid.Children.Add(tb);

                    bool committed = false;
                    void Commit()
                    {
                        if (committed) return;
                        committed = true;
                        _renamingRequestId = null;
                        var name = tb.Text.Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            r.Name = name;
                            if (ReferenceEquals(_activeRequest, r))
                            {
                                _loading = true;
                                TxtReqName.Text = name;
                                _loading = false;
                            }
                        }
                        CollectionService.Save(_collections);
                        RefreshSidebar();
                    }
                    tb.KeyDown  += (_, e) =>
                    {
                        if (e.Key == Key.Enter)  { e.Handled = true; Commit(); }
                        if (e.Key == Key.Escape) { _renamingRequestId = null; RefreshSidebar(); }
                    };
                    tb.LostFocus += (_, _) => Commit();
                    Dispatcher.BeginInvoke(() => { tb.Focus(); tb.SelectAll(); },
                        System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    var nameLabel = new TextBlock
                    {
                        Text              = r.Name,
                        Foreground        = (SolidColorBrush)FindResource("FgBrush"),
                        FontSize          = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(nameLabel, 1);
                    itemGrid.Children.Add(nameLabel);
                }

                btn.Child = itemGrid;

                btn.MouseEnter += (_, _) =>
                    btn.Background = (SolidColorBrush)FindResource("HoverBrush");
                btn.MouseLeave += (_, _) =>
                    btn.Background = ReferenceEquals(_activeRequest, r)
                        ? (SolidColorBrush)FindResource("Bg3Brush")
                        : Brushes.Transparent;

                btn.MouseLeftButtonDown += (_, e) =>
                {
                    if (e.ClickCount == 2 && !reqRenaming)
                    {
                        _renamingRequestId = r.Id;
                        RefreshSidebar();
                        e.Handled = true;
                    }
                };
                btn.MouseLeftButtonUp += (_, e) =>
                {
                    if (!reqRenaming && e.ClickCount <= 1)
                    {
                        _activeCollection = colRef;
                        LoadRequest(r);
                    }
                };

                // 우클릭 컨텍스트 메뉴
                btn.ContextMenu = MakeContextMenu(
                    ("이름 변경", () => { _renamingRequestId = r.Id; RefreshSidebar(); }),
                    ("복제",      () => DuplicateRequest(colRef, r)),
                    (null, null),
                    ("요청 삭제", () => DeleteRequest(colRef, r))
                );

                reqPanel.Children.Add(btn);
            }
            CollectionPanel.Children.Add(reqPanel);
        }
    }

    // ── 헬퍼: 인라인 이름 변경 TextBox ──────────────────────────
    private TextBox MakeRenameBox(string text, SolidColorBrush fg) => new()
    {
        Text              = text,
        FontSize          = 12,
        FontWeight        = FontWeights.SemiBold,
        Background        = (SolidColorBrush)FindResource("Bg3Brush"),
        Foreground        = fg,
        BorderThickness   = new Thickness(0, 0, 0, 1),
        BorderBrush       = (SolidColorBrush)FindResource("AccentBrush"),
        Padding           = new Thickness(2),
        VerticalAlignment = VerticalAlignment.Center,
    };

    // ── 헬퍼: 다크 컨텍스트 메뉴 생성 ───────────────────────────
    // null 항목은 구분선(Separator)으로 처리
    private ContextMenu MakeContextMenu(params (string? Label, Action? Action)[] items)
    {
        var menu = new ContextMenu();
        foreach (var (label, action) in items)
        {
            if (label is null)
            {
                menu.Items.Add(new Separator());
            }
            else
            {
                var item = new MenuItem { Header = label };
                if (action is not null) item.Click += (_, _) => action();
                menu.Items.Add(item);
            }
        }
        return menu;
    }

    // ── 삭제 ─────────────────────────────────────────────────────
    private void DeleteCollection(ApiCollection col)
    {
        var result = MessageBox.Show(
            $"'{col.Name}' 컬렉션과 모든 요청({col.Requests.Count}개)을 삭제하시겠습니까?",
            "컬렉션 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        if (ReferenceEquals(_activeCollection, col))
        {
            _activeCollection = null;
            _activeRequest    = null;
            ClearEditor();
        }
        _collections.Remove(col);
        CollectionService.Save(_collections);
        RefreshSidebar();
    }

    private void DuplicateRequest(ApiCollection col, ApiRequest src)
    {
        var copy = new ApiRequest
        {
            Name        = src.Name + " (복사)",
            Method      = src.Method,
            Url         = src.Url,
            Body        = src.Body,
            ContentType = src.ContentType,
            Headers     = new System.Collections.ObjectModel.ObservableCollection<HeaderItem>(
                src.Headers.Select(h => new HeaderItem { Key = h.Key, Value = h.Value, Enabled = h.Enabled }))
        };
        var idx = col.Requests.IndexOf(src);
        col.Requests.Insert(idx + 1, copy);
        CollectionService.Save(_collections);
        RefreshSidebar();
        LoadRequest(copy);
    }

    private void DeleteRequest(ApiCollection col, ApiRequest req)
    {
        if (ReferenceEquals(_activeRequest, req))
        {
            _activeRequest = null;
            ClearEditor();
        }
        col.Requests.Remove(req);
        CollectionService.Save(_collections);
        RefreshSidebar();
    }

    private void ClearEditor()
    {
        _loading        = true;
        TxtUrl.Text     = "";
        TxtBody.Text    = "";
        TxtReqName.Text = "";
        HeaderGrid.DataContext = null;
        ParamGrid.DataContext  = null;
        _loading = false;
    }

    // ── 기타 ─────────────────────────────────────────────────────
    private static string MethodLabel(string m) => m switch
    {
        "DELETE"  => "DEL",
        "OPTIONS" => "OPT",
        _         => m,
    };

    private static readonly Dictionary<string, SolidColorBrush> _methodBrushCache = new()
    {
        ["GET"]    = new SolidColorBrush(Color.FromRgb(0x14, 0xB8, 0xA6)),
        ["POST"]   = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        ["PUT"]    = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        ["PATCH"]  = new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
        ["DELETE"] = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
    };

    private SolidColorBrush MethodBrush(string m) =>
        _methodBrushCache.TryGetValue(m, out var brush)
            ? brush
            : (SolidColorBrush)FindResource("FgDimBrush");

    private void LoadRequest(ApiRequest req)
    {
        _loading       = true;
        _activeRequest = req;

        CmbMethod.SelectedItem = CmbMethod.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Content == req.Method)
            ?? CmbMethod.Items[0];

        TxtUrl.Text     = req.Url;
        TxtBody.Text    = req.Body;
        TxtReqName.Text = req.Name;

        CmbContentType.SelectedItem = CmbContentType.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Content == req.ContentType)
            ?? CmbContentType.Items[0];

        HeaderGrid.DataContext = req;
        ParamGrid.DataContext  = req;
        _loading = false;

        RefreshSidebar();
    }

    // ── 사이드바 버튼 ─────────────────────────────────────────────
    private void AddCollection(object s, RoutedEventArgs e)
    {
        var col = new ApiCollection { Name = "새 컬렉션" };
        _collections.Add(col);
        _activeCollection     = col;
        _renamingCollectionId = col.Id;
        CollectionService.Save(_collections);
        RefreshSidebar();
    }

    private void AddRequest(object s, RoutedEventArgs e)
    {
        if (_collections.Count == 0) AddCollection(s, e);
        var target = _activeCollection ?? _collections[0];
        AddRequestToCollection(target);
    }

    private void AddRequestToCollection(ApiCollection col)
    {
        var req = new ApiRequest { Name = "새 요청" };
        col.Requests.Add(req);
        _activeCollection = col;
        CollectionService.Save(_collections);
        RefreshSidebar();
        LoadRequest(req);
    }

    // ── 요청 전송 ─────────────────────────────────────────────────
    private async void SendRequest(object s, RoutedEventArgs e)
    {
        SyncActiveRequest();

        var url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        // 활성화된 쿼리 파라미터를 URL에 합산
        var enabledParams = _activeRequest?.Params.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key)).ToList() ?? [];
        if (enabledParams.Count > 0)
        {
            var qs = string.Join("&", enabledParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            url = url.Contains('?') ? $"{url}&{qs}" : $"{url}?{qs}";
        }

        var req = new ApiRequest
        {
            Method      = ((ComboBoxItem)CmbMethod.SelectedItem).Content.ToString()!,
            Url         = url,
            Body        = TxtBody.Text,
            ContentType = ((ComboBoxItem)CmbContentType.SelectedItem).Content.ToString()!,
            Headers     = _activeRequest?.Headers ?? []
        };

        var envIdx  = CmbEnv.SelectedIndex;
        var envVars = envIdx > 0 && envIdx - 1 < _envPresets.Count
            ? _envPresets[envIdx - 1].Variables
            : [];

        TxtRespBody.Text       = "요청 전송 중...";
        TxtRespHeaders.Text    = "";
        StatusBadge.Visibility = Visibility.Collapsed;
        TxtElapsed.Text        = "";

        var result = await HttpService.SendAsync(req, envVars);

        TxtRespBody.Text    = result.Body;
        TxtRespHeaders.Text = result.Headers;
        TxtElapsed.Text     = $"{result.ElapsedMs} ms";

        StatusBadge.Visibility = Visibility.Visible;
        TxtStatus.Text = $"{result.StatusCode} {result.StatusText}";
        StatusBadge.Background = result.StatusCode switch
        {
            >= 200 and < 300 => new SolidColorBrush(Color.FromRgb(0x14, 0x78, 0x20)),
            >= 300 and < 400 => new SolidColorBrush(Color.FromRgb(0x78, 0x5A, 0x00)),
            >= 400 and < 500 => new SolidColorBrush(Color.FromRgb(0x78, 0x14, 0x14)),
            >= 500           => new SolidColorBrush(Color.FromRgb(0x60, 0x00, 0x00)),
            _                => new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x55))
        };
        TxtStatus.Foreground = Brushes.White;

        if (_activeRequest != null)
        {
            _activeRequest.Url    = url;
            _activeRequest.Body   = TxtBody.Text;
            _activeRequest.Method = req.Method;
            CollectionService.Save(_collections);
        }

        // 히스토리 기록
        var entry = new HistoryEntry(
            Summary:   $"[{result.StatusCode}] {req.Method} {TxtUrl.Text.Trim()}",
            Timestamp: DateTime.Now.ToString("HH:mm:ss"),
            Body:      result.Body,
            Headers:   result.Headers);
        _history.Insert(0, entry);
        if (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);
        LstHistory.ItemsSource = null;
        LstHistory.ItemsSource = _history;
    }

    private void HistoryItem_Selected(object s, SelectionChangedEventArgs e)
    {
        if (LstHistory.SelectedItem is HistoryEntry entry)
            TxtHistoryBody.Text = $"// {entry.Summary}  ({entry.Timestamp})\n\n{entry.Body}";
    }

    private void TxtUrl_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SendRequest(s, new RoutedEventArgs());
    }

    private void CopyCurl(object s, RoutedEventArgs e)
    {
        SyncActiveRequest();
        var req = new ApiRequest
        {
            Method      = ((ComboBoxItem)CmbMethod.SelectedItem).Content.ToString()!,
            Url         = TxtUrl.Text,
            Body        = TxtBody.Text,
            ContentType = ((ComboBoxItem)CmbContentType.SelectedItem).Content.ToString()!,
            Headers     = _activeRequest?.Headers ?? []
        };
        Clipboard.SetText(CurlConverter.Convert(req));

        if (s is Button btn)
        {
            btn.Content = "복사됨!";
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (_, _) => { t.Stop(); btn.Content = "cURL 복사"; };
            t.Start();
        }
    }

    private void CopyResponse(object s, RoutedEventArgs e)
    {
        var text = TxtRespBody.Text;
        if (!string.IsNullOrEmpty(text) && text != RespPlaceholder)
            Clipboard.SetText(text);
    }

    private void ReqName_Changed(object s, TextChangedEventArgs e)
    {
        if (_loading || _activeRequest is null) return;
        _activeRequest.Name = TxtReqName.Text;
        RefreshSidebar();

        _saveTimer?.Stop();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer!.Stop(); CollectionService.Save(_collections); };
        _saveTimer.Start();
    }

    private void ReloadEnvCombo()
    {
        var prev = CmbEnv.SelectedIndex;
        CmbEnv.ItemsSource = new[] { "없음" }
            .Concat(_envPresets.Select(p => p.Name))
            .ToList();
        CmbEnv.SelectedIndex = Math.Clamp(prev, 0, CmbEnv.Items.Count - 1);
    }

    private void EditEnv(object s, RoutedEventArgs e)
    {
        var editor = new EnvEditorWindow(_envPresets) { Owner = this };
        editor.ShowDialog();
        _envPresets = EnvService.Load();
        ReloadEnvCombo();
    }

    private void CmbEnv_Changed(object s, SelectionChangedEventArgs e) { }

    private void AddHeader(object s, RoutedEventArgs e)
    {
        if (_activeRequest is null) return;
        _activeRequest.Headers.Add(new HeaderItem { Key = "Authorization", Value = "Bearer " });
    }

    private void AddParam(object s, RoutedEventArgs e)
    {
        if (_activeRequest is null) return;
        _activeRequest.Params.Add(new QueryParam { Key = "", Value = "" });
    }

    private void ExportCollection(object s, RoutedEventArgs e)
    {
        if (_collections.Count == 0)
        {
            MessageBox.Show("내보낼 컬렉션이 없습니다.", "내보내기",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new SaveFileDialog
        {
            Title      = "컬렉션 내보내기",
            Filter     = "JSON 파일 (*.json)|*.json",
            FileName   = "api-probe-collections.json",
            DefaultExt = "json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = JsonSerializer.Serialize(_collections,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dlg.FileName, json);
            MessageBox.Show($"컬렉션 {_collections.Count}개를 저장했습니다.", "내보내기 완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportCollection(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "컬렉션 가져오기",
            Filter = "JSON 파일 (*.json)|*.json"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json    = File.ReadAllText(dlg.FileName);
            var imported = JsonSerializer.Deserialize<List<ApiCollection>>(json);
            if (imported is null or { Count: 0 })
            {
                MessageBox.Show("유효한 컬렉션 파일이 아닙니다.", "가져오기 실패",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            foreach (var col in imported)
                _collections.Add(col);
            CollectionService.Save(_collections);
            RefreshSidebar();
            MessageBox.Show($"컬렉션 {imported.Count}개를 가져왔습니다.", "가져오기 완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"가져오기 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void FormatJson(object s, RoutedEventArgs e)
    {
        var raw = TxtBody.Text;
        if (string.IsNullOrWhiteSpace(raw)) return;
        try
        {
            var obj = System.Text.Json.JsonSerializer.Deserialize<object>(raw);
            TxtBody.Text = System.Text.Json.JsonSerializer.Serialize(
                obj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            MessageBox.Show("유효한 JSON이 아닙니다.", "JSON 파싱 오류",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SyncActiveRequest()
    {
        if (_activeRequest is null) return;
        _activeRequest.Method = ((ComboBoxItem)CmbMethod.SelectedItem).Content.ToString()!;
        _activeRequest.Url    = TxtUrl.Text;
        _activeRequest.Body   = TxtBody.Text;
    }
}
