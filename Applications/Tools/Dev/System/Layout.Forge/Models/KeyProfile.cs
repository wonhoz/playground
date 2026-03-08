namespace Layout.Forge.Models;

/// <summary>
/// 키 리매핑 프로파일.
/// Mappings: source ScanCode → target ScanCode (0x0000 = 비활성화)
/// </summary>
public class KeyProfile
{
    public string Name { get; set; } = "기본 프로파일";

    /// <summary>source → target (ushort 값을 문자열 키로 JSON 저장)</summary>
    public Dictionary<ushort, ushort> Mappings { get; set; } = new();

    public KeyProfile Clone() => new()
    {
        Name     = Name,
        Mappings = new Dictionary<ushort, ushort>(Mappings)
    };
}
