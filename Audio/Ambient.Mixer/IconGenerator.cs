using System.Drawing;
using System.Drawing.Imaging;

namespace AmbientMixer;

/// <summary>
/// Ambient Mixer 앱 아이콘 생성.
/// 이퀄라이저 바 5개(높이 차이)로 앰비언트 사운드 믹서를 직관적으로 표현.
/// 색상: 청록(#00BCD4) → 보라(#7B1FA2) 그라디언트.
/// </summary>
public static class IconGenerator
{
    public static string IconFileName => "ambientmixer.ico";

    // 5개 바의 높이 비율 (0.0~1.0) — 불규칙한 파형 느낌
    private static readonly float[] BarHeights = [0.45f, 0.85f, 1.00f, 0.65f, 0.40f];

    // 그라디언트 색상 (청록 → 보라)
    private static readonly Color[] BarColors =
    [
        Color.FromArgb(  0, 188, 212),  // #00BCD4 cyan
        Color.FromArgb( 38, 166, 211),  // 중간
        Color.FromArgb( 63, 136, 209),  // 중간
        Color.FromArgb(100,  90, 200),  // 중간
        Color.FromArgb(123,  31, 162),  // #7B1FA2 purple
    ];

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
        g.Clear(Color.FromArgb(26, 26, 46));   // #1A1A2E

        int bars    = BarHeights.Length;
        int padX    = Math.Max(1, size / 8);
        int padY    = Math.Max(1, size / 8);
        int gap     = Math.Max(1, size / 16);
        int totalW  = size - padX * 2;
        int barW    = (totalW - gap * (bars - 1)) / bars;
        int maxH    = size - padY * 2;

        if (barW < 1) barW = 1;

        for (int i = 0; i < bars; i++)
        {
            int bh = Math.Max(2, (int)(maxH * BarHeights[i]));
            int x  = padX + i * (barW + gap);
            int y  = padY + (maxH - bh);

            using var brush = new SolidBrush(BarColors[i]);
            g.FillRectangle(brush, x, y, barW, bh);

            // 상단 하이라이트 (밝은 흰색 띠) — 깊이감
            if (bh >= 4 && barW >= 2)
            {
                int highlightH = Math.Max(1, barW / 3);
                using var hi = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
                g.FillRectangle(hi, x, y, barW, highlightH);
            }
        }
        return bmp;
    }

    private static void SaveIco(Bitmap[] frames, string path)
    {
        var pngs = frames.Select(f =>
        {
            using var ms = new MemoryStream();
            f.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToArray();

        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)frames.Length);

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
