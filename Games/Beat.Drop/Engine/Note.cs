namespace BeatDrop.Engine;

public enum HitGrade { None, Perfect, Great, Good, Miss }

public sealed class Note
{
    public int Lane { get; } // 0~3
    public double HitTime { get; } // 곡 시작부터 초
    public bool IsHit { get; set; }
    public bool IsMissed { get; set; }
    public HitGrade Grade { get; set; } = HitGrade.None;
    public bool IsProcessed => IsHit || IsMissed;

    // 롱노트 (선택)
    public bool IsLong { get; }
    public double Duration { get; }
    public bool IsHolding { get; set; }

    public Note(int lane, double hitTime, bool isLong = false, double duration = 0)
    {
        Lane = lane;
        HitTime = hitTime;
        IsLong = isLong;
        Duration = duration;
    }
}
