using System.Windows;
using CircuitBreak.Models;

namespace CircuitBreak.Services;

/// <summary>내장 퍼즐 레벨 생성</summary>
public static class LevelFactory
{
    public static List<PuzzleLevel> CreateAll() =>
    [
        CreateLevel1(),
        CreateLevel2(),
        CreateLevel3(),
        CreateLevel4(),
        CreateLevel5(),
    ];

    // ─── 레벨 1: 단순 직렬 회로 (단선 버그) ──────────────────────────────
    static PuzzleLevel CreateLevel1() => new()
    {
        Number = 1,
        Name = "첫 번째 회로",
        Description = "9V 전원에 직렬로 연결된 저항 회로입니다.\nLED에 전류가 흐르지 않습니다. 원인을 찾으세요!",
        SourceVoltage = 9.0,
        SourceNodeId = 0,
        GroundNodeId = 3,
        TargetVoltages = new() { [1] = 6.0, [2] = 3.0 },
        TargetCurrentAmps = 0.003,
        Nodes =
        [
            new() { Id = 0, Position = new Point(100, 150), IsVoltageSource = true },
            new() { Id = 1, Position = new Point(250, 150) },
            new() { Id = 2, Position = new Point(400, 150) },
            new() { Id = 3, Position = new Point(550, 150), IsGround = true },
        ],
        Components =
        [
            new() { Id = 0, Type = ComponentType.Battery, NodeA = 0, NodeB = 3, Value = 9, Label = "9V" },
            new() { Id = 1, Type = ComponentType.Resistor, NodeA = 0, NodeB = 1, Value = 1000, Label = "R1 1kΩ" },
            new() { Id = 2, Type = ComponentType.BrokenWire, NodeA = 1, NodeB = 2,
                    Value = 0, BugValue = 1e9, IsBug = true, Label = "⚡ 단선!" },
            new() { Id = 3, Type = ComponentType.Resistor, NodeA = 2, NodeB = 3, Value = 1000, Label = "R2 1kΩ" },
        ]
    };

    // ─── 레벨 2: 병렬 회로 (저항값 오류) ────────────────────────────────
    static PuzzleLevel CreateLevel2() => new()
    {
        Number = 2,
        Name = "병렬 회로 오류",
        Description = "두 저항이 병렬 연결되어 있습니다.\nR2의 저항값이 잘못 납땜되어 전류가 예상보다 적습니다.",
        SourceVoltage = 6.0,
        SourceNodeId = 0,
        GroundNodeId = 2,
        TargetCurrentAmps = 0.009,  // 6V / (1kΩ || 1kΩ) = 12mA → 실제 목표 9mA
        Nodes =
        [
            new() { Id = 0, Position = new Point(150, 200), IsVoltageSource = true },
            new() { Id = 1, Position = new Point(350, 100) },
            new() { Id = 2, Position = new Point(350, 300), IsGround = true },
        ],
        Components =
        [
            new() { Id = 0, Type = ComponentType.Battery, NodeA = 0, NodeB = 2, Value = 6, Label = "6V" },
            new() { Id = 1, Type = ComponentType.Resistor, NodeA = 0, NodeB = 2, Value = 1000, Label = "R1 1kΩ" },
            new() { Id = 2, Type = ComponentType.WrongResistance, NodeA = 0, NodeB = 2,
                    Value = 1000, BugValue = 3000, IsBug = true, Label = "R2 ?Ω" },
        ]
    };

    // ─── 레벨 3: T자형 회로 (단락) ───────────────────────────────────────
    static PuzzleLevel CreateLevel3() => new()
    {
        Number = 3,
        Name = "T자형 회로 단락",
        Description = "3개 저항의 T자형 회로입니다.\n중간에 단락이 발생해 R3에 전류가 너무 많이 흐릅니다.",
        SourceVoltage = 12.0,
        SourceNodeId = 0,
        GroundNodeId = 3,
        TargetVoltages = new() { [1] = 8.0, [2] = 4.0 },
        Nodes =
        [
            new() { Id = 0, Position = new Point(100, 200), IsVoltageSource = true },
            new() { Id = 1, Position = new Point(280, 200) },
            new() { Id = 2, Position = new Point(460, 200) },
            new() { Id = 3, Position = new Point(460, 360), IsGround = true },
        ],
        Components =
        [
            new() { Id = 0, Type = ComponentType.Battery, NodeA = 0, NodeB = 3, Value = 12, Label = "12V" },
            new() { Id = 1, Type = ComponentType.Resistor, NodeA = 0, NodeB = 1, Value = 1000, Label = "R1 1kΩ" },
            new() { Id = 2, Type = ComponentType.Resistor, NodeA = 1, NodeB = 2, Value = 1000, Label = "R2 1kΩ" },
            new() { Id = 3, Type = ComponentType.ShortCircuit, NodeA = 1, NodeB = 3,
                    Value = 10000, BugValue = 0.001, IsBug = true, Label = "R3 단락!" },
            new() { Id = 4, Type = ComponentType.Resistor, NodeA = 2, NodeB = 3, Value = 1000, Label = "R4 1kΩ" },
        ]
    };

