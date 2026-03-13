namespace GolfCast.Models;

/// <summary>타일 종류</summary>
public enum TileKind { Fairway, Wall, Sand, Water, Slope, Hole, Tee }

/// <summary>경사 방향</summary>
public enum SlopeDir { Up, Down, Left, Right }

/// <summary>장애물 종류</summary>
public enum ObstacleKind { Windmill, MovingWall, Magnet, Wind, Warp }

/// <summary>코스 타일 (16×16 픽셀 격자)</summary>
public class Tile
{
    public TileKind Kind      { get; init; }
    public SlopeDir Slope     { get; init; }   // TileKind.Slope 일 때만 사용
    public Vec2     WarpTarget { get; init; }   // TileKind.Warp 일 때 목적지
}

/// <summary>폴리곤 벽 세그먼트</summary>
public class WallSegment
{
    public Vec2 A { get; init; }
    public Vec2 B { get; init; }
    public Vec2 Normal { get; }   // 오른쪽 법선 (공이 닿는 쪽)

    public WallSegment(Vec2 a, Vec2 b)
    {
        A = a; B = b;
        var d = (b - a).Normalized;
        Normal = new Vec2(d.Y, -d.X);   // 오른쪽 법선
    }
}

/// <summary>장애물 (회전 풍차, 이동 벽 등)</summary>
public class Obstacle
{
    public ObstacleKind Kind   { get; set; }
    public Vec2         Center { get; set; }
    public double       Width  { get; set; }
    public double       Height { get; set; }
    public double       Angle  { get; set; }       // 현재 각도 (라디안)
    public double       Speed  { get; set; }       // 회전/이동 속도
    public Vec2         MoveA  { get; set; }       // 이동 벽 A 끝점
    public Vec2         MoveB  { get; set; }       // 이동 벽 B 끝점
    public double       MoveT  { get; set; }       // 이동 매개변수 0~1
    public double       MoveDir { get; set; } = 1; // 이동 방향

    public void Update(double dt)
    {
        switch (Kind)
        {
            case ObstacleKind.Windmill:
                Angle += Speed * dt;
                break;
            case ObstacleKind.MovingWall:
                MoveT += MoveDir * Speed * dt;
                if (MoveT >= 1.0) { MoveT = 1.0; MoveDir = -1; }
                if (MoveT <= 0.0) { MoveT = 0.0; MoveDir =  1; }
                Center = MoveA + (MoveB - MoveA) * MoveT;
                break;
        }
    }

    /// <summary>현재 각도 기준 풍차 블레이드 세그먼트 (2개)</summary>
    public IEnumerable<WallSegment> GetWindmillBlades()
    {
        double len = Width / 2;
        for (int i = 0; i < 2; i++)
        {
            double a = Angle + i * Math.PI / 2;
            var n1 = new Vec2(Math.Cos(a), Math.Sin(a));
            var n2 = -n1;
            yield return new WallSegment(Center + n2 * len, Center + n1 * len);
        }
    }
}

/// <summary>골프공</summary>
public class Ball
{
    public Vec2   Pos      { get; set; } = Vec2.Zero;
    public Vec2   Vel      { get; set; } = Vec2.Zero;
    public double Radius   { get; } = 7.0;
    public bool   InMotion => Vel.LengthSq > 0.01;
    public bool   InWater  { get; set; }
    public bool   InHole   { get; set; }
    public int    Strokes  { get; set; }

    public void Reset(Vec2 teePos)
    {
        Pos      = teePos;
        Vel      = Vec2.Zero;
        InWater  = false;
        InHole   = false;
        Strokes  = 0;
    }
}

/// <summary>홀 (코스 하나)</summary>
public record HoleData
{
    public int    Number      { get; init; }
    public int    Par         { get; init; }
    public string Name        { get; init; } = "";
    public Vec2   TeePos      { get; init; }
    public Vec2   HolePos     { get; init; }
    public double HoleRadius  { get; } = 10.0;
    public List<WallSegment> Walls     { get; init; } = [];
    public List<Obstacle>    Obstacles { get; init; } = [];

    /// <summary>타일 폴리곤 영역 (모래, 물, 경사 등)</summary>
    public List<TileRegion> Regions { get; init; } = [];
}

/// <summary>특수 지형 영역</summary>
public class TileRegion
{
    public TileKind Kind    { get; init; }
    public Rect     Bounds  { get; init; }
    public SlopeDir Slope   { get; init; }
}

/// <summary>코스 세트 (18홀)</summary>
public class CourseSet
{
    public string         Name  { get; init; } = "";
    public Difficulty     Level { get; init; }
    public List<HoleData> Holes { get; init; } = [];
}

public enum Difficulty { Easy, Normal, Hard }

/// <summary>라운드 스코어카드</summary>
public class ScoreCard
{
    public CourseSet           Course  { get; set; } = null!;
    public List<int>           Scores  { get; }      = [];   // 홀별 타수
    public List<bool>          HoleIn1 { get; }      = [];   // 홀인원 여부
    public int                 Total   => Scores.Sum();
    public int                 TotalPar => Course.Holes.Sum(h => h.Par);
    public int                 ToPar    => Total - TotalPar;
}
