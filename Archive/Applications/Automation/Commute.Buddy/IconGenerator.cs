using System.Drawing;
using System.Drawing.Drawing2D;

namespace CommuteBuddy;

public static class IconGenerator
{
    public const string IconFileName = "app.ico";

    public static void Generate(string resDir)
    {
        Directory.CreateDirectory(resDir);
        var icoPath = Path.Combine(resDir, IconFileName);
        if (File.Exists(icoPath)) return;

        using var ms = new MemoryStream();
        WriteIco(ms, [256, 48, 32, 16]);
        File.WriteAllBytes(icoPath, ms.ToArray());
    }

    private static void WriteIco(Stream output, int[] sizes)
    {
        var pngs = sizes.Select(RenderPng).ToArray();

        using var bw = new BinaryWriter(output, System.Text.Encoding.Default, leaveOpen: true);
        bw.Write((short)0);           // reserved
        bw.Write((short)1);           // type: ICO
        bw.Write((short)sizes.Length);

        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            int sz  = sizes[i];
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(pngs[i].Length);
            bw.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) bw.Write(png);
    }

    private static byte[] RenderPng(int size)
    {
        using var bmp = new Bitmap(size, size);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float s  = size;
        float cx = s / 2f;

        // 배경 원 (다크 네이비)
        using (var bg = new SolidBrush(Color.FromArgb(220, 13, 27, 62)))
            g.FillEllipse(bg, 1f, 1f, s - 2f, s - 2f);

        // WiFi 호 3개 (상단 60%)
        float wifiCx = cx;
        float wifiCy = s * 0.46f;
        float[] radii  = [s * 0.34f, s * 0.22f, s * 0.10f];
        float[] widths = [s * 0.055f, s * 0.050f, s * 0.045f];
        var wifiColor  = Color.FromArgb(0, 188, 212); // 청록

        for (int i = 0; i < radii.Length; i++)
        {
            float r = radii[i];
            float w = widths[i];
            using var pen = new Pen(wifiColor, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(pen, wifiCx - r, wifiCy - r, r * 2, r * 2, 205f, 130f);
        }

        // WiFi 점
        float dotR = s * 0.05f;
        float dotCy = wifiCy + radii[2] + dotR * 0.4f;
        using (var dotBrush = new SolidBrush(wifiColor))
            g.FillEllipse(dotBrush, wifiCx - dotR, dotCy, dotR * 2, dotR * 2);

        // 위치 핀 (하단 40%)
        float pinCx  = cx;
        float pinTop = s * 0.57f;
        float pinR   = s * 0.155f;
        float pinTip = pinTop + pinR * 2.3f;
        var pinColor = Color.FromArgb(255, 100, 100); // 코랄 레드

        using (var pinBrush = new SolidBrush(pinColor))
        {
            // 핀 머리 원
            g.FillEllipse(pinBrush, pinCx - pinR, pinTop, pinR * 2, pinR * 2);
            // 핀 꼬리 삼각형
            var pts = new PointF[]
            {
                new(pinCx - pinR * 0.65f, pinTop + pinR),
                new(pinCx + pinR * 0.65f, pinTop + pinR),
                new(pinCx, pinTip),
            };
            g.FillPolygon(pinBrush, pts);
        }

        // 핀 내부 흰 점
        float innerR = pinR * 0.42f;
        using (var innerBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            g.FillEllipse(innerBrush,
                pinCx - innerR, pinTop + (pinR - innerR),
                innerR * 2, innerR * 2);

        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
