namespace Layout.Forge.Models;

/// <summary>
/// 키보드 키 정의. ScanCode 0x0000 + Id="" 이면 레이아웃용 스페이서.
/// Extended 키(E0 prefix)는 ScanCode 0xE0XX 형식으로 표현.
/// </summary>
public record KeyDef(string Id, string Label, ushort ScanCode, double Width = 1.0)
{
    public bool IsSpacer => ScanCode == 0 && string.IsNullOrEmpty(Id);
    public bool IsModifier => ScanCode is 0x001D or 0xE01D or 0x0038 or 0xE038
        or 0x002A or 0x0036 or 0xE05B or 0xE05C or 0xE05D;
}

/// <summary>리매핑 대상 선택용 (ComboBox)</summary>
public record KeyTarget(ushort ScanCode, string Name)
{
    public override string ToString() => Name;
}
