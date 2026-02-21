namespace BeatDrop.Engine;

public sealed class ScoreManager
{
    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public int PerfectCount { get; private set; }
    public int GreatCount { get; private set; }
    public int GoodCount { get; private set; }
    public int MissCount { get; private set; }
    public int TotalNotes { get; set; }

    // 판정 윈도우 (초)
    public const double PerfectWindow = 0.04;
    public const double GreatWindow = 0.08;
    public const double GoodWindow = 0.12;
    public const double MissWindow = 0.18;

    public HitGrade Judge(double timeDiff)
    {
        double abs = Math.Abs(timeDiff);
        if (abs <= PerfectWindow) return HitGrade.Perfect;
        if (abs <= GreatWindow) return HitGrade.Great;
        if (abs <= GoodWindow) return HitGrade.Good;
        return HitGrade.Miss;
    }

    public void RegisterHit(HitGrade grade)
    {
        switch (grade)
        {
            case HitGrade.Perfect:
                PerfectCount++;
                Combo++;
                Score += 300 + Combo * 5;
                break;
            case HitGrade.Great:
                GreatCount++;
                Combo++;
                Score += 200 + Combo * 3;
                break;
            case HitGrade.Good:
                GoodCount++;
                Combo++;
                Score += 100 + Combo;
                break;
            case HitGrade.Miss:
                MissCount++;
                Combo = 0;
                break;
        }
        if (Combo > MaxCombo) MaxCombo = Combo;
    }

    public void Reset()
    {
        Score = 0;
        Combo = 0;
        MaxCombo = 0;
        PerfectCount = 0;
        GreatCount = 0;
        GoodCount = 0;
        MissCount = 0;
        TotalNotes = 0;
    }

    public string Rank
    {
        get
        {
            if (TotalNotes == 0) return "?";
            double accuracy = (double)(PerfectCount * 3 + GreatCount * 2 + GoodCount) / (TotalNotes * 3) * 100;
            return accuracy switch
            {
                >= 95 => "S",
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                _ => "D"
            };
        }
    }

    public double Accuracy
    {
        get
        {
            if (TotalNotes == 0) return 0;
            return (double)(PerfectCount * 3 + GreatCount * 2 + GoodCount) / (TotalNotes * 3) * 100;
        }
    }
}
