using System.Windows.Threading;
using Geo.Quiz.Data;
using Geo.Quiz.Services;

namespace Geo.Quiz.ViewModels;

public class MainViewModel : BaseViewModel
{
    // ── 설정 화면 ────────────────────────────────────────────────────────────
    QuizMode _mode      = QuizMode.Capital;
    string   _continent = "전체";
    int      _questionCount = 10;
    bool     _timerMode;

    public QuizMode Mode          { get => _mode;          set { Set(ref _mode, value);          StartCmd.Raise(); SaveSettings(); } }
    public string   Continent     { get => _continent;     set { Set(ref _continent, value);     StartCmd.Raise(); SaveSettings(); } }
    public int      QuestionCount { get => _questionCount; set { Set(ref _questionCount, value); StartCmd.Raise(); SaveSettings(); } }
    public bool     TimerMode     { get => _timerMode;     set { Set(ref _timerMode, value);                       SaveSettings(); } }

    // ── 타이머 ───────────────────────────────────────────────────────────────
    const int TimerSeconds = 15;
    int _timeLeft;
    DispatcherTimer? _timer;

    public int TimeLeft { get => _timeLeft; private set { Set(ref _timeLeft, value); Notify(nameof(TimerPct)); } }
    public int TimerPct => TimerSeconds == 0 ? 0 : TimeLeft * 100 / TimerSeconds;

    public IReadOnlyList<string> Continents  { get; } = ["전체", "아시아", "유럽", "아프리카", "아메리카", "오세아니아"];
    public IReadOnlyList<int>    CountOptions { get; } = [5, 10, 15, 20];

    static readonly Dictionary<string, string> _korToEng = new()
    {
        ["아시아"]    = "Asia",
        ["유럽"]      = "Europe",
        ["아프리카"]  = "Africa",
        ["아메리카"]  = "Americas",
        ["오세아니아"]= "Oceania",
    };

    static readonly Dictionary<string, string> _engToKor = new()
    {
        ["Asia"]     = "아시아",
        ["Europe"]   = "유럽",
        ["Africa"]   = "아프리카",
        ["Americas"] = "아메리카",
        ["Oceania"]  = "오세아니아",
    };

    // ── 퀴즈 화면 ────────────────────────────────────────────────────────────
    QuizScreen _screen = QuizScreen.Start;
    int        _currentIndex;
    string?    _selectedAnswer;
    bool       _answered;
    bool       _isPaused;

    public QuizScreen Screen         { get => _screen;         private set { Set(ref _screen, value); Notify(nameof(IsStartScreen)); Notify(nameof(IsQuizScreen)); Notify(nameof(IsResultScreen)); RaiseAll(); } }
    public bool       IsStartScreen  => _screen == QuizScreen.Start;
    public bool       IsQuizScreen   => _screen == QuizScreen.Quiz;
    public bool       IsResultScreen => _screen == QuizScreen.Result;
    public int        CurrentIndex   { get => _currentIndex;   private set => Set(ref _currentIndex, value); }
    public string?    SelectedAnswer { get => _selectedAnswer; private set => Set(ref _selectedAnswer, value); }
    public bool       Answered       { get => _answered;       private set => Set(ref _answered, value); }
    public bool       IsPaused       { get => _isPaused;       private set { Set(ref _isPaused, value); Notify(nameof(PauseLabel)); PauseCmd.Raise(); } }
    public string     PauseLabel     => _isPaused ? "▶ 재개" : "⏸ 일시정지";

    List<QuizQuestion> _questions    = [];
    List<string?>      _userAnswers  = [];
    List<bool>         _hintUsed     = [];

    // ── 힌트 ────────────────────────────────────────────────────────────────
    string? _eliminatedChoice;
    bool    _hintUsedThisQuestion;

    public string? EliminatedChoice { get => _eliminatedChoice; private set => Set(ref _eliminatedChoice, value); }
    public bool    HintAvailable    => Screen == QuizScreen.Quiz && !Answered && !_hintUsedThisQuestion && CurrentQuestion != null && !IsPaused;

    public QuizQuestion? CurrentQuestion =>
        _currentIndex < _questions.Count ? _questions[_currentIndex] : null;

