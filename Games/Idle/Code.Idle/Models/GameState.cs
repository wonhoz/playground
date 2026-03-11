using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CodeIdle.Models;

public enum GameStage
{
    SoloDevloper,    // 혼자 개발
    FirstApp,        // 첫 앱 출시
    SmallTeam,       // 소규모 팀
    StartupFunded,   // 스타트업 투자
    ScaleUp,         // 성장
    IPO              // 상장
}

public record UpgradeItem(
    string Id,
    string Name,
    string Description,
    string Emoji,
    double CostLines,
    double LinesPerSecBonus,
    double ClickMultiplier,
    int MaxLevel = 1);

public record Event(string Title, string Description, string Emoji, double LinesDelta, double UsersDelta);

public class GameState : INotifyPropertyChanged
{
    private double _lines;
    private double _linesPerSec;
    private double _clickPower = 1;
    private double _totalLinesEver;
    private long _users;
    private double _revenue;
    private double _techDebt;
    private int _bugs;
    private GameStage _stage = GameStage.SoloDevloper;
    private string _lastEvent = "";
    private int _prestige;

    public double Lines
    {
        get => _lines;
        set { _lines = Math.Max(0, value); OnPropertyChanged(); OnPropertyChanged(nameof(LinesDisplay)); }
    }

    public double LinesPerSec
    {
        get => _linesPerSec;
        set { _linesPerSec = value; OnPropertyChanged(); OnPropertyChanged(nameof(LpsDisplay)); }
    }

    public double ClickPower
    {
        get => _clickPower;
        set { _clickPower = value; OnPropertyChanged(); }
    }

    public double TotalLinesEver
    {
        get => _totalLinesEver;
        set { _totalLinesEver = value; OnPropertyChanged(); }
    }

    public long Users
    {
        get => _users;
        set { _users = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsersDisplay)); }
    }

    public double Revenue
    {
        get => _revenue;
        set { _revenue = value; OnPropertyChanged(); OnPropertyChanged(nameof(RevenueDisplay)); }
    }

    public double TechDebt
    {
        get => _techDebt;
        set { _techDebt = Math.Max(0, Math.Min(100, value)); OnPropertyChanged(); }
    }

    public int Bugs
    {
        get => _bugs;
        set { _bugs = Math.Max(0, value); OnPropertyChanged(); }
    }

    public GameStage Stage
    {
        get => _stage;
        set { _stage = value; OnPropertyChanged(); OnPropertyChanged(nameof(StageLabel)); }
    }

    public string LastEvent
    {
        get => _lastEvent;
        set { _lastEvent = value; OnPropertyChanged(); }
    }

    public int Prestige
    {
        get => _prestige;
        set { _prestige = value; OnPropertyChanged(); }
    }

    public string LinesDisplay => FormatNumber(_lines);
    public string LpsDisplay   => $"{FormatNumber(_linesPerSec)}/초";
    public string UsersDisplay => FormatNumber(_users);
    public string RevenueDisplay => $"${FormatNumber(_revenue)}";

    public string StageLabel => _stage switch
    {
        GameStage.SoloDevloper  => "🧑‍💻 1인 개발자",
        GameStage.FirstApp      => "📱 첫 앱 출시",
        GameStage.SmallTeam     => "👥 소규모 팀",
        GameStage.StartupFunded => "🚀 스타트업",
        GameStage.ScaleUp       => "📈 스케일업",
        GameStage.IPO           => "🏦 IPO",
        _                       => "???"
    };

    public Dictionary<string, int> UpgradeLevels { get; } = new();

    private static string FormatNumber(double n) => n switch
    {
        >= 1_000_000_000_000 => $"{n / 1_000_000_000_000:F1}T",
        >= 1_000_000_000     => $"{n / 1_000_000_000:F1}B",
        >= 1_000_000         => $"{n / 1_000_000:F1}M",
        >= 1_000             => $"{n / 1_000:F1}K",
        _                    => $"{(long)n}"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
