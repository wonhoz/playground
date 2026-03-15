using GolfCast.Models;

namespace GolfCast.Services;

/// <summary>18홀 × 3세트 코스 데이터 팩토리</summary>
public static class CourseData
{
    public static List<CourseSet> AllSets { get; } = [BuildEasy(), BuildNormal(), BuildHard()];

    // ── 쉬움 코스 ─────────────────────────────────────────────────────────────

    private static CourseSet BuildEasy() => new()
    {
        Name  = "클로버 코스",
        Level = Difficulty.Easy,
        Holes =
        [
            MakeSimpleHole(1, 2, "직선 퍼팅",        tee: new(110, 300), hole: new(550, 300),
                wallPairs: [((80, 260),(580, 260)),((580, 260),(580, 340)),((580, 340),(80, 340)),((80, 340),(80, 260))]),

            MakeSimpleHole(2, 2, "L자 코스",         tee: new(110, 200), hole: new(500, 470),
                wallPairs: [((80,160),(300,160)),((300,160),(300,500)),((300,500),(540,500)),((540,500),(540,400)),
                            ((540,400),(340,400)),((340,400),(340,240)),((340,240),(80,240)),((80,240),(80,160))]),

            MakeSimpleHole(3, 3, "모래 구덩이",      tee: new(110, 300), hole: new(570, 300),
                wallPairs: [((80,260),(600,260)),((600,260),(600,340)),((600,340),(80,340)),((80,340),(80,260))],
                sandRects: [(200, 260, 100, 80), (400, 260, 100, 80)]),

            MakeSimpleHole(4, 2, "곡선 터널",        tee: new(110, 400), hole: new(560, 160),
                wallPairs: [((80,360),(340,360)),((340,360),(340,200)),((340,200),(600,200)),
                            ((600,200),(600,120)),((340,120),(340,160)),((340,160),(120,160)),
                            ((120,160),(120,440)),((120,440),(80,440)),((80,440),(80,360))]),

            MakeSimpleHole(5, 3, "물 해자",          tee: new(110, 300), hole: new(570, 300),
                wallPairs: [((80,260),(600,260)),((600,260),(600,340)),((600,340),(80,340)),((80,340),(80,260))],
                waterRects: [(280, 265, 80, 70)]),

            MakeSimpleHole(6, 2, "풍차 통과",        tee: new(110, 300), hole: new(570, 300),
                wallPairs: [((80,260),(600,260)),((600,260),(600,340)),((600,340),(80,340)),((80,340),(80,260))],
                windmill: new Vec2(340, 300)),

            MakeSimpleHole(7, 3, "S자 루트",         tee: new(110, 480), hole: new(530, 100),
                wallPairs: [((80,440),(280,440)),((280,440),(280,240)),((280,240),(500,240)),
                            ((500,240),(500,60)),((560,60),(560,320)),((560,320),(340,320)),
                            ((340,320),(340,540)),((340,540),(80,540)),((80,540),(80,440))]),

            MakeSimpleHole(8, 2, "귀퉁이 반사",      tee: new(110, 420), hole: new(560, 70),
                wallPairs: [((80,440),(600,440)),((600,440),(600,40)),((600,40),(80,40)),((80,40),(80,440))],
                innerWalls: [((200,40),(200,350)),((400,130),(400,440))]),

            MakeSimpleHole(9, 4, "미로 코스",        tee: new(110, 440), hole: new(560, 70),
                wallPairs: [((80,460),(600,460)),((600,460),(600,40)),((600,40),(80,40)),((80,40),(80,460))],
                innerWalls: [((200,460),(200,200)),((200,200),(400,200)),((400,200),(400,40)),
                             ((320,340),(320,460)),((480,200),(480,300)),((480,300),(200,300))]),

            MakeSimpleHole(10, 2, "도넛 홀",        tee: new(110, 300), hole: new(550, 300),
                wallPairs: [((80,260),(580,260)),((580,260),(580,340)),((580,340),(80,340)),((80,340),(80,260))],
                innerWalls: [((300,260),(300,200)),((300,200),(360,200)),((360,200),(360,340)),((300,340),(300,260))]),

            MakeSimpleHole(11, 3, "산 경사",        tee: new(110, 420), hole: new(560, 90),
                wallPairs: [((80,440),(600,440)),((600,440),(600,60)),((600,60),(80,60)),((80,60),(80,440))],
                slopeRects: [(80, 200, 250, 240, SlopeDir.Up)]),

            MakeSimpleHole(12, 2, "당구대",         tee: new(110, 300), hole: new(550, 300),
                wallPairs: [((80,100),(580,100)),((580,100),(580,500)),((580,500),(80,500)),((80,500),(80,100))],
                innerWalls: [((200,100),(200,350)),((400,250),(400,500))]),

            MakeSimpleHole(13, 3, "이동 벽",        tee: new(110, 300), hole: new(570, 300),
                wallPairs: [((80,260),(600,260)),((600,260),(600,340)),((600,340),(80,340)),((80,340),(80,260))],
                movingWall: (new Vec2(340, 270), new Vec2(340, 220), 340, 0.4)),

            MakeSimpleHole(14, 3, "복합 장애물",    tee: new(110, 300), hole: new(570, 300),
                wallPairs: [((80,220),(600,220)),((600,220),(600,380)),((600,380),(80,380)),((80,380),(80,220))],
                sandRects: [(150, 225, 80, 150)], windmill: new Vec2(420, 300)),

            MakeSimpleHole(15, 4, "물 섬 코스",    tee: new(110, 280), hole: new(550, 280),
                wallPairs: [((80,240),(580,240)),((580,240),(580,320)),((580,320),(80,320)),((80,320),(80,240))],
                waterRects: [(200, 245, 80, 70), (360, 245, 80, 70)]),

            MakeSimpleHole(16, 3, "반사 미로",      tee: new(110, 390), hole: new(560, 90),
                wallPairs: [((80,420),(600,420)),((600,420),(600,60)),((600,60),(80,60)),((80,60),(80,420))],
                innerWalls: [((200,60),(200,280)),((400,200),(400,420)),((200,360),(400,360))]),

            MakeSimpleHole(17, 4, "풍차 + 물",     tee: new(110, 300), hole: new(570, 300),
                wallPairs: [((80,220),(600,220)),((600,220),(600,380)),((600,380),(80,380)),((80,380),(80,220))],
                windmill: new Vec2(260, 300), waterRects: [(380, 225, 100, 150)]),

            MakeSimpleHole(18, 5, "마지막 도전",    tee: new(110, 440), hole: new(560, 70),
                wallPairs: [((80,460),(600,460)),((600,460),(600,40)),((600,40),(80,40)),((80,40),(80,460))],
                innerWalls: [((200,460),(200,280)),((200,280),(420,280)),((420,280),(420,130)),
                             ((320,130),(320,280)),((320,460),(320,370)),((420,370),(600,370))],
                sandRects: [(420, 130, 180, 150)], waterRects: [(80, 130, 120, 150)]),
        ],
    };

