using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NitroDrift.Engine;

namespace NitroDrift.Entities;

public class Car
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Angle { get; set; } // 라디안
    public double Speed { get; set; }
    public double MaxSpeed { get; set; } = 300;
    public double Acceleration { get; set; } = 200;
    public double Braking { get; set; } = 300;
    public double TurnSpeed { get; set; } = 3.0;
    public double Friction { get; set; } = 0.97;

    public int CurrentWaypoint { get; set; }
    public int Lap { get; set; }
    public bool Finished { get; set; }
    public double RaceTime { get; set; }
    public string Name { get; set; } = "";

    // 부스트
    public double BoostFuel { get; set; } = 100;
    public bool IsBoosting { get; set; }

    public Canvas Visual { get; }
    private readonly Rectangle _body;

    public Car(Color color, string name)
    {
        Name = name;
        Visual = new Canvas { Width = 20, Height = 12 };

        _body = new Rectangle
        {
            Width = 20, Height = 12,
            Fill = new SolidColorBrush(color),
            RadiusX = 3, RadiusY = 3,
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1
        };
        Visual.Children.Add(_body);

        // 앞유리
        var windshield = new Rectangle
        {
            Width = 5, Height = 8,
            Fill = new SolidColorBrush(Color.FromArgb(150, 100, 200, 255)),
            RadiusX = 1, RadiusY = 1
        };
        Canvas.SetLeft(windshield, 13);
        Canvas.SetTop(windshield, 2);
        Visual.Children.Add(windshield);
    }

    public virtual void Update(double dt, Track track, bool accel, bool brake, bool turnLeft, bool turnRight, bool boost)
    {
        // 가속/브레이크
        double currentMax = MaxSpeed;
        if (boost && BoostFuel > 0)
        {
            IsBoosting = true;
            BoostFuel -= 40 * dt;
            currentMax = MaxSpeed * 1.5;
            Speed += Acceleration * 1.8 * dt;
        }
        else
        {
            IsBoosting = false;
        }

        if (accel) Speed += Acceleration * dt;
        if (brake) Speed -= Braking * dt;

        Speed = Math.Clamp(Speed, -50, currentMax);
        Speed *= Friction;

        // 회전 (속도에 비례, 최소 0.3 보장 — 정지 상태서도 방향 전환 가능)
        double turnFactor = Math.Max(0.3, Math.Min(1, Math.Abs(Speed) / 100));
        if (turnLeft) Angle -= TurnSpeed * turnFactor * dt;
        if (turnRight) Angle += TurnSpeed * turnFactor * dt;

        // 이동
        X += Math.Cos(Angle) * Speed * dt;
        Y += Math.Sin(Angle) * Speed * dt;

        // 트랙 이탈 방지
        var (cx, cy, trackDist) = track.NearestTrackPoint(X, Y);
        double boundary = track.TrackWidth / 2.0 - 4;
        if (trackDist > boundary)
        {
            double excess = trackDist - boundary;
            double pushAngle = Math.Atan2(cy - Y, cx - X);
            X += Math.Cos(pushAngle) * excess;
            Y += Math.Sin(pushAngle) * excess;
            Speed *= 0.65; // 벽 충돌 시 감속
        }

        // 웨이포인트 체크
        if (track.DistanceToWaypoint(X, Y, CurrentWaypoint) < 40)
        {
            int next = track.NextWaypoint(CurrentWaypoint);
            if (next == 0 && CurrentWaypoint == track.Waypoints.Length - 1)
            {
                Lap++;
                if (Lap > track.TotalLaps) Finished = true;
            }
            CurrentWaypoint = next;
        }

        // 부스트 자동 충전
        if (!IsBoosting && BoostFuel < 100)
            BoostFuel += 8 * dt;
    }

    public void SyncPosition()
    {
        Canvas.SetLeft(Visual, X - 10);
        Canvas.SetTop(Visual, Y - 6);
        Visual.RenderTransform = new RotateTransform(Angle * 180 / Math.PI, 10, 6);
    }

    public void Reset(double x, double y, double angle)
    {
        X = x; Y = y; Angle = angle;
        Speed = 0;
        CurrentWaypoint = 0;
        Lap = 1;
        Finished = false;
        RaceTime = 0;
        BoostFuel = 100;
        IsBoosting = false;
    }
}
