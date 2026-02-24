using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MockDesk.Models;
using MockDesk.Services;

namespace MockDesk;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly ObservableCollection<MockEndpoint> _endpoints = [];
    private readonly ObservableCollection<string>        _logItems  = [];
    private readonly MockServerService                   _server    = new();

    private MockEndpoint? _selected;
    private bool          _editorUpdating;

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

        // 요청 로그 콜백
        _server.OnRequest = entry =>
            Dispatcher.BeginInvoke(() =>
            {
                _logItems.Insert(0, entry.Display);
                if (_logItems.Count > 200) _logItems.RemoveAt(_logItems.Count - 1);
                LogList.ItemsSource = _logItems;
            });

        // 기본 엔드포인트
        _endpoints.Add(new MockEndpoint
        {
            Method = "GET", Path = "/api/users",
            StatusCode = 200,
            ResponseBody = "[\n  {\"id\": 1, \"name\": \"Alice\"},\n  {\"id\": 2, \"name\": \"Bob\"}\n]",
            Description = "사용자 목록"
        });
        _endpoints.Add(new MockEndpoint
        {
            Method = "POST", Path = "/api/users",
            StatusCode = 201,
            ResponseBody = "{\"id\": 3, \"name\": \"Charlie\", \"created\": true}",
            Description = "사용자 생성"
        });
        _endpoints.Add(new MockEndpoint
        {
            Method = "GET", Path = "/api/error",
            StatusCode = 500,
            ResponseBody = "{\"error\": \"Internal Server Error\"}",
            Description = "에러 시뮬레이션"
        });

        RefreshList();
    }

    // ── 엔드포인트 목록 ────────────────────────────────────────────
    private void RefreshList()
    {
        EndpointList.Items.Clear();
        foreach (var ep in _endpoints)
        {
            var item = new ListBoxItem { Tag = ep };
            item.Content = BuildEndpointRow(ep);
            EndpointList.Items.Add(item);
        }
        _server.SetEndpoints(_endpoints);
    }

    private UIElement BuildEndpointRow(MockEndpoint ep)
    {
        var panel = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Opacity = ep.Enabled ? 1.0 : 0.45
        };
        var sp = new StackPanel();

        var row1 = new StackPanel { Orientation = Orientation.Horizontal };
        row1.Children.Add(new TextBlock
        {
            Text       = ep.Method,
            Foreground = MethodBrush(ep.Method),
            FontWeight = FontWeights.Bold,
            FontSize   = 11,
            Width      = 46,
            VerticalAlignment = VerticalAlignment.Center
        });
        row1.Children.Add(new TextBlock
        {
            Text = ep.Path, FontSize = 12,
            Foreground = (SolidColorBrush)FindResource("FgBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        sp.Children.Add(row1);

        if (!string.IsNullOrEmpty(ep.Description))
        {
            sp.Children.Add(new TextBlock
            {
                Text       = ep.Description,
                FontSize   = 11,
                Foreground = (SolidColorBrush)FindResource("FgDimBrush"),
                Margin     = new Thickness(46, 2, 0, 0)
            });
        }

        // 상태 코드 뱃지
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(46, 2, 0, 0) };
        statusRow.Children.Add(new Border
        {
            Background  = StatusBrush(ep.StatusCode),
            CornerRadius = new CornerRadius(3),
            Padding     = new Thickness(4, 1, 4, 1),
            Child       = new TextBlock
            {
                Text     = ep.StatusCode.ToString(),
                FontSize = 10,
                Foreground = Brushes.White
            }
        });
        if (ep.DelayMs > 0)
        {
            statusRow.Children.Add(new TextBlock
            {
                Text     = $"  +{ep.DelayMs}ms",
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("FgDimBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        sp.Children.Add(statusRow);

        panel.Child = sp;
        return panel;
    }

    private SolidColorBrush MethodBrush(string m) => m switch
    {
        "GET"    => new SolidColorBrush(Color.FromRgb(0x14, 0xB8, 0xA6)),
        "POST"   => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
        "PUT"    => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
        "PATCH"  => new SolidColorBrush(Color.FromRgb(0xA7, 0x8B, 0xFA)),
        "DELETE" => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
        _        => (SolidColorBrush)FindResource("FgDimBrush")
    };

    private static SolidColorBrush StatusBrush(int code) => code switch
    {
        >= 200 and < 300 => new SolidColorBrush(Color.FromRgb(0x14, 0x78, 0x20)),
        >= 300 and < 400 => new SolidColorBrush(Color.FromRgb(0x78, 0x5A, 0x00)),
        >= 400 and < 500 => new SolidColorBrush(Color.FromRgb(0x78, 0x14, 0x14)),
        _                => new SolidColorBrush(Color.FromRgb(0x60, 0x00, 0x00))
    };

    // ── 엔드포인트 선택 ────────────────────────────────────────────
    private void EndpointList_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (EndpointList.SelectedItem is not ListBoxItem item) return;
        _selected = item.Tag as MockEndpoint;
        if (_selected is null) return;

        _editorUpdating = true;
        TxtEditorPlaceholder.Visibility = Visibility.Collapsed;
        EditorScroll.Visibility         = Visibility.Visible;

        ChkEnabled.IsChecked = _selected.Enabled;

        CmbEdMethod.SelectedItem = CmbEdMethod.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(c => (string)c.Content == _selected.Method)
            ?? CmbEdMethod.Items[0];

        TxtEdPath.Text  = _selected.Path;
        TxtEdDelay.Text = _selected.DelayMs.ToString();
        TxtEdDesc.Text  = _selected.Description;
        TxtEdBody.Text  = _selected.ResponseBody;

        // 상태 코드 — 앞 3자리로 매칭
        var sc = _selected.StatusCode.ToString();
        foreach (ComboBoxItem ci in CmbEdStatus.Items)
        {
            if (((string)ci.Content).StartsWith(sc))
            {
                CmbEdStatus.SelectedItem = ci;
                break;
            }
        }

        _editorUpdating = false;
    }

    // ── 에디터 변경 ─────────────────────────────────────────────────
    private void Editor_Changed(object s, RoutedEventArgs e)
    {
        if (_editorUpdating || _selected is null || !IsLoaded) return;
        SaveEditor();
    }

    private void Editor_Changed(object s, SelectionChangedEventArgs e)
    {
        if (_editorUpdating || _selected is null || !IsLoaded) return;
        SaveEditor();
    }

    private void SaveEditor()
    {
        if (_selected is null) return;
        _selected.Enabled  = ChkEnabled.IsChecked == true;
        _selected.Method   = ((ComboBoxItem)CmbEdMethod.SelectedItem).Content.ToString()!;
        _selected.Path     = TxtEdPath.Text.Trim();
        _selected.Description = TxtEdDesc.Text;
        _selected.ResponseBody = TxtEdBody.Text;

        var statusStr = ((ComboBoxItem)CmbEdStatus.SelectedItem)?.Content.ToString() ?? "200";
        _selected.StatusCode = int.TryParse(statusStr[..3], out var sc) ? sc : 200;

        _selected.DelayMs = int.TryParse(TxtEdDelay.Text, out var d) ? Math.Max(0, d) : 0;

        RefreshList();
        // 선택 복원
        foreach (ListBoxItem li in EndpointList.Items)
        {
            if (li.Tag == _selected) { EndpointList.SelectedItem = li; break; }
        }
    }

    // ── 엔드포인트 추가/삭제 ───────────────────────────────────────
    private void AddEndpoint(object s, RoutedEventArgs e)
    {
        var ep = new MockEndpoint();
        _endpoints.Add(ep);
        RefreshList();

        // 새 항목 선택
        var last = EndpointList.Items[^1] as ListBoxItem;
        if (last != null) EndpointList.SelectedItem = last;
    }

    private void DeleteEndpoint(object s, RoutedEventArgs e)
    {
        if (_selected is null) return;
        if (MessageBox.Show($"'{_selected.Method} {_selected.Path}' 엔드포인트를 삭제할까요?",
            "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _endpoints.Remove(_selected);
        _selected = null;
        TxtEditorPlaceholder.Visibility = Visibility.Visible;
        EditorScroll.Visibility         = Visibility.Collapsed;
        RefreshList();
        EndpointList.SelectedIndex = -1;
    }

    // ── 서버 토글 ─────────────────────────────────────────────────
    private async void ToggleServer(object s, RoutedEventArgs e)
    {
        if (_server.IsRunning)
        {
            await _server.StopAsync();
            BtnStart.Content = "▶ 시작";
            BtnStart.Style   = (Style)FindResource("AccentButton");
            TxtServerStatus.Text       = "● 중지됨";
            TxtServerStatus.Foreground = (SolidColorBrush)FindResource("FgDimBrush");
        }
        else
        {
            if (!int.TryParse(TxtPort.Text, out var port) || port < 1024 || port > 65535)
            {
                MessageBox.Show("올바른 포트 번호를 입력하세요 (1024~65535).", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _server.SetEndpoints(_endpoints);
                await _server.StartAsync(port);
                BtnStart.Content = "■ 중지";
                BtnStart.Style   = (Style)FindResource("DangerButton");
                TxtServerStatus.Text       = $"● http://localhost:{port}";
                TxtServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x14, 0xB8, 0xA6));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서버 시작 실패: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ── 로그 지우기 ───────────────────────────────────────────────
    private void ClearLog(object s, RoutedEventArgs e)
    {
        _logItems.Clear();
        LogList.ItemsSource = _logItems;
    }

    // ── JSON 내보내기 / 가져오기 ──────────────────────────────────
    private void ExportConfig(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "설정 파일 내보내기",
            Filter     = "JSON 파일|*.json",
            FileName   = "mock-endpoints.json"
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, MockServerService.ExportJson(_endpoints));
        MessageBox.Show("설정이 저장되었습니다.", "내보내기 완료",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ImportConfig(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "설정 파일 가져오기",
            Filter = "JSON 파일|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var list = MockServerService.ImportJson(json);
            _endpoints.Clear();
            foreach (var ep in list) _endpoints.Add(ep);
            RefreshList();
            TxtEditorPlaceholder.Visibility = Visibility.Visible;
            EditorScroll.Visibility         = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 파싱 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 창 닫기 ───────────────────────────────────────────────────
    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_server.IsRunning) await _server.StopAsync();
    }
}
