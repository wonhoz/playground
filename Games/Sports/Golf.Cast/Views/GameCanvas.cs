using System.Windows.Media;
using GolfCast.Models;
using GolfCast.Services;

namespace GolfCast.Views;

/// <summary>WPF Canvas 기반 게임 렌더러</summary>
public class GameCanvas : System.Windows.Controls.Canvas
{
    // ── 브러시 팔레트 ──────────────────────────────────────────────────────
    private static readonly SolidColorBrush BrushFairway  = new(WpfColor.FromRgb(34, 100, 34));
    private static readonly SolidColorBrush BrushSand     = new(WpfColor.FromRgb(210, 185, 110));
    private static readonly SolidColorBrush BrushWater    = new(WpfColor.FromArgb(200, 30, 100, 200));
    private static readonly SolidColorBrush BrushSlope    = new(WpfColor.FromRgb(60, 130, 60));
    private static readonly SolidColorBrush BrushWall     = new(WpfColor.FromRgb(80, 80, 90));
    private static readonly SolidColorBrush BrushHole     = new(WpfColor.FromRgb(10, 10, 10));
    private static readonly SolidColorBrush BrushTee      = new(WpfColor.FromRgb(255, 255, 150));
    private static readonly SolidColorBrush BrushBall     = new(WpfColor.FromRgb(245, 245, 255));
    private static readonly SolidColorBrush BrushBallSh   = new(WpfColor.FromArgb(80, 0, 0, 0));
    private static readonly SolidColorBrush BrushAimLine  = new(WpfColor.FromArgb(180, 255, 220, 50));
    private static readonly SolidColorBrush BrushPower    = new(WpfColor.FromArgb(200, 255, 80, 40));
    private static readonly SolidColorBrush BrushWindmill = new(WpfColor.FromRgb(180, 60, 60));
    private static readonly SolidColorBrush BrushMoveWall = new(WpfColor.FromRgb(160, 80, 200));
    private static readonly SolidColorBrush BrushFlag     = new(WpfColor.FromRgb(220, 40, 40));

    private static readonly System.Windows.Media.Pen WallPen  = new(BrushWall, 3) { LineJoin = PenLineJoin.Round };
    private static readonly System.Windows.Media.Pen AimPen   = new(BrushAimLine, 1.5) { DashStyle = DashStyles.Dash };
    private static readonly System.Windows.Media.Pen SlopePen = new(new SolidColorBrush(WpfColor.FromArgb(120, 255,255,255)), 1);

