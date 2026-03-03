namespace GearWorks.Entities;

public enum GearRole { Motor, Fixed, Slot, Output }

/// <summary>기어 강체 — 위치·반지름·각속도·역할.</summary>
public class Gear
{
    public double   X, Y;
    public double   Radius;
    public double   Angle;             // 현재 회전 각도 (라디안)
    public double   AngularVelocity;   // rad/s (양수=CW, 음수=CCW)
    public GearRole Role;
    public bool     IsSolved;          // BFS에서 각속도 결정됨
    public bool     IsConnected;       // 모터와 체인 연결됨

    // 이빨 수 — 반지름에 비례
    public int TeethCount => Math.Max(8, (int)(Radius * 0.55));

    // 회전 방향 문자열
    public string DirectionLabel => AngularVelocity > 0 ? "CW" : AngularVelocity < 0 ? "CCW" : "---";
    public double Rpm => Math.Abs(AngularVelocity) * 60.0 / (Math.PI * 2);
}
