using System.Windows.Media;
using NitroDrift.Engine;

namespace NitroDrift.Entities;

public sealed class AiCar : Car
{
    private readonly Random _rng;
    private readonly double _skillLevel; // 0.7~1.0

    public AiCar(Color color, string name, double skillLevel, Random rng) : base(color, name)
    {
        _rng = rng;
        _skillLevel = skillLevel;
        MaxSpeed = 250 + skillLevel * 80;
    }

    public void UpdateAi(double dt, Track track)
    {
        if (Finished) return;

        RaceTime += dt;

        var (targetAngle, dist) = track.DirectionTo(X, Y, CurrentWaypoint);

        // 각도 차이
        double angleDiff = targetAngle - Angle;
        while (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
        while (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;

        bool turnL = angleDiff < -0.05;
        bool turnR = angleDiff > 0.05;
        bool accel = Math.Abs(angleDiff) < 1.0;
        bool brake = Math.Abs(angleDiff) > 1.5 && Speed > 100;
        bool boost = dist > 80 && Math.Abs(angleDiff) < 0.3 && _rng.NextDouble() < 0.02 * _skillLevel;

        // Car.Update() 내부에서 웨이포인트·트랙경계 처리 완료
        Update(dt, track, accel, brake, turnL, turnR, boost);
    }
}
