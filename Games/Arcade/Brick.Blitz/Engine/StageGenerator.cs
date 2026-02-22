using System.Windows.Media;

namespace BrickBlitz.Engine;

public static class StageGenerator
{
    private static readonly Color Red = Color.FromRgb(0xE7, 0x4C, 0x3C);
    private static readonly Color Orange = Color.FromRgb(0xFF, 0x8C, 0x00);
    private static readonly Color Yellow = Color.FromRgb(0xFF, 0xD7, 0x00);
    private static readonly Color Green = Color.FromRgb(0x2E, 0xCC, 0x71);
    private static readonly Color Blue = Color.FromRgb(0x3A, 0x86, 0xFF);
    private static readonly Color Purple = Color.FromRgb(0x9B, 0x59, 0xB6);
    private static readonly Color Silver = Color.FromRgb(0xC0, 0xC0, 0xC0);
    private static readonly Color Grey = Color.FromRgb(0x66, 0x66, 0x66);

    private static readonly Color[] RowColors = [Red, Orange, Yellow, Green, Blue, Purple];

    // Layout area: columns 0-8, each brick 50w + 5 gap = ~55 per col
    // Offset from left edge so bricks are centered in 500px window
    private const double BrickW = 50;
    private const double BrickH = 18;
    private const double GapX = 5;
    private const double GapY = 4;
    private const int Cols = 8;
    private const double OffsetX = 22.5; // (500 - 8*50 - 7*5) / 2
    private const double OffsetY = 50;

    public static List<Brick> Generate(int stage)
    {
        return stage switch
        {
            1 => Stage1(),
            2 => Stage2(),
            3 => Stage3(),
            4 => Stage4(),
            5 => Stage5(),
            6 => Stage6(),
            7 => Stage7(),
            _ => Stage8()
        };
    }

    private static (double x, double y) Pos(int col, int row)
    {
        double x = OffsetX + col * (BrickW + GapX);
        double y = OffsetY + row * (BrickH + GapY);
        return (x, y);
    }

    private static Brick Normal(int col, int row, Color color, int points = 100)
    {
        var (x, y) = Pos(col, row);
        return new Brick(x, y, BrickType.Normal, color, points);
    }

    private static Brick Hard(int col, int row)
    {
        var (x, y) = Pos(col, row);
        return new Brick(x, y, BrickType.Hard, Silver, 200);
    }

    private static Brick Unbreakable(int col, int row)
    {
        var (x, y) = Pos(col, row);
        return new Brick(x, y, BrickType.Unbreakable, Grey, 0);
    }

    // Stage 1: Simple rows
    private static List<Brick> Stage1()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 5; row++)
        {
            var color = RowColors[row % RowColors.Length];
            for (int col = 0; col < Cols; col++)
                bricks.Add(Normal(col, row, color));
        }
        return bricks;
    }

    // Stage 2: Checkerboard with hard bricks
    private static List<Brick> Stage2()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if ((row + col) % 2 == 0)
                    bricks.Add(Normal(col, row, RowColors[row % RowColors.Length]));
                else if (row < 2)
                    bricks.Add(Hard(col, row));
            }
        }
        return bricks;
    }

    // Stage 3: Diamond pattern
    private static List<Brick> Stage3()
    {
        var bricks = new List<Brick>();
        int[] widths = [2, 4, 6, 8, 8, 6, 4, 2];
        for (int row = 0; row < widths.Length; row++)
        {
            int w = widths[row];
            int start = (Cols - w) / 2;
            for (int col = start; col < start + w; col++)
            {
                if (row == 0 || row == widths.Length - 1)
                    bricks.Add(Hard(col, row));
                else
                    bricks.Add(Normal(col, row, RowColors[row % RowColors.Length]));
            }
        }
        return bricks;
    }

    // Stage 4: Fortress with unbreakable walls
    private static List<Brick> Stage4()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if (col == 0 || col == Cols - 1)
                    bricks.Add(Unbreakable(col, row));
                else if (row == 3)
                    bricks.Add(Unbreakable(col, row));
                else if (row < 3)
                    bricks.Add(Hard(col, row));
                else
                    bricks.Add(Normal(col, row, RowColors[(row + col) % RowColors.Length]));
            }
        }
        return bricks;
    }

    // Stage 5: Stripes alternating hard and normal
    private static List<Brick> Stage5()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if (row % 2 == 0)
                    bricks.Add(Hard(col, row));
                else
                    bricks.Add(Normal(col, row, RowColors[col % RowColors.Length]));
            }
        }
        return bricks;
    }

    // Stage 6: Cross pattern with unbreakable border
    private static List<Brick> Stage6()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                bool isCross = col == 3 || col == 4 || row == 3 || row == 4;
                bool isBorder = row == 0 || row == 7 || col == 0 || col == Cols - 1;

                if (isBorder && !isCross)
                    bricks.Add(Unbreakable(col, row));
                else if (isCross)
                    bricks.Add(Hard(col, row));
                else
                    bricks.Add(Normal(col, row, RowColors[(row + col) % RowColors.Length]));
            }
        }
        return bricks;
    }

    // Stage 7: Pyramid
    private static List<Brick> Stage7()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 8; row++)
        {
            int start = row;
            int end = Cols - row;
            if (start >= end) break;
            for (int col = start; col < end; col++)
            {
                if (row < 2)
                    bricks.Add(Hard(col, row));
                else if (col == start || col == end - 1)
                    bricks.Add(Unbreakable(col, row));
                else
                    bricks.Add(Normal(col, row, RowColors[row % RowColors.Length]));
            }
        }
        return bricks;
    }

    // Stage 8: Final - dense with lots of hard and unbreakable
    private static List<Brick> Stage8()
    {
        var bricks = new List<Brick>();
        for (int row = 0; row < 9; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if ((row + col) % 4 == 0)
                    bricks.Add(Unbreakable(col, row));
                else if ((row + col) % 3 == 0)
                    bricks.Add(Hard(col, row));
                else
                    bricks.Add(Normal(col, row, RowColors[(row * 3 + col) % RowColors.Length]));
            }
        }
        return bricks;
    }
}
