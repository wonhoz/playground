namespace StackCrash.Models;

/// <summary>레벨에 배치될 블록 하나를 정의합니다.</summary>
/// <param name="X">물리 좌표 (미터, 중심 기준)</param>
/// <param name="Y">물리 좌표 (미터, 바닥 기준 Y-up)</param>
/// <param name="W">너비 (미터)</param>
/// <param name="H">높이 (미터)</param>
/// <param name="Angle">초기 각도 (도)</param>
/// <param name="Material">재질</param>
public record BlockDef(
    float         X,
    float         Y,
    float         W,
    float         H,
    float         Angle,
    BlockMaterial Material
);

/// <summary>레벨 정의</summary>
/// <param name="Name">레벨 이름</param>
/// <param name="Description">설명</param>
/// <param name="MaxMoves">최대 제거 횟수 (0 = 무제한)</param>
/// <param name="Star3Moves">3성 조건 (이하 이동 수)</param>
/// <param name="Star2Moves">2성 조건</param>
/// <param name="Blocks">배치 블록 목록</param>
public record LevelDef(
    string              Name,
    string              Description,
    int                 MaxMoves,
    int                 Star3Moves,
    int                 Star2Moves,
    IReadOnlyList<BlockDef> Blocks
);
