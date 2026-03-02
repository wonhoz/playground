# File.Unlocker 아이콘 생성 스크립트
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

public static class IconGen {
    static void FillRounded(Graphics g, Brush brush, int x, int y, int w, int h, int r) {
        using (var path = new GraphicsPath()) {
            int d = r * 2;
            if (d > w) d = w;
            if (d > h) d = h;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }
    }

    public static Bitmap Create(int size) {
        var bmp = new Bitmap(size, size);
        var g   = Graphics.FromImage(bmp);
        g.SmoothingMode   = SmoothingMode.AntiAlias;
        g.CompositingMode = CompositingMode.SourceOver;
        g.Clear(Color.Transparent);

        float s = size / 32.0f;

        // 배경 원 (짙은 파란색)
        using (var bg = new SolidBrush(Color.FromArgb(255, 18, 24, 48)))
            g.FillEllipse(bg, 0, 0, size - 1, size - 1);

        // 자물쇠 몸통
        int bx = (int)(8*s), by = (int)(16*s), bw = (int)(16*s), bh = (int)(12*s), br = (int)(3*s);
        using (var bodyBrush = new SolidBrush(Color.FromArgb(255, 75, 158, 255)))
            FillRounded(g, bodyBrush, bx, by, bw, bh, br);

        // 자물쇠 하이라이트
        using (var hl = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
            FillRounded(g, hl, bx, by, bw, (int)(5*s), br);

        // 열쇠구멍
        int kx = (int)(14*s), ky = (int)(19*s), kr = (int)(3*s);
        using (var kh = new SolidBrush(Color.FromArgb(255, 10, 12, 28))) {
            g.FillEllipse(kh, kx, ky, kr*2, kr*2);
            g.FillRectangle(kh, kx + (int)(0.7f*s), ky + kr + (int)(s), (int)(2.5f*s), (int)(3*s));
        }

        // 걸쇠 (열린 자물쇠)
        float penW = Math.Max(2f, 2.5f * s);
        using (var pen = new Pen(Color.FromArgb(255, 120, 190, 255), penW)) {
            pen.StartCap = LineCap.Round;
            pen.EndCap   = LineCap.Round;
            // 오른쪽 세로선 (몸통 연결)
            g.DrawLine(pen, 24*s, 12*s, 24*s, 16*s);
            // 상단 호 (180도)
            g.DrawArc(pen, 14*s, 5*s, 10*s, 11*s, 0, -180);
            // 왼쪽 세로선 (열린 상태: 위에 떠 있음)
            g.DrawLine(pen, 14*s, 5*s, 14*s, 9*s);
        }

        g.Dispose();
        return bmp;
    }

    public static void SaveIco(string path, int[] sizes) {
        var pngData = new List<byte[]>();
        foreach (var sz in sizes) {
            using (var bmp = Create(sz))
            using (var ms = new MemoryStream()) {
                bmp.Save(ms, ImageFormat.Png);
                pngData.Add(ms.ToArray());
            }
        }

        using (var fs = new FileStream(path, FileMode.Create))
        using (var w  = new BinaryWriter(fs)) {
            // ICO 헤더
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++) {
                int sz = sizes[i];
                w.Write((byte)(sz >= 256 ? 0 : sz));
                w.Write((byte)(sz >= 256 ? 0 : sz));
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((ushort)1);
                w.Write((ushort)32);
                w.Write((uint)pngData[i].Length);
                w.Write((uint)offset);
                offset += pngData[i].Length;
            }
            foreach (var d in pngData) w.Write(d);
        }
    }
}
"@ -ReferencedAssemblies "System.Drawing"

$icoPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "app.ico"
[IconGen]::SaveIco($icoPath, @(256, 48, 32, 16))
Write-Host "아이콘 생성 완료: $icoPath"
