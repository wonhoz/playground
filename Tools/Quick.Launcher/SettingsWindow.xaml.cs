using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QuickLauncher.Models;
using QuickLauncher.Services;

namespace QuickLauncher;

/// <summary>ListBox 바인딩용 ViewModel 래퍼</summary>
public class CustomItemViewModel
{
    public string Name     { get; set; }
    public string Target   { get; set; }
    public bool   IsSnippet { get; set; }
    public string TypeIcon => IsSnippet ? "📋" : "🌐";

    public CustomItemViewModel(CustomItem item)
    {
        Name      = item.Name;
        Target    = item.Target;
        IsSnippet = item.IsSnippet;
    }

    public CustomItem ToModel() => new() { Name = Name, Target = Target, IsSnippet = IsSnippet };
}

public partial class SettingsWindow : Window
{
    private readonly LauncherSettings _settings;
    private readonly ObservableCollection<CustomItemViewModel> _items;

    private static readonly int MOD_CTRL  = GlobalHotkeyService.MOD_CONTROL;
    private static readonly int MOD_ALT   = GlobalHotkeyService.MOD_ALT;
    private static readonly int MOD_SHIFT = GlobalHotkeyService.MOD_SHIFT;

    public SettingsWindow(LauncherSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        HotkeyBox.Text = settings.HotkeyText;

        _items = new ObservableCollection<CustomItemViewModel>(
            settings.CustomItems.Select(i => new CustomItemViewModel(i)));
        ItemsList.ItemsSource = _items;
    }

    // ── 단축키 캡처 ───────────────────────────────────────────────────────────

    private void Hotkey_GotFocus(object sender, RoutedEventArgs e)
        => HotkeyBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 70));
    private void Hotkey_LostFocus(object sender, RoutedEventArgs e)
        => HotkeyBox.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 62));

    private void Hotkey_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        if (key is Key.Back or Key.Delete)
        {
            _settings.HotkeyVk   = 0;
            _settings.HotkeyMods = 0;
            _settings.HotkeyText = "";
            HotkeyBox.Text = "";
            return;
        }

        if (key == Key.Escape) { DialogResult = false; return; }

        int mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl))  mods |= MOD_CTRL;
        if (Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt))   mods |= MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= MOD_SHIFT;

        var vk   = KeyInterop.VirtualKeyFromKey(key);
        var text = "";
        if ((mods & MOD_CTRL)  != 0) text += "Ctrl+";
        if ((mods & MOD_ALT)   != 0) text += "Alt+";
        if ((mods & MOD_SHIFT) != 0) text += "Shift+";
        text += key.ToString();

        _settings.HotkeyVk   = vk;
        _settings.HotkeyMods = mods;
        _settings.HotkeyText = text;
        HotkeyBox.Text = text;
    }

    // ── 항목 CRUD ─────────────────────────────────────────────────────────────

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        var name   = NewNameBox.Text.Trim();
        var target = NewTargetBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(target))
        {
            MessageBox.Show("이름과 URL/텍스트를 모두 입력하세요.", "Quick.Launcher",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _items.Add(new CustomItemViewModel(new CustomItem
        {
            Name      = name,
            Target    = target,
            IsSnippet = IsSnippetCheck.IsChecked == true
        }));

        NewNameBox.Text   = "";
        NewTargetBox.Text = "";
        IsSnippetCheck.IsChecked = false;
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (((System.Windows.Controls.Button)sender).Tag is CustomItemViewModel vm)
            _items.Remove(vm);
    }

    // ── 저장 / 닫기 ───────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.CustomItems = _items.Select(vm => vm.ToModel()).ToList();
        SettingsService.Save(_settings);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