    // ─── 레벨 4: 휘트스톤 브리지 ─────────────────────────────────────────
    static PuzzleLevel CreateLevel4() => new()
    {
        Number = 4,
        Name = "휘트스톤 브리지",
        Description = "휘트스톤 브리지 회로입니다.\n브리지가 평형이 되도록 R4의 값을 맞추세요.",
        SourceVoltage = 10.0,
        SourceNodeId = 0,
        GroundNodeId = 4,
        TargetVoltages = new() { [2] = 5.0, [3] = 5.0 },  // 평형 조건
        Nodes =
        [
            new() { Id = 0, Position = new Point(200, 100), IsVoltageSource = true },
            new() { Id = 1, Position = new Point(200, 300) },
            new() { Id = 2, Position = new Point(360, 200) },
            new() { Id = 3, Position = new Point(40, 200) },
            new() { Id = 4, Position = new Point(200, 500), IsGround = true },
        ],
        Components =
        [
            new() { Id = 0, Type = ComponentType.Battery, NodeA = 0, NodeB = 4, Value = 10, Label = "10V" },
            new() { Id = 1, Type = ComponentType.Resistor, NodeA = 0, NodeB = 2, Value = 1000, Label = "R1 1kΩ" },
            new() { Id = 2, Type = ComponentType.Resistor, NodeA = 0, NodeB = 3, Value = 1000, Label = "R2 1kΩ" },
            new() { Id = 3, Type = ComponentType.Resistor, NodeA = 2, NodeB = 4, Value = 1000, Label = "R3 1kΩ" },
            new() { Id = 4, Type = ComponentType.WrongResistance, NodeA = 3, NodeB = 4,
                    Value = 1000, BugValue = 2000, IsBug = true, Label = "R4 ?Ω" },
            new() { Id = 5, Type = ComponentType.Wire, NodeA = 2, NodeB = 1, Label = "갈바노미터" },
            new() { Id = 6, Type = ComponentType.Wire, NodeA = 3, NodeB = 1, Label = "" },
            new() { Id = 7, Type = ComponentType.Wire, NodeA = 1, NodeB = 4, Label = "" },
        ]
    };

    // ─── 레벨 5: 복합 디버깅 (다중 버그) ────────────────────────────────
    static PuzzleLevel CreateLevel5() => new()
    {
        Number = 5,
        Name = "복합 회로 디버깅",
        Description = "여러 버그가 숨어있는 복합 회로입니다.\n모든 버그를 찾아 LED에 정상 전류를 흐르게 하세요.",
        SourceVoltage = 5.0,
        SourceNodeId = 0,
        GroundNodeId = 5,
        TargetVoltages = new() { [2] = 2.5, [4] = 1.0 },
        TargetCurrentAmps = 0.0025,
        Nodes =
        [
            new() { Id = 0, Position = new Point(80, 200), IsVoltageSource = true },
            new() { Id = 1, Position = new Point(200, 200) },
            new() { Id = 2, Position = new Point(320, 200) },
            new() { Id = 3, Position = new Point(440, 200) },
            new() { Id = 4, Position = new Point(560, 200) },
            new() { Id = 5, Position = new Point(680, 200), IsGround = true },
        ],
        Components =
        [
            new() { Id = 0, Type = ComponentType.Battery, NodeA = 0, NodeB = 5, Value = 5, Label = "5V" },
            new() { Id = 1, Type = ComponentType.Resistor, NodeA = 0, NodeB = 1, Value = 500, Label = "R1 500Ω" },
            new() { Id = 2, Type = ComponentType.BrokenWire, NodeA = 1, NodeB = 2,
                    Value = 0, BugValue = 1e9, IsBug = true, Label = "⚡ 단선!" },
            new() { Id = 3, Type = ComponentType.Resistor, NodeA = 2, NodeB = 3, Value = 500, Label = "R2 500Ω" },
            new() { Id = 4, Type = ComponentType.WrongResistance, NodeA = 3, NodeB = 4,
                    Value = 500, BugValue = 2000, IsBug = true, Label = "R3 ?Ω" },
            new() { Id = 5, Type = ComponentType.Resistor, NodeA = 4, NodeB = 5, Value = 500, Label = "R4 500Ω" },
        ]
    };
}
