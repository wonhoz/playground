namespace DominoChain.Entities;

public enum DominoState { Standing, Falling, Fallen }
public enum DominoKind  { Fixed, Placed } // Fixed=미리 배치(회색), Placed=사용자 배치(초록)

/// <summary>
/// 도미노 강체.
/// PivotX/PivotY: 하단 중심 좌표(피벗).
/// Angle: 라디안, 양수=오른쪽, 음수=왼쪽.
/// </summary>
public class Domino
{
    // 치수
    public double W { get; init; } = 14;
    public double H { get; init; } = 64;

    // 위치 (하단 중심)
    public double PivotX { get; set; }
    public double PivotY { get; set; }

    // 물리 상태
    public DominoState State           { get; set; } = DominoState.Standing;
    public double       Angle          { get; set; } = 0;  // 라디안
    public double       AngularVelocity { get; set; } = 0;
    public int          FallDir        { get; set; } = 0;  // +1=오른쪽, -1=왼쪽

    // 메타
    public DominoKind Kind     { get; init; } = DominoKind.Fixed;
    public bool       IsSlot    { get; set; } = false;  // 빈 슬롯 (배치 가능 위치)
    public int        SlotIndex { get; set; } = -1;    // 슬롯 번호

    public void Reset()
    {
        State = DominoState.Standing;
        Angle = 0;
        AngularVelocity = 0;
        FallDir = 0;
    }

    /// <summary>상단 중심 좌표 (기울기 반영)</summary>
    public (double X, double Y) TopCenter =>
        (PivotX + H * Math.Sin(Angle), PivotY - H * Math.Cos(Angle));
}
