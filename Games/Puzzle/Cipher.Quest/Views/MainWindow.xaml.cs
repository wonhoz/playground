using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using CipherQuest.Models;
using CipherQuest.Services;

namespace CipherQuest.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int val, int sz);

    private readonly GameService    _game = new();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly FrequencyView  _freqView = new();

    // 현재 챕터용 도구 컨트롤 참조
    private Slider?   _caesarSlider;
    private TextBox?  _vigenereBox;
    private TextBox[] _subBoxes = new TextBox[26];
    private Slider?   _railSlider;
    private ComboBox? _enigmaR1, _enigmaR2, _enigmaR3;

    public MainWindow()
    {
        InitializeComponent();

        FreqPresenter.Content = _freqView;

        _uiTimer.Tick += (_, _) =>
        {
            TxtTimer.Text = _game.TimerRunning
                ? $"⏱ {_game.ElapsedSeconds / 60:D2}:{_game.ElapsedSeconds % 60:D2}"
                : "";
        };
        _uiTimer.Start();

        Loaded += (_, _) =>
        {
            ApplyDarkTitleBar();
            BuildChapterTabs();
            LoadPuzzle();
        };
    }

    // ── 챕터 탭 ─────────────────────────────────────────────────────

    private void BuildChapterTabs()
    {
        ChapterTabs.Children.Clear();
        for (int i = 0; i < PuzzleData.All.Count; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Content = $"{i + 1}. {PuzzleData.All[i].Name}",
                Padding = new Thickness(10, 6, 10, 6),
                Margin  = new Thickness(0, 0, 6, 0),
                Tag     = i,
            };
            btn.Click += (_, _) => { _game.GoToChapter(idx); LoadPuzzle(); };
            ChapterTabs.Children.Add(btn);
        }
    }

    // ── 퍼즐 로드 ────────────────────────────────────────────────────

    private void LoadPuzzle()
    {
        var ch = _game.CurrentChapter;
        var pz = _game.CurrentPuzzle;

        TxtPuzzleInfo.Text  = $"— {ch.Era}  │  {pz.Title}";
        TxtPuzzleNum.Text   = $"{pz.Number} / {ch.Puzzles.Count}";
        TxtCipher.Text      = pz.CipherText;
        TxtStatus.Text      = ch.Desc;
        TxtDecrypted.Text   = "";
        TxtTimer.Text       = "";

        BuildToolPanel();
        RefreshDecrypted();

        // 치환 챕터: 빈도 분석 표시
        bool isSub = ch.Type == CipherType.Substitution;
        FreqBorder.Visibility = isSub ? Visibility.Visible : Visibility.Collapsed;
        if (isSub) _freqView.Update(pz.CipherText);
    }

    // ── 도구 패널 생성 ────────────────────────────────────────────────

    private void BuildToolPanel()
    {
        ToolPanel.Children.Clear();
        _caesarSlider = null; _vigenereBox = null;
        _railSlider = null; _enigmaR1 = null;

        switch (_game.Type)
        {
            case CipherType.Caesar:       BuildCaesarPanel(); break;
            case CipherType.Vigenere:     BuildVigenerePanel(); break;
            case CipherType.Substitution: BuildSubstitutionPanel(); break;
            case CipherType.RailFence:    BuildRailFencePanel(); break;
            case CipherType.Enigma:       BuildEnigmaPanel(); break;
        }
    }

    private void BuildCaesarPanel()
    {
        AddLabel("이동 거리 (0~25)");
        var row = NewRow();

        _caesarSlider = new Slider
        {
            Minimum = 0, Maximum = 25, Value = _game.CaesarShift,
            SmallChange = 1, LargeChange = 1,
            Width = 500, VerticalAlignment = VerticalAlignment.Center,
        };
        var valTxt = new TextBlock
        {
            Text = _game.CaesarShift.ToString(),
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xC1, 0x07)),
            Margin = new Thickness(12, 0, 0, 0), Width = 30,
        };

        _caesarSlider.ValueChanged += (_, e) =>
        {
            _game.CaesarShift = (int)e.NewValue;
            valTxt.Text = _game.CaesarShift.ToString();
            _game.StartTimer();
            RefreshDecrypted();
        };

        row.Children.Add(_caesarSlider);
        row.Children.Add(valTxt);
        ToolPanel.Children.Add(row);

        // 알파벳 참조 표시
        AddAlphabetReference();
    }

    private void BuildVigenerePanel()
    {
        AddLabel("키워드 입력 (A~Z 대문자)");
        _vigenereBox = new TextBox
        {
            Text = _game.VigenereKey, FontSize = 16, Width = 300,
            CharacterCasing = CharacterCasing.Upper,
        };
        _vigenereBox.TextChanged += (_, _) =>
        {
            _game.VigenereKey = _vigenereBox.Text;
            _game.StartTimer();
            RefreshDecrypted();
        };
        ToolPanel.Children.Add(_vigenereBox);
    }

    private void BuildSubstitutionPanel()
    {
        AddLabel("글자 매핑 (암호문 → 평문 추측)");

        for (int half = 0; half < 2; half++)
        {
            var header = NewRow();
            var inputs = NewRow();

            for (int j = 0; j < 13; j++)
            {
                int i = half * 13 + j;
                char lbl = (char)('A' + i);

                var lblTb = new TextBlock
                {
                    Text = lbl.ToString(),
                    Width = 28, FontSize = 11, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(0x80, 0x80, 0x80)),
                };
                header.Children.Add(lblTb);

                var tb = new TextBox
                {
                    Width = 26, MaxLength = 1, FontSize = 13, FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(1),
                    CharacterCasing = CharacterCasing.Upper,
                    Text = _game.SubMapping[i] != '\0' ? _game.SubMapping[i].ToString() : "",
                };
                int ci = i;
                tb.TextChanged += (_, _) =>
                {
                    string t = tb.Text.Trim().ToUpper();
                    _game.SubMapping[ci] = t.Length > 0 ? t[0] : '\0';
                    _game.StartTimer();
                    RefreshDecrypted();
                };
                inputs.Children.Add(tb);
                _subBoxes[i] = tb;
            }

            ToolPanel.Children.Add(header);
            ToolPanel.Children.Add(inputs);
            if (half == 0) ToolPanel.Children.Add(new System.Windows.Controls.Separator
                { Margin = new Thickness(0, 4, 0, 4), Background = new SolidColorBrush(WpfColor.FromRgb(0x33, 0x33, 0x33)) });
        }
    }

    private void BuildRailFencePanel()
    {
        AddLabel("레일 수 (2~6)");
        var row = NewRow();

        _railSlider = new Slider
        {
            Minimum = 2, Maximum = 6, Value = _game.RailCount,
            SmallChange = 1, LargeChange = 1,
            Width = 300, VerticalAlignment = VerticalAlignment.Center,
        };
        var valTxt = new TextBlock
        {
            Text = _game.RailCount.ToString(),
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xC1, 0x07)),
            Margin = new Thickness(12, 0, 0, 0), Width = 20,
        };

        _railSlider.ValueChanged += (_, e) =>
        {
            _game.RailCount = (int)e.NewValue;
            valTxt.Text = _game.RailCount.ToString();
            _game.StartTimer();
            RefreshDecrypted();
        };

        row.Children.Add(_railSlider);
        row.Children.Add(valTxt);
        ToolPanel.Children.Add(row);
    }

    private void BuildEnigmaPanel()
    {
        AddLabel("로터 초기 위치 설정 (각각 A~Z)");
        var row = NewRow();

        _enigmaR1 = MakeRotorCombo(_game.Rotor1);
        _enigmaR2 = MakeRotorCombo(_game.Rotor2);
        _enigmaR3 = MakeRotorCombo(_game.Rotor3);

        _enigmaR1.SelectionChanged += (_, _) => { if (_enigmaR1.SelectedItem is char c) { _game.Rotor1 = c; _game.StartTimer(); RefreshDecrypted(); } };
        _enigmaR2.SelectionChanged += (_, _) => { if (_enigmaR2.SelectedItem is char c) { _game.Rotor2 = c; _game.StartTimer(); RefreshDecrypted(); } };
        _enigmaR3.SelectionChanged += (_, _) => { if (_enigmaR3.SelectedItem is char c) { _game.Rotor3 = c; _game.StartTimer(); RefreshDecrypted(); } };

        foreach (var (lbl, cb) in new[] { ("로터 I", _enigmaR1), ("로터 II", _enigmaR2), ("로터 III", _enigmaR3) })
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, Foreground = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x88)) });
            sp.Children.Add(cb);
            row.Children.Add(sp);
        }
        ToolPanel.Children.Add(row);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private void AddLabel(string text)
    {
        ToolPanel.Children.Add(new TextBlock
        {
            Text = text, FontSize = 11,
            Foreground = new SolidColorBrush(WpfColor.FromRgb(0x80, 0x80, 0x80)),
            Margin = new Thickness(0, 0, 0, 6),
        });
    }

    private void AddAlphabetReference()
    {
        var row = NewRow();
        for (int i = 0; i < 26; i++)
        {
            int shift = _game.CaesarShift;
            char enc = (char)(((i + shift) % 26) + 'A');
            row.Children.Add(new TextBlock
            {
                Text = $"{(char)('A' + i)}→{enc}",
                FontSize = 9, Foreground = new SolidColorBrush(WpfColor.FromRgb(0x60, 0x60, 0x60)),
                Margin = new Thickness(0, 0, 3, 0),
            });
        }
        ToolPanel.Children.Add(row);
    }

    private static StackPanel NewRow() => new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 0, 0, 4),
    };

    private static ComboBox MakeRotorCombo(char selected)
    {
        var cb = new ComboBox { Width = 60, Margin = new Thickness(0, 0, 0, 0) };
        for (char c = 'A'; c <= 'Z'; c++) cb.Items.Add(c);
        cb.SelectedItem = selected;
        return cb;
    }

    // ── 복호화 갱신 ──────────────────────────────────────────────────

    private void RefreshDecrypted()
    {
        TxtDecrypted.Text = _game.Decrypt();

        // Caesar: 알파벳 참조 갱신
        if (_game.Type == CipherType.Caesar && ToolPanel.Children.Count > 2)
        {
            if (ToolPanel.Children[^1] is StackPanel refRow)
            {
                for (int i = 0; i < 26 && i < refRow.Children.Count; i++)
                {
                    int shift = _game.CaesarShift;
                    char enc = (char)(((i + shift) % 26) + 'A');
                    if (refRow.Children[i] is TextBlock tb)
                        tb.Text = $"{(char)('A' + i)}→{enc}";
                }
            }
        }
    }

    // ── 이벤트 ───────────────────────────────────────────────────────

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        bool ok = _game.CheckAnswer();
        if (ok)
        {
            int s = _game.GetStars(_game.CurrentChapter.Number - 1, _game.CurrentPuzzle.Number - 1);
            TxtStatus.Text = $"🎉 정답!  {"★".PadRight(s, '★').PadRight(3, '☆')}  {_game.ElapsedSeconds}초  |  {_game.CurrentPuzzle.History}";
            TxtDecrypted.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x4C, 0xAF, 0x50));
        }
        else
        {
            TxtStatus.Text = "❌ 틀렸습니다. 설정을 다시 조정해보세요.";
        }
    }

    private void OnHint(object sender, RoutedEventArgs e) =>
        TxtStatus.Text = $"💡 {_game.CurrentPuzzle.Hint}";

    private void OnPrev(object sender, RoutedEventArgs e)
    {
        if (_game.PrevPuzzle())
        {
            TxtDecrypted.ClearValue(TextBlock.ForegroundProperty);
            LoadPuzzle();
        }
    }

    private void OnNextPuzzle(object sender, RoutedEventArgs e)
    {
        if (_game.NextPuzzle())
        {
            TxtDecrypted.ClearValue(TextBlock.ForegroundProperty);
            LoadPuzzle();
        }
    }

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }
}
