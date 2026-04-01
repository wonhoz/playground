namespace TriviaCast;

public partial class SettingsDialog : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public int CorrectDelayMs { get; private set; }
    public int WrongDelayMs { get; private set; }
    public bool BgmEnabled { get; private set; }
    public bool SfxEnabled { get; private set; }

    public SettingsDialog(int correctMs, int wrongMs, bool bgmEnabled, bool sfxEnabled)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

            BgmCheck.IsChecked = bgmEnabled;
            SfxCheck.IsChecked = sfxEnabled;
            CorrectSlider.Value = correctMs;
            WrongSlider.Value = wrongMs;
            UpdateHints();
        };
    }

    private void UpdateHints()
    {
        CorrectHint.Text = $"{(int)CorrectSlider.Value / 1000.0:0.0}초";
        WrongHint.Text   = $"{(int)WrongSlider.Value / 1000.0:0.0}초";
    }

    private void CorrectSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateHints();
    private void WrongSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateHints();

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        CorrectDelayMs = (int)CorrectSlider.Value;
        WrongDelayMs   = (int)WrongSlider.Value;
        BgmEnabled     = BgmCheck.IsChecked == true;
        SfxEnabled     = SfxCheck.IsChecked == true;
        DialogResult = true;
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