    // ── 보통 / 어려움 (Easy 기반으로 파 +1 씩 적용) ──────────────────────────

    private static CourseSet BuildNormal()
    {
        var easy = BuildEasy();
        return new CourseSet
        {
            Name  = "오크 코스",
            Level = Difficulty.Normal,
            Holes = easy.Holes.Select((h, i) => h with
            {
                Number = i + 1,
                Name   = h.Name + " (변형)",
                Par    = Math.Min(h.Par + 1, 6),
            }).ToList(),
        };
    }

    private static CourseSet BuildHard()
    {
        var easy = BuildEasy();
        return new CourseSet
        {
            Name  = "블랙 코스",
            Level = Difficulty.Hard,
            Holes = easy.Holes.Select((h, i) => h with
            {
                Number = i + 1,
                Name   = h.Name + " (고급)",
                Par    = Math.Min(h.Par + 2, 7),
            }).ToList(),
        };
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    private static HoleData MakeSimpleHole(
        int  number,
        int  par,
        string name,
        Vec2 tee,
        Vec2 hole,
        ((double, double), (double, double))[]? wallPairs   = null,
        ((double, double), (double, double))[]? innerWalls  = null,
        (double x, double y, double w, double h)[]?   sandRects   = null,
        (double x, double y, double w, double h)[]?   waterRects  = null,
        (double x, double y, double w, double h, SlopeDir dir)[]? slopeRects = null,
        Vec2? windmill    = null,
        (Vec2 center, Vec2 a, double width, double speed)? movingWall = null)
    {
        var walls = new List<WallSegment>();
        var regions = new List<TileRegion>();
        var obstacles = new List<Obstacle>();

        foreach (var ((ax, ay), (bx, by)) in wallPairs ?? [])
            walls.Add(new WallSegment(new Vec2(ax, ay), new Vec2(bx, by)));

        foreach (var ((ax, ay), (bx, by)) in innerWalls ?? [])
            walls.Add(new WallSegment(new Vec2(ax, ay), new Vec2(bx, by)));

        foreach (var (x, y, w, h) in sandRects ?? [])
            regions.Add(new TileRegion { Kind = TileKind.Sand, Bounds = new Rect(x, y, w, h) });

        foreach (var (x, y, w, h) in waterRects ?? [])
            regions.Add(new TileRegion { Kind = TileKind.Water, Bounds = new Rect(x, y, w, h) });

        foreach (var (x, y, w, h, dir) in slopeRects ?? [])
            regions.Add(new TileRegion { Kind = TileKind.Slope, Bounds = new Rect(x, y, w, h), Slope = dir });

        if (windmill.HasValue)
            obstacles.Add(new Obstacle
            {
                Kind   = ObstacleKind.Windmill,
                Center = windmill.Value,
                Width  = 80,
                Height = 12,
                Speed  = 1.8,
            });

        if (movingWall.HasValue)
        {
            var (c, a, w, sp) = movingWall.Value;
            obstacles.Add(new Obstacle
            {
                Kind   = ObstacleKind.MovingWall,
                Center = c,
                MoveA  = a,
                MoveB  = new Vec2(c.X, c.Y + 60),
                Width  = w,
                Height = 14,
                Speed  = sp,
            });
        }

        return new HoleData
        {
            Number    = number,
            Par       = par,
            Name      = name,
            TeePos    = tee,
            HolePos   = hole,
            Walls     = walls,
            Regions   = regions,
            Obstacles = obstacles,
        };
    }
}
