using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;

namespace Layout.Forge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    const double BASE_KEY = 42.0;  // 기본 키 너비 (px)

    readonly MainViewModel _vm;
    KeyViewModel? _selectedKeyVm;

    public MainWindow()
    {
        _vm = new MainViewModel();
        InitializeComponent();

        CmbProfiles.ItemsSource   = _vm.Profiles;
        CmbProfiles.SelectionChanged += CmbProfiles_SelectionChanged;
        CmbProfiles.SelectedIndex = 0;

        CmbTarget.ItemsSource = KeyboardLayout.Targets;

        Loaded += OnLoaded;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        BuildKeyboard();
    }

    // ── 키보드 동적 빌드 ──────────────────────────────────────────────

    void BuildKeyboard()
    {
        foreach (var row in _vm.MainRows)
            PnlMain.Children.Add(BuildRow(row));
        foreach (var row in _vm.NavRows)
            PnlNav.Children.Add(BuildRow(row));
    }

    StackPanel BuildRow(IReadOnlyList<KeyViewModel> kvms)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
        foreach (var kvm in kvms)
        {
            if (kvm.Key.IsSpacer)
            {
                sp.Children.Add(new Border { Width = kvm.Key.Width * BASE_KEY, Height = 38, Margin = new Thickness(2) });
                continue;
            }
            var btn = CreateKeyButton(kvm);
            sp.Children.Add(btn);
        }
        return sp;
    }

    Button CreateKeyButton(KeyViewModel kvm)
    {
        var btn = new Button
        {
            Width   = kvm.Key.Width * BASE_KEY,
            Height  = 38,
            Margin  = new Thickness(2),
            Cursor  = System.Windows.Input.Cursors.Hand,
            DataContext = kvm,
            Tag     = kvm,
        };
        btn.Click += Key_Click;
        RefreshKeyButton(btn, kvm);

        // PropertyChanged 반응
        kvm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(KeyViewModel.IsRemapped)
                                  or nameof(KeyViewModel.IsDisabled)
                                  or nameof(KeyViewModel.IsSelected)
                                  or nameof(KeyViewModel.BadgeText))
                RefreshKeyButton(btn, kvm);
        };
        return btn;
    }

    void RefreshKeyButton(Button btn, KeyViewModel kvm)
    {
        // 배경/테두리 색 결정
        Color bg, border;
        if (kvm.IsSelected)         { bg = Color.FromRgb(20,40,80);   border = Color.FromRgb(96,165,250); }
        else if (kvm.IsDisabled)    { bg = Color.FromRgb(42,20,20);   border = Color.FromRgb(127,29,29); }
        else if (kvm.IsRemapped)    { bg = Color.FromRgb(20,38,70);   border = Color.FromRgb(37,99,235); }
        else                        { bg = Color.FromRgb(26,26,46);   border = Color.FromRgb(42,42,62); }

        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 2, 3, 2) };

        var lbl = new TextBlock
        {
            Text = kvm.Key.Label,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = kvm.IsDisabled
                ? new SolidColorBrush(Color.FromRgb(107, 32, 32))
                : new SolidColorBrush(Color.FromRgb(192, 192, 208)),
        };
        sp.Children.Add(lbl);

        if (kvm.IsRemapped && !string.IsNullOrEmpty(kvm.BadgeText))
        {
            sp.Children.Add(new TextBlock
            {
                Text = kvm.BadgeText,
                FontSize = 9,
                Foreground = kvm.IsDisabled
                    ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                    : new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        var bd = new Border
        {
            Background   = new SolidColorBrush(bg),
            BorderBrush  = new SolidColorBrush(border),
            BorderThickness = kvm.IsSelected ? new Thickness(2) : new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child        = sp,
        };

        // 호버 효과
        btn.MouseEnter += (_, _) =>
        {
            if (!kvm.IsSelected)
                bd.Background = new SolidColorBrush(Color.FromRgb(37, 37, 64));
        };
        btn.MouseLeave += (_, _) => RefreshButtonBackground(bd, kvm);

        btn.Content = bd;
    }

    void RefreshButtonBackground(Border bd, KeyViewModel kvm)
    {
        Color bg;
        if (kvm.IsSelected)      bg = Color.FromRgb(20, 40, 80);
        else if (kvm.IsDisabled) bg = Color.FromRgb(42, 20, 20);
        else if (kvm.IsRemapped) bg = Color.FromRgb(20, 38, 70);
        else                     bg = Color.FromRgb(26, 26, 46);
        bd.Background = new SolidColorBrush(bg);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────

    void Key_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not KeyViewModel kvm) return;

        _vm.SelectKey(kvm);
        _selectedKeyVm = kvm;

        TxtSelectedKey.Text         = kvm.Key.Label;
        SelectionBar.Visibility     = Visibility.Visible;
        CmbTarget.SelectedItem      = kvm.IsRemapped
            ? KeyboardLayout.Targets.FirstOrDefault(t => t.ScanCode == kvm.MappedTo)
            : null;
    }

    void CmbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _vm.SelectedProfile = CmbProfiles.SelectedItem as KeyProfile;
    }

    void SetRemap_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedKeyVm == null || CmbTarget.SelectedItem is not KeyTarget target) return;
        _vm.SelectedTarget = target;
        _vm.SetRemap();
        TxtStatus.Text = _vm.StatusText;
    }

    void ClearRemap_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearRemap();
        CmbTarget.SelectedItem = null;
        TxtStatus.Text = _vm.StatusText;
    }

    void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("새 프로파일 이름", "프로파일 이름:") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Result)) return;
        _vm.NewProfile(dlg.Result.Trim());
        CmbProfiles.SelectedItem = _vm.SelectedProfile;
    }

    void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProfile == null) return;
        var dlg = new InputDialog("프로파일 이름 변경", "새 이름:", _vm.SelectedProfile.Name) { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Result)) return;
        _vm.RenameProfile(dlg.Result.Trim());
        CmbProfiles.Items.Refresh();
    }

    void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Profiles.Count <= 1) { TxtStatus.Text = "프로파일이 하나뿐이라 삭제할 수 없습니다"; return; }
        if (MessageBox.Show("선택된 프로파일을 삭제하시겠습니까?", "확인",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _vm.DeleteProfile();
        CmbProfiles.SelectedItem = _vm.SelectedProfile;
    }

    void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.ApplyToSystem();
            TxtStatus.Text = _vm.StatusText;
            MessageBox.Show("레지스트리에 적용되었습니다.\n재부팅 후 키 배치가 변경됩니다.",
                "Layout.Forge", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"적용 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Scancode Map을 삭제하여 기본 키 배치를 복원하시겠습니까?\n재부팅 후 적용됩니다.",
            "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            _vm.RestoreDefault();
            TxtStatus.Text = _vm.StatusText;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"복원 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
