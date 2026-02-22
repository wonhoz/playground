namespace DungeonDash.Engine;

public enum Tile { Wall, Floor, Door, StairsDown, Chest }

/// <summary>
/// BSP 기반 랜덤 던전 생성기.
/// </summary>
public sealed class DungeonGenerator
{
    private readonly Random _rng;
    public int Width { get; } = 40;
    public int Height { get; } = 30;
    public Tile[,] Map { get; private set; } = null!;
    public List<(int X, int Y, int W, int H)> Rooms { get; } = [];

    public DungeonGenerator(Random rng) => _rng = rng;

    public Tile[,] Generate(int floor)
    {
        Map = new Tile[Width, Height];
        Rooms.Clear();

        // 벽으로 초기화
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                Map[x, y] = Tile.Wall;

        // 방 생성 (5~8개)
        int roomCount = 5 + _rng.Next(4) + floor / 2;
        int attempts = 0;
        while (Rooms.Count < roomCount && attempts < 200)
        {
            attempts++;
            int rw = 4 + _rng.Next(5); // 4~8
            int rh = 3 + _rng.Next(4); // 3~6
            int rx = 1 + _rng.Next(Width - rw - 2);
            int ry = 1 + _rng.Next(Height - rh - 2);

            // 겹침 체크
            bool overlaps = false;
            foreach (var (ox, oy, ow, oh) in Rooms)
            {
                if (rx - 1 < ox + ow && rx + rw + 1 > ox && ry - 1 < oy + oh && ry + rh + 1 > oy)
                { overlaps = true; break; }
            }
            if (overlaps) continue;

            Rooms.Add((rx, ry, rw, rh));
            CarveRoom(rx, ry, rw, rh);
        }

        // 방 연결 (복도)
        for (int i = 1; i < Rooms.Count; i++)
        {
            var (ax, ay, aw, ah) = Rooms[i - 1];
            var (bx, by, bw, bh) = Rooms[i];
            int cx1 = ax + aw / 2, cy1 = ay + ah / 2;
            int cx2 = bx + bw / 2, cy2 = by + bh / 2;
            CarveCorridor(cx1, cy1, cx2, cy2);
        }

        // 마지막 방에 계단
        if (Rooms.Count > 0)
        {
            var last = Rooms[^1];
            Map[last.X + last.W / 2, last.Y + last.H / 2] = Tile.StairsDown;
        }

        // 랜덤 보물 상자
        int chests = 1 + _rng.Next(2) + floor / 3;
        for (int i = 0; i < chests && Rooms.Count > 2; i++)
        {
            var room = Rooms[1 + _rng.Next(Rooms.Count - 2)];
            int cx = room.X + 1 + _rng.Next(Math.Max(1, room.W - 2));
            int cy = room.Y + 1 + _rng.Next(Math.Max(1, room.H - 2));
            if (Map[cx, cy] == Tile.Floor)
                Map[cx, cy] = Tile.Chest;
        }

        return Map;
    }

    private void CarveRoom(int rx, int ry, int rw, int rh)
    {
        for (int x = rx; x < rx + rw; x++)
            for (int y = ry; y < ry + rh; y++)
                Map[x, y] = Tile.Floor;
    }

    private void CarveCorridor(int x1, int y1, int x2, int y2)
    {
        // L자 복도
        if (_rng.NextDouble() < 0.5)
        {
            CarveHLine(x1, x2, y1);
            CarveVLine(y1, y2, x2);
        }
        else
        {
            CarveVLine(y1, y2, x1);
            CarveHLine(x1, x2, y2);
        }
    }

    private void CarveHLine(int x1, int x2, int y)
    {
        for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                Map[x, y] = Tile.Floor;
    }

    private void CarveVLine(int y1, int y2, int x)
    {
        for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                Map[x, y] = Tile.Floor;
    }
}
