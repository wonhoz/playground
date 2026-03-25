using System.Windows.Threading;
using Geo.Quiz.Data;
using Geo.Quiz.Services;

namespace Geo.Quiz.ViewModels;

public class MainViewModel : BaseViewModel
{
    // ── 설정 화면 ────────────────────────────────────────────────────────────
    QuizMode _mode = QuizMode.Capital;
    string   _continent = "전체";
    int      _questionCount = 10;
    bool     _timerMode;

    public QuizMode Mode           { get => _mode;           set { Set(ref _mode, value);           StartCmd.Raise(); } }
    public string   Continent      { get => _continent;      set { Set(ref _continent, value);      StartCmd.Raise(); } }
    public int      QuestionCount  { get => _questionCount;  set { Set(ref _questionCount, value);  StartCmd.Raise(); } }
    public bool     TimerMode      { get => _timerMode;      set { Set(ref _timerMode, value); } }

    // ── 타이머 ───────────────────────────────────────────────────────────────
    const int TimerSeconds = 15;
    int _timeLeft;
    DispatcherTimer? _timer;

    public int  TimeLeft  { get => _timeLeft; private set { Set(ref _timeLeft, value); Notify(nameof(TimerPct)); } }
    public int  TimerPct  => TimerSeconds == 0 ? 0 : TimeLeft * 100 / TimerSeconds;

