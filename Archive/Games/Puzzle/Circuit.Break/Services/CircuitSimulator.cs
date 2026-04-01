using CircuitBreak.Models;

namespace CircuitBreak.Services;

/// <summary>
/// 수정된 노달 분석법(MNA)으로 회로를 시뮬레이션.
/// 가우스-요르단 소거법으로 선형 방정식 풀기.
/// </summary>
public static class CircuitSimulator
{
    public static SimulationResult Simulate(PuzzleLevel level)
    {
        var result = new SimulationResult();
        var nodes = level.Nodes;
        int n = nodes.Count;

        // 노드 ID → 인덱스 맵
        var idxOf = nodes.ToDictionary(nd => nd.Id, nd => nodes.IndexOf(nd));

        // 접지 노드 인덱스
        int gnd = idxOf[level.GroundNodeId];

        // G 행렬 (컨덕턴스), B/C 벡터 구성
        double[,] G = new double[n, n];
        double[] I = new double[n];

        try
        {
            foreach (var comp in level.Components)
            {
                int a = idxOf[comp.NodeA];
                int b = idxOf[comp.NodeB];
                double val = comp.EffectiveValue;

                switch (comp.Type)
                {
                    case ComponentType.Wire:
                        // 이상적인 도선: 매우 작은 저항
                        AddResistor(G, a, b, gnd, 0.001);
                        break;

                    case ComponentType.Resistor:
                    case ComponentType.WrongResistance:
                        if (val <= 0) val = 0.001;
                        AddResistor(G, a, b, gnd, val);
                        break;

                    case ComponentType.BrokenWire:
                        // 단선: 매우 큰 저항 (개방)
                        AddResistor(G, a, b, gnd, 1e9);
                        break;

                    case ComponentType.ShortCircuit:
                        // 단락: 매우 작은 저항
                        AddResistor(G, a, b, gnd, 0.0001);
                        break;

                    case ComponentType.Battery:
                        // 전압원: 소스 노드에 전류 주입 방식으로 처리
                        // 이 단순화에서는 전압원을 이미 SourceVoltage/GroundNodeId로 처리
                        break;
                }
            }

            // 접지 조건: Vgnd = 0
            for (int j = 0; j < n; j++) G[gnd, j] = 0;
            G[gnd, gnd] = 1;
            I[gnd] = 0;

            // 전압원: 소스 노드에 전압 설정
            int src = idxOf[level.SourceNodeId];
            for (int j = 0; j < n; j++) G[src, j] = 0;
            G[src, src] = 1;
            I[src] = level.SourceVoltage;

            // 가우스-요르단 소거법
            var voltages = GaussJordan(G, I, n);

            if (voltages == null)
            {
                result.IsValid = false;
                result.ErrorMessage = "방정식 풀기 실패 (특이 행렬)";
                return result;
            }

            // 결과 저장
            for (int i = 0; i < n; i++)
                result.NodeVoltages[nodes[i].Id] = Math.Round(voltages[i], 4);

            // 전체 전류 계산 (소스에서 나가는 전류)
            double totalI = 0;
            foreach (var comp in level.Components)
            {
                if (comp.Type == ComponentType.Battery) continue;
                int a = idxOf[comp.NodeA];
                int b = idxOf[comp.NodeB];
                double va = voltages[a], vb = voltages[b];
                double r = comp.Type switch
                {
                    ComponentType.Resistor or ComponentType.WrongResistance => Math.Max(comp.EffectiveValue, 0.001),
                    ComponentType.Wire => 0.001,
                    ComponentType.BrokenWire => 1e9,
                    ComponentType.ShortCircuit => 0.0001,
                    _ => 1e9
                };
                double current = (va - vb) / r;
                result.BranchCurrents[comp.Id] = Math.Round(current, 6);
                if (comp.NodeA == level.SourceNodeId) totalI += current;
            }

            result.TotalCurrent = Math.Round(totalI, 6);
            result.IsValid = true;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    static void AddResistor(double[,] G, int a, int b, int gnd, double r)
    {
        double g = 1.0 / r;
        G[a, a] += g;
        G[b, b] += g;
        if (a != gnd && b != gnd)
        {
            G[a, b] -= g;
            G[b, a] -= g;
        }
    }

    static double[]? GaussJordan(double[,] A, double[] b, int n)
    {
        // Augmented matrix
        double[,] M = new double[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) M[i, j] = A[i, j];
            M[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            // Partial pivoting
            int maxRow = col;
            double maxVal = Math.Abs(M[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(M[row, col]) > maxVal)
                {
                    maxVal = Math.Abs(M[row, col]);
                    maxRow = row;
                }
            }
            if (maxRow != col)
                for (int k = 0; k <= n; k++)
                    (M[col, k], M[maxRow, k]) = (M[maxRow, k], M[col, k]);

            if (Math.Abs(M[col, col]) < 1e-12) return null;

            double pivot = M[col, col];
            for (int k = 0; k <= n; k++) M[col, k] /= pivot;

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double factor = M[row, col];
                for (int k = 0; k <= n; k++)
                    M[row, k] -= factor * M[col, k];
            }
        }

        return Enumerable.Range(0, n).Select(i => M[i, n]).ToArray();
    }

    // ─── 정답 검사 ────────────────────────────────────────────────────────
    public static bool CheckSolution(PuzzleLevel level, SimulationResult result)
    {
        if (!result.IsValid) return false;

        foreach (var (nodeId, targetV) in level.TargetVoltages)
        {
            if (!result.NodeVoltages.TryGetValue(nodeId, out double actual)) return false;
            double tol = Math.Abs(targetV) * level.Tolerance + 0.01;
            if (Math.Abs(actual - targetV) > tol) return false;
        }

        if (level.TargetCurrentAmps.HasValue)
        {
            double tol = Math.Abs(level.TargetCurrentAmps.Value) * level.Tolerance + 0.001;
            if (Math.Abs(result.TotalCurrent - level.TargetCurrentAmps.Value) > tol) return false;
        }

        return true;
    }
}
