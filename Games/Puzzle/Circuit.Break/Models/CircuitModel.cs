using System.Windows;

namespace CircuitBreak.Models;

// ─── 노드 (회로 교점) ─────────────────────────────────────────────────────
public class Node
{
    public int Id { get; set; }
    public Point Position { get; set; }
    public double Voltage { get; set; } = 0;  // 시뮬레이션 결과
    public bool IsGround { get; set; }
    public bool IsVoltageSource { get; set; }

    public override string ToString() => $"N{Id}({Voltage:F2}V)";
}

// ─── 소자 유형 ────────────────────────────────────────────────────────────
public enum ComponentType
{
    Wire,
    Resistor,
    Battery,
    BrokenWire,   // 단선 버그
    ShortCircuit, // 단락 버그
    WrongResistance // 저항값 오류 버그
}

// ─── 소자 ─────────────────────────────────────────────────────────────────
public class Component
{
    public int Id { get; set; }
    public ComponentType Type { get; set; }
    public int NodeA { get; set; }
    public int NodeB { get; set; }
    public double Value { get; set; }     // 저항(Ω) 또는 전압(V)
    public double BugValue { get; set; } // 버그일 때의 실제 값
    public bool IsBug { get; set; }
    public bool IsFixed { get; set; }    // 플레이어가 수정 완료
    public string Label { get; set; } = "";

    public double EffectiveValue => IsFixed ? Value : BugValue;
}

// ─── 퍼즐 레벨 ───────────────────────────────────────────────────────────
public class PuzzleLevel
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Node> Nodes { get; set; } = [];
    public List<Component> Components { get; set; } = [];
    public int SourceNodeId { get; set; }   // 전압원 양극
    public int GroundNodeId { get; set; }   // 접지 노드
    public double SourceVoltage { get; set; } = 9.0;

    // 목표: 특정 노드의 전압이 예상값이어야 함
    public Dictionary<int, double> TargetVoltages { get; set; } = [];
    public double Tolerance { get; set; } = 0.05; // ±5% 허용

    // 출력 전류 목표
    public double? TargetCurrentAmps { get; set; }
}

// ─── 측정 도구 ────────────────────────────────────────────────────────────
public enum MeterMode { Voltmeter, Ohmmeter, Ammeter }

// ─── 시뮬레이션 결과 ─────────────────────────────────────────────────────
public class SimulationResult
{
    public Dictionary<int, double> NodeVoltages { get; set; } = [];
    public Dictionary<int, double> BranchCurrents { get; set; } = [];
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = "";
    public double TotalCurrent { get; set; }
}
