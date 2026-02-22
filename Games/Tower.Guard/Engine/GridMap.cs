namespace TowerGuard.Engine;

public enum TileType
{
    Empty,
    Path,
    Buildable,
    Start,
    End
}

public sealed class GridMap
{
    public const int Cols = 20;
    public const int Rows = 14;
    public const double TileSize = 32;

    public TileType[,] Tiles { get; } = new TileType[Cols, Rows];
    public List<(int X, int Y)> PathTiles { get; } = [];

    public GridMap()
    {
        // Fill all as buildable first
        for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                Tiles[x, y] = TileType.Buildable;

        // S-shaped path from left to right
        var path = new List<(int X, int Y)>();

        // Row 2: left to right (x=0..17)
        for (int x = 0; x <= 17; x++) path.Add((x, 2));
        // Down from (17,2) to (17,5)
        for (int y = 3; y <= 5; y++) path.Add((17, y));
        // Row 5: right to left (x=17..2)
        for (int x = 16; x >= 2; x--) path.Add((x, 5));
        // Down from (2,5) to (2,8)
        for (int y = 6; y <= 8; y++) path.Add((2, y));
        // Row 8: left to right (x=2..17)
        for (int x = 3; x <= 17; x++) path.Add((x, 8));
        // Down from (17,8) to (17,11)
        for (int y = 9; y <= 11; y++) path.Add((17, y));
        // Row 11: right to left (x=17..19 exit)
        for (int x = 18; x <= 19; x++) path.Add((x, 11));

        foreach (var (px, py) in path)
        {
            Tiles[px, py] = TileType.Path;
        }

        // Mark start and end
        Tiles[0, 2] = TileType.Start;
        Tiles[19, 11] = TileType.End;

        PathTiles.AddRange(path);
        // Insert start at beginning
        PathTiles.Insert(0, (0, 2));
    }

    public bool IsBuildable(int x, int y)
    {
        if (x < 0 || x >= Cols || y < 0 || y >= Rows) return false;
        return Tiles[x, y] == TileType.Buildable;
    }
}
