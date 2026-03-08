namespace HookCast.Models;

// ── 벡터 헬퍼 ─────────────────────────────────────────────────────
public record struct Vec2(double X, double Y)
{
    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, double s) => new(a.X * s, a.Y * s);
    public static Vec2 operator *(double s, Vec2 a) => new(a.X * s, a.Y * s);
    public double Length  => Math.Sqrt(X * X + Y * Y);
    public Vec2 Normalized => Length < 1e-6 ? new(0, 0) : new(X / Length, Y / Length);
    public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
}

// ── 버르텍스 로프 노드 (낚싯줄 물리) ─────────────────────────────
public class RopeNode
{
    public Vec2 Pos     { get; set; }
    public Vec2 PrevPos { get; set; }
    public bool Pinned  { get; set; }

    public RopeNode(Vec2 pos, bool pinned = false)
    {
        Pos     = pos;
        PrevPos = pos;
        Pinned  = pinned;
    }
}

// ── 물고기 종류 ───────────────────────────────────────────────────
public enum FishSpecies
{
    Crucian,   // 붕어  — 느림, 흔함
    Bass,      // 배스  — 중간
    Trout,     // 송어  — 빠름, 활발
    Salmon,    // 연어  — 크고 강함
    Snakehead, // 가물치 — 희귀, 공격적
}

// ── 날씨 ──────────────────────────────────────────────────────────
public enum Weather { Sunny, Cloudy, Rainy }

// ── 물고기 AI 상태 ────────────────────────────────────────────────
public enum FishState { Roaming, Approaching, Biting, Hooked, Escaped }

public class Fish
{
    public FishSpecies Species   { get; init; }
    public Vec2        Pos       { get; set; }
    public Vec2        Velocity  { get; set; }
    public FishState   State     { get; set; } = FishState.Roaming;
    public double      Size      { get; init; }   // cm
    public double      Weight    { get; init; }   // kg
    public double      Speed     { get; init; }   // px/frame
    public double      LookRange { get; init; }   // 미끼 탐지 거리
    public double      BiteCooldown { get; set; } // 입질 쿨다운 (프레임)

    // 파이팅 강도 (낚시 중 저항)
    public double FightStrength { get; init; }

    public string KorName => Species switch
    {
        FishSpecies.Crucian   => "붕어",
        FishSpecies.Bass      => "배스",
        FishSpecies.Trout     => "송어",
        FishSpecies.Salmon    => "연어",
        FishSpecies.Snakehead => "가물치",
        _ => "물고기"
    };
}

// ── 낚시 결과 기록 ────────────────────────────────────────────────
public record CatchRecord(
    DateTime   Time,
    FishSpecies Species,
    string     KorName,
    double     Size,
    double     Weight,
    Weather    Weather
);

// ── 게임 상태 ─────────────────────────────────────────────────────
public enum GamePhase
{
    Aiming,      // 드래그로 캐스팅 조준 중
    Flying,      // 낚싯줄 날아가는 중
    Waiting,     // 물 위에서 대기
    FightReel,   // 챔질 후 릴링
    Result,      // 낚시 결과 표시
}
