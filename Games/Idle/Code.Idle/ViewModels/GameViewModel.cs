using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CodeIdle.Models;

namespace CodeIdle.ViewModels;

public class GameViewModel : INotifyPropertyChanged
{
    private readonly GameState _state = new();
    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();
    private int _tickCount;

    public GameState State => _state;

    public ObservableCollection<UpgradeItem> Upgrades { get; } = new();
    public ObservableCollection<string> EventLog { get; } = new();
    public ObservableCollection<(string Label, bool Unlocked)> Achievements { get; } = new();

    private static readonly List<UpgradeItem> AllUpgrades = new()
    {
        new("coffee",     "☕ 커피머신",       "집중력 +10% — 라인/초 증가",         "☕",   50,        0.5,   1.0),
        new("monitor",    "🖥️ 모니터 추가",     "멀티태스킹 — 라인/초 +1",            "🖥️",  200,       1.0,   1.0),
        new("junior",     "👶 주니어 개발자",   "팀원 고용 — 라인/초 +5",             "👶",  500,       5.0,   1.0),
        new("github",     "🐙 GitHub 스폰서",   "오픈소스 부스트 — 라인/초 +10",      "🐙",  1_500,     10.0,  1.0),
        new("rubber",     "🦆 고무 오리",       "디버깅 친구 — 클릭 파워 ×2",         "🦆",  3_000,     0,     2.0),
        new("senior",     "🧑‍🦳 시니어 개발자", "경험치 — 라인/초 +30",               "🧑‍🦳",  8_000,     30.0,  1.0),
        new("ci",         "⚙️ CI/CD 파이프라인","자동화 — 라인/초 +50",               "⚙️",  20_000,    50.0,  1.0),
        new("ai",         "🤖 AI 코드 생성",    "ChatGPT 도입 — 라인/초 ×2",         "🤖",  100_000,   0,     1.0),
        new("office",     "🏢 사무실 임대",      "팀 확장 — 라인/초 +200",            "🏢",  500_000,   200.0, 1.0),
        new("vc",         "💰 VC 투자 유치",    "시리즈A — 라인/초 +1000",            "💰",  5_000_000, 1000.0,1.0),
    };

    private static readonly List<Event> RandomEvents = new()
    {
        new("🎉 Product Hunt 1위!", "트래픽 폭발! 사용자 +10%", "🎉",   0,      0.1),
        new("🐛 NPM 취약점 발견",   "긴급 패치 — 코드 라인 -20%", "🐛", -0.2,   0),
        new("📰 테크크런치 기사",    "홍보 효과 — 사용자 +5%",    "📰",  0,      0.05),
        new("☁️ 서버 다운",         "인프라 비용 폭증 — 라인 -10%","☁️", -0.1,   0),
        new("🏆 기술 면접 통과",     "스타 개발자 영입 — 라인/초 +50","🏆", 50,   0),
        new("💸 Azure 비용 폭탄",    "클라우드 청구서 — 라인 -15%","💸",  -0.15, 0),
        new("🔥 Hacker News 핫",    "커뮤니티 붐 — 사용자 +15%", "🔥",  0,      0.15),
        new("🤦 기술 부채 위기",     "리팩토링 필요 — 생산성 일시 하락","🤦",-0.1,0),
    };

