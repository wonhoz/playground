namespace ClothCut.Entities;

/// <summary>
/// 접시 저울 하나의 정의 및 결과 상태.
/// 무게 단위 = 절단 후 해당 저울 영역에 속한 노드 수.
/// </summary>
public class Scale
{
    // ── 정의 ──────────────────────────────────────────────
    public double CenterX  { get; init; }    // 저울 중심 X
    public double BaseY    { get; init; }    // 저울 바닥 Y
    public double Width    { get; init; }    // 저울 너비 (귀속 판정 범위)
    public double MinRatio { get; init; }    // 목표 최소 비율 (0~1)
    public double MaxRatio { get; init; }    // 목표 최대 비율 (0~1)
    public string Label    { get; init; } = "";

    // ── 결과 ──────────────────────────────────────────────
    public int    ReceivedNodes { get; set; } = 0;
    public double ActualRatio   { get; set; } = 0;
    public bool   IsSuccess     { get; set; } = false;

    public void Reset()
    {
        ReceivedNodes = 0;
        ActualRatio   = 0;
        IsSuccess     = false;
    }

    /// <summary>컴포넌트 중심 X가 이 저울의 범위에 해당하는지.</summary>
    public bool Contains(double cx) =>
        cx >= CenterX - Width * 0.5 && cx <= CenterX + Width * 0.5;
}
