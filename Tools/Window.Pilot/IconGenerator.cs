using System.Drawing;
using System.Drawing.Imaging;

namespace WindowPilot;

/// <summary>
/// Window.Pilot 아이콘: 창(사각형) 위에 핀 고정 표시.
/// 어두운 배경 위 창 아이콘(흰색 테두리) + 우상단 파란 핀.
/// </summary>
public static class IconGenerator
{
    public static string IconFileName => "windowpilot.ico";

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
        g.Clear(Color.FromArgb(26, 26, 46));

        int pad   = Math.Max(2, size / 8);
        int ww    = size - pad * 2;
        int wh    = (int)(ww * 0.72f);
        int wx    = pad;
        int wy    = (size - wh) / 2;
        int thick = Math.Max(1, size / 18);

        // 창 배경 (어두운 청회색)
        using (var bg = new SolidBrush(Color.FromArgb(40, 44, 68)))
            g.FillRectangle(bg, wx, wy, ww, wh);

        // 창 타이틀바 (더 어두운 줄)
        int tbH = Math.Max(2, wh / 5);
        using (var tb = new SolidBrush(Color.FromArgb(55, 60, 90)))
            g.FillRectangle(tb, wx, wy, ww, tbH);

        // 창 테두리
        using (var pen = new Pen(Color.FromArgb(120, 130, 160), thick))
            g.DrawRectangle(pen, wx, wy, ww, wh);

        // 핀 (📌) 우상단 — 파란 원 안에 흰 점
        if (size >= 20)
        {
            int pr = Math.Max(3, size / 6);
            int px = wx + ww - pr / 2;
            int py = wy - pr / 2;

            using (var pin = new SolidBrush(Color.FromArgb(0, 150, 230)))
                g.FillEllipse(pin, px - pr, py - pr, pr * 2, pr * 2);
            using (var dot = new SolidBrush(Color.White))
            {
                int dr = Math.Max(1, pr / 3);
                g.FillEllipse(dot, px - dr, py - dr, dr * 2, dr * 2);
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
            w.Write((byte)(sz >= 256 ? 0 : sz)); w.Write((byte)(sz >= 256 ? 0 : sz));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(pngs[i].Length); w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);
        File.WriteAllBytes(path, ms.ToArray());
    }
}
