using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Geo.Quiz.Services;
using Geo.Quiz.ViewModels;

namespace Geo.Quiz;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;
    readonly AudioService  _audio = new();

    static readonly (string emoji, string fore, string grade)[] GradeInfo =
    [
        ("🏆", "#FFD700", "A"),
        ("🥈", "#C0C0C0", "B"),
        ("🥉", "#CD7F32", "C"),
        ("📝", "#9CA3AF", "D"),
        ("💀", "#F87171", "F"),
    ];

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += (_, _) => { ApplyDarkTitleBar(); _audio.PlayBg(); };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_vm.Screen) or
                nameof(_vm.CurrentQuestion) or
                nameof(_vm.Answered) or
                nameof(_vm.EliminatedChoice))
            {
                UpdateChoiceColors();
                LoadFlagImage();

                // 정답/오답 효과음
                if (e.PropertyName == nameof(_vm.Answered) && _vm.Answered && _vm.CurrentQuestion != null)
                {
                    bool isCorrect = _vm.SelectedAnswer == _vm.CurrentQuestion.CorrectAnswer;
                    if (isCorrect) _audio.PlayCorrect();
                    else           _audio.PlayWrong();
                }

                if (_vm.Screen == QuizScreen.Result) ShowResult();
            }
        };
    }

    void ApplyDarkTitleBar()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    // ── 화면 전환용 Binding 보조 속성 ──────────────────────────────────────
    // XAML BoolVis 사용을 위해 VM에 별도 bool 프로퍼티 추가 대신 code-behind에서 패널 직접 제어
    // → VM PropertyChanged 구독으로 Visibility 갱신

    void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        _audio.Muted   = !_audio.Muted;
        BtnMute.Content = _audio.Muted ? "🔇" : "🔊";
        if (!_audio.Muted) _audio.PlayBg();
        else               _audio.PauseBg();
    }

    void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _vm.Mode = RbCapital.IsChecked == true   ? QuizMode.Capital
                 : RbFlag.IsChecked    == true   ? QuizMode.Flag
                 :                                 QuizMode.Continent;
    }

    void CbTimer_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        // TimerMode는 CheckBox.IsChecked 바인딩으로 처리 — 이벤트는 필요 시 추가 처리용
    }

    void LoadFlagImage()
    {
        var q = _vm.CurrentQuestion;
        if (q == null || string.IsNullOrEmpty(q.FlagIsoCode))
        {
            FlagImage.Visibility   = Visibility.Collapsed;
            TbFlagError.Visibility = Visibility.Collapsed;
            FlagImage.Source       = null;
            return;
        }

        TbFlagError.Visibility = Visibility.Collapsed;
        var uri = new Uri($"https://flagcdn.com/w160/{q.FlagIsoCode.ToLower()}.png");

        // 캐시된 이미지 확인
        var cached = FlagCache.GetCached(q.FlagIsoCode);
        if (cached != null)
        {
            FlagImage.Source     = cached;
            FlagImage.Visibility = Visibility.Visible;
            return;
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource   = uri;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();

        bmp.DownloadFailed  += (_, _) =>
        {
            FlagImage.Visibility   = Visibility.Collapsed;
            TbFlagError.Visibility = Visibility.Visible;
        };
        bmp.DownloadCompleted += (_, _) => FlagCache.Add(q.FlagIsoCode, bmp);

        FlagImage.Source     = bmp;
        FlagImage.Visibility = Visibility.Visible;
    }

    void Choice_Click(object sender, RoutedEventArgs e)
    {
        // 선택지 색상 즉시 갱신
        UpdateChoiceColors();
    }

    void UpdateChoiceColors()
    {
        if (_vm.CurrentQuestion == null) return;

        var buttons = new[] { BtnChoice0, BtnChoice1, BtnChoice2, BtnChoice3 };
        var choices = _vm.CurrentQuestion.Choices;

        for (int i = 0; i < buttons.Length; i++)
        {
            string choice = i < choices.Count ? choices[i] : "";

            // 힌트로 제거된 선택지 — 흐리게 표시하고 클릭 불가
            bool isEliminated = !string.IsNullOrEmpty(_vm.EliminatedChoice)
                                && choice == _vm.EliminatedChoice
                                && !_vm.Answered;
            if (isEliminated)
            {
                buttons[i].Background = new SolidColorBrush(Colors.Transparent);
                buttons[i].Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#282838"));
                buttons[i].IsEnabled  = false;
                continue;
            }

            buttons[i].IsEnabled  = true;
            buttons[i].Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_vm.GetChoiceBrush(choice)));
            buttons[i].Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_vm.GetChoiceFore(choice)));
        }
    }

    void ShowResult()
    {
        if (_vm.Result == null) return;
        var r = _vm.Result;

        var info = GradeInfo.FirstOrDefault(g => g.grade == r.Grade);
        TbGradeEmoji.Text   = info.emoji ?? "📊";
        TbGrade.Text        = $"{r.Score:F0}점";
        TbGrade.Foreground  = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(info.fore ?? "#E0E0E0"));
        string newRecord    = _vm.IsNewRecord ? "  🏆 신기록!" : $"  (최고: {_vm.BestScore:F0}점)";
        string hintNote     = r.HintsUsed > 0 ? $"  💡 힌트 {r.HintsUsed}회 사용 (-{r.HintsUsed * 5}점)" : "";
        TbScore.Text        = $"{r.Correct}문제 정답 / {r.Total}문제 (등급: {r.Grade}){newRecord}{hintNote}";
        TbCorrect.Text      = r.Correct.ToString();
        TbWrong.Text        = r.Wrong.ToString();

        // 오답 노트
        WrongList.ItemsSource    = r.WrongItems;
        WrongPanel.Visibility    = r.WrongItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        _audio.Dispose();
        base.OnClosed(e);
    }
}