    public int    Total        => _questions.Count;
    public int    ProgressPct  => Total == 0 ? 0 : (CurrentIndex + 1) * 100 / Total;
    public string ProgressText => $"{CurrentIndex + 1} / {Total}";

    // 선택지 배경색 (기본/정답/오답)
    public string GetChoiceBrush(string choice)
    {
        if (!Answered) return "#1E1E2E";
        if (choice == CurrentQuestion?.CorrectAnswer) return "#0F2D1F";
        if (choice == SelectedAnswer)                 return "#2D0F0F";
        return "#1E1E2E";
    }
    public string GetChoiceFore(string choice)
    {
        if (!Answered) return "#C0C0D0";
        if (choice == CurrentQuestion?.CorrectAnswer) return "#4ADE80";
        if (choice == SelectedAnswer)                 return "#F87171";
        return "#606070";
    }

    // ── 결과 화면 ────────────────────────────────────────────────────────────
    QuizResult? _result;
    public QuizResult? Result    { get => _result; private set => Set(ref _result, value); }
    public int         BestScore => RecordService.GetBest(Mode.ToString(), Continent);
    public bool        IsNewRecord { get; private set; }

    // ── 커맨드 ───────────────────────────────────────────────────────────────
    public RelayCommand StartCmd     { get; }
    public RelayCommand RestartCmd   { get; }
    public RelayCommand Choice0Cmd   { get; }
    public RelayCommand Choice1Cmd   { get; }
    public RelayCommand Choice2Cmd   { get; }
    public RelayCommand Choice3Cmd   { get; }
    public RelayCommand NextCmd      { get; }
    public RelayCommand HintCmd      { get; }
    public RelayCommand RetryWrongCmd{ get; }
    public RelayCommand PauseCmd     { get; }

    static readonly Random _rng = new();

    public MainViewModel()
    {
        // 저장된 설정 복원
        var s = SettingsService.Load();
        if (Enum.TryParse<QuizMode>(s.Mode, out var m)) _mode = m;
        _continent     = s.Continent;
        _questionCount = s.QuestionCount;
        _timerMode     = s.TimerMode;

        StartCmd      = new(DoStart,      () => GetPool().Count >= 4);
        RestartCmd    = new(DoRestart);
        Choice0Cmd    = new(() => SelectAnswer(0), () => Screen == QuizScreen.Quiz && !Answered && !IsPaused);
        Choice1Cmd    = new(() => SelectAnswer(1), () => Screen == QuizScreen.Quiz && !Answered && !IsPaused);
        Choice2Cmd    = new(() => SelectAnswer(2), () => Screen == QuizScreen.Quiz && !Answered && !IsPaused);
        Choice3Cmd    = new(() => SelectAnswer(3), () => Screen == QuizScreen.Quiz && !Answered && !IsPaused);
        NextCmd       = new(DoNext,        () => Answered);
        HintCmd       = new(DoHint,        () => HintAvailable);
        RetryWrongCmd = new(DoRetryWrong,  () => Result?.WrongItems.Count > 0);
        PauseCmd      = new(DoTogglePause, () => TimerMode && Screen == QuizScreen.Quiz && !Answered);
    }

    void SaveSettings() =>
        SettingsService.Save(new AppSettings
        {
            Mode          = _mode.ToString(),
            Continent     = _continent,
            QuestionCount = _questionCount,
            TimerMode     = _timerMode,
        });

    IReadOnlyList<Country> GetPool()
    {
        var all = CountryDb.All;
        if (Continent == "전체") return all;
        var eng = _korToEng.TryGetValue(Continent, out var v) ? v : Continent;
        return all.Where(c => c.Continent == eng).ToList();
    }

    void DoStart()
    {
        var pool = GetPool().ToList();
        if (pool.Count < 4) return;

        var subjects = pool.OrderBy(_ => _rng.Next()).Take(QuestionCount).ToList();
        _questions   = subjects.Select(s => BuildQuestion(s, pool)).ToList();
        _userAnswers = Enumerable.Repeat<string?>(null, _questions.Count).ToList();
        _hintUsed    = Enumerable.Repeat(false, _questions.Count).ToList();

        _currentIndex         = 0;
        _hintUsedThisQuestion = false;
        SelectedAnswer        = null;
        EliminatedChoice      = null;
        Answered              = false;
        IsPaused              = false;
        Result                = null;
        Screen                = QuizScreen.Quiz;
        NotifyQuizProps();
        HintCmd.Raise();
        Notify(nameof(HintAvailable));
        StartTimer();
    }

