namespace HueFlow.Game;

/// <summary>
/// Hue Flow 게임 보드.
/// 10×10 그리드의 타일을 6가지 색으로 채우고,
/// 좌상단(0,0)에서 시작한 영역을 30회 이내에 전체로 확장하면 승리.
/// </summary>
public class FloodBoard
{
    public const int Size      = 10;
    public const int Colors    = 6;
    public const int MaxMoves  = 30;

    public int[,] Grid { get; } = new int[Size, Size];
    public int    Moves         { get; private set; }
    public bool   IsWon         => _territory.Count == Size * Size;
    public bool   IsLost        => Moves >= MaxMoves && !IsWon;
    public int    TerritorySize => _territory.Count;

    private readonly HashSet<(int r, int c)> _territory = [];
    private readonly Random _rng = new();

    public FloodBoard() => Reset();

    /// <summary>새 게임 시작</summary>
    public void Reset()
    {
        _territory.Clear();
        Moves = 0;

        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                Grid[r, c] = _rng.Next(Colors);

        // 초기 영역: (0,0)에서 같은 색으로 연결된 모든 셀
        ExpandFrom(0, 0, Grid[0, 0]);
    }

    /// <summary>색 선택 → 영역 확장</summary>
    public void ChooseColor(int color)
    {
        if (color == Grid[0, 0] || IsWon || IsLost) return;

        // 영역 전체를 새 색으로 변경
        foreach (var (r, c) in _territory)
            Grid[r, c] = color;

        // 인접한 같은 색 셀을 BFS로 흡수
        ExpandBorder(color);
        Moves++;
    }

    private void ExpandFrom(int startR, int startC, int color)
    {
        var queue = new Queue<(int, int)>();
        queue.Enqueue((startR, startC));
        _territory.Add((startR, startC));

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();
            foreach (var n in Adj(r, c))
                if (!_territory.Contains(n) && Grid[n.r, n.c] == color)
                {
                    _territory.Add(n);
                    queue.Enqueue(n);
                }
        }
    }

    private void ExpandBorder(int color)
    {
        var visited = new HashSet<(int r, int c)>(_territory);
        var queue   = new Queue<(int r, int c)>();

        foreach (var (r, c) in _territory)
            foreach (var n in Adj(r, c))
                if (!visited.Contains(n) && Grid[n.r, n.c] == color)
                {
                    visited.Add(n);
                    queue.Enqueue(n);
                }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            _territory.Add(cell);
            foreach (var n in Adj(cell.r, cell.c))
                if (!visited.Contains(n) && Grid[n.r, n.c] == color)
                {
                    visited.Add(n);
                    queue.Enqueue(n);
                }
        }
    }

    private static IEnumerable<(int r, int c)> Adj(int r, int c)
    {
        if (r > 0)        yield return (r - 1, c);
        if (r < Size - 1) yield return (r + 1, c);
        if (c > 0)        yield return (r, c - 1);
        if (c < Size - 1) yield return (r, c + 1);
    }
}
