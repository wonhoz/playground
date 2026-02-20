using System.Drawing;
using System.Drawing.Imaging;
using DrawColor = System.Drawing.Color;

namespace BatchRename;

/// <summary>
/// Batch Rename 아이콘: 파일 3개가 화살표(→)로 이름이 바뀌는 모습.
/// 왼쪽 파일 스택(회색) → 오른쪽 파일(황금색).
/// </summary>
public static class IconGenerator
{
    public static string IconFileName => "batchrename.ico";

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
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(DrawColor.FromArgb(26, 26, 46));

        int pad   = Math.Max(1, size / 10);
        int halfW = (size - pad * 2) / 2;
        int fileH = Math.Max(4, size / 3);
        int fileW = Math.Max(3, (int)(halfW * 0.7));

        // 왼쪽: 파일 3개 스택 (오프셋 적용)
        var srcColor = DrawColor.FromArgb(100, 110, 130);
        for (int i = 2; i >= 0; i--)
        {
            int ox = pad + i * Math.Max(1, size / 24);
            int oy = (size - fileH) / 2 - i * Math.Max(1, size / 24);
            DrawFile(g, ox, oy, fileW, fileH, srcColor);
        }

        // 오른쪽: 파일 1개 (황금색 = 변환 완료)
        var dstColor = DrawColor.FromArgb(255, 180, 30);
        int rx = pad + halfW + Math.Max(2, size / 10);
        int ry = (size - fileH) / 2;
        DrawFile(g, rx, ry, fileW, fileH, dstColor);

        // 화살표 (→)
        if (size >= 24)
        {
            int ax  = pad + fileW + Math.Max(1, size / 16);
            int ay  = size / 2;
            int aw  = halfW - fileW - Math.Max(2, size / 8);
            int ah  = Math.Max(2, size / 14);

            if (aw > 2)
            {
                using var brush = new SolidBrush(DrawColor.FromArgb(200, 200, 200));
                g.FillRectangle(brush, ax, ay - ah / 2, aw, ah);

                // 화살촉
                var pts = new[]
                {
                    new PointF(ax + aw, ay),
                    new PointF(ax + aw - size / 8f, ay - size / 8f),
                    new PointF(ax + aw - size / 8f, ay + size / 8f),
                };
                g.FillPolygon(brush, pts);
            }
        }

        return bmp;
    }

    private static void DrawFile(
        Graphics g, int x, int y, int w, int h, DrawColor color)
    {
        int fold = Math.Max(2, w / 4);

        // 파일 몸체
        using var body = new SolidBrush(DrawColor.FromArgb(color.R, color.G, color.B));
        g.FillRectangle(body, x, y + fold, w, h - fold);

        // 우상단 접힌 부분
        using var dark = new SolidBrush(DrawColor.FromArgb(
            (int)(color.R * 0.6), (int)(color.G * 0.6), (int)(color.B * 0.6)));
        var foldPts = new[]
        {
            new PointF(x + w - fold, y),
            new PointF(x + w,        y + fold),
            new PointF(x + w - fold, y + fold),
        };
        g.FillPolygon(dark, foldPts);

        // 상단 (폴드 제외)
        var topPts = new[]
        {
            new PointF(x,            y + fold),
            new PointF(x + w - fold, y + fold),
            new PointF(x + w - fold, y),
            new PointF(x,            y),
        };
        g.FillPolygon(body, topPts);
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
            w.Write(pngs[i].Length); w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);
        System.IO.File.WriteAllBytes(path, ms.ToArray());
    }
}
