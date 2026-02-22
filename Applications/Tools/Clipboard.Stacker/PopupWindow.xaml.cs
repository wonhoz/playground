using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using ClipboardStacker.Models;
using ClipboardStacker.Services;

namespace ClipboardStacker;

public partial class PopupWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly ClipboardStack _stack;
    private readonly AppSettings    _settings;
    private readonly Action<string> _pasteCallback; // App에 붙여넣기 요청

    public PopupWindow(ClipboardStack stack, AppSettings settings, Action<string> pasteCallback)
    {
        _stack         = stack;
        _settings      = settings;
        _pasteCallback = pasteCallback;

        InitializeComponent();

        _stack.Changed += () => Dispatcher.Invoke(Rebuild);
        Loaded         += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        LoadWindowIcon(hwnd);
        Rebuild();
    }

    private void LoadWindowIcon(IntPtr hwnd)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", IconGenerator.IconFileName);
            if (File.Exists(path))
            {
                using var s = File.OpenRead(path);
                Icon = BitmapFrame.Create(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }
        catch { }
    }

    // ── 화면 우하단(트레이 근처)에 팝업 표시 ────────────────────────
    public void ShowPopup()
    {
        Rebuild();
        var area = SystemParameters.WorkArea;
        Left = area.Right  - Width  - 8;
        Top  = area.Bottom - ActualHeight - 8;
        Show();
        Activate();
        // ActualHeight가 아직 0일 수 있으므로 레이아웃 후 재조정
        Dispatcher.InvokeAsync(() =>
        {
            Top = area.Bottom - ActualHeight - 8;
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void HidePopup() => Hide();

    public void TogglePopup()
    {
        if (IsVisible) HidePopup();
        else           ShowPopup();
    }

    // ── UI 재빌드 ─────────────────────────────────────────────────
    private void Rebuild()
    {
        BuildStackPanel();
        BuildPinnedPanel();
        SyncTransformButtons();
    }

    private void BuildStackPanel()
    {
        StackPanel.Children.Clear();
        var items = _stack.Items;
        StackHeader.Text = $"스택 ({items.Count}개)";
        EmptyHint.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        for (int i = 0; i < items.Count; i++)
        {
            var entry = items[i];
            int  idx  = i;

            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 순번 (1 = 다음에 붙여넣기)
            int order = items.Count - idx; // 스택 뒤쪽이 순서 1번
            var numBlock = new TextBlock
            {
                Text = $"{order}",
                FontSize = 10,
                Foreground = new SolidColorBrush(
                    order == 1 ? Color.FromRgb(0x4F, 0xC3, 0xF7) : Color.FromRgb(0x44, 0x44, 0x66)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(numBlock, 0);

            // 텍스트 버튼 (클릭 → 붙여넣기)
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = entry.Preview,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                Style = (Style)Resources["ClipItemBtn"],
                ToolTip = entry.Text.Length > 100 ? entry.Text[..100] + "..." : entry.Text,
            };
            btn.Click += (_, _) => PasteEntry(entry);
            Grid.SetColumn(btn, 1);

            // 핀 버튼
            var pinBtn = new Button
            {
                Content = "📌",
                Style   = (Style)Resources["IconBtn"],
                FontSize = 12,
                ToolTip = "즐겨찾기에 추가",
            };
            pinBtn.Click += (_, _) => PinEntry(entry);
            Grid.SetColumn(pinBtn, 2);

            row.Children.Add(numBlock);
            row.Children.Add(btn);
            row.Children.Add(pinBtn);
            StackPanel.Children.Add(row);
        }
    }

    private void BuildPinnedPanel()
    {
        PinnedPanel.Children.Clear();
        var pinned = _settings.Pinned;
        NoPinnedHint.Visibility = pinned.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var pin in pinned)
        {
            var p = pin; // capture
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = string.IsNullOrEmpty(p.Name) ? p.Text : $"⭐ {p.Name}",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                Style   = (Style)Resources["ClipItemBtn"],
                ToolTip = p.Text,
            };
            btn.Click += (_, _) => PasteText(p.Text);
            Grid.SetColumn(btn, 0);

            var removeBtn = new Button { Content = "✕", Style = (Style)Resources["IconBtn"] };
            removeBtn.Click += (_, _) =>
            {
                _settings.Pinned.Remove(p);
                SettingsService.Save(_settings);
                BuildPinnedPanel();
            };
            Grid.SetColumn(removeBtn, 1);

            row.Children.Add(btn);
            row.Children.Add(removeBtn);
            PinnedPanel.Children.Add(row);
        }
    }

    private void SyncTransformButtons()
    {
        BtnNone.IsChecked  = _settings.Transform == TransformMode.None;
        BtnUpper.IsChecked = _settings.Transform == TransformMode.Upper;
        BtnLower.IsChecked = _settings.Transform == TransformMode.Lower;
        BtnTrim.IsChecked  = _settings.Transform == TransformMode.Trim;
    }

    // ── 붙여넣기 동작 ─────────────────────────────────────────────
    private void PasteEntry(ClipEntry entry)
    {
        _stack.Remove(entry);
        PasteText(entry.Text);
        if (_stack.Count == 0) HidePopup();
    }

    private void PasteText(string text)
    {
        var transformed = ApplyTransform(text);
        HidePopup();
        _pasteCallback(transformed);
    }

    private string ApplyTransform(string text) => _settings.Transform switch
    {
        TransformMode.Upper => text.ToUpperInvariant(),
        TransformMode.Lower => text.ToLowerInvariant(),
        TransformMode.Trim  => text.Trim(),
        _                   => text,
    };

    private void PinEntry(ClipEntry entry)
    {
        _settings.Pinned.Add(new PinnedItem { Name = "", Text = entry.Text });
        SettingsService.Save(_settings);
        BuildPinnedPanel();
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────
    private void Window_Deactivated(object sender, EventArgs e) => HidePopup();
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => HidePopup();

    private void ClearStack_Click(object sender, RoutedEventArgs e)
    {
        _stack.Clear();
        Rebuild();
    }

    private void Transform_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.ToggleButton btn &&
            btn.Tag is string tag &&
            Enum.TryParse<TransformMode>(tag, out var mode))
        {
            _settings.Transform = mode;
            SettingsService.Save(_settings);
            SyncTransformButtons();
        }
    }
}
