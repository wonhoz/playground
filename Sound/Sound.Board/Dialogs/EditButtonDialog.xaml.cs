using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SoundBoard.Models;
using SoundBoard.Services;

namespace SoundBoard.Dialogs;

public partial class EditButtonDialog : Window
{
    public SoundButton Result { get; private set; }

    private string _selectedColor;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private static readonly string[] Palette =
    [
        "#C0392B", "#E74C3C", "#D35400", "#E67E22", "#F39C12",
        "#27AE60", "#16A085", "#1ABC9C", "#2980B9", "#3498DB",
        "#8E44AD", "#9B59B6", "#2C3E50", "#7F8C8D", "#BDC3C7"
    ];

    private static readonly int MOD_CTRL  = GlobalHotkeyService.MOD_CONTROL;
    private static readonly int MOD_ALT   = GlobalHotkeyService.MOD_ALT;
    private static readonly int MOD_SHIFT = GlobalHotkeyService.MOD_SHIFT;

    public EditButtonDialog(SoundButton? src = null)
    {
        InitializeComponent();

        Result = src is null ? new SoundButton() : new SoundButton
        {
            Id         = src.Id,
            Name       = src.Name,
            Emoji      = src.Emoji,
            FilePath   = src.FilePath,
            BuiltInKey = src.BuiltInKey,
            Color      = src.Color,
            HotkeyVk   = src.HotkeyVk,
            HotkeyMods = src.HotkeyMods,
            HotkeyText = src.HotkeyText,
        };

        _selectedColor = Result.Color;

        // 폼 초기값
        NameBox.Text  = Result.Name;
        EmojiBox.Text = Result.Emoji;
        HotkeyBox.Text = Result.HotkeyText;

        // 내장 사운드 목록
        foreach (var kv in SoundSynthesizer.BuiltIns)
            BuiltInCombo.Items.Add(new ComboBoxItem
            {
                Tag     = kv.Key,
                Content = $"{kv.Value.Emoji}  {kv.Value.Name}"
            });

        // 소스 라디오 초기화
        if (Result.IsBuiltIn)
        {
            BuiltInRadio.IsChecked = true;
            SelectBuiltIn(Result.BuiltInKey);
        }
        else if (!string.IsNullOrEmpty(Result.FilePath))
        {
            FileRadio.IsChecked = true;
            FilePathBox.Text    = Result.FilePath;
        }
        else
        {
            BuiltInRadio.IsChecked = true;
            if (BuiltInCombo.Items.Count > 0) BuiltInCombo.SelectedIndex = 0;
        }

        BuildColorPanel();

        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                int val = 1;
                DwmSetWindowAttribute(source.Handle, 20, ref val, sizeof(int));
            }
        };
    }

    // ── 색상 팔레트 ───────────────────────────────────────────────────────────

    private void BuildColorPanel()
    {
        ColorPanel.Children.Clear();
        foreach (var hex in Palette)
        {
            var isSelected = string.Equals(hex, _selectedColor, StringComparison.OrdinalIgnoreCase);
            var border = new Border
            {
                Width        = 28, Height    = 28,
                CornerRadius = new CornerRadius(14),
                Margin       = new Thickness(3),
                Background   = (Brush)new BrushConverter().ConvertFrom(hex)!,
                BorderBrush  = isSelected ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 2.5 : 0),
                Cursor       = Cursors.Hand,
                Tag          = hex,
            };
            border.MouseLeftButtonDown += (_, _) =>
            {
                _selectedColor = (string)border.Tag;
                BuildColorPanel();
            };
            ColorPanel.Children.Add(border);
        }
    }

    // ── 소스 변경 ─────────────────────────────────────────────────────────────

    private void Source_Changed(object sender, RoutedEventArgs e)
    {
        if (BuiltInPanel is null) return;
        bool builtIn = BuiltInRadio.IsChecked == true;
        BuiltInPanel.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        FilePanel.Visibility    = builtIn ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BuiltIn_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (BuiltInCombo.SelectedItem is ComboBoxItem item)
        {
            var key = (string)item.Tag;
            if (SoundSynthesizer.BuiltIns.TryGetValue(key, out var info))
            {
                if (string.IsNullOrWhiteSpace(NameBox.Text) ||
                    SoundSynthesizer.BuiltIns.Values.Any(v => v.Name == NameBox.Text))
                    NameBox.Text = info.Name;
                if (string.IsNullOrWhiteSpace(EmojiBox.Text) ||
                    SoundSynthesizer.BuiltIns.Values.Any(v => v.Emoji == EmojiBox.Text))
                    EmojiBox.Text = info.Emoji;
            }
        }
    }

    private void SelectBuiltIn(string key)
    {
        foreach (ComboBoxItem item in BuiltInCombo.Items)
            if ((string)item.Tag == key) { BuiltInCombo.SelectedItem = item; return; }
        if (BuiltInCombo.Items.Count > 0) BuiltInCombo.SelectedIndex = 0;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "오디오 파일|*.mp3;*.wav;*.ogg;*.flac;*.aac;*.wma|모든 파일|*.*",
            Title  = "오디오 파일 선택"
        };
        if (dlg.ShowDialog() == true)
            FilePathBox.Text = dlg.FileName;
    }

    // ── 단축키 캡처 ───────────────────────────────────────────────────────────

    private void Hotkey_GotFocus(object sender, RoutedEventArgs e)  => HotkeyBox.Background = new SolidColorBrush(Color.FromRgb(60, 40, 70));
    private void Hotkey_LostFocus(object sender, RoutedEventArgs e) => HotkeyBox.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45));

    private void Hotkey_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 수식키만 누른 경우 무시
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        // Backspace / Delete → 단축키 제거
        if (key is Key.Back or Key.Delete)
        {
            Result.HotkeyVk   = 0;
            Result.HotkeyMods = 0;
            Result.HotkeyText = "";
            HotkeyBox.Text    = "";
            return;
        }

        // Escape → 닫기
        if (key == Key.Escape) { DialogResult = false; return; }

        int mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl))  mods |= MOD_CTRL;
        if (Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt))   mods |= MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= MOD_SHIFT;

        var vk = KeyInterop.VirtualKeyFromKey(key);

        var text = "";
        if ((mods & MOD_CTRL)  != 0) text += "Ctrl+";
        if ((mods & MOD_ALT)   != 0) text += "Alt+";
        if ((mods & MOD_SHIFT) != 0) text += "Shift+";
        text += key.ToString();

        Result.HotkeyVk   = vk;
        Result.HotkeyMods = mods;
        Result.HotkeyText = text;
        HotkeyBox.Text    = text;
    }

    // ── OK / Cancel ───────────────────────────────────────────────────────────

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("버튼 이름을 입력하세요.", "Sound.Board", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result.Name  = NameBox.Text.Trim();
        Result.Emoji = string.IsNullOrWhiteSpace(EmojiBox.Text) ? "🔊" : EmojiBox.Text.Trim();
        Result.Color = _selectedColor;

        if (BuiltInRadio.IsChecked == true)
        {
            Result.BuiltInKey = BuiltInCombo.SelectedItem is ComboBoxItem item ? (string)item.Tag : "ding";
            Result.FilePath   = "";
        }
        else
        {
            Result.FilePath   = FilePathBox.Text;
            Result.BuiltInKey = "";
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
