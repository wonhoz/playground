using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DodgeBlitz;

/// <summary>
/// Dodge.Blitz 앱 아이콘 생성. 어두운 배경 + 시안 다이아몬드 + 4방향 빨간 총알.
/// </summary>
public static class IconGenerator
{
    public static void EnsureIcon()
    {
        string ico = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(ico))
        {
            try { ApplyWindowIcon(ico); } catch { }
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ico)!);
        var data = GenerateIcoBytes();
        File.WriteAllBytes(ico, data);
        try { ApplyWindowIcon(data); } catch { }
    }

    private static void ApplyWindowIcon(string path)
    {
        using var fs = File.OpenRead(path);
        var frame = BitmapFrame.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Application.Current.MainWindow.Icon = frame;
    }

    private static void ApplyWindowIcon(byte[] data)
    {
        using var ms = new MemoryStream(data);
        var frame = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Application.Current.MainWindow.Icon = frame;
    }

    private static byte[] GenerateIcoBytes()
    {
        int[] sizes   = [256, 48, 32, 16];
        var   bitmaps = sizes.Select(Draw).ToList();
        var   result  = WriteIco(bitmaps, sizes);
        foreach (var bmp in bitmaps) bmp.Dispose();
        return result;
    }

    private static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.FromArgb(5, 5, 15));

        float c  = size / 2f;
        float pr = size * 0.20f;   // 플레이어 다이아몬드 반경
        float br = size * 0.09f;   // 총알 반경
        float bd = size * 0.36f;   // 총알 중심까지 거리

        // ── 총알 글로우 (4방향) ──────────────────────
        var bulletGlowColor = System.Drawing.Color.FromArgb(120, 255, 55, 55);
        float gr = br * 2.0f;
        using var glowBrush = new SolidBrush(bulletGlowColor);
        g.FillEllipse(glowBrush, c - gr / 2, c - bd - gr / 2, gr, gr);
        g.FillEllipse(glowBrush, c - gr / 2, c + bd - gr / 2, gr, gr);
        g.FillEllipse(glowBrush, c - bd - gr / 2, c - gr / 2, gr, gr);
        g.FillEllipse(glowBrush, c + bd - gr / 2, c - gr / 2, gr, gr);

        // ── 총알 본체 (4방향) ─────────────────────────
        using var bulletBrush = new SolidBrush(System.Drawing.Color.FromArgb(255, 60, 60));
        g.FillEllipse(bulletBrush, c - br / 2, c - bd - br / 2, br, br);   // 위
        g.FillEllipse(bulletBrush, c - br / 2, c + bd - br / 2, br, br);   // 아래
        g.FillEllipse(bulletBrush, c - bd - br / 2, c - br / 2, br, br);   // 왼쪽
        g.FillEllipse(bulletBrush, c + bd - br / 2, c - br / 2, br, br);   // 오른쪽

        // ── 플레이어 다이아몬드 글로우 ─────────────────
        var playerGlowColor = System.Drawing.Color.FromArgb(100, 0, 255, 204);
        float pgr = pr * 1.5f;
        var pts = new PointF[] { new(c, c - pgr), new(c + pgr, c), new(c, c + pgr), new(c - pgr, c) };
        using var playerGlowBrush = new SolidBrush(playerGlowColor);
        g.FillPolygon(playerGlowBrush, pts);

        // ── 플레이어 다이아몬드 본체 ───────────────────
        var playerPts = new PointF[] { new(c, c - pr), new(c + pr, c), new(c, c + pr), new(c - pr, c) };
        using var playerBrush = new SolidBrush(System.Drawing.Color.FromArgb(0, 255, 204));
        g.FillPolygon(playerBrush, playerPts);

        using var playerPen = new Pen(System.Drawing.Color.FromArgb(0, 200, 160), Math.Max(1, size / 48f));
        g.DrawPolygon(playerPen, playerPts);

        return bmp;
    }

    private static byte[] WriteIco(List<Bitmap> bitmaps, int[] sizes)
    {
        var pngStreams = bitmaps.Select(bmp =>
        {
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms;
        }).ToList();

        using var output = new MemoryStream();
        using var bw     = new BinaryWriter(output, System.Text.Encoding.UTF8, true);

        int count  = bitmaps.Count;
        int offset = 6 + count * 16;

        // ICO 헤더
        bw.Write((short)0);       // reserved
        bw.Write((short)1);       // type: ICO
        bw.Write((short)count);

        // 디렉터리 항목
        for (int i = 0; i < count; i++)
        {
            int sz = sizes[i];
            bw.Write((byte)(sz == 256 ? 0 : sz));   // width (256→0)
            bw.Write((byte)(sz == 256 ? 0 : sz));   // height
            bw.Write((byte)0);                        // color count
            bw.Write((byte)0);                        // reserved
            bw.Write((short)1);                       // planes
            bw.Write((short)32);                      // bit count
            bw.Write((int)pngStreams[i].Length);
            bw.Write(offset);
            offset += (int)pngStreams[i].Length;
        }

        // PNG 데이터
        foreach (var ms in pngStreams)
        {
            ms.Position = 0;
            ms.CopyTo(output);
            ms.Dispose();
        }

        return output.ToArray();
    }
}
