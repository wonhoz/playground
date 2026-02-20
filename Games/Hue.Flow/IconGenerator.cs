using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace HueFlow;

/// <summary>
/// Hue.Flow 앱 아이콘 생성 — 3×3 컬러 타일 그리드를 어두운 배경에 그림.
/// </summary>
public static class IconGenerator
{
    private static readonly System.Drawing.Color[] Palette =
    [
        System.Drawing.Color.FromArgb(231, 76,  60),  // Red
        System.Drawing.Color.FromArgb( 52,152,219),   // Blue
        System.Drawing.Color.FromArgb( 46,204,113),   // Green
        System.Drawing.Color.FromArgb(243,156, 18),   // Orange
        System.Drawing.Color.FromArgb(155, 89,182),   // Purple
        System.Drawing.Color.FromArgb( 26,188,156),   // Teal
    ];

    // 3×3 배치에 사용할 색상 인덱스
    private static readonly int[] Layout = [0, 1, 2, 3, 4, 5, 1, 0, 3];

    public static void Generate(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        int[] sizes = [16, 32, 48, 256];
        var frames = sizes.Select(DrawFrame).ToArray();
        SaveIco(frames, Path.Combine(outputDir, "app.ico"));
        foreach (var f in frames) f.Dispose();
    }

    private static Bitmap DrawFrame(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(System.Drawing.Color.FromArgb(26, 26, 46));

        int pad   = Math.Max(1, size / 9);
        int total = size - pad * 2;
        int cell  = total / 3;
        int gap   = Math.Max(1, size / 24);
        int tile  = cell - gap;

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                var color = Palette[Layout[r * 3 + c]];
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, pad + c * cell, pad + r * cell, tile, tile);
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
