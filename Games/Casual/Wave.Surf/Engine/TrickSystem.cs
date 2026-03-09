namespace WaveSurf.Engine;

public record TrickResult(string Name, int BaseScore, int FinalScore, int Combo, double Multiplier);

/// <summary>묘기 판정, 콤보, 점수 관리</summary>
public class TrickSystem
{
    private int _combo;
    private int _totalScore;
    private double _survivalFraction; // 잔여 점수 누산용

    public int TotalScore  => _totalScore;
    public int Combo       => _combo;
    public double Multiplier => 1.0 + (_combo * 0.5);

    /// <summary>착지 성공 시 묘기 처리. 회전량(도)에 따른 점수 반환</summary>
    public TrickResult? ProcessLanding(double trickRotation)
    {
        double full = Math.Abs(trickRotation);
        if (full < 150) return null; // 묘기 없음

        string name;
        int baseScore;

        double mod = ((trickRotation % 360) + 360) % 360;
        double distToFull = Math.Min(mod, 360 - mod);

        // 360° 배수 판정 (이미 SurferPhysics에서 검증했으므로 여기선 이름/점수만)
        int rotations = (int)((full + 30) / 360);
        if (rotations == 0)
        {
            name = "180°";
            baseScore = 350;
        }
        else
        {
            name = rotations switch
            {
                1 => "360° 🌀",
                2 => "720° 🔥",
                3 => "1080° ⚡",
                _ => $"{rotations * 360}° 💥"
            };
            baseScore = rotations switch
            {
                1 => 1000,
                2 => 2800,
                3 => 5500,
                _ => 5500 + (rotations - 3) * 3000
            };
        }

        _combo++;
        double mult = Multiplier;
        int final = (int)(baseScore * mult);
        _totalScore += final;

        return new TrickResult(name, baseScore, final, _combo, mult);
    }

    /// <summary>와이프아웃 시 콤보 초기화</summary>
    public void BreakCombo() => _combo = 0;

    /// <summary>매 프레임 생존 점수 +10/s</summary>
    public void AddSurvivalScore(double dt)
    {
        _survivalFraction += 10.0 * dt;
        int add = (int)_survivalFraction;
        _totalScore += add;
        _survivalFraction -= add;
    }

    public void Reset()
    {
        _combo = 0;
        _totalScore = 0;
        _survivalFraction = 0;
    }
}
