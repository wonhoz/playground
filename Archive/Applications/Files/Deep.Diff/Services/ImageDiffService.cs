namespace DeepDiff.Services;

public class ImageDiffService
{
    public record ImageDiffResult(
        BitmapSource? LeftImage,
        BitmapSource? RightImage,
        BitmapSource? DiffImage,
        int TotalPixels,
        int DiffPixels,
        double DiffPercent,
        int LeftWidth, int LeftHeight,
        int RightWidth, int RightHeight);

    public ImageDiffResult Compare(string leftPath, string rightPath)
    {
        BitmapSource? left  = TryLoad(leftPath);
        BitmapSource? right = TryLoad(rightPath);

        if (left == null || right == null)
            return new(left, right, null, 0, 0, 0,
                (int)(left?.Width ?? 0), (int)(left?.Height ?? 0),
                (int)(right?.Width ?? 0), (int)(right?.Height ?? 0));

        int lw = (int)left.Width,  lh = (int)left.Height;
        int rw = (int)right.Width, rh = (int)right.Height;
        int w  = Math.Min(lw, rw), h = Math.Min(lh, rh);

        var lb = ToArgbArray(left,  w, h);
        var rb = ToArgbArray(right, w, h);

        int diffPx = 0;
        var db = new byte[w * h * 4];

        for (int i = 0; i < w * h; i++)
        {
            byte la = lb[i * 4 + 3], lr = lb[i * 4 + 2], lg = lb[i * 4 + 1], lbl = lb[i * 4];
            byte ra = rb[i * 4 + 3], rr = rb[i * 4 + 2], rg = rb[i * 4 + 1], rbl = rb[i * 4];

            int da = Math.Abs(la - ra), dr = Math.Abs(lr - rr);
            int dg = Math.Abs(lg - rg), db2 = Math.Abs(lbl - rbl);
            int delta = (da + dr + dg + db2) / 4;

            if (delta > 4)
            {
                diffPx++;
                db[i * 4 + 3] = 255;
                db[i * 4 + 2] = (byte)Math.Min(255, delta * 3);   // R channel
                db[i * 4 + 1] = 0;
                db[i * 4 + 0] = (byte)(delta / 2);                 // B channel
            }
            else
            {
                // 같은 픽셀은 어두운 회색
                byte v = (byte)(lr / 4 + 20);
                db[i * 4 + 3] = 160;
                db[i * 4 + 2] = v;
                db[i * 4 + 1] = v;
                db[i * 4 + 0] = v;
            }
        }

        var diffBmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, db, w * 4);
        diffBmp.Freeze();

        double pct = w * h > 0 ? diffPx * 100.0 / (w * h) : 0;
        return new(left, right, diffBmp, w * h, diffPx, pct, lw, lh, rw, rh);
    }

    private static BitmapSource? TryLoad(string path)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    private static byte[] ToArgbArray(BitmapSource src, int w, int h)
    {
        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        var scaled = new TransformedBitmap(conv, new System.Windows.Media.ScaleTransform(
            (double)w / src.PixelWidth, (double)h / src.PixelHeight));
        var pixels = new byte[w * h * 4];
        scaled.CopyPixels(pixels, w * 4, 0);
        return pixels;
    }
}
