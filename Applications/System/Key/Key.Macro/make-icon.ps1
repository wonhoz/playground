# Builds Resources/app.ico - a key cap that fires a mouse cursor (multi-size ICO)
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

    public static byte[] MakePng(int size) {
        Bitmap bmp = new Bitmap(size, size);
        Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float s = size;

        // vivid background
        GraphicsPath bg = Round(0, 0, s, s, s * 0.22f);
        LinearGradientBrush grad = new LinearGradientBrush(
            new PointF(0, 0), new PointF(s, s),
            Color.FromArgb(255, 45, 212, 231), Color.FromArgb(255, 109, 94, 246));
        g.FillPath(grad, bg);
        grad.Dispose();
        bg.Dispose();

        // key cap
        float kx = s * 0.13f, ky = s * 0.11f, kw = s * 0.50f, kh = s * 0.50f;
        GraphicsPath shadow = Round(kx + s*0.035f, ky + s*0.05f, kw, kh, s*0.11f);
        SolidBrush sh = new SolidBrush(Color.FromArgb(70, 15, 20, 60));
        g.FillPath(sh, shadow);
        sh.Dispose(); shadow.Dispose();

        GraphicsPath cap = Round(kx, ky, kw, kh, s * 0.11f);
        SolidBrush capBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
        g.FillPath(capBrush, cap);
        capBrush.Dispose();
        Pen capPen = new Pen(Color.FromArgb(255, 76, 86, 160), Math.Max(1f, s * 0.022f));
        g.DrawPath(capPen, cap);
        capPen.Dispose();
        cap.Dispose();

        // "1" printed on the key cap - the app is about "press this key"
        float fs = kh * 0.72f;
        Font font = new Font("Segoe UI", fs, FontStyle.Bold, GraphicsUnit.Pixel);
        SolidBrush txt = new SolidBrush(Color.FromArgb(255, 47, 56, 130));
        StringFormat sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment = StringAlignment.Center;
        g.DrawString("1", font, txt, new RectangleF(kx, ky - s*0.015f, kw, kh), sf);
        font.Dispose(); txt.Dispose(); sf.Dispose();

        // mouse cursor shooting out of the key
        PointF[] arrow = new PointF[] {
            new PointF(s*0.52f, s*0.46f),
            new PointF(s*0.52f, s*0.93f),
            new PointF(s*0.645f, s*0.805f),
            new PointF(s*0.725f, s*0.955f),
            new PointF(s*0.835f, s*0.895f),
            new PointF(s*0.755f, s*0.748f),
            new PointF(s*0.915f, s*0.723f)
        };
        GraphicsPath cur = new GraphicsPath();
        cur.AddPolygon(arrow);

        Matrix mv = new Matrix();
        mv.Translate(s*0.02f, s*0.03f);
        GraphicsPath curShadow = (GraphicsPath)cur.Clone();
        curShadow.Transform(mv);
        SolidBrush cs = new SolidBrush(Color.FromArgb(80, 15, 20, 60));
        g.FillPath(cs, curShadow);
        cs.Dispose(); curShadow.Dispose(); mv.Dispose();

        SolidBrush cf = new SolidBrush(Color.White);
        g.FillPath(cf, cur);
        cf.Dispose();
        Pen cp = new Pen(Color.FromArgb(255, 32, 40, 104), Math.Max(1f, s * 0.028f));
        cp.LineJoin = LineJoin.Round;
        g.DrawPath(cp, cur);
        cp.Dispose();
        cur.Dispose();

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
if ($args.Count -gt 0) { [IconGen]::WritePng($args[0], 256) }
Write-Host "app.ico written to $outDir"
