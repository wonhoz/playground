namespace ClothCut.Entities;

public enum LinkType { Structural, Shear }

/// <summary>
/// 두 질점을 연결하는 스프링 링크.
/// IsCut = true 이면 제약 해결에서 제외 (절단됨).
/// </summary>
public class ClothLink
{
    public ClothNode A, B;
    public double    RestLength;
    public LinkType  Type;
    public bool      IsCut;

    public ClothLink(ClothNode a, ClothNode b, LinkType type)
    {
        A = a; B = b; Type = type;
        double dx = b.X - a.X, dy = b.Y - a.Y;
        RestLength = Math.Sqrt(dx * dx + dy * dy);
    }
}
