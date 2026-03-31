using System.Windows.Media.Effects;

namespace TriviaCast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly StorageService _storage = new();

    // 게임 상태
    private List<Question> _questions = [];
    private int _currentIndex = 0;
    private int _score = 0;
    private int _correctCount = 0;
    private int _streak = 0;
    private int _maxStreak = 0;
    private int _timerSeconds = 15;
    private DispatcherTimer? _timer;
    private string _currentMode = "normal";
    private string? _lastCategory = null;
    private List<(Question q, string chosen)> _wrongAnswers = [];
    private bool _answerGiven = false;

    // 진행바 너비 캐시
    private double _progressBarMaxWidth = 0;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        RefreshMenuState();
        SizeChanged += (_, _) => _progressBarMaxWidth = 0;
    }

    // ─── 메뉴 화면 ────────────────────────────────────────────────────────────

    private void RefreshMenuState()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var daily = _storage.GetDailyChallenge(today);
        if (daily.HasValue)
        {
            DailySubText.Text = $"오늘 완료 ✓  {daily.Value.score}/{daily.Value.total}점";
            DailyArrow.Text = "✓";
            DailyArrow.Foreground = (SolidColorBrush)FindResource("GreenBrush");
        }
    }

    private void NormalModeCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => StartGame("normal", null, 20);

    private void CategoryModeCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ShowCategoryScreen();

    private void DailyModeCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (_storage.IsDailyChallengeCompleted(today))
        {
            var d = _storage.GetDailyChallenge(today)!.Value;
            MessageBox.Show($"오늘의 데일리 챌린지를 이미 완료했습니다!\n결과: {d.score}/{d.total}점",
                "데일리 챌린지", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rng = new Random(int.Parse(today.Replace("-", "")));
        var allQ = QuestionDatabase.All.ToList();
        var dailyQ = allQ.OrderBy(_ => rng.Next()).Take(10).ToList();
        StartGame("daily", null, 10, dailyQ);
    }

    private void WrongReviewBtn_Click(object sender, RoutedEventArgs e) => ShowWrongScreen();
    private void ScoreBoardBtn_Click(object sender, RoutedEventArgs e) => ShowScoreScreen();

    // ─── 카테고리 선택 ─────────────────────────────────────────────────────────

    private void ShowCategoryScreen()
    {
        CategoryPanel.Children.Clear();
        foreach (var (key, label) in QuestionDatabase.CategoryLabels)
        {
            var btn = new Border
            {
                Width = 140, Height = 70,
                Margin = new Thickness(6),
                Background = (SolidColorBrush)FindResource("SurfaceBrush"),
                BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = CategoryEmoji(key),
                FontSize = 22,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = (SolidColorBrush)FindResource("TextPrimary"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
            });
            btn.Child = sp;

            string capturedKey = key;
            btn.MouseEnter += (_, _) => btn.Background = (SolidColorBrush)FindResource("Surface2Brush");
            btn.MouseLeave += (_, _) => btn.Background = (SolidColorBrush)FindResource("SurfaceBrush");
            btn.MouseLeftButtonUp += (_, _) => StartGame("category", capturedKey, 10);

            CategoryPanel.Children.Add(btn);
        }

        Show(CategoryScreen);
    }

    private static string CategoryEmoji(string key) => key switch
    {
        "science"   => "🔬",
        "history"   => "📜",
        "it"        => "💻",
        "sports"    => "⚽",
        "movies"    => "🎬",
        "music"     => "🎵",
        "food"      => "🍜",
        "geography" => "🌍",
        "math"      => "🔢",
        "art"       => "🎨",
        "games"     => "🎮",
        "general"   => "🌟",
        _ => "❓",
    };

    // ─── 게임 시작 ─────────────────────────────────────────────────────────────

    private void StartGame(string mode, string? category, int count, List<Question>? fixedList = null)
    {
        _currentMode = mode;
        _lastCategory = category;
        _score = 0;
        _correctCount = 0;
        _streak = 0;
        _maxStreak = 0;
        _wrongAnswers = [];
        _currentIndex = 0;
        _answerGiven = false;

        if (fixedList != null)
        {
            _questions = fixedList;
        }
        else
        {
            var pool = category == null
                ? QuestionDatabase.All.ToList()
                : QuestionDatabase.All.Where(q => q.Category == category).ToList();
            _questions = pool.OrderBy(_ => Random.Shared.Next()).Take(Math.Min(count, pool.Count)).ToList();
        }

        _progressBarMaxWidth = 0;
        FeedbackOverlay.Visibility = Visibility.Collapsed;
        Show(QuizScreen);
        ShowQuestion();
    }

    // ─── 퀴즈 화면 ────────────────────────────────────────────────────────────

    private void ShowQuestion()
    {
        if (_currentIndex >= _questions.Count)
        {
            EndGame();
            return;
        }

        _answerGiven = false;
        FeedbackOverlay.Visibility = Visibility.Collapsed;

        var q = _questions[_currentIndex];

        QuestionProgress.Text = $"{_currentIndex + 1} / {_questions.Count}";
        CategoryLabel.Text = QuestionDatabase.CategoryLabels.TryGetValue(q.Category, out var lbl) ? lbl : q.Category;
        QuestionText.Text = q.Text;
        ScoreText.Text = $"점수: {_score}";
        StreakText.Text = _streak >= 3 ? $"🔥 {_streak}연속!" : "";
        TimerText.Text = "15";
        TimerText.Foreground = (SolidColorBrush)FindResource("GoldBrush");

        // 진행바
        if (_progressBarMaxWidth == 0)
            _progressBarMaxWidth = ((Grid)QuizScreen.Children[1]).ActualWidth - 48;
        if (_progressBarMaxWidth > 0)
            ProgressBar.Width = _progressBarMaxWidth * _currentIndex / _questions.Count;

        // 보기 셔플
        var answers = new[] { q.Correct, q.Wrong1, q.Wrong2, q.Wrong3 }
            .OrderBy(_ => Random.Shared.Next()).ToArray();

        AnswerGrid.Children.Clear();
        string[] prefixes = ["A", "B", "C", "D"];
        for (int i = 0; i < 4; i++)
        {
            var ans = answers[i];
            var btn = MakeAnswerButton(prefixes[i], ans, q);
            AnswerGrid.Children.Add(btn);
        }

        // 타이머 시작
        _timer?.Stop();
        _timerSeconds = 15;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private Border MakeAnswerButton(string prefix, string text, Question q)
    {
        var border = new Border
        {
            Margin = new Thickness(4),
            Background = (SolidColorBrush)FindResource("SurfaceBrush"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        border.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, Opacity = 0.2, ShadowDepth = 1 };

        var grid = new System.Windows.Controls.Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = prefix,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = (SolidColorBrush)FindResource("AccentBrush"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };

        var val = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 10, 12, 10),
        };

        System.Windows.Controls.Grid.SetColumn(label, 0);
        System.Windows.Controls.Grid.SetColumn(val, 1);
        grid.Children.Add(label);
        grid.Children.Add(val);
        border.Child = grid;

        border.MouseEnter += (_, _) =>
        {
            if (!_answerGiven)
                border.Background = (SolidColorBrush)FindResource("Surface2Brush");
        };
        border.MouseLeave += (_, _) =>
        {
            if (!_answerGiven)
                border.Background = (SolidColorBrush)FindResource("SurfaceBrush");
        };
        border.MouseLeftButtonUp += (_, _) => OnAnswerSelected(text, q, border);

        return border;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timerSeconds--;
        TimerText.Text = _timerSeconds.ToString();

        if (_timerSeconds <= 5)
            TimerText.Foreground = (SolidColorBrush)FindResource("RedBrush");
        else if (_timerSeconds <= 10)
            TimerText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00));

        if (_timerSeconds <= 0)
        {
            _timer?.Stop();
            if (!_answerGiven)
                OnTimeUp(_questions[_currentIndex]);
        }
    }

    private void OnAnswerSelected(string chosen, Question q, Border? clickedBorder)
    {
        if (_answerGiven) return;
        _answerGiven = true;
        _timer?.Stop();

        bool correct = chosen == q.Correct;
        if (correct)
        {
            _correctCount++;
            _streak++;
            _maxStreak = Math.Max(_maxStreak, _streak);
            int bonus = _streak >= 5 ? 150 : _streak >= 3 ? 120 : 100;
            _score += bonus;

            // 정답 버튼 초록
            if (clickedBorder != null)
                clickedBorder.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x55, 0x30));

            ShowFeedback(true, q, null);
        }
        else
        {
            _streak = 0;
            _wrongAnswers.Add((q, chosen));
            _storage.SaveWrongAnswer(q.Category, q.Text, q.Correct, chosen);

            // 오답 버튼 빨강, 정답 버튼 초록
            if (clickedBorder != null)
                clickedBorder.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x10, 0x10));
            HighlightCorrectButton(q.Correct);

            ShowFeedback(false, q, chosen);
        }
    }

    private void OnTimeUp(Question q)
    {
        _answerGiven = true;
        _streak = 0;
        _wrongAnswers.Add((q, "(시간 초과)"));
        _storage.SaveWrongAnswer(q.Category, q.Text, q.Correct, "(시간 초과)");
        HighlightCorrectButton(q.Correct);
        ShowFeedback(false, q, "(시간 초과)");
    }

    private void HighlightCorrectButton(string correct)
    {
        foreach (Border b in AnswerGrid.Children)
        {
            if (b.Child is System.Windows.Controls.Grid g &&
                g.Children.OfType<TextBlock>().Skip(1).FirstOrDefault()?.Text == correct)
            {
                b.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x55, 0x30));
                break;
            }
        }
    }

    private void ShowFeedback(bool correct, Question q, string? chosen)
    {
        FeedbackIcon.Text = correct ? "✅" : "❌";
        FeedbackTitle.Text = correct ? "정답!" : (chosen == "(시간 초과)" ? "⏰ 시간 초과!" : "오답");
        FeedbackTitle.Foreground = correct
            ? (SolidColorBrush)FindResource("GreenBrush")
            : (SolidColorBrush)FindResource("RedBrush");
        FeedbackCorrect.Text = correct ? "" : $"정답: {q.Correct}";
        FeedbackExplanation.Text = q.Explanation;
        FeedbackOverlay.Visibility = Visibility.Visible;

        var delay = correct ? 1400 : 2200;
        Task.Delay(delay).ContinueWith(_ => Dispatcher.Invoke(() =>
        {
            FeedbackOverlay.Visibility = Visibility.Collapsed;
            _currentIndex++;
            ShowQuestion();
        }));
    }

    // ─── 게임 종료 ─────────────────────────────────────────────────────────────

    private void EndGame()
    {
        _timer?.Stop();
        FeedbackOverlay.Visibility = Visibility.Collapsed;

        _storage.SaveScore(_currentMode, _score, _questions.Count, _maxStreak);

        if (_currentMode == "daily")
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            _storage.SaveDailyChallenge(today, _score, _questions.Count);
        }

        ShowResultScreen();
    }

    private void ShowResultScreen()
    {
        double accuracy = _questions.Count > 0 ? _correctCount * 100.0 / _questions.Count : 0;

        ResultEmoji.Text = accuracy >= 90 ? "🏆" : accuracy >= 70 ? "⭐" : accuracy >= 50 ? "👍" : "📚";
        ResultTitle.Text = accuracy >= 90 ? "완벽합니다!" : accuracy >= 70 ? "훌륭해요!" : accuracy >= 50 ? "잘 했어요!" : "더 공부해봐요!";
        ResultTitle.Foreground = accuracy >= 70
            ? (SolidColorBrush)FindResource("GoldBrush")
            : (SolidColorBrush)FindResource("AccentBrush");
        ResultSubtitle.Text = $"{_correctCount}/{_questions.Count} 문제 정답";
        ResultScore.Text = _score.ToString();
        ResultAccuracy.Text = $"{accuracy:0}%";
        ResultStreak.Text = _maxStreak.ToString();

        // 오답 목록
        WrongAnswerList.Children.Clear();
        if (_wrongAnswers.Count == 0)
        {
            WrongAnswerList.Children.Add(new TextBlock
            {
                Text = "🎉 오답이 없습니다!",
                FontSize = 16,
                Foreground = (SolidColorBrush)FindResource("GreenBrush"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
            });
        }
        else
        {
            WrongAnswerList.Children.Add(new TextBlock
            {
                Text = "오답 목록",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 0, 0, 8),
            });
            foreach (var (q, chosen) in _wrongAnswers)
                WrongAnswerList.Children.Add(MakeWrongRow(q, chosen));
        }

        Show(ResultScreen);
    }

    private Border MakeWrongRow(Question q, string chosen)
    {
        var border = new Border
        {
            Background = (SolidColorBrush)FindResource("SurfaceBrush"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 4, 0, 4),
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = q.Text,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            TextWrapping = TextWrapping.Wrap,
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"내 답: {chosen}",
            FontSize = 12,
            Foreground = (SolidColorBrush)FindResource("RedBrush"),
            Margin = new Thickness(0, 3, 0, 0),
        });
        sp.Children.Add(new TextBlock
        {
            Text = $"정답: {q.Correct}",
            FontSize = 12,
            Foreground = (SolidColorBrush)FindResource("GreenBrush"),
        });
        if (!string.IsNullOrEmpty(q.Explanation))
            sp.Children.Add(new TextBlock
            {
                Text = q.Explanation,
                FontSize = 11,
                Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0),
            });
        border.Child = sp;
        return border;
    }

    // ─── 오답 노트 화면 ────────────────────────────────────────────────────────

    private void ShowWrongScreen()
    {
        WrongNoteList.Children.Clear();
        var wrongs = _storage.GetWrongAnswers(50);
        if (wrongs.Count == 0)
        {
            WrongNoteList.Children.Add(new TextBlock
            {
                Text = "아직 오답 기록이 없습니다.",
                FontSize = 15,
                Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }
        else
        {
            foreach (var (cat, question, correct, chosen) in wrongs)
            {
                var border = new Border
                {
                    Background = (SolidColorBrush)FindResource("SurfaceBrush"),
                    BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 4, 0, 4),
                };
                var sp = new StackPanel();
                if (QuestionDatabase.CategoryLabels.TryGetValue(cat, out var catLabel))
                    sp.Children.Add(new TextBlock
                    {
                        Text = catLabel,
                        FontSize = 11,
                        Foreground = (SolidColorBrush)FindResource("AccentBrush"),
                        Margin = new Thickness(0, 0, 0, 3),
                    });
                sp.Children.Add(new TextBlock
                {
                    Text = question,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (SolidColorBrush)FindResource("TextPrimary"),
                    TextWrapping = TextWrapping.Wrap,
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"내 답: {chosen}  →  정답: {correct}",
                    FontSize = 12,
                    Foreground = (SolidColorBrush)FindResource("GreenBrush"),
                    Margin = new Thickness(0, 3, 0, 0),
                });
                border.Child = sp;
                WrongNoteList.Children.Add(border);
            }
        }

        Show(WrongScreen);
    }

    // ─── 점수 기록 화면 ────────────────────────────────────────────────────────

    private void ShowScoreScreen()
    {
        ScoreList.Children.Clear();
        var scores = _storage.GetTopScores(20);
        if (scores.Count == 0)
        {
            ScoreList.Children.Add(new TextBlock
            {
                Text = "아직 점수 기록이 없습니다.",
                FontSize = 15,
                Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }
        else
        {
            string[] medals = ["🥇", "🥈", "🥉"];
            for (int i = 0; i < scores.Count; i++)
            {
                var (mode, s, total, streak, date) = scores[i];
                var border = new Border
                {
                    Background = (SolidColorBrush)FindResource("SurfaceBrush"),
                    BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 4, 0, 4),
                };
                var grid = new System.Windows.Controls.Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var medal = new TextBlock
                {
                    Text = i < 3 ? medals[i] : $"{i + 1}",
                    FontSize = i < 3 ? 20 : 14,
                    Foreground = (SolidColorBrush)FindResource("GoldBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                string modeLabel = mode switch
                {
                    "normal" => "일반",
                    "category" => "카테고리",
                    "daily" => "데일리",
                    _ => mode,
                };
                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock
                {
                    Text = $"{modeLabel} 모드  🔥 {streak}연속",
                    FontSize = 12,
                    Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                });
                info.Children.Add(new TextBlock
                {
                    Text = date,
                    FontSize = 11,
                    Foreground = (SolidColorBrush)FindResource("TextSecondary"),
                });

                var scoreText = new TextBlock
                {
                    Text = $"{s}점",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = (SolidColorBrush)FindResource("AccentBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                System.Windows.Controls.Grid.SetColumn(medal, 0);
                System.Windows.Controls.Grid.SetColumn(info, 1);
                System.Windows.Controls.Grid.SetColumn(scoreText, 2);
                grid.Children.Add(medal);
                grid.Children.Add(info);
                grid.Children.Add(scoreText);
                border.Child = grid;
                ScoreList.Children.Add(border);
            }
        }

        Show(ScoreScreen);
    }

    // ─── 버튼 이벤트 ──────────────────────────────────────────────────────────

    private void BackToMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshMenuState();
        Show(MenuScreen);
    }

    private void QuitBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        RefreshMenuState();
        Show(MenuScreen);
    }

    private void PlayAgainBtn_Click(object sender, RoutedEventArgs e)
        => StartGame(_currentMode, _lastCategory, _questions.Count);

    private void MainMenuBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshMenuState();
        Show(MenuScreen);
    }

    // ─── 헬퍼 ─────────────────────────────────────────────────────────────────

    private void Show(UIElement target)
    {
        UIElement[] all = [MenuScreen, CategoryScreen, QuizScreen, ResultScreen, WrongScreen, ScoreScreen];
        foreach (var el in all)
            el.Visibility = el == target ? Visibility.Visible : Visibility.Collapsed;
    }
}