    public GameService? GameService { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        var gs = GameService;
        if (gs?.CurrentHole is null) return;

        var hole = gs.CurrentHole;

        // 배경
        dc.DrawRectangle(BrushFairway, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // 특수 지형 영역
        foreach (var region in hole.Regions)
        {
            var brush = region.Kind switch
            {
                TileKind.Sand  => BrushSand,
                TileKind.Water => BrushWater,
                TileKind.Slope => BrushSlope,
                _              => null,
            };
            if (brush is not null) dc.DrawRectangle(brush, null, region.Bounds);

            // 경사 화살표
            if (region.Kind == TileKind.Slope)
                DrawSlopeArrows(dc, region);
        }

        // 홀 (목표)
        var holeP = hole.HolePos.ToPoint();
        dc.DrawEllipse(BrushHole, null, holeP, hole.HoleRadius, hole.HoleRadius);
        DrawFlag(dc, hole.HolePos);

        // 티 위치
        var teeP = hole.TeePos.ToPoint();
        dc.DrawEllipse(BrushTee, null, teeP, 8, 8);
        dc.DrawText(MakeText("T", 9, BrushTee), new Point(teeP.X - 4, teeP.Y - 7));

        // 벽
        foreach (var wall in hole.Walls)
            dc.DrawLine(WallPen, wall.A.ToPoint(), wall.B.ToPoint());

        // 장애물
        foreach (var obs in hole.Obstacles)
            DrawObstacle(dc, obs);

        // 조준선
        if (gs.AimMode && !gs.Ball.InMotion && !gs.Ball.InWater && !gs.Ball.InHole)
            DrawAimLine(dc, gs);

        // 공
        DrawBall(dc, gs.Ball);
    }

    // ── 렌더링 헬퍼 ──────────────────────────────────────────────────────────

    private static void DrawBall(DrawingContext dc, Ball ball)
    {
        var p = ball.Pos.ToPoint();
        // 그림자
        dc.DrawEllipse(BrushBallSh, null, new Point(p.X + 2, p.Y + 2), ball.Radius, ball.Radius * 0.6);
        // 공
        dc.DrawEllipse(BrushBall, null, p, ball.Radius, ball.Radius);
        // 하이라이트
        dc.DrawEllipse(new SolidColorBrush(WpfColor.FromArgb(120, 255, 255, 255)), null,
            new Point(p.X - ball.Radius * 0.3, p.Y - ball.Radius * 0.3),
            ball.Radius * 0.35, ball.Radius * 0.35);
    }

    private static void DrawAimLine(DrawingContext dc, GameService gs)
    {
        if (gs.AimDir.Length < 0.01) return;
        var from = gs.Ball.Pos;
        var to   = from + gs.AimDir * (gs.AimPower * 160);
        dc.DrawLine(AimPen, from.ToPoint(), to.ToPoint());

        // 세기 게이지 막대
        double gx = 20, gy = 20, gw = 150, gh = 12;
        dc.DrawRoundedRectangle(new SolidColorBrush(WpfColor.FromArgb(100,0,0,0)), null,
            new Rect(gx, gy, gw, gh), 4, 4);
        dc.DrawRoundedRectangle(BrushPower, null,
            new Rect(gx, gy, gw * gs.AimPower, gh), 4, 4);
        dc.DrawText(MakeText($"파워 {gs.AimPower:P0}", 9, BrushBall), new Point(gx, gy + gh + 3));
    }

    private static void DrawObstacle(DrawingContext dc, Obstacle obs)
    {
        switch (obs.Kind)
        {
            case ObstacleKind.Windmill:
                foreach (var blade in obs.GetWindmillBlades())
                {
                    var pen = new System.Windows.Media.Pen(BrushWindmill, obs.Height);
                    dc.DrawLine(pen, blade.A.ToPoint(), blade.B.ToPoint());
                }
                dc.DrawEllipse(BrushWindmill, null, obs.Center.ToPoint(), 6, 6);
                break;

            case ObstacleKind.MovingWall:
                var mwPen = new System.Windows.Media.Pen(BrushMoveWall, obs.Height);
                var left  = new Vec2(obs.Center.X - obs.Width / 2, obs.Center.Y);
                var right = new Vec2(obs.Center.X + obs.Width / 2, obs.Center.Y);
                dc.DrawLine(mwPen, left.ToPoint(), right.ToPoint());
                break;
        }
    }

    private static void DrawFlag(DrawingContext dc, Vec2 holePos)
    {
        // 깃대
        var pole = new System.Windows.Media.Pen(new SolidColorBrush(WpfColor.FromRgb(200,200,200)), 1.5);
        dc.DrawLine(pole, holePos.ToPoint(), new Point(holePos.X, holePos.Y - 28));
        // 깃발
        var flag = new PathGeometry();
        var fig  = new PathFigure { StartPoint = new Point(holePos.X, holePos.Y - 28) };
        fig.Segments.Add(new LineSegment(new Point(holePos.X + 14, holePos.Y - 22), true));
        fig.Segments.Add(new LineSegment(new Point(holePos.X,      holePos.Y - 16), true));
        flag.Figures.Add(fig);
        dc.DrawGeometry(BrushFlag, null, flag);
    }

    private static void DrawSlopeArrows(DrawingContext dc, TileRegion region)
    {
        var r = region.Bounds;
        double cx = r.X + r.Width / 2;
        double cy = r.Y + r.Height / 2;
        var (dx, dy) = region.Slope switch
        {
            SlopeDir.Up    => (0.0, -1.0),
            SlopeDir.Down  => (0.0,  1.0),
            SlopeDir.Left  => (-1.0, 0.0),
            SlopeDir.Right => ( 1.0, 0.0),
            _              => (0.0, 0.0),
        };
        for (int i = -1; i <= 1; i++)
        {
            double ox = cx + i * 20 * dy;
            double oy = cy + i * 20 * dx;
            DrawArrow(dc, new Point(ox, oy), dx, dy);
        }
    }

    private static void DrawArrow(DrawingContext dc, Point center, double dx, double dy)
    {
        double len = 10;
        var tip  = new Point(center.X + dx * len, center.Y + dy * len);
        var base1 = new Point(center.X - dx * len / 2 + dy * 5,
                              center.Y - dy * len / 2 - dx * 5);
        var base2 = new Point(center.X - dx * len / 2 - dy * 5,
                              center.Y - dy * len / 2 + dx * 5);
        var geom = new PathGeometry();
        var fig  = new PathFigure { StartPoint = tip };
        fig.Segments.Add(new LineSegment(base1, true));
        fig.Segments.Add(new LineSegment(base2, true));
        fig.IsClosed = true;
        geom.Figures.Add(fig);
        dc.DrawGeometry(SlopePen.Brush, null, geom);
    }

    private static FormattedText MakeText(string text, double size, Brush brush)
        => new(text, System.Globalization.CultureInfo.CurrentCulture,
               System.Windows.FlowDirection.LeftToRight,
               new Typeface("Segoe UI"), size, brush, 96);
}
