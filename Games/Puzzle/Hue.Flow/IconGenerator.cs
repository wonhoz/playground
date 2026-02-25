using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace HueFlow;

/// <summary>
/// Hue.Flow 앱 아이콘 생성.
/// 3×3 타일 그리드에서 좌상단 L자 영역을 UI 강조색(Cyan)으로 채워
/// 플러드필 게임 메카닉을 직관적으로 표현.
/// </summary>
public static class IconGenerator
{
    // Catppuccin Mocha 기반 파스텔 6색 — MainWindow.xaml.cs의 ColorHex와 동일
    private static readonly System.Drawing.Color[] Palette =
    [
        System.Drawing.Color.FromArgb(243, 139, 168),  // 0 Red   #F38BA8
        System.Drawing.Color.FromArgb(137, 180, 250),  // 1 Blue  #89B4FA
        System.Drawing.Color.FromArgb(166, 227, 161),  // 2 Green #A6E3A1
        System.Drawing.Color.FromArgb(250, 179, 135),  // 3 Peach #FAB387
        System.Drawing.Color.FromArgb(203, 166, 247),  // 4 Mauve #CBA6F7
        System.Drawing.Color.FromArgb(137, 220, 235),  // 5 Sky   #89DCEB
        System.Drawing.Color.FromArgb( 79, 195, 247),  // 6 UI Accent (Cyan) — territory
    ];

    // 3×3 배치: 6=Cyan 영역(L자 좌상단), 나머지=게임 색상
    //   [Cyan][Cyan][ Red]
    //   [Cyan][Org][Pur]
    //   [Blue][Grn][Tea]
    private static readonly int[] Layout       = [6, 6, 0, 6, 3, 4, 1, 2, 5];
    private static readonly bool[] IsTerritory = [true, true, false, true, false, false, false, false, false];

    public static string IconFileName => "hueflow.ico";

    public static void Generate(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        int[] sizes = [16, 32, 48, 256];
        var frames = sizes.Select(DrawFrame).ToArray();
        SaveIco(frames, Path.Combine(outputDir, IconFileName));
        foreach (var f in frames) f.Dispose();
    }

    private static Bitmap DrawFrame(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(18, 18, 30));   // #12121E

        int pad   = Math.Max(1, size / 9);
        int total = size - pad * 2;
        int cell  = total / 3;
        int gap   = Math.Max(1, size / 24);
        int tile  = cell - gap;

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                int idx   = r * 3 + c;
                var color = Palette[Layout[idx]];

                // 비영역 타일은 약간 어둡게 — "미점령" 느낌
                if (!IsTerritory[idx])
                    color = System.Drawing.Color.FromArgb(
                        (int)(color.R * 0.75),
                        (int)(color.G * 0.75),
                        (int)(color.B * 0.75));

                using var brush = new SolidBrush(color);
                int x = pad + c * cell;
                int y = pad + r * cell;
                g.FillRectangle(brush, x, y, tile, tile);

                // 영역 타일: 우하단 작은 하이라이트(밝은 흰 점) → 확장 중 느낌
                if (IsTerritory[idx] && tile >= 8)
                {
                    int dot = Math.Max(2, tile / 5);
                    using var hi = new SolidBrush(System.Drawing.Color.FromArgb(160, 255, 255, 255));
                    g.FillRectangle(hi, x + tile - dot - 1, y + 1, dot, dot);
                }
            }
        return bmp;
    }

    private static void SaveIco(Bitmap[] frames, string path)
    {
        var pngs = frames.Select(f =>
        {
            using var ms = new System.IO.MemoryStream();
            f.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToArray();

        using var ms = new System.IO.MemoryStream();
        using var w  = new BinaryWriter(ms);

        w.Write((short)0);                  // reserved
        w.Write((short)1);                  // type = ICO
        w.Write((short)frames.Length);      // image count

        int offset = 6 + frames.Length * 16;
        for (int i = 0; i < frames.Length; i++)
        {
            int sz = frames[i].Width;
            w.Write((byte)(sz >= 256 ? 0 : sz));
            w.Write((byte)(sz >= 256 ? 0 : sz));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(pngs[i].Length);
            w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);

        File.WriteAllBytes(path, ms.ToArray());
    }
}