    public IReadOnlyList<string> Continents { get; } =
        ["전체", "아시아", "유럽", "아프리카", "아메리카", "오세아니아"];
    public IReadOnlyList<int> CountOptions { get; } = [5, 10, 15, 20];

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
        ["Asia"]      = "아시아",
        ["Europe"]    = "유럽",
        ["Africa"]    = "아프리카",
        ["Americas"]  = "아메리카",
        ["Oceania"]   = "오세아니아",
    };

    // ── 퀴즈 화면 ────────────────────────────────────────────────────────────
    QuizScreen _screen = QuizScreen.Start;
    int        _currentIndex;
    string?    _selectedAnswer;
    bool       _answered;

    public QuizScreen Screen        { get => _screen;         private set { Set(ref _screen, value); Notify(nameof(IsStartScreen)); Notify(nameof(IsQuizScreen)); Notify(nameof(IsResultScreen)); RaiseAll(); } }

    public bool IsStartScreen  => _screen == QuizScreen.Start;
    public bool IsQuizScreen   => _screen == QuizScreen.Quiz;
    public bool IsResultScreen => _screen == QuizScreen.Result;
    public int        CurrentIndex  { get => _currentIndex;   private set => Set(ref _currentIndex, value); }
    public string?    SelectedAnswer{ get => _selectedAnswer; private set => Set(ref _selectedAnswer, value); }
    public bool       Answered      { get => _answered;       private set => Set(ref _answered, value); }

    List<QuizQuestion> _questions = [];
    List<string?>      _userAnswers = [];

    public QuizQuestion? CurrentQuestion =>
        _currentIndex < _questions.Count ? _questions[_currentIndex] : null;

    public int Total        => _questions.Count;
    public int ProgressPct  => Total == 0 ? 0 : (CurrentIndex + 1) * 100 / Total;
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
    public QuizResult? Result { get => _result; private set => Set(ref _result, value); }

    // 최고 기록
    public int BestScore => RecordService.GetBest(Mode.ToString(), Continent);
    public bool IsNewRecord { get; private set; }

    // ── 커맨드 ───────────────────────────────────────────────────────────────
    public RelayCommand StartCmd   { get; }
    public RelayCommand RestartCmd { get; }

    // 선택지 선택 (파라미터로 전달하기 어려우므로 바인딩용 RelayCommand 4개)
    public RelayCommand Choice0Cmd { get; }
    public RelayCommand Choice1Cmd { get; }
    public RelayCommand Choice2Cmd { get; }
    public RelayCommand Choice3Cmd { get; }

    public RelayCommand NextCmd { get; }

    static readonly Random _rng = new();

    public MainViewModel()
    {
        StartCmd   = new(DoStart,   () => GetPool().Count >= 4);
        RestartCmd = new(DoRestart);
        Choice0Cmd = new(() => SelectAnswer(0), () => Screen == QuizScreen.Quiz && !Answered);
        Choice1Cmd = new(() => SelectAnswer(1), () => Screen == QuizScreen.Quiz && !Answered);
        Choice2Cmd = new(() => SelectAnswer(2), () => Screen == QuizScreen.Quiz && !Answered);
        Choice3Cmd = new(() => SelectAnswer(3), () => Screen == QuizScreen.Quiz && !Answered);
        NextCmd    = new(DoNext, () => Answered);
    }

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

        // 질문 목록 생성
        var subjects = pool.OrderBy(_ => _rng.Next()).Take(QuestionCount).ToList();
        _questions   = subjects.Select(s => BuildQuestion(s, pool)).ToList();
        _userAnswers = Enumerable.Repeat<string?>(null, _questions.Count).ToList();

        _currentIndex = 0;
        SelectedAnswer = null;
        Answered       = false;
        Result         = null;
        Screen         = QuizScreen.Quiz;
        NotifyQuizProps();
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
                var continents = new[] { "아시아","유럽","아프리카","아메리카","오세아니아" };
                distractors = continents.Where(c => c != correct)
                                        .OrderBy(_ => _rng.Next())
                                        .Take(3)
                                        .ToList();
                break;
        }

        var choices = distractors.Prepend(correct).OrderBy(_ => _rng.Next()).ToList();
        return new QuizQuestion
        {
            Subject      = subject,
            QuestionText = question,
            FlagIsoCode  = Mode == QuizMode.Flag ? GetIsoCode(subject.FlagEmoji) : "",
            CorrectAnswer= correct,
            Choices      = choices,
        };
    }

    // 국기 이모지(U+1F1E6~U+1F1FF 지역 표시 문자 쌍) → 2자리 ISO 코드
    static string GetIsoCode(string flagEmoji) =>
        string.Concat(
            flagEmoji.EnumerateRunes()
                     .Where(r => r.Value is >= 0x1F1E6 and <= 0x1F1FF)
                     .Select(r => (char)('A' + r.Value - 0x1F1E6)));

    void SelectAnswer(int idx)
    {
        if (Answered || CurrentQuestion == null) return;
        if (idx >= CurrentQuestion.Choices.Count) return;

        StopTimer();
        SelectedAnswer              = CurrentQuestion.Choices[idx];
        _userAnswers[_currentIndex] = SelectedAnswer;
        Answered             = true;
        NextCmd.Raise();
        Notify(nameof(GetChoiceBrush)); // force re-eval
        NotifyChoiceProps();
    }

    void DoNext()
    {
        if (_currentIndex < _questions.Count - 1)
        {
            CurrentIndex++;
            SelectedAnswer = null;
            Answered       = false;
            NextCmd.Raise();
            NotifyQuizProps();
            NotifyChoiceProps();
            StartTimer();
        }
        else
        {
            // 결과 집계
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
                Total      = _questions.Count,
                Correct    = correct,
                Wrong      = _questions.Count - correct,
                WrongItems = wrongItems,
            };
            IsNewRecord = RecordService.TryUpdate(Mode.ToString(), Continent, (int)result.Score);
            Result = result;
            Screen = QuizScreen.Result;
        }
    }

    void DoRestart()
    {
        StopTimer();
        Screen = QuizScreen.Start;
        RaiseAll();
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
            TimeLeft--;
            if (TimeLeft <= 0)
            {
                StopTimer();
                // 시간 초과 → 미선택 오답 처리
                if (!Answered && CurrentQuestion != null)
                {
                    _userAnswers[_currentIndex] = null;
                    Answered = true;
                    NextCmd.Raise();
                    NotifyChoiceProps();

                    // 1.5초 후 자동으로 다음 문제 진행
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
        _timer = null;
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
        for (int i = 0; i < 4; i++)
        {
            // 선택지 텍스트/색상은 CurrentQuestion에서 읽히므로 갱신
        }
        Notify(nameof(CurrentQuestion));
    }

    void RaiseAll()
    {
        StartCmd.Raise();
        Choice0Cmd.Raise(); Choice1Cmd.Raise();
        Choice2Cmd.Raise(); Choice3Cmd.Raise();
        NextCmd.Raise();
        RestartCmd.Raise();
    }
}
