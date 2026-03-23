# Disk.Lens 아이콘 생성 스크립트
# 하드 드라이브 + 돋보기 아이콘 (다크 테마 블루 계열)
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;

public static class IconGen {
    public static void DrawIcon(Graphics g, int sz) {
        g.SmoothingMode    = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        float s = sz / 256f;

        // ── 배경 원 (다크 블루) ──
        using (var bg = new SolidBrush(Color.FromArgb(30, 40, 70))) {
            g.FillEllipse(bg, 8*s, 8*s, 240*s, 240*s);
        }

        // ── 트리맵 블록들 (중앙) ──
        // 큰 블록 (좌상)
        DrawBlock(g, 28*s, 30*s, 90*s, 90*s, Color.FromArgb(66, 139, 202), s);
        // 우상 블록 (세로 2개)
        DrawBlock(g, 124*s, 30*s, 110*s, 42*s, Color.FromArgb(155, 89, 182), s);
        DrawBlock(g, 124*s, 78*s, 110*s, 42*s, Color.FromArgb(39, 174, 96), s);
        // 좌하 블록들
        DrawBlock(g, 28*s, 126*s, 44*s, 90*s, Color.FromArgb(243, 156, 18), s);
        DrawBlock(g, 78*s, 126*s, 44*s, 42*s, Color.FromArgb(231, 76, 60), s);
        DrawBlock(g, 78*s, 174*s, 44*s, 42*s, Color.FromArgb(241, 196, 15), s);
        // 우하 블록
        DrawBlock(g, 128*s, 126*s, 106*s, 90*s, Color.FromArgb(22, 160, 133), s);

        // ── 돋보기 (우하단 오버레이) ──
        float mx = 158*s, my = 154*s, mr = 46*s;
        // 렌즈 테두리
        using (var lp = new Pen(Color.White, 6*s) { LineJoin = LineJoin.Round }) {
            g.DrawEllipse(lp, mx - mr, my - mr, mr*2, mr*2);
        }
        // 렌즈 내부 (반투명)
        using (var lf = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) {
            g.FillEllipse(lf, mx - mr + 3*s, my - mr + 3*s, (mr-3)*2, (mr-3)*2);
        }
        // 손잡이
        using (var hp = new Pen(Color.White, 8*s) { StartCap = LineCap.Round, EndCap = LineCap.Round }) {
            g.DrawLine(hp, mx + mr * 0.68f, my + mr * 0.68f,
                           mx + mr * 1.30f, my + mr * 1.30f);
        }
    }

    private static void DrawBlock(Graphics g, float x, float y, float w, float h, Color c, float s) {
        float pad = 2 * s;
        var rect = new RectangleF(x + pad, y + pad, w - pad*2, h - pad*2);
        using (var br = new SolidBrush(c)) { g.FillRectangle(br, rect); }
        using (var p2 = new Pen(Color.FromArgb(40, 255, 255, 255), 1)) { g.DrawRectangle(p2, rect.X, rect.Y, rect.Width, rect.Height); }
    }

    public static void SaveIco(string path, int[] sizes) {
        var bitmaps = new List<Bitmap>();
        foreach (int sz in sizes) {
            var bmp = new Bitmap(sz, sz);
            using (var g = Graphics.FromImage(bmp)) DrawIcon(g, sz);
            bitmaps.Add(bmp);
        }
        using (var fs = new FileStream(path, FileMode.Create)) {
            int count = bitmaps.Count;
            // ICO 헤더
            fs.Write(new byte[]{0,0,1,0}, 0, 4);
            fs.Write(BitConverter.GetBytes((short)count), 0, 2);
            long dirPos = fs.Position;
            // 디렉터리 영역 예약
            fs.Write(new byte[16 * count], 0, 16 * count);
            var offsets = new int[count];
            var lengths = new int[count];
            for (int i = 0; i < count; i++) {
                offsets[i] = (int)fs.Position;
                using (var ms = new MemoryStream()) {
                    bitmaps[i].Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] data = ms.ToArray();
                    fs.Write(data, 0, data.Length);
                    lengths[i] = data.Length;
                }
            }
            // 디렉터리 기록
            fs.Seek(dirPos, SeekOrigin.Begin);
            for (int i = 0; i < count; i++) {
                int sz = bitmaps[i].Width;
                fs.WriteByte((byte)(sz >= 256 ? 0 : sz));
                fs.WriteByte((byte)(sz >= 256 ? 0 : sz));
                fs.WriteByte(0); fs.WriteByte(0);
                fs.Write(new byte[]{1,0,32,0}, 0, 4);
                fs.Write(BitConverter.GetBytes(lengths[i]), 0, 4);
                fs.Write(BitConverter.GetBytes(offsets[i]), 0, 4);
            }
        }
        foreach (var b in bitmaps) b.Dispose();
        Console.WriteLine("ICO saved: " + path);
    }
}
"@ -ReferencedAssemblies "System.Drawing"

$outPath = Join-Path $PSScriptRoot "app.ico"
[IconGen]::SaveIco($outPath, @(256, 48, 32, 16))
Write-Host "아이콘 생성 완료: $outPath"
