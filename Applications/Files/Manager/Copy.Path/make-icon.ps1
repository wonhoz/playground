Add-Type -AssemblyName System.Drawing
$asmPath = [System.Drawing.Bitmap].Assembly.Location

$src = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

public static class IconGen {
    public static byte[] MakePng(int size) {
        Bitmap bmp = new Bitmap(size, size);
        Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        int r = (int)(size * 0.18);
        GraphicsPath path = new GraphicsPath();
        path.AddArc(0,        0,        r*2, r*2, 180, 90);
        path.AddArc(size-r*2, 0,        r*2, r*2, 270, 90);
        path.AddArc(size-r*2, size-r*2, r*2, r*2, 0,   90);
        path.AddArc(0,        size-r*2, r*2, r*2, 90,  90);
        path.CloseFigure();

        SolidBrush bg = new SolidBrush(Color.FromArgb(255, 12, 24, 56));
        g.FillPath(bg, path);
        bg.Dispose();

        using (var pen = new Pen(Color.FromArgb(180, 74, 158, 255), Math.Max(1f, size/32f)))
            g.DrawPath(pen, path);

        int fs = (int)(size * 0.40);
        Font font = new Font("Segoe UI Symbol", fs, FontStyle.Regular, GraphicsUnit.Pixel);
        SolidBrush tb = new SolidBrush(Color.FromArgb(255, 74, 158, 255));
        StringFormat sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment = StringAlignment.Center;
        g.DrawString("\U0001F4C2", font, tb, new RectangleF(0, -size/20f, size, size), sf);
        font.Dispose(); tb.Dispose();

        int arrowY = (int)(size * 0.72);
        int arrowX1 = (int)(size * 0.25);
        int arrowX2 = (int)(size * 0.75);
        using (var ap = new Pen(Color.FromArgb(255, 100, 200, 255), Math.Max(1.5f, size/24f))) {
            ap.EndCap = LineCap.ArrowAnchor;
            g.DrawLine(ap, arrowX1, arrowY, arrowX2, arrowY);
        }

        g.Dispose();
        MemoryStream ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        bmp.Dispose();
        return ms.ToArray();
    }

    public static void WriteIco(string icoPath, int[] sizes) {
        byte[][] pngs = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) pngs[i] = MakePng(sizes[i]);
        MemoryStream ms = new MemoryStream();
        BinaryWriter w = new BinaryWriter(ms);
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
$icoPath = Join-Path $outDir "app.ico"
[IconGen]::WriteIco($icoPath, @(16, 32, 48, 256))
Write-Host "app.ico done: $icoPath"
