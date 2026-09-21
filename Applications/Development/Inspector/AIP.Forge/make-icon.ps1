# Builds Resources/app.ico - an installer package box labeled "AIP" with a green build (play) badge
Add-Type -AssemblyName System.Drawing
$asmPath = [System.Drawing.Bitmap].Assembly.Location

$src = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

public static class IconGen {
    static GraphicsPath Round(float x, float y, float w, float h, float r) {
        GraphicsPath p = new GraphicsPath();
        p.AddArc(x,         y,         r*2, r*2, 180, 90);
        p.AddArc(x + w-r*2, y,         r*2, r*2, 270, 90);
        p.AddArc(x + w-r*2, y + h-r*2, r*2, r*2, 0,   90);
        p.AddArc(x,         y + h-r*2, r*2, r*2, 90,  90);
        p.CloseFigure();
        return p;
    }

    static PointF P(float s, float x, float y) { return new PointF(s * x, s * y); }

    static void Face(Graphics g, PointF[] pts, Color fill, Pen pen) {
        using (SolidBrush b = new SolidBrush(fill)) g.FillPolygon(b, pts);
        g.DrawPolygon(pen, pts);
    }

    public static byte[] MakePng(int size) {
        Bitmap bmp = new Bitmap(size, size);
        Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float s = size;
        bool tiny = size <= 16;

        // warm amber background
        using (GraphicsPath bg = Round(0, 0, s, s, s * 0.22f))
        using (LinearGradientBrush grad = new LinearGradientBrush(new PointF(0, 0), new PointF(s, s),
                   Color.FromArgb(255, 255, 196, 72), Color.FromArgb(255, 255, 112, 48)))
            g.FillPath(grad, bg);

        // soft shadow under the box
        using (SolidBrush sh = new SolidBrush(Color.FromArgb(60, 90, 30, 0)))
            g.FillEllipse(sh, s * 0.12f, s * 0.80f, s * 0.72f, s * 0.12f);

        Pen outline = new Pen(Color.FromArgb(255, 92, 44, 8), Math.Max(1f, s * (tiny ? 0.07f : 0.03f)));
        outline.LineJoin = LineJoin.Round;

        // isometric package box: top, right side, front
        PointF[] top   = { P(s,0.12f,0.40f), P(s,0.36f,0.26f), P(s,0.84f,0.26f), P(s,0.60f,0.40f) };
        PointF[] side  = { P(s,0.60f,0.40f), P(s,0.84f,0.26f), P(s,0.84f,0.72f), P(s,0.60f,0.86f) };
        PointF[] front = { P(s,0.12f,0.40f), P(s,0.60f,0.40f), P(s,0.60f,0.86f), P(s,0.12f,0.86f) };
        Face(g, top,   Color.FromArgb(255, 255, 246, 228), outline);
        Face(g, side,  Color.FromArgb(255, 246, 206, 150), outline);
        Face(g, front, Color.FromArgb(255, 255, 255, 255), outline);

        // packing tape across the lid and down the front
        using (SolidBrush tape = new SolidBrush(Color.FromArgb(255, 214, 142, 58))) {
            g.FillPolygon(tape, new PointF[] { P(s,0.31f,0.40f), P(s,0.41f,0.40f), P(s,0.65f,0.26f), P(s,0.55f,0.26f) });
            if (!tiny) g.FillRectangle(tape, s * 0.31f, s * 0.40f + outline.Width / 2, s * 0.10f, s * 0.08f);
        }

        // "AIP" printed on the front face
        if (!tiny) {
            using (Font font = new Font("Segoe UI", s * 0.17f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush txt = new SolidBrush(Color.FromArgb(255, 92, 44, 8)))
            using (StringFormat sf = new StringFormat()) {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("AIP", font, txt, new RectangleF(s * 0.12f, s * 0.50f, s * 0.48f, s * 0.34f), sf);
            }
        }

        // green build badge with a play triangle
        float r = s * (tiny ? 0.22f : 0.17f);
        float cx = s * 0.80f, cy = s * 0.80f;
        using (SolidBrush badge = new SolidBrush(Color.FromArgb(255, 30, 163, 90)))
            g.FillEllipse(badge, cx - r, cy - r, r * 2, r * 2);
        using (Pen ring = new Pen(Color.White, Math.Max(1f, s * 0.025f)))
            g.DrawEllipse(ring, cx - r, cy - r, r * 2, r * 2);
        using (SolidBrush play = new SolidBrush(Color.White))
            g.FillPolygon(play, new PointF[] {
                new PointF(cx - r * 0.35f, cy - r * 0.50f),
                new PointF(cx - r * 0.35f, cy + r * 0.50f),
                new PointF(cx + r * 0.55f, cy) });

        outline.Dispose();
        g.Dispose();
        MemoryStream ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        bmp.Dispose();
        return ms.ToArray();
    }

    public static void WritePng(string path, int size) {
        File.WriteAllBytes(path, MakePng(size));
    }

    public static void WriteIco(string icoPath, int[] sizes) {
        byte[][] pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) pngs[i] = MakePng(sizes[i]);

        MemoryStream ms = new MemoryStream();
        BinaryWriter w  = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++) {
            byte b = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i];
            w.Write(b); w.Write(b); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write((uint)pngs[i].Length); w.Write((uint)offset);
            offset += pngs[i].Length;
        }
        foreach (byte[] png in pngs) w.Write(png);
        File.WriteAllBytes(icoPath, ms.ToArray());
    }
}
'@

Add-Type -TypeDefinition $src -ReferencedAssemblies $asmPath

$outDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

[IconGen]::WriteIco((Join-Path $outDir "app.ico"), @(16, 32, 48, 256))
if ($args.Count -gt 0) {
    [IconGen]::WritePng($args[0], 256)
    if ($args.Count -gt 1) { [IconGen]::WritePng($args[1], 32) }
}
Write-Host "app.ico written to $outDir"
