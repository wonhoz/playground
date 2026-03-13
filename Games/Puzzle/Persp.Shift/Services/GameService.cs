using PerspShift.Models;

namespace PerspShift.Services;

public class GameService
{
    public const int N = 5;

    private readonly bool[,,] _grid = new bool[N, N, N]; // [x, y, z]

    public Level? CurrentLevel { get; private set; }

    public void LoadLevel(Level level)
    {
        CurrentLevel = level;
        Reset();
    }

    public void Reset() => Array.Clear(_grid, 0, _grid.Length);

    // ── 실루엣 투영 ──────────────────────────────────────────────────────

    /// <summary>Front(XY): z 방향으로 OR → [x, y]</summary>
    public bool[,] GetFrontSilhouette()
    {
        var s = new bool[N, N];
        for (int x = 0; x < N; x++)
        for (int y = 0; y < N; y++)
        for (int z = 0; z < N; z++)
            if (_grid[x, y, z]) { s[x, y] = true; break; }
        return s;
    }

    /// <summary>Top(XZ): y 방향으로 OR → [x, z]</summary>
    public bool[,] GetTopSilhouette()
    {
        var s = new bool[N, N];
        for (int x = 0; x < N; x++)
        for (int z = 0; z < N; z++)
        for (int y = 0; y < N; y++)
            if (_grid[x, y, z]) { s[x, z] = true; break; }
        return s;
    }

    /// <summary>Side(YZ): x 방향으로 OR → [y, z]</summary>
    public bool[,] GetSideSilhouette()
    {
        var s = new bool[N, N];
        for (int y = 0; y < N; y++)
        for (int z = 0; z < N; z++)
        for (int x = 0; x < N; x++)
            if (_grid[x, y, z]) { s[y, z] = true; break; }
        return s;
    }

    // ── 클릭: depth-line 전체 토글 ────────────────────────────────────

    public void ToggleFront(int x, int y) => ToggleLine(a: x, b: y, axis: 2);
    public void ToggleTop(int x,   int z) => ToggleLine(a: x, b: z, axis: 1);
    public void ToggleSide(int y,  int z) => ToggleLine(a: y, b: z, axis: 0);

    private void ToggleLine(int a, int b, int axis)
    {
        bool anySet = false;
        for (int i = 0; i < N; i++)
        {
            bool v = axis switch { 0 => _grid[i, a, b], 1 => _grid[a, i, b], _ => _grid[a, b, i] };
            if (v) { anySet = true; break; }
        }
        bool fill = !anySet;
        for (int i = 0; i < N; i++)
        {
            if      (axis == 0) _grid[i, a, b] = fill;
            else if (axis == 1) _grid[a, i, b] = fill;
            else                _grid[a, b, i] = fill;
        }
    }

    // ── 정답 확인 ─────────────────────────────────────────────────────

    public bool IsSolved()
    {
        if (CurrentLevel is null) return false;
        return Match(GetFrontSilhouette(), CurrentLevel.FrontTarget)
            && Match(GetTopSilhouette(),   CurrentLevel.TopTarget)
            && Match(GetSideSilhouette(),  CurrentLevel.SideTarget);
    }

    private static bool Match(bool[,] a, bool[,] b)
    {
        for (int i = 0; i < N; i++)
        for (int j = 0; j < N; j++)
            if (a[i, j] != b[i, j]) return false;
        return true;
    }
}
