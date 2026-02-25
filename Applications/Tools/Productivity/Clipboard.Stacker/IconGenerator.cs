using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ClipboardStacker;

/// <summary>
/// Clipboard.Stacker 아이콘 — 겹쳐진 클립보드 3장으로 "스택" 시각화.
/// </summary>
public static class IconGenerator
{
    public static string IconFileName => "clipstacker.ico";

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
        g.Clear(Color.FromArgb(18, 18, 30));
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int pad = Math.Max(2, size / 10);

        // 3장의 클립보드를 뒤에서 앞으로 겹쳐서 그림
        for (int layer = 2; layer >= 0; layer--)
        {
            int offset = layer * Math.Max(1, size / 12);
            int x = pad + offset;
            int y = pad + offset;
            int w = size - pad * 2 - offset * 2 - Math.Max(1, size / 12);
            int h = (int)(w * 1.2);
            if (h > size - y - pad) h = size - y - pad;

            // 배경 (클립보드 종이)
            float alpha = layer == 0 ? 255 : (layer == 1 ? 200 : 140);
            using var paperBrush = new SolidBrush(Color.FromArgb((int)alpha,
                layer == 0 ? Color.FromArgb(79, 195, 247)   // Cyan (최전면)
                : layer == 1 ? Color.FromArgb(52, 152, 219) // Blue (중간)
                : Color.FromArgb(41, 128, 185)));            // DarkBlue (뒤)
            g.FillRectangle(paperBrush, x, y, w, h);

            // 최전면 클립보드에만 선 그리기 (텍스트 줄 표현)
            if (layer == 0 && size >= 32)
            {
                int lineY = y + Math.Max(3, size / 8);
                int lineStep = Math.Max(3, size / 8);
                using var linePen = new Pen(Color.FromArgb(160, 0, 50, 100), 1);
                for (int ln = 0; ln < 3 && lineY + ln * lineStep + lineStep < y + h - 2; ln++)
                    g.DrawLine(linePen, x + 3, lineY + ln * lineStep, x + w - 3, lineY + ln * lineStep);
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
        w.Write((short)0); w.Write((short)1); w.Write((short)frames.Length);

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
