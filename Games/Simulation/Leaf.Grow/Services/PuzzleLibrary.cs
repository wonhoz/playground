namespace LeafGrow.Services;

/// <summary>퍼즐 모드 목표 목록</summary>
public static class PuzzleLibrary
{
    public static readonly List<PuzzleGoal> All =
    [
        new PuzzleGoal
        {
            Title       = "첫 번째 발아",
            Description = "프랙탈 나무를 3단계까지 키워보세요.",
            TargetIter  = 3,
            SpeciesName = "Fractal Tree",
            MinSun = 0.3, MaxSun = 1.0,
            MinWater = 0.3, MaxWater = 1.0,
        },
        new PuzzleGoal
        {
            Title       = "고사리 완전 성장",
            Description = "고사리를 최대 5단계까지 성장시키세요.",
            TargetIter  = 5,
            SpeciesName = "Fern",
            MinSun = 0.2, MaxSun = 0.7,
            MinWater = 0.5, MaxWater = 1.0,
        },
        new PuzzleGoal
        {
            Title       = "벚꽃 만개",
            Description = "벚꽃을 4단계로 키우고 꽃을 피워보세요.",
            TargetIter  = 4,
            SpeciesName = "Cherry Blossom",
            NeedFlower  = true,
            MinSun = 0.6, MaxSun = 1.0,
            MinWater = 0.4, MaxWater = 0.8,
        },
        new PuzzleGoal
        {
            Title       = "사막 선인장",
            Description = "선인장을 건조한 환경에서 4단계까지 키우세요.",
            TargetIter  = 4,
            SpeciesName = "Cactus",
            NeedFlower  = true,
            MinSun = 0.7, MaxSun = 1.0,
            MinWater = 0.0, MaxWater = 0.3,
        },
        new PuzzleGoal
        {
            Title       = "연꽃 개화",
            Description = "연꽃을 물이 풍부한 환경에서 완전히 꽃피우세요.",
            TargetIter  = 3,
            SpeciesName = "Lotus",
            NeedFlower  = true,
            MinSun = 0.4, MaxSun = 0.8,
            MinWater = 0.7, MaxWater = 1.0,
        },
        new PuzzleGoal
        {
            Title       = "수정 정원",
            Description = "수정 식물을 4단계의 기하학적 형태로 키우세요.",
            TargetIter  = 4,
            SpeciesName = "Crystal Plant",
            MinSun = 0.5, MaxSun = 1.0,
            MinWater = 0.2, MaxWater = 0.6,
        },
        new PuzzleGoal
        {
            Title       = "대나무 숲",
            Description = "대나무를 햇빛과 영양이 풍부한 환경에서 최대 성장시키세요.",
            TargetIter  = 5,
            SpeciesName = "Bamboo",
            MinSun = 0.6, MaxSun = 1.0,
            MinWater = 0.5, MaxWater = 0.9,
        },
        new PuzzleGoal
        {
            Title       = "해바라기 밭",
            Description = "해바라기를 완전히 꽃피우세요.",
            TargetIter  = 3,
            SpeciesName = "Sunflower",
            NeedFlower  = true,
            MinSun = 0.8, MaxSun = 1.0,
            MinWater = 0.4, MaxWater = 0.7,
        },
    ];
}