    public GameViewModel()
    {
        // 업그레이드 초기화
        foreach (var u in AllUpgrades)
        {
            Upgrades.Add(u);
            _state.UpgradeLevels[u.Id] = 0;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tickCount++;

        // 라인 생산
        double produced = _state.LinesPerSec * 0.1 * (1 - _state.TechDebt / 200.0);
        _state.Lines          += produced;
        _state.TotalLinesEver += produced;

        // 기술 부채 증가 (매초)
        if (_tickCount % 10 == 0)
            _state.TechDebt += _state.LinesPerSec * 0.0001;

        // 버그 발생
        if (_tickCount % 50 == 0 && _rng.NextDouble() < 0.1 * (_state.TechDebt / 100))
            _state.Bugs++;

        // 사용자 성장 (라인 기반)
        _state.Users  = (long)(_state.TotalLinesEver * 0.01 * (1 + _state.Prestige * 0.1));
        _state.Revenue = _state.Users * 0.001;

        // 스테이지 업데이트
        UpdateStage();

        // 랜덤 이벤트 (약 30초마다)
        if (_tickCount % 300 == 0 && _rng.NextDouble() < 0.4)
            TriggerRandomEvent();

        // 업적
        CheckAchievements();
    }

    public void Click()
    {
        _state.Lines          += _state.ClickPower;
        _state.TotalLinesEver += _state.ClickPower;
    }

    public bool BuyUpgrade(UpgradeItem upgrade)
    {
        if (_state.Lines < upgrade.CostLines) return false;

        var level = _state.UpgradeLevels[upgrade.Id];
        if (level >= upgrade.MaxLevel) return false;

        _state.Lines -= upgrade.CostLines;
        _state.UpgradeLevels[upgrade.Id] = level + 1;

        // AI 코드 생성 = LPS ×2
        if (upgrade.Id == "ai")
            _state.LinesPerSec *= 2;
        else
            _state.LinesPerSec += upgrade.LinesPerSecBonus;

        if (upgrade.ClickMultiplier > 1)
            _state.ClickPower *= upgrade.ClickMultiplier;

        AddEvent($"✅ {upgrade.Name} 구매 완료!");
        OnPropertyChanged(nameof(Upgrades));
        return true;
    }

    public void FixBug()
    {
        if (_state.Bugs <= 0) return;
        _state.Bugs--;
        _state.TechDebt -= 2;
        AddEvent("🔧 버그 수정 완료 — 기술 부채 -2");
    }

    public void Prestige()
    {
        if (_state.Stage < GameStage.IPO) return;
        _state.Prestige++;
        _state.Lines         = 0;
        _state.TotalLinesEver = 0;
        _state.LinesPerSec   = 0;
        _state.ClickPower    = 1 + _state.Prestige * 0.5;
        _state.TechDebt      = 0;
        _state.Bugs          = 0;
        _state.Stage         = GameStage.SoloDevloper;
        foreach (var k in _state.UpgradeLevels.Keys.ToList())
            _state.UpgradeLevels[k] = 0;
        AddEvent($"⭐ 프레스티지 {_state.Prestige}회! 클릭 파워 ×{1 + _state.Prestige * 0.5:F1}");
    }

    private void UpdateStage()
    {
        var newStage = _state.TotalLinesEver switch
        {
            >= 100_000_000 => GameStage.IPO,
            >= 10_000_000  => GameStage.ScaleUp,
            >= 1_000_000   => GameStage.StartupFunded,
            >= 100_000     => GameStage.SmallTeam,
            >= 10_000      => GameStage.FirstApp,
            _              => GameStage.SoloDevloper
        };

        if (newStage != _state.Stage)
        {
            _state.Stage = newStage;
            AddEvent($"🎊 단계 달성: {_state.StageLabel}");
        }
    }

    private void TriggerRandomEvent()
    {
        var ev = RandomEvents[_rng.Next(RandomEvents.Count)];
        if (ev.LinesDelta < 0)
            _state.Lines *= (1 + ev.LinesDelta);
        else if (ev.LinesDelta > 1)
            _state.LinesPerSec += ev.LinesDelta;
        _state.Users = (long)(_state.Users * (1 + ev.UsersDelta));
        AddEvent($"{ev.Emoji} {ev.Title} — {ev.Description}");
    }

    private void CheckAchievements()
    {
        // 간단한 달성 로그 (중복 방지)
    }

    private void AddEvent(string msg)
    {
        _state.LastEvent = msg;
        if (EventLog.Count > 50) EventLog.RemoveAt(EventLog.Count - 1);
        EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
