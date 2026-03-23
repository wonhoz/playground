using System.Runtime.InteropServices;
using System.Windows;
using Color = System.Windows.Media.Color;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AmbientMixer.Models;
using AmbientMixer.Services;

namespace AmbientMixer;

public partial class MainWindow : Window
{
    private readonly MixerService  _mixer;
    private readonly MixerSettings _settings;

    private readonly Dictionary<AmbientTrack, Slider>    _sliders = [];
    private readonly Dictionary<AmbientTrack, TextBlock> _valLbls = [];

    // 슬립 버튼 (꺼짐/15분/30분/45분/1시간/2시간)
    private readonly (string Label, int Mins)[] _sleepOptions =
    [
        ("꺼짐",   0),
        ("15분",  15),
        ("30분",  30),
        ("45분",  45),
        ("1시간",  60),
        ("2시간", 120),
    ];
    private Button? _activeSleepBtn;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public MainWindow(MixerService mixer, MixerSettings settings)
    {
        InitializeComponent();
        _mixer    = mixer;
        _settings = settings;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkTitlebar();
        BuildTrackRows();
        BuildPresets();
        BuildSleepButtons();
        LoadSettings();

        _mixer.SleepTickChanged += OnSleepTick;
        _mixer.SleepExpired     += OnSleepExpired;
    }

    private void ApplyDarkTitlebar()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }

    // ─────────────────────────────────────────────
    // UI 빌드
    // ─────────────────────────────────────────────

    private void BuildTrackRows()
    {
        foreach (AmbientTrack track in Enum.GetValues<AmbientTrack>())
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(94) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            var label = new TextBlock
            {
                Text              = $"{AmbientTrackInfo.Emoji(track)} {AmbientTrackInfo.Label(track)}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground        = new SolidColorBrush(Color.FromRgb(0xBB, 0xBB, 0xCC)),
                FontSize          = 12,
            };

            float initVol = _settings.GetVolume(track);
            var slider = new Slider
            {
                Style      = (Style)FindResource("AmbiSlider"),
                Value      = initVol,
                Margin     = new Thickness(8, 0, 0, 0),
                Tag        = track,
            };
            slider.ValueChanged += TrackSlider_ValueChanged;
            _sliders[track] = slider;

            var valLbl = new TextBlock
            {
                Text                = $"{(int)(initVol * 100)}%",
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground          = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88)),
                FontSize            = 10,
            };
            _valLbls[track] = valLbl;

            Grid.SetColumn(label,  0);
            Grid.SetColumn(slider, 1);
            Grid.SetColumn(valLbl, 2);
            row.Children.Add(label);
            row.Children.Add(slider);
            row.Children.Add(valLbl);

            TrackPanel.Children.Add(row);
        }
    }

    private void BuildPresets()
    {
        foreach (var preset in _settings.Presets)
        {
            var btn = new Button
            {
                Content = preset.Name,
                Style   = (Style)FindResource("PresetBtn"),
                Tag     = preset,
            };
            btn.Click += PresetBtn_Click;
            PresetPanel.Children.Add(btn);
        }
    }

    private void BuildSleepButtons()
    {
        foreach (var (lbl, mins) in _sleepOptions)
        {
            var btn = new Button
            {
                Content    = lbl,
                Style      = (Style)FindResource("PresetBtn"),
                Tag        = mins,
                Margin     = new Thickness(0, 0, 4, 0),
                Padding    = new Thickness(8, 4, 8, 4),
            };
            btn.Click += SleepBtn_Click;
            SleepPanel.Children.Add(btn);

            if (mins == 0)
            {
                _activeSleepBtn = btn;
                HighlightSleepBtn(btn, true);
            }
        }
    }

    private void HighlightSleepBtn(Button btn, bool active)
    {
        btn.Background = active
            ? new SolidColorBrush(Color.FromRgb(0x00, 0x60, 0x64))
            : new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x4A));
    }

    // ─────────────────────────────────────────────
    // 설정 로드
    // ─────────────────────────────────────────────

    private void LoadSettings()
    {
        SliderMaster.Value = _settings.MasterVolume;
        _mixer.ApplySettings(_settings);
    }

    // ─────────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────────

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        _mixer.TogglePlay();
        UpdatePlayState();
    }

    private void UpdatePlayState()
    {
        BtnPlay.Content = _mixer.IsPlaying ? "⏸" : "▶";
        TxtStatus.Text  = _mixer.IsPlaying ? "재생 중" : "정지됨";
    }

    private void SliderMaster_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMaster == null) return;   // InitializeComponent 이전 호출 방지
        float v = (float)e.NewValue;
        _settings.MasterVolume = v;
        TxtMaster.Text = $"{(int)(v * 100)}%";
        _mixer.MasterVolume = v;
        // MasterVolume setter 내부에서 RefreshAllVolumes() 호출됨
    }

    private void TrackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not Slider s || s.Tag is not AmbientTrack track) return;
        float v = (float)s.Value;
        _settings.SetVolume(track, v);
        _mixer.SetTrackVolume(track, v);
        if (_valLbls.TryGetValue(track, out var lbl))
            lbl.Text = $"{(int)(v * 100)}%";
    }

    private void PresetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Preset preset) return;

        // 전체 초기화 후 프리셋 볼륨 적용
        foreach (AmbientTrack t in Enum.GetValues<AmbientTrack>())
            _settings.SetVolume(t, 0f);

        foreach (var (key, vol) in preset.Volumes)
            if (Enum.TryParse<AmbientTrack>(key, out var t))
                _settings.SetVolume(t, vol);

        // 슬라이더 UI 동기화
        foreach (var (t, s) in _sliders)
        {
            float v = _settings.GetVolume(t);
            s.Value = v;
            // TrackSlider_ValueChanged → SetVolume + SetTrackVolume 자동 호출
        }

        _mixer.ApplySettings(_settings);
    }

    private void SleepBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int mins) return;

        // 이전 선택 해제
        if (_activeSleepBtn != null) HighlightSleepBtn(_activeSleepBtn, false);
        _activeSleepBtn = btn;
        HighlightSleepBtn(btn, true);

        _settings.SleepTimerMins = mins;

        if (mins > 0)
        {
            _mixer.StartSleepTimer(mins);
            TxtCountdown.Visibility = Visibility.Visible;
        }
        else
        {
            _mixer.StopSleepTimer();
            TxtCountdown.Visibility = Visibility.Collapsed;
        }
    }

    // ─────────────────────────────────────────────
    // 슬립 타이머 콜백 (타이머 스레드 → UI 스레드)
    // ─────────────────────────────────────────────

    private void OnSleepTick(int remainSec)
    {
        Dispatcher.Invoke(() =>
        {
            var ts = TimeSpan.FromSeconds(remainSec);
            TxtCountdown.Text = ts.TotalMinutes >= 1
                ? ts.ToString(@"m\:ss")
                : $"{remainSec}초";
        });
    }

    private void OnSleepExpired()
    {
        Dispatcher.Invoke(() =>
        {
            TxtCountdown.Visibility = Visibility.Collapsed;
            UpdatePlayState();

            // 슬립 버튼 → "꺼짐" 초기화
            if (_activeSleepBtn != null) HighlightSleepBtn(_activeSleepBtn, false);
            if (SleepPanel.Children.Count > 0 && SleepPanel.Children[0] is Button first)
            {
                _activeSleepBtn = first;
                HighlightSleepBtn(first, true);
            }
        });
    }

    // ─────────────────────────────────────────────
    // 창 닫기 → 숨기기 (트레이 앱 패턴)
    // ─────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