    QuizQuestion BuildQuestion(Country subject, List<Country> pool)
    {
        string question, correct;
        List<string> distractors;

        switch (Mode)
        {
            case QuizMode.Capital:
                question    = $"다음 나라의 수도는?  {subject.KorName}";
                correct     = subject.KorCapital;
                distractors = pool.Where(c => c != subject)
                                  .OrderBy(_ => _rng.Next())
                                  .Take(3)
                                  .Select(c => c.KorCapital)
                                  .ToList();
                break;
            case QuizMode.Flag:
                question    = "이 국기는 어느 나라?";
                correct     = subject.KorName;
                distractors = pool.Where(c => c != subject)
                                  .OrderBy(_ => _rng.Next())
                                  .Take(3)
                                  .Select(c => c.KorName)
                                  .ToList();
                break;
            default: // Continent
                question    = $"{subject.KorName}은(는) 어느 대륙?";
                correct     = _engToKor.TryGetValue(subject.Continent, out var kor) ? kor : subject.Continent;
                var continents = new[] { "아시아", "유럽", "아프리카", "아메리카", "오세아니아" };
                distractors = continents.Where(c => c != correct)
                                        .OrderBy(_ => _rng.Next())
                                        .Take(3)
                                        .ToList();
                break;
        }

        var choices = distractors.Prepend(correct).OrderBy(_ => _rng.Next()).ToList();
        return new QuizQuestion
        {
            Subject       = subject,
            QuestionText  = question,
            FlagIsoCode   = Mode == QuizMode.Flag ? GetIsoCode(subject.FlagEmoji) : "",
            CorrectAnswer = correct,
            Choices       = choices,
        };
    }

    static string GetIsoCode(string flagEmoji) =>
        string.Concat(
            flagEmoji.EnumerateRunes()
                     .Where(r => r.Value is >= 0x1F1E6 and <= 0x1F1FF)
                     .Select(r => (char)('A' + r.Value - 0x1F1E6)));

    void DoHint()
    {
        if (CurrentQuestion == null || _hintUsedThisQuestion || IsPaused) return;

        var wrongChoices = CurrentQuestion.Choices
            .Where(c => c != CurrentQuestion.CorrectAnswer && c != _eliminatedChoice)
            .ToList();
        if (wrongChoices.Count == 0) return;

        _hintUsedThisQuestion    = true;
        _hintUsed[_currentIndex] = true;
        EliminatedChoice         = wrongChoices[_rng.Next(wrongChoices.Count)];
        HintCmd.Raise();
        Notify(nameof(HintAvailable));
        NotifyChoiceProps();
    }

    void DoRetryWrong()
    {
        if (Result == null || Result.WrongItems.Count == 0) return;

        var wrongQuestions = _questions
            .Zip(_userAnswers)
            .Where(t => t.First.CorrectAnswer != t.Second)
            .Select(t => new QuizQuestion
            {
                Subject       = t.First.Subject,
                QuestionText  = t.First.QuestionText,
                FlagIsoCode   = t.First.FlagIsoCode,
                CorrectAnswer = t.First.CorrectAnswer,
                Choices       = t.First.Choices.OrderBy(_ => _rng.Next()).ToList(),
            })
            .ToList();

        if (wrongQuestions.Count == 0) return;

        _questions   = wrongQuestions;
        _userAnswers = Enumerable.Repeat<string?>(null, _questions.Count).ToList();
        _hintUsed    = Enumerable.Repeat(false, _questions.Count).ToList();

        _currentIndex         = 0;
        _hintUsedThisQuestion = false;
        SelectedAnswer        = null;
        EliminatedChoice      = null;
        Answered              = false;
        IsPaused              = false;
        Result                = null;
        Screen                = QuizScreen.Quiz;
        NotifyQuizProps();
        HintCmd.Raise();
        Notify(nameof(HintAvailable));
        StartTimer();
    }

