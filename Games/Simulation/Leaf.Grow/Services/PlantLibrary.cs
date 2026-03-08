namespace LeafGrow.Services;

/// <summary>30종 식물 데이터베이스</summary>
public static class PlantLibrary
{
    public static readonly List<PlantSpecies> All = Build();

    private static List<PlantSpecies> Build() =>
    [
        // ── 나무류 ────────────────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Fractal Tree", KorName = "프랙탈 나무",
            Axiom = "F", Rules = [new('F', "F[+F]F[-F]F")],
            Angle = 25.7, Length = 8, LenDecay = 0.68, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x5C, 0x3A, 0x1E),
            LeafColor  = Color.FromRgb(0x2E, 0xCC, 0x60),
            Description = "고전 프랙탈 이진 분기 나무"
        },
        new PlantSpecies
        {
            Name = "Symmetric Tree", KorName = "대칭 나무",
            Axiom = "F", Rules = [new('F', "FF+[+F-F-F]-[-F+F+F]")],
            Angle = 22.5, Length = 7, LenDecay = 0.65, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x4A, 0x2E, 0x0C),
            LeafColor  = Color.FromRgb(0x40, 0xC0, 0x50),
            Description = "좌우 대칭 펼침 나무"
        },
        new PlantSpecies
        {
            Name = "Stochastic Tree", KorName = "불규칙 나무",
            Axiom = "F", Rules = [new('F', "F[+F][-F][++F][--F]F")],
            Angle = 20, Length = 6, LenDecay = 0.72, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x60, 0x3C, 0x20),
            LeafColor  = Color.FromRgb(0x30, 0xD0, 0x55),
            Description = "다가지 불규칙 성장 나무"
        },
        new PlantSpecies
        {
            Name = "Weeping Willow", KorName = "수양버들",
            Axiom = "F", Rules = [new('F', "F[-F]F[+F][-F]")],
            Angle = 30, Length = 7, LenDecay = 0.7, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x50, 0x6A, 0x28),
            LeafColor  = Color.FromRgb(0x90, 0xD0, 0x30),
            Description = "늘어지는 가지의 수양버들"
        },
        new PlantSpecies
        {
            Name = "Pine", KorName = "소나무",
            Axiom = "F", Rules = [new('F', "F[++F][-F]F")],
            Angle = 35, Length = 9, LenDecay = 0.7, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x5A, 0x38, 0x18),
            LeafColor  = Color.FromRgb(0x20, 0x90, 0x30),
            Description = "직립 성장 소나무"
        },

        // ── 꽃 피는 식물 ──────────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Cherry Blossom", KorName = "벚꽃",
            Axiom = "F", Rules = [new('F', "FF[+F+F][-F-F]")],
            Angle = 27, Length = 7, LenDecay = 0.65, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x8B, 0x60, 0x40),
            LeafColor  = Color.FromRgb(0xFF, 0xC0, 0xCB),
            FlowerColor = Color.FromRgb(0xFF, 0x80, 0xAA),
            HasFlower = true,
            Description = "봄의 벚꽃 분기"
        },
        new PlantSpecies
        {
            Name = "Sunflower", KorName = "해바라기",
            Axiom = "F+F+F+F", Rules = [new('F', "FF+F-F+F+FF")],
            Angle = 90, Length = 5, LenDecay = 0.85, MaxIter = 3,
            TrunkColor = Color.FromRgb(0x80, 0x6A, 0x20),
            LeafColor  = Color.FromRgb(0x50, 0xC0, 0x30),
            FlowerColor = Color.FromRgb(0xFF, 0xD0, 0x00),
            HasFlower = true,
            Description = "태양을 닮은 해바라기"
        },
        new PlantSpecies
        {
            Name = "Rose Bush", KorName = "장미 덤불",
            Axiom = "F", Rules = [new('F', "F+[-F-F]+[+F+F][-F]")],
            Angle = 20, Length = 6, LenDecay = 0.7, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x4A, 0x28, 0x10),
            LeafColor  = Color.FromRgb(0x28, 0xA0, 0x40),
            FlowerColor = Color.FromRgb(0xFF, 0x30, 0x50),
            HasFlower = true,
            Description = "붉은 장미 덤불"
        },
        new PlantSpecies
        {
            Name = "Lavender", KorName = "라벤더",
            Axiom = "F", Rules = [new('F', "F[+F][--F+F]")],
            Angle = 15, Length = 8, LenDecay = 0.78, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x60, 0x50, 0x40),
            LeafColor  = Color.FromRgb(0x70, 0x90, 0x50),
            FlowerColor = Color.FromRgb(0xAA, 0x70, 0xFF),
            HasFlower = true,
            Description = "보라빛 라벤더 줄기"
        },
        new PlantSpecies
        {
            Name = "Dandelion", KorName = "민들레",
            Axiom = "F+F+F+F+F+F", Rules = [new('F', "F[+F+F][-F-F]")],
            Angle = 60, Length = 5, LenDecay = 0.75, MaxIter = 3,
            TrunkColor = Color.FromRgb(0x70, 0x80, 0x30),
            LeafColor  = Color.FromRgb(0x60, 0xC0, 0x30),
            FlowerColor = Color.FromRgb(0xFF, 0xE0, 0x00),
            HasFlower = true,
            Description = "방사형 민들레"
        },

        // ── 양치식물 / 덩굴류 ─────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Fern", KorName = "고사리",
            Axiom = "X", Rules = [new('X', "F-[[X]+X]+F[+FX]-X"), new('F', "FF")],
            Angle = 25, Length = 4, LenDecay = 0.85, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x30, 0x70, 0x30),
            LeafColor  = Color.FromRgb(0x40, 0xCC, 0x50),
            Description = "Prusinkiewicz 고사리"
        },
        new PlantSpecies
        {
            Name = "Seaweed", KorName = "해초",
            Axiom = "F", Rules = [new('F', "F[+F]F[-F][F]")],
            Angle = 25.7, Length = 5, LenDecay = 0.8, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x20, 0x80, 0x60),
            LeafColor  = Color.FromRgb(0x30, 0xB0, 0x80),
            Description = "바닷속 흔들리는 해초"
        },
        new PlantSpecies
        {
            Name = "Vine", KorName = "덩굴",
            Axiom = "F+F+F+F", Rules = [new('F', "F+F-F-FF+F+F-F")],
            Angle = 90, Length = 4, LenDecay = 0.9, MaxIter = 3,
            TrunkColor = Color.FromRgb(0x30, 0x60, 0x20),
            LeafColor  = Color.FromRgb(0x50, 0xC0, 0x40),
            Description = "사각 패턴 덩굴"
        },
        new PlantSpecies
        {
            Name = "Moss", KorName = "이끼",
            Axiom = "F", Rules = [new('F', "[+F][-F]F[+F][-F]")],
            Angle = 18, Length = 3, LenDecay = 0.82, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x20, 0x50, 0x20),
            LeafColor  = Color.FromRgb(0x40, 0x80, 0x30),
            Description = "촘촘히 자라는 이끼"
        },
        new PlantSpecies
        {
            Name = "Bamboo", KorName = "대나무",
            Axiom = "F", Rules = [new('F', "F[+F][-F]FF")],
            Angle = 12, Length = 10, LenDecay = 0.9, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x40, 0x70, 0x20),
            LeafColor  = Color.FromRgb(0x60, 0xC0, 0x30),
            Description = "빠르게 자라는 대나무"
        },

        // ── 다육 / 선인장 ─────────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Cactus", KorName = "선인장",
            Axiom = "F", Rules = [new('F', "F[-F][+F]F[-F]")],
            Angle = 90, Length = 7, LenDecay = 0.8, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x30, 0x88, 0x30),
            LeafColor  = Color.FromRgb(0x28, 0x99, 0x28),
            FlowerColor = Color.FromRgb(0xFF, 0x60, 0x20),
            HasFlower = true,
            Description = "다육 선인장"
        },
        new PlantSpecies
        {
            Name = "Aloe", KorName = "알로에",
            Axiom = "F+F+F+F+F+F+F+F", Rules = [new('F', "F-[F+F]")],
            Angle = 45, Length = 6, LenDecay = 0.8, MaxIter = 3,
            TrunkColor = Color.FromRgb(0x40, 0x80, 0x40),
            LeafColor  = Color.FromRgb(0x50, 0xA0, 0x50),
            Description = "방사형 알로에"
        },

        // ── 수목 / 관목 ───────────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Oak", KorName = "참나무",
            Axiom = "F", Rules = [new('F', "F[+FF][-FF]F[-F][+F]F")],
            Angle = 22, Length = 8, LenDecay = 0.65, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x52, 0x34, 0x10),
            LeafColor  = Color.FromRgb(0x3A, 0xA0, 0x40),
            Description = "풍성한 참나무"
        },
        new PlantSpecies
        {
            Name = "Maple", KorName = "단풍나무",
            Axiom = "F", Rules = [new('F', "F[++F]F[--F][+F]")],
            Angle = 25, Length = 7, LenDecay = 0.68, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x60, 0x30, 0x10),
            LeafColor  = Color.FromRgb(0xEE, 0x60, 0x20),
            Description = "가을빛 단풍나무"
        },
        new PlantSpecies
        {
            Name = "Spruce", KorName = "가문비나무",
            Axiom = "F", Rules = [new('F', "F[++F][+F][-F][--F]F")],
            Angle = 35, Length = 8, LenDecay = 0.62, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x48, 0x30, 0x18),
            LeafColor  = Color.FromRgb(0x20, 0x70, 0x28),
            Description = "침엽 가문비나무"
        },
        new PlantSpecies
        {
            Name = "Bonsai", KorName = "분재",
            Axiom = "F", Rules = [new('F', "FF+[+F-F]-[-F+F]")],
            Angle = 30, Length = 6, LenDecay = 0.6, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x70, 0x45, 0x20),
            LeafColor  = Color.FromRgb(0x30, 0xA0, 0x40),
            Description = "아담한 분재"
        },

        // ── 기하/추상 식물 ─────────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Dragon Tree", KorName = "용혈수",
            Axiom = "FFFF", Rules = [new('F', "F[+F+F][F][-F-F]")],
            Angle = 40, Length = 7, LenDecay = 0.7, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x80, 0x40, 0x30),
            LeafColor  = Color.FromRgb(0xC0, 0x50, 0x30),
            Description = "이국적인 용혈수"
        },
        new PlantSpecies
        {
            Name = "Crystal Plant", KorName = "수정 식물",
            Axiom = "F+F+F+F", Rules = [new('F', "F+F-F-F+F")],
            Angle = 90, Length = 5, LenDecay = 0.9, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x60, 0xA0, 0xC0),
            LeafColor  = Color.FromRgb(0x80, 0xD0, 0xFF),
            Description = "기하학적 수정 식물"
        },
        new PlantSpecies
        {
            Name = "Spiral Plant", KorName = "나선 식물",
            Axiom = "F+F+F+F+F", Rules = [new('F', "F+[+F-F]-F-F")],
            Angle = 72, Length = 6, LenDecay = 0.78, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x50, 0x80, 0x30),
            LeafColor  = Color.FromRgb(0x80, 0xD0, 0x50),
            Description = "황금비 나선 식물"
        },
        new PlantSpecies
        {
            Name = "Snowflake Plant", KorName = "눈결정 식물",
            Axiom = "F++F++F", Rules = [new('F', "F-F++F-F")],
            Angle = 60, Length = 5, LenDecay = 0.85, MaxIter = 4,
            TrunkColor = Color.FromRgb(0xA0, 0xC0, 0xE0),
            LeafColor  = Color.FromRgb(0xC0, 0xE0, 0xFF),
            Description = "눈송이 모양 성장"
        },
        new PlantSpecies
        {
            Name = "Sierpinski Plant", KorName = "시에르핀스키 식물",
            Axiom = "F-G-G", Rules = [new('F', "F-G+F+G-F"), new('G', "GG")],
            Angle = 120, Length = 5, LenDecay = 0.5, MaxIter = 5,
            TrunkColor = Color.FromRgb(0x30, 0x80, 0x60),
            LeafColor  = Color.FromRgb(0x50, 0xD0, 0x90),
            Description = "시에르핀스키 삼각형 성장"
        },

        // ── 열대 식물 ────────────────────────────────────────────────
        new PlantSpecies
        {
            Name = "Palm Tree", KorName = "야자나무",
            Axiom = "FFFF", Rules = [new('F', "F[++FF][--FF]F")],
            Angle = 45, Length = 9, LenDecay = 0.65, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x80, 0x60, 0x30),
            LeafColor  = Color.FromRgb(0x50, 0xD0, 0x40),
            Description = "열대 야자나무"
        },
        new PlantSpecies
        {
            Name = "Mangrove", KorName = "맹그로브",
            Axiom = "F", Rules = [new('F', "F[+F+F][-F-F]F[+F][-F]")],
            Angle = 28, Length = 6, LenDecay = 0.7, MaxIter = 4,
            TrunkColor = Color.FromRgb(0x40, 0x50, 0x20),
            LeafColor  = Color.FromRgb(0x30, 0xA0, 0x50),
            Description = "수상 뿌리 맹그로브"
        },
        new PlantSpecies
        {
            Name = "Banana Plant", KorName = "바나나나무",
            Axiom = "FFF", Rules = [new('F', "F[+FF+F][-FF-F]")],
            Angle = 20, Length = 10, LenDecay = 0.75, MaxIter = 3,
            TrunkColor = Color.FromRgb(0x50, 0x70, 0x20),
            LeafColor  = Color.FromRgb(0x60, 0xD0, 0x30),
            Description = "넓은 잎 바나나나무"
        },
        new PlantSpecies
        {
            Name = "Lotus", KorName = "연꽃",
            Axiom = "F+F+F+F+F+F+F+F", Rules = [new('F', "F[+F-F+F]-F")],
            Angle = 45, Length = 5, LenDecay = 0.8, MaxIter = 3,
            TrunkColor = Color.FromRgb(0x30, 0x80, 0x50),
            LeafColor  = Color.FromRgb(0x40, 0xA0, 0x60),
            FlowerColor = Color.FromRgb(0xFF, 0x80, 0xCC),
            HasFlower = true,
            Description = "청정한 연꽃"
        },
    ];
}
