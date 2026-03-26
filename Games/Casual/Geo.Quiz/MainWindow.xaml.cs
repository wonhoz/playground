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

                if (e.PropertyName == nameof(_vm.Answered) && _vm.Answered && _vm.CurrentQuestion != null)
                {
                    bool isCorrect = _vm.SelectedAnswer == _vm.CurrentQuestion.CorrectAnswer;
                    if (isCorrect) _audio.PlayCorrect();
                    else           _audio.PlayWrong();
                }

                if (_vm.Screen == QuizScreen.Result) ShowResult();
            }
        };

        KeyDown += OnWindowKeyDown;
    }

    void ApplyDarkTitleBar()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    // ── 키보드 단축키 ─────────────────────────────────────────────────────────
    void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_vm.IsQuizScreen)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.D1 or System.Windows.Input.Key.NumPad1:
                    if (_vm.Choice0Cmd.CanExecute(null)) { _vm.Choice0Cmd.Execute(null); UpdateChoiceColors(); }
                    break;
                case System.Windows.Input.Key.D2 or System.Windows.Input.Key.NumPad2:
                    if (_vm.Choice1Cmd.CanExecute(null)) { _vm.Choice1Cmd.Execute(null); UpdateChoiceColors(); }
                    break;
                case System.Windows.Input.Key.D3 or System.Windows.Input.Key.NumPad3:
                    if (_vm.Choice2Cmd.CanExecute(null)) { _vm.Choice2Cmd.Execute(null); UpdateChoiceColors(); }
                    break;
                case System.Windows.Input.Key.D4 or System.Windows.Input.Key.NumPad4:
                    if (_vm.Choice3Cmd.CanExecute(null)) { _vm.Choice3Cmd.Execute(null); UpdateChoiceColors(); }
                    break;
                case System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space:
                    if (_vm.NextCmd.CanExecute(null)) _vm.NextCmd.Execute(null);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.H:
                    if (_vm.HintCmd.CanExecute(null)) _vm.HintCmd.Execute(null);
                    break;
                case System.Windows.Input.Key.P or System.Windows.Input.Key.Escape:
                    if (_vm.PauseCmd.CanExecute(null)) _vm.PauseCmd.Execute(null);
                    break;
            }
        }
        else if (_vm.IsStartScreen)
        {
            if (e.Key == System.Windows.Input.Key.F1)
                HelpPopup.IsOpen = !HelpPopup.IsOpen;
        }

        if (e.Key == System.Windows.Input.Key.F1)
            HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }

    void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        _audio.Muted    = !_audio.Muted;
        BtnMute.Content = _audio.Muted ? "🔇" : "🔊";
        if (!_audio.Muted) _audio.PlayBg();
        else               _audio.PauseBg();
    }

    void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        HelpPopup.IsOpen = !HelpPopup.IsOpen;
    }

    void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _vm.Mode = RbCapital.IsChecked == true ? QuizMode.Capital
                 : RbFlag.IsChecked    == true ? QuizMode.Flag
                 :                               QuizMode.Continent;
    }

    void CbTimer_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        // TimerMode는 CheckBox.IsChecked 바인딩으로 처리됨
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
        // 새 문제로 전환 시 이전 이미지 즉시 숨김 (깜빡임 방지)
        FlagImage.Visibility   = Visibility.Collapsed;
        FlagImage.Source       = null;

        var cached = FlagCache.GetCached(q.FlagIsoCode);
        if (cached != null)
        {
            FlagImage.Source     = cached;
            FlagImage.Visibility = Visibility.Visible;
            return;
        }

        var uri = new Uri($"https://flagcdn.com/w160/{q.FlagIsoCode.ToLower()}.png");
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
        bmp.DownloadCompleted += (_, _) =>
        {
            FlagImage.Source     = bmp;
            FlagImage.Visibility = Visibility.Visible;
            FlagCache.Add(q.FlagIsoCode, bmp);
        };

        FlagImage.Source = bmp;
    }

    void Choice_Click(object sender, RoutedEventArgs e)
    {
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
        TbGradeEmoji.Text  = info.emoji ?? "📊";
        TbGrade.Text       = $"{r.Score:F0}점";
        TbGrade.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(info.fore ?? "#E0E0E0"));
        string newRecord = _vm.IsNewRecord ? "  🏆 신기록!" : $"  (최고: {_vm.BestScore:F0}점)";
        string hintNote  = r.HintsUsed > 0 ? $"  💡 힌트 {r.HintsUsed}회 사용 (-{r.HintsUsed * 5}점)" : "";
        TbScore.Text  = $"{r.Correct}문제 정답 / {r.Total}문제 (등급: {r.Grade}){newRecord}{hintNote}";
        TbCorrect.Text = r.Correct.ToString();
        TbWrong.Text   = r.Wrong.ToString();

        WrongList.ItemsSource = r.WrongItems;
        WrongPanel.Visibility = r.WrongItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        _audio.Dispose();
        base.OnClosed(e);
    }
}