    void SelectAnswer(int idx)
    {
        if (Answered || CurrentQuestion == null || IsPaused) return;
        if (idx >= CurrentQuestion.Choices.Count) return;
        if (CurrentQuestion.Choices[idx] == _eliminatedChoice) return;

        StopTimer();
        SelectedAnswer              = CurrentQuestion.Choices[idx];
        _userAnswers[_currentIndex] = SelectedAnswer;
        Answered                    = true;
        NextCmd.Raise();
        NotifyChoiceProps();
    }

    void DoNext()
    {
        if (_currentIndex < _questions.Count - 1)
        {
            CurrentIndex++;
            _hintUsedThisQuestion = false;
            SelectedAnswer        = null;
            EliminatedChoice      = null;
            Answered              = false;
            IsPaused              = false;
            NextCmd.Raise();
            HintCmd.Raise();
            Notify(nameof(HintAvailable));
            NotifyQuizProps();
            NotifyChoiceProps();
            StartTimer();
        }
        else
        {
            var pairs   = _questions.Zip(_userAnswers).ToList();
            int correct = pairs.Count(t => t.First.CorrectAnswer == t.Second);
            var wrongItems = pairs
                .Where(t => t.First.CorrectAnswer != t.Second)
                .Select(t => new WrongItem(
                    t.First.QuestionText,
                    t.Second ?? "미선택",
                    t.First.CorrectAnswer))
                .ToList();

            var result = new QuizResult
            {
                Total     = _questions.Count,
                Correct   = correct,
                Wrong     = _questions.Count - correct,
                HintsUsed = _hintUsed.Count(h => h),
                WrongItems = wrongItems,
            };
            IsNewRecord = RecordService.TryUpdate(Mode.ToString(), Continent, (int)result.Score);
            Result = result;
            Screen = QuizScreen.Result;
            RetryWrongCmd.Raise();
        }
    }

    void DoRestart()
    {
        StopTimer();
        IsPaused = false;
        Screen   = QuizScreen.Start;
        RaiseAll();
    }

    void DoTogglePause()
    {
        if (!TimerMode || Screen != QuizScreen.Quiz || Answered) return;
        IsPaused = !IsPaused;
        if (IsPaused) _timer?.Stop();
        else          _timer?.Start();
        // 일시정지 시 선택지 버튼 상태 갱신
        Choice0Cmd.Raise(); Choice1Cmd.Raise();
        Choice2Cmd.Raise(); Choice3Cmd.Raise();
        Notify(nameof(HintAvailable));
        HintCmd.Raise();
    }

    // ── 타이머 내부 ──────────────────────────────────────────────────────────
    void StartTimer()
    {
        StopTimer();
        if (!_timerMode) return;

        TimeLeft = TimerSeconds;
        _timer   = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            if (IsPaused) return;
            TimeLeft--;
            if (TimeLeft <= 0)
            {
                StopTimer();
                if (!Answered && CurrentQuestion != null)
                {
                    _userAnswers[_currentIndex] = null;
                    Answered = true;
                    NextCmd.Raise();
                    NotifyChoiceProps();

                    var autoNext = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                    autoNext.Tick += (_, _) =>
                    {
                        autoNext.Stop();
                        if (Answered) DoNext();
                    };
                    autoNext.Start();
                }
            }
        };
        _timer.Start();
    }

    void StopTimer()
    {
        _timer?.Stop();
        _timer   = null;
        TimeLeft = 0;
    }

    void NotifyQuizProps()
    {
        Notify(nameof(CurrentQuestion));
        Notify(nameof(ProgressPct));
        Notify(nameof(ProgressText));
    }

    void NotifyChoiceProps()
    {
        Notify(nameof(SelectedAnswer));
        Notify(nameof(Answered));
        Notify(nameof(CurrentQuestion));
    }

    void RaiseAll()
    {
        StartCmd.Raise();
        Choice0Cmd.Raise(); Choice1Cmd.Raise();
        Choice2Cmd.Raise(); Choice3Cmd.Raise();
        NextCmd.Raise();
        HintCmd.Raise();
        RetryWrongCmd.Raise();
        RestartCmd.Raise();
        PauseCmd.Raise();
        Notify(nameof(HintAvailable));
    }
}
