namespace GearWorks.Entities;

public enum GearSize { None, Small, Medium, Large }

/// <summary>
/// 플레이어가 기어를 배치할 수 있는 슬롯.
/// 클릭마다 크기 순환: None → Small → Medium → Large → None.
/// </summary>
public class GearSlot
{
    public double   X, Y;
    public GearSize Size    = GearSize.None;
    public Gear?    Placed;   // null이면 비어있음

    // 크기별 반지름
    public static double RadiusOf(GearSize sz) => sz switch
    {
        GearSize.Small  => 24,
        GearSize.Medium => 36,
        GearSize.Large  => 52,
        _               => 0
    };

    /// <summary>클릭마다 크기 순환, PlacedGear 갱신.</summary>
    public void Cycle()
    {
        Size = Size switch
        {
            GearSize.None   => GearSize.Small,
            GearSize.Small  => GearSize.Medium,
            GearSize.Medium => GearSize.Large,
            GearSize.Large  => GearSize.None,
            _               => GearSize.None
        };

        Placed = Size == GearSize.None ? null : new Gear
        {
            X      = X,
            Y      = Y,
            Radius = RadiusOf(Size),
            Role   = GearRole.Slot
        };
    }

    public void Reset()
    {
        Size   = GearSize.None;
        Placed = null;
    }
}
