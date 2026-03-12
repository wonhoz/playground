using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Geo.Quiz.ViewModels;

namespace Geo.Quiz;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;

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

        Loaded += (_, _) => ApplyDarkTitleBar();

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_vm.Screen) or
                nameof(_vm.CurrentQuestion) or
                nameof(_vm.Answered))
            {
                UpdateChoiceColors();
                LoadFlagImage();
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

    void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _vm.Mode = RbCapital.IsChecked == true   ? QuizMode.Capital
                 : RbFlag.IsChecked    == true   ? QuizMode.Flag
                 :                                 QuizMode.Continent;
    }

    void LoadFlagImage()
    {
        var q = _vm.CurrentQuestion;
        if (q == null || string.IsNullOrEmpty(q.FlagIsoCode))
        {
            FlagImage.Visibility = Visibility.Collapsed;
            FlagImage.Source     = null;
            return;
        }

        var uri = new Uri($"https://flagcdn.com/w160/{q.FlagIsoCode.ToLower()}.png");
        var bmp = new BitmapImage(uri);
        bmp.DownloadFailed += (_, _) => { FlagImage.Visibility = Visibility.Collapsed; };
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
        TbScore.Text        = $"{r.Correct}문제 정답 / {r.Total}문제 (등급: {r.Grade})";
        TbCorrect.Text      = r.Correct.ToString();
        TbWrong.Text        = r.Wrong.ToString();
    }
}
