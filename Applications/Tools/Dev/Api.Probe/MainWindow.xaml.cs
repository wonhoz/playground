using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ApiProbe.Models;
using ApiProbe.Services;

namespace ApiProbe;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 상태 ────────────────────────────────────────────────────
    private ObservableCollection<ApiCollection> _collections = [];
    private ApiRequest?   _activeRequest;
    private ApiCollection? _activeCollection;
    private Guid?         _renamingCollectionId;
    private bool          _loading;

    private static readonly List<EnvPreset> _envPresets =
    [
        new() { Name = "없음", Variables = [] },
        new() { Name = "Local",  Variables = new() { ["BASE_URL"] = "http://localhost:3000" } },
        new() { Name = "Dev",    Variables = new() { ["BASE_URL"] = "https://dev.example.com" } },
        new() { Name = "Prod",   Variables = new() { ["BASE_URL"] = "https://api.example.com" } },
    ];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        // 환경변수 ComboBox
        CmbEnv.ItemsSource   = _envPresets.Select(p => p.Name).ToList();
        CmbEnv.SelectedIndex = 0;

        // 컬렉션 로드
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
            var colRef    = col;
            bool renaming = _renamingCollectionId == col.Id;

            // ── 컬렉션 헤더 그리드 ──────────────────────────────
            var hGrid = new Grid();
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 이름 영역: 이름변경 중이면 TextBox, 아니면 TextBlock
            if (renaming)
            {
                var tb = new TextBox
                {
                    Text              = col.Name,
                    FontSize          = 13,
                    FontWeight        = FontWeights.SemiBold,
                    Background        = (SolidColorBrush)FindResource("Bg3Brush"),
                    Foreground        = (SolidColorBrush)FindResource("AccentBrush"),
                    BorderThickness   = new Thickness(0, 0, 0, 1),
                    BorderBrush       = (SolidColorBrush)FindResource("AccentBrush"),
                    Padding           = new Thickness(2),
                    VerticalAlignment = VerticalAlignment.Center,
                };
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

                // 레이아웃 완료 후 포커스
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
                    ToolTip           = "더블클릭: 이름 변경",
                };
                Grid.SetColumn(label, 0);
                hGrid.Children.Add(label);
            }

            // [+] 요청 추가 버튼
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

            // 헤더 컨테이너
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
                if (e.ClickCount == 1)
                {
                    // 컬렉션 선택 (싱글 클릭)
                    _activeCollection = colRef;
                }
                else if (e.ClickCount == 2 && !renaming)
                {
                    // 이름 변경 시작 (더블 클릭)
                    _renamingCollectionId = colRef.Id;
                    RefreshSidebar();
                }
            };

            CollectionPanel.Children.Add(colBorder);

            // ── 요청 목록 ────────────────────────────────────────
            var reqPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 4) };
            foreach (var req in col.Requests)
            {
                var r = req;
                bool isActive = ReferenceEquals(_activeRequest, r);

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

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text              = MethodLabel(r.Method),
                    Foreground        = MethodBrush(r.Method),
                    FontSize          = 11,
                    FontWeight        = FontWeights.Bold,
                    Width             = 46,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                sp.Children.Add(new TextBlock
                {
                    Text              = r.Name,
                    Foreground        = (SolidColorBrush)FindResource("FgBrush"),
                    FontSize          = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                btn.Child = sp;

                btn.MouseEnter += (_, _) =>
                    btn.Background = (SolidColorBrush)FindResource("HoverBrush");
                btn.MouseLeave += (_, _) =>
                    btn.Background = ReferenceEquals(_activeRequest, r)
                        ? (SolidColorBrush)FindResource("Bg3Brush")
                        : Brushes.Transparent;
                btn.MouseLeftButtonUp += (_, _) =>
                {
                    _activeCollection = colRef;
                    LoadRequest(r);
                };

                reqPanel.Children.Add(btn);
            }
            CollectionPanel.Children.Add(reqPanel);
        }
    }

    private static string MethodLabel(string m) => m switch
    {
        "DELETE"  => "DEL",
        "OPTIONS" => "OPT",
        _         => m,
    };

    private SolidColorBrush MethodBrush(string m) => m switch
    {
        "GET"     => new SolidColorBrush(Color.FromRgb(0x14, 0xB8, 0xA6)),
        "POST"    => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        "PUT"     => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        "PATCH"   => new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
        "DELETE"  => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        _         => (SolidColorBrush)FindResource("FgDimBrush"),
    };

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
        _loading = false;

        RefreshSidebar(); // 활성 요청 하이라이트 갱신
    }

    // ── 사이드바 버튼 ─────────────────────────────────────────────
    private void AddCollection(object s, RoutedEventArgs e)
    {
        var col = new ApiCollection { Name = "새 컬렉션" };
        _collections.Add(col);
        _activeCollection = col;
        CollectionService.Save(_collections);

        // 즉시 이름 변경 모드로 진입
        _renamingCollectionId = col.Id;
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

        var req = new ApiRequest
        {
            Method      = ((ComboBoxItem)CmbMethod.SelectedItem).Content.ToString()!,
            Url         = url,
            Body        = TxtBody.Text,
            ContentType = ((ComboBoxItem)CmbContentType.SelectedItem).Content.ToString()!,
            Headers     = _activeRequest?.Headers ?? []
        };

        var envVars = _envPresets[CmbEnv.SelectedIndex].Variables;

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
    }

    private void TxtUrl_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SendRequest(s, new RoutedEventArgs());
    }

    // ── cURL 복사 ─────────────────────────────────────────────────
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
        var curl = CurlConverter.Convert(req);
        Clipboard.SetText(curl);
        MessageBox.Show("cURL 명령어가 클립보드에 복사되었습니다.", "복사 완료",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyResponse(object s, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtRespBody.Text))
            Clipboard.SetText(TxtRespBody.Text);
    }

    // ── 이벤트 ─────────────────────────────────────────────────────
    private void ReqName_Changed(object s, TextChangedEventArgs e)
    {
        if (_loading || _activeRequest is null) return;
        _activeRequest.Name = TxtReqName.Text;
        CollectionService.Save(_collections);
        RefreshSidebar();
    }

    private void CmbEnv_Changed(object s, SelectionChangedEventArgs e) { }

    private void AddHeader(object s, RoutedEventArgs e)
    {
        if (_activeRequest is null) return;
        _activeRequest.Headers.Add(new HeaderItem { Key = "Authorization", Value = "Bearer " });
    }

    private void SyncActiveRequest()
    {
        if (_activeRequest is null) return;
        _activeRequest.Method = ((ComboBoxItem)CmbMethod.SelectedItem).Content.ToString()!;
        _activeRequest.Url    = TxtUrl.Text;
        _activeRequest.Body   = TxtBody.Text;
    }
}
