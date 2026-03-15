using System.Windows.Threading;
using GolfCast.Core;
using GolfCast.Models;

namespace GolfCast.Services;

/// <summary>게임 루프 + 상태 관리</summary>
public class GameService
{
    private readonly PhysicsEngine _physics = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private DateTime _lastTick = DateTime.UtcNow;

    public Ball       Ball      { get; } = new();
    public HoleData?  CurrentHole { get; private set; }
    public ScoreCard  ScoreCard { get; private set; } = new();
    public int        HoleIndex { get; private set; }
    public bool       AimMode   { get; set; } = true;  // true=조준, false=볼 이동 중
    public Vec2       AimDir    { get; set; }
    public double     AimPower  { get; set; }           // 0~1
    public bool       IsFinished => HoleIndex >= ScoreCard.Course.Holes.Count;

    public event Action? Updated;         // 렌더 요청
    public event Action? HoleCompleted;   // 홀 완료
    public event Action? CourseCompleted; // 코스 완료
    public event Action? BallInWater;     // 물 빠짐

    public void StartCourse(CourseSet course)
    {
        ScoreCard = new ScoreCard { Course = course };
        HoleIndex = 0;
        LoadHole(0);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Fire(Vec2 dir, double power)
    {
        if (!AimMode || Ball.InMotion) return;
        const double MaxSpeed = 700.0;
        Ball.Vel  = dir * (power * MaxSpeed);
        Ball.Strokes++;
        AimMode = false;
    }

    public void RetryFromWater()
    {
        if (!Ball.InWater) return;
        Ball.Reset(CurrentHole!.TeePos);
        Ball.Strokes++;  // 벌타 1타
        AimMode = true;
    }

    private void LoadHole(int idx)
    {
        CurrentHole = ScoreCard.Course.Holes[idx];
        Ball.Reset(CurrentHole.TeePos);
        AimMode = true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = Math.Min((now - _lastTick).TotalSeconds, 0.05);
        _lastTick = now;

        if (CurrentHole is null) return;

        // 홀 흡입 반경 내에서는 속도가 0이어도 물리 계속 실행
        bool nearHole = (Ball.Pos - CurrentHole.HolePos).Length
                        < CurrentHole.HoleRadius * PhysicsEngine.HoleSuckRadius;

        if (!AimMode && (Ball.InMotion || nearHole))
            _physics.Step(Ball, CurrentHole, dt);

        // 정지 감지 (홀 근처 제외)
        if (!AimMode && !Ball.InMotion && !Ball.InHole && !Ball.InWater && !nearHole)
            AimMode = true;

        // 물 빠짐 알림
        if (Ball.InWater)
        {
            BallInWater?.Invoke();
            AimMode = true;
        }

        // 홀 완료
        if (Ball.InHole)
        {
            ScoreCard.Scores.Add(Ball.Strokes);
            ScoreCard.HoleIn1.Add(Ball.Strokes == 1);
            HoleIndex++;

            if (HoleIndex >= ScoreCard.Course.Holes.Count)
            {
                _timer.Stop();
                CourseCompleted?.Invoke();
            }
            else
            {
                LoadHole(HoleIndex);
                HoleCompleted?.Invoke();
            }
        }

        Updated?.Invoke();
    }

    public void Stop() => _timer.Stop();
}
