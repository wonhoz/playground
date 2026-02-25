namespace WordCloud.Services;

public static class MaskService
{
    private static readonly Random _rng = new();

    public static SKBitmap Generate(CloudShape shape, int w, int h)
    {
        if (shape == CloudShape.Random)
        {
            var shapes = Enum.GetValues<CloudShape>()
                             .Where(s => s != CloudShape.Random)
                             .ToArray();
            shape = shapes[_rng.Next(shapes.Length)];
        }

        var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);

        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White };

        switch (shape)
        {
            case CloudShape.Rectangle:
                canvas.DrawRect(0, 0, w, h, paint);
                break;

            case CloudShape.Circle:
                float cx = w / 2f, cy = h / 2f;
                float rx = w / 2f * 0.95f, ry = h / 2f * 0.95f;
                canvas.DrawOval(cx, cy, rx, ry, paint);
                break;

            case CloudShape.Heart:
                DrawHeart(canvas, paint, w, h);
                break;

            case CloudShape.Star:
                DrawStar(canvas, paint, w, h);
                break;

            case CloudShape.Diamond:
                DrawDiamond(canvas, paint, w, h);
                break;

            case CloudShape.Cloud:
                DrawCloud(canvas, paint, w, h);
                break;
        }

        return bmp;
    }

    private static void DrawHeart(SKCanvas canvas, SKPaint paint, int w, int h)
    {
        using var path = new SKPath();
        float scale = Math.Min(w, h) * 0.45f;
        float cx = w / 2f;
        float cy = h / 2f + scale * 0.15f;

        int steps = 200;
        bool first = true;
        for (int i = 0; i <= steps; i++)
        {
            double t = 2 * Math.PI * i / steps;
            float x = (float)(16 * Math.Pow(Math.Sin(t), 3));
            float y = (float)(13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t));
            float px = cx + x * scale / 17f;
            float py = cy - y * scale / 17f;
            if (first) { path.MoveTo(px, py); first = false; }
            else path.LineTo(px, py);
        }
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static void DrawStar(SKCanvas canvas, SKPaint paint, int w, int h)
    {
        using var path = new SKPath();
        float cx = w / 2f, cy = h / 2f;
        float outerR = Math.Min(w, h) * 0.47f;
        float innerR = outerR * 0.38f;
        int points = 5;

        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI / points * i - Math.PI / 2;
            float r = (i % 2 == 0) ? outerR : innerR;
            float px = cx + (float)(r * Math.Cos(angle));
            float py = cy + (float)(r * Math.Sin(angle));
            if (i == 0) path.MoveTo(px, py);
            else path.LineTo(px, py);
        }
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static void DrawDiamond(SKCanvas canvas, SKPaint paint, int w, int h)
    {
        using var path = new SKPath();
        float cx = w / 2f, cy = h / 2f;
        float rx = w * 0.47f, ry = h * 0.47f;
        path.MoveTo(cx, cy - ry);
        path.LineTo(cx + rx, cy);
        path.LineTo(cx, cy + ry);
        path.LineTo(cx - rx, cy);
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static void DrawCloud(SKCanvas canvas, SKPaint paint, int w, int h)
    {
        float s = Math.Min(w, h);
        float cx = w / 2f, cy = h * 0.52f;

        // 큰 중앙 원
        canvas.DrawCircle(cx, cy, s * 0.22f, paint);
        // 왼쪽 중간 원
        canvas.DrawCircle(cx - s * 0.20f, cy + s * 0.05f, s * 0.16f, paint);
        // 오른쪽 중간 원
        canvas.DrawCircle(cx + s * 0.20f, cy + s * 0.05f, s * 0.14f, paint);
        // 맨 왼쪽 작은 원
        canvas.DrawCircle(cx - s * 0.33f, cy + s * 0.10f, s * 0.12f, paint);
        // 바닥 직사각형으로 연결
        canvas.DrawRect(
            cx - s * 0.44f,
            cy + s * 0.10f,
            s * 0.88f,
            s * 0.18f,
            paint);
    }
}
