using KeyMacro.Models;
using KeyMacro.Services;

namespace KeyMacro.Views;

/// <summary>앱 전체에 적용되는 설정 창</summary>
public partial class OptionsDialog : Window
{
    private readonly AppSettings _settings;

    private int _vk;
    private bool _ctrl, _alt, _shift, _win;

    private OptionsDialog(AppSettings settings)
    {
        InitializeComponent();
        DarkTitle.Apply(this);

        _settings = settings;
        _vk = settings.AbortVk;
        _ctrl = settings.AbortCtrl;
        _alt = settings.AbortAlt;
        _shift = settings.AbortShift;
        _win = settings.AbortWin;

        ChkEsc.IsChecked = settings.AbortOnEsc;
        TxtUrl.Text = settings.TimeSyncUrl;
        UpdateKeyDisplay();
    }

    /// <summary>설정 창을 띄우고, 확인을 눌렀으면 true</summary>
    public static bool Show(Window owner, AppSettings settings)
        => new OptionsDialog(settings) { Owner = owner }.ShowDialog() == true;

    private void UpdateKeyDisplay()
        => TxtAbortKey.Text = _vk == 0 ? "(없음)" : KeyNames.Combo(_vk, _ctrl, _alt, _shift, _win);

    private void BtnSetKey_Click(object sender, RoutedEventArgs e)
    {
        if (!KeyCaptureDialog.TryCapture(this, "중단에 사용할 키를 눌러 주세요",
                "Ctrl · Alt · Shift · Win 조합을 권장합니다 (실수로 눌리지 않도록)", out var combo))
            return;

        _vk = combo.Vk;
        _ctrl = combo.Ctrl;
        _alt = combo.Alt;
        _shift = combo.Shift;
        _win = combo.Win;
        UpdateKeyDisplay();
    }

    private void BtnDefault_Click(object sender, RoutedEventArgs e)
    {
        _vk = 0x7B;      // F12
        _ctrl = true;
        _alt = true;
        _shift = _win = false;
        UpdateKeyDisplay();
    }

    private void BtnTimeSync_Click(object sender, RoutedEventArgs e)
    {
        _settings.TimeSyncUrl = TxtUrl.Text.Trim();
        TimeSyncDialog.Show(this, _settings);
        TxtUrl.Text = _settings.TimeSyncUrl;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        _settings.AbortVk = _vk;
        _settings.AbortCtrl = _ctrl;
        _settings.AbortAlt = _alt;
        _settings.AbortShift = _shift;
        _settings.AbortWin = _win;
        _settings.AbortOnEsc = ChkEsc.IsChecked == true;
        _settings.TimeSyncUrl = TxtUrl.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
