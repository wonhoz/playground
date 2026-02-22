using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CommuteBuddy.Models;

namespace CommuteBuddy.Views;

public partial class SettingsWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly AppSettings _settings;
    private List<Location>       _locations        = [];
    private Location?            _selectedLocation;
    private bool                 _suppressSelection;

    /// <summary>저장 시 채워지는 업데이트 설정 (null이면 취소)</summary>
    public AppSettings? UpdatedSettings { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings  = settings;
        _locations = settings.Locations.Select(Clone).ToList();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var handle = new WindowInteropHelper(this).EnsureHandle();
        int dark   = 1;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));

        // 일반 설정 로드
        TxtStayAwakePath.Text   = _settings.StayAwakePath;
        ChkRemoteWork.IsChecked = _settings.RemoteWork.Enabled;
        TxtRemoteHour.Text      = _settings.RemoteWork.StartHour.ToString();
        TxtRemoteMinute.Text    = _settings.RemoteWork.StartMinute.ToString("00");

        UpdateRemoteCombo(_settings.RemoteWork.LocationName);
        RefreshLocationList(_locations.Count > 0 ? _locations[0] : null);
    }

    // ── 장소 목록 ─────────────────────────────────────────────────────────

    private void RefreshLocationList(Location? selectItem)
    {
        _suppressSelection = true;
        LocationList.ItemsSource = null;
        LocationList.ItemsSource = _locations;

        _selectedLocation = selectItem;
        if (selectItem != null && _locations.Contains(selectItem))
            LocationList.SelectedItem = selectItem;
        else
            LocationList.SelectedIndex = -1;

        _suppressSelection = false;
        PopulateDetail(_selectedLocation);
        UpdateRemoteCombo(ComboRemoteLocation.SelectedItem as string
                          ?? _settings.RemoteWork.LocationName);
    }

    private void LocationList_SelectionChanged(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressSelection) return;
        if (_selectedLocation != null) SaveDetailToLocation();
        _selectedLocation = LocationList.SelectedItem as Location;
        PopulateDetail(_selectedLocation);
    }

    private void AddLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLocation != null) SaveDetailToLocation();
        var newLoc = new Location
        {
            Name             = "새 장소",
            Emoji            = "📍",
            ArrivalRoutine   = new() { ShowNotification = true },
            DepartureRoutine = new() { ShowNotification = true },
        };
        _locations.Add(newLoc);
        RefreshLocationList(newLoc);
    }

    private void RemoveLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLocation == null) return;
        _locations.Remove(_selectedLocation);
        _selectedLocation = null;
        RefreshLocationList(_locations.Count > 0 ? _locations[0] : null);
    }

    // ── 상세 폼 ───────────────────────────────────────────────────────────

    private void PopulateDetail(Location? loc)
    {
        if (loc == null) { ShowDetail(false); return; }

        TxtName.Text  = loc.Name;
        TxtEmoji.Text = loc.Emoji;
        TxtSsids.Text = string.Join("\n", loc.Ssids);

        ChkArrivalStartSA.IsChecked = loc.ArrivalRoutine.StartStayAwake;
        ChkArrivalStopSA.IsChecked  = loc.ArrivalRoutine.StopStayAwake;
        TxtArrivalAppsLaunch.Text   = string.Join("\n", loc.ArrivalRoutine.AppsToLaunch);
        TxtArrivalAppsClose.Text    = string.Join("\n", loc.ArrivalRoutine.AppsToClose);

        ChkDepartureStartSA.IsChecked = loc.DepartureRoutine.StartStayAwake;
        ChkDepartureStopSA.IsChecked  = loc.DepartureRoutine.StopStayAwake;
        TxtDepartureAppsLaunch.Text   = string.Join("\n", loc.DepartureRoutine.AppsToLaunch);
        TxtDepartureAppsClose.Text    = string.Join("\n", loc.DepartureRoutine.AppsToClose);

        ShowDetail(true);
    }

    private void SaveDetailToLocation()
    {
        if (_selectedLocation == null) return;

        _selectedLocation.Name  = TxtName.Text.Trim();
        _selectedLocation.Emoji = TxtEmoji.Text.Trim();
        _selectedLocation.Ssids = SplitLines(TxtSsids.Text);

        _selectedLocation.ArrivalRoutine.StartStayAwake = ChkArrivalStartSA.IsChecked == true;
        _selectedLocation.ArrivalRoutine.StopStayAwake  = ChkArrivalStopSA.IsChecked  == true;
        _selectedLocation.ArrivalRoutine.AppsToLaunch   = SplitLines(TxtArrivalAppsLaunch.Text);
        _selectedLocation.ArrivalRoutine.AppsToClose    = SplitLines(TxtArrivalAppsClose.Text);

        _selectedLocation.DepartureRoutine.StartStayAwake = ChkDepartureStartSA.IsChecked == true;
        _selectedLocation.DepartureRoutine.StopStayAwake  = ChkDepartureStopSA.IsChecked  == true;
        _selectedLocation.DepartureRoutine.AppsToLaunch   = SplitLines(TxtDepartureAppsLaunch.Text);
        _selectedLocation.DepartureRoutine.AppsToClose    = SplitLines(TxtDepartureAppsClose.Text);
    }

    private void ShowDetail(bool show)
    {
        TxtNoSelection.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        DetailScroll.Visibility   = show ? Visibility.Visible   : Visibility.Collapsed;
    }

    // ── 일반 설정 ─────────────────────────────────────────────────────────

    private void ChkRemoteWork_Changed(object sender, RoutedEventArgs e) { }

    private void UpdateRemoteCombo(string selected = "")
    {
        ComboRemoteLocation.ItemsSource = _locations.Select(l => l.Name).ToList();
        if (!string.IsNullOrEmpty(selected) &&
            _locations.Any(l => l.Name == selected))
            ComboRemoteLocation.SelectedItem = selected;
        else if (ComboRemoteLocation.Items.Count > 0)
            ComboRemoteLocation.SelectedIndex = 0;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "StayAwake.exe 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            TxtStayAwakePath.Text = dlg.FileName;
    }

    // ── 저장 / 취소 ───────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLocation != null) SaveDetailToLocation();

        _settings.Locations     = _locations;
        _settings.StayAwakePath = TxtStayAwakePath.Text.Trim();

        _settings.RemoteWork.Enabled =
            ChkRemoteWork.IsChecked == true;
        _settings.RemoteWork.StartHour =
            int.TryParse(TxtRemoteHour.Text,   out var h) ? Math.Clamp(h, 0, 23) : 9;
        _settings.RemoteWork.StartMinute =
            int.TryParse(TxtRemoteMinute.Text, out var m) ? Math.Clamp(m, 0, 59) : 0;
        _settings.RemoteWork.LocationName =
            ComboRemoteLocation.SelectedItem as string
            ?? (_locations.Count > 0 ? _locations[0].Name : "");

        UpdatedSettings = _settings;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ── 유틸 ──────────────────────────────────────────────────────────────

    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    private static Location Clone(Location src) => new()
    {
        Name             = src.Name,
        Emoji            = src.Emoji,
        Ssids            = [.. src.Ssids],
        ArrivalRoutine   = CloneRoutine(src.ArrivalRoutine),
        DepartureRoutine = CloneRoutine(src.DepartureRoutine),
    };

    private static Routine CloneRoutine(Routine src) => new()
    {
        StartStayAwake   = src.StartStayAwake,
        StopStayAwake    = src.StopStayAwake,
        AppsToLaunch     = [.. src.AppsToLaunch],
        AppsToClose      = [.. src.AppsToClose],
        ShowNotification = src.ShowNotification,
    };
}
