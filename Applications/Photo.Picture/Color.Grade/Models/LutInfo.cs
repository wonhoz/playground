namespace Color.Grade.Models;

/// <summary>알고리즘 프리셋 또는 .cube 파일 기반 LUT</summary>
public class LutInfo
{
    public string Name     { get; set; } = "";
    public bool   IsBuiltIn { get; set; }
    public string? FilePath { get; set; }

    /// <summary>알고리즘 프리셋인 경우 처리 함수 (null = .cube 파일)</summary>
    public Func<float, float, float, (float R, float G, float B)>? AlgoApply { get; set; }
}
