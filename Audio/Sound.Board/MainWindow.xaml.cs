using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SoundBoard.Dialogs;
using SoundBoard.Models;
using SoundBoard.Services;

namespace SoundBoard;

public partial class MainWindow : Window
{
    private readonly BoardSettings      _settings;
    private readonly AudioService       _audio;
    private GlobalHotkeyService?        _hotkeys;
    private readonly Dictionary<Guid, int> _hotkeyIds = [];

    private bool _editMode;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsService.Load();
        _audio    = new AudioService
        {
            Volume       = _settings.Volume,
            OverlapSounds = _settings.OverlapSounds
        };

        VolumeSlider.Value     = _settings.Volume;
        OverlapCheck.IsChecked = _settings.OverlapSounds;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바 적용
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            int value = 1;
            DwmSetWindowAttribute(hwndSource.Handle, 20, ref value, sizeof(int));
        }

        // 전역 단축키 서비스 초기화 (메인 창 HWND 이용)
        var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        src?.AddHook(WndProc);
        _hotkeys = new GlobalHotkeyService(new WindowInteropHelper(this).Handle);

        RebuildBoard();
    }

    // ── 전역 단축키 WndProc ───────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.WM_HOTKEY && _hotkeys is not null)
            handled = _hotkeys.HandleMessage(wParam);
        return IntPtr.Zero;
    }

    // ── 보드 렌더링 ───────────────────────────────────────────────────────────

    private void RebuildBoard()
    {
        // 기존 단축키 전부 해제 후 재등록
        _hotkeys?.UnregisterAll();
        _hotkeyIds.Clear();

        ButtonBoard.Children.Clear();

        foreach (var btn in _settings.Buttons)
        {
            ButtonBoard.Children.Add(CreateCard(btn));

            // 단축키 등록
            if (btn.HasHotkey && _hotkeys is not null)
            {
                int id = _hotkeys.Register(btn.HotkeyMods, btn.HotkeyVk, () =>
                    Dispatcher.Invoke(() => _audio.Play(btn)));
                if (id >= 0) _hotkeyIds[btn.Id] = id;
            }
        }

        // 편집 모드일 때 [+ 새 버튼] 카드 추가
        if (_editMode)
            ButtonBoard.Children.Add(CreateAddPlaceholder());
    }

    // ── 사운드 카드 생성 ──────────────────────────────────────────────────────

    private UIElement CreateCard(SoundButton btn)
    {
        var bg = (Brush)new BrushConverter().ConvertFrom(btn.Color)!;

        // 버튼 내부 레이아웃
        var emoji = new TextBlock
        {
            Text = btn.Emoji, FontSize = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 2)
        };
        var name = new TextBlock
        {
            Text = btn.Name, FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center, MaxWidth = 110,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var hotkey = new TextBlock
        {
            Text = btn.HotkeyText, FontSize = 10, Opacity = 0.7,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 4)
        };

        var stack = new StackPanel { Margin = new Thickness(8) };
        stack.Children.Add(emoji);
        stack.Children.Add(name);
        stack.Children.Add(hotkey);

        // 편집 모드 오버레이 (수정/삭제 아이콘)
        var editOverlay = new Grid { Visibility = _editMode ? Visibility.Visible : Visibility.Collapsed };
        editOverlay.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
        var editBtns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };

        var editBtn   = MakeOverlayBtn("✏", "편집", () => EditButton(btn));
        var deleteBtn = MakeOverlayBtn("🗑", "삭제", () => DeleteButton(btn));
        editBtns.Children.Add(editBtn);
        editBtns.Children.Add(deleteBtn);
        editOverlay.Children.Add(editBtns);

        var root = new Grid();
        root.Children.Add(stack);
        root.Children.Add(editOverlay);

        var normalShadow = new DropShadowEffect { Color = Colors.Black, Opacity = 0.4,  BlurRadius = 8,  ShadowDepth = 2 };
        var hoverShadow  = new DropShadowEffect { Color = Colors.White, Opacity = 0.25, BlurRadius = 12, ShadowDepth = 0 };

        var card = new Border
        {
            Width = 130, Height = 110, Margin = new Thickness(6),
            CornerRadius = new CornerRadius(10), Background = bg,
            Cursor = _editMode ? Cursors.Arrow : Cursors.Hand,
            Effect = normalShadow,
            Child = root
        };

        if (!_editMode)
        {
            card.MouseLeftButtonDown += (_, _) => _audio.Play(btn);
            card.MouseEnter += (_, _) => card.Effect = hoverShadow;
            card.MouseLeave += (_, _) => card.Effect = normalShadow;
        }

        return card;
    }

    private static Button MakeOverlayBtn(string icon, string tip, Action onClick)
    {
        var b = new Button
        {
            Content = icon, FontSize = 20, Width = 44, Height = 44,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = tip, Margin = new Thickness(4)
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private UIElement CreateAddPlaceholder()
    {
        var tb = new TextBlock
        {
            Text = "+\n추가",
            FontSize = 22, TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 200, 200, 200)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            LineHeight = 28
        };
        var card = new Border
        {
            Width = 130, Height = 110, Margin = new Thickness(6),
            CornerRadius = new CornerRadius(10), Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(2),
            Child = tb
        };
        card.MouseLeftButtonDown += (_, _) => Add_Click(this, new RoutedEventArgs());
        return card;
    }

    // ── 버튼 CRUD ─────────────────────────────────────────────────────────────

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EditButtonDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _settings.Buttons.Add(dlg.Result);
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    private void EditButton(SoundButton btn)
    {
        var dlg = new EditButtonDialog(btn) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var idx = _settings.Buttons.FindIndex(b => b.Id == btn.Id);
        if (idx >= 0) _settings.Buttons[idx] = dlg.Result;
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    private void DeleteButton(SoundButton btn)
    {
        var res = MessageBox.Show($"'{btn.Name}' 버튼을 삭제하시겠습니까?",
            "Sound.Board", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        _settings.Buttons.RemoveAll(b => b.Id == btn.Id);
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    // ── 툴바 이벤트 ───────────────────────────────────────────────────────────

    private void EditMode_Changed(object sender, RoutedEventArgs e)
    {
        _editMode = EditModeToggle.IsChecked == true;
        RebuildBoard();
    }

    private void StopAll_Click(object sender, RoutedEventArgs e) => _audio.StopAll();

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_audio is null) return;
        _audio.Volume     = (float)e.NewValue;
        _settings.Volume  = (float)e.NewValue;
    }

    private void Overlap_Changed(object sender, RoutedEventArgs e)
    {
        if (_audio is null) return;
        _audio.OverlapSounds     = OverlapCheck.IsChecked == true;
        _settings.OverlapSounds  = _audio.OverlapSounds;
    }

    // ── 드래그앤드롭 오디오 파일 ──────────────────────────────────────────────

    private static readonly HashSet<string> AudioExts =
        [".mp3", ".wav", ".ogg", ".flac", ".aac", ".wma", ".m4a"];

    private void Board_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Board_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);

        foreach (var f in files.Where(f => AudioExts.Contains(
            System.IO.Path.GetExtension(f).ToLowerInvariant())))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(f);
            _settings.Buttons.Add(new SoundButton
            {
                Name     = name.Length > 20 ? name[..20] : name,
                FilePath = f,
                Color    = "#2C3E50"
            });
        }

        SettingsService.Save(_settings);
        RebuildBoard();
    }

    // ── 종료 ──────────────────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SettingsService.Save(_settings);
        _hotkeys?.Dispose();
        _audio.Dispose();
    }
}
