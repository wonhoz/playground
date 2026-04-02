using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Brush.Scale.Services;

public class UpscaleService : IDisposable
{
    const int TileSize    = 256;
    const int TileOverlap = 16;

    InferenceSession? _session;
    UpscaleModelType  _loadedModel = UpscaleModelType.Bicubic;

    // ── 세션 로드 ──────────────────────────────────────────────────────────
    public void LoadModel(UpscaleModelType modelType)
    {
        if (modelType == UpscaleModelType.Bicubic)
        {
            _session?.Dispose();
            _session = null;
            _loadedModel = UpscaleModelType.Bicubic;
            return;
        }

        var path = ModelManager.GetModelPath(modelType);
        if (path is null || !File.Exists(path))
            throw new FileNotFoundException($"모델 파일을 찾을 수 없습니다: {modelType.ModelFileName()}\n경로: {ModelManager.ModelDir}");

        _session?.Dispose();

        var opts = new SessionOptions();
        try
        {
            opts.AppendExecutionProvider_DML(0);  // DirectML (GPU)
        }
        catch
        {
            // DirectML 미지원 시 CPU 폴백
        }
        opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        _session     = new InferenceSession(path, opts);
        _loadedModel = modelType;
    }

    // ── 단일 이미지 업스케일 ────────────────────────────────────────────────
    public async Task<SKBitmap> UpscaleAsync(
        SKBitmap src,
        UpscaleModelType modelType,
        int scaleFactor,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (modelType != _loadedModel)
            LoadModel(modelType);

        if (modelType == UpscaleModelType.Bicubic)
            return await BicubicAsync(src, scaleFactor, progress, ct);

        return await OnnxUpscaleAsync(src, scaleFactor, progress, ct);
    }

    // ── 배치 처리 ──────────────────────────────────────────────────────────
    public async Task BatchAsync(
        IReadOnlyList<UpscaleJob> jobs,
        IProgress<(int done, int total, string file)>? progress = null,
        CancellationToken ct = default)
    {
        if (_loadedModel == UpscaleModelType.Bicubic && jobs.Count > 0)
            LoadModel(jobs[0].Model);

        for (int i = 0; i < jobs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var job = jobs[i];
            progress?.Report((i, jobs.Count, Path.GetFileName(job.InputPath)));

            using var src = SKBitmap.Decode(job.InputPath)
                ?? throw new InvalidOperationException($"이미지 로드 실패: {job.InputPath}");

            var tileProgress = new Progress<double>(v =>
                progress?.Report((i, jobs.Count, Path.GetFileName(job.InputPath))));

            if (job.Model != _loadedModel) LoadModel(job.Model);

            using var result = await UpscaleAsync(src, job.Model, job.ScaleFactor, tileProgress, ct);
            SaveImage(result, job.OutputPath, job.Format, job.JpegQuality);
        }
        progress?.Report((jobs.Count, jobs.Count, "완료"));
    }

    // ── ONNX 타일 업스케일 ────────────────────────────────────────────────
    async Task<SKBitmap> OnnxUpscaleAsync(
        SKBitmap src,
        int scaleFactor,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        int nativeScale = _loadedModel.NativeScale();
        // scaleFactor보다 nativeScale이 작으면 다단 처리
        int passes = 1;
        if (scaleFactor > nativeScale && nativeScale > 0)
            passes = (int)Math.Ceiling(Math.Log(scaleFactor) / Math.Log(nativeScale));

        SKBitmap current = src.Copy();
        for (int p = 0; p < passes; p++)
        {
            ct.ThrowIfCancellationRequested();
            var passProgress = new Progress<double>(v =>
                progress?.Report((p + v) / passes));
            var next = await RunOnnxPassAsync(current, passProgress, ct);
            if (p > 0) current.Dispose();
            current = next;
        }

        // scaleFactor가 nativeScale의 배수가 아닐 경우 최종 리사이즈
        int targetW = src.Width  * scaleFactor;
        int targetH = src.Height * scaleFactor;
        if (current.Width != targetW || current.Height != targetH)
        {
            var resized = current.Resize(new SKImageInfo(targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell));
            current.Dispose();
            return resized;
        }
        return current;
    }

    async Task<SKBitmap> RunOnnxPassAsync(
        SKBitmap src,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var session = _session!;
            int nativeScale = _loadedModel.NativeScale();
            int outW = src.Width  * nativeScale;
            int outH = src.Height * nativeScale;
            var output = new SKBitmap(outW, outH, SKColorType.Rgb888x, SKAlphaType.Opaque);

            // 타일 목록 계산
            var tiles = BuildTiles(src.Width, src.Height, TileSize, TileOverlap);
            int total = tiles.Count;

            for (int ti = 0; ti < total; ti++)
            {
                ct.ThrowIfCancellationRequested();
                var (sx, sy, sw, sh) = tiles[ti];

                // 타일 크롭
                using var tileBmp = new SKBitmap(sw, sh);
                using (var c = new SKCanvas(tileBmp))
                    c.DrawBitmap(src, new SKRect(sx, sy, sx + sw, sy + sh),
                                      new SKRect(0,  0,  sw,       sh));

                // float 텐서 변환 (NCHW, [0,1])
                var tensor = BitmapToTensor(tileBmp);

                // 추론
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), tensor)
                };
                using var results = session.Run(inputs);
                var outTensor = results.First().AsTensor<float>();

                // 타일 결과 → SKBitmap
                int tw = sw * nativeScale;
                int th = sh * nativeScale;
                using var outTile = TensorToBitmap(outTensor, tw, th);

                // 오버랩 제거 후 복사
                int ox = sx > 0 ? TileOverlap * nativeScale : 0;
                int oy = sy > 0 ? TileOverlap * nativeScale : 0;
                int ow = tw - ox - (sx + sw < src.Width  ? TileOverlap * nativeScale : 0);
                int oh = th - oy - (sy + sh < src.Height ? TileOverlap * nativeScale : 0);

                int dx = sx * nativeScale + (sx > 0 ? TileOverlap * nativeScale : 0);
                int dy = sy * nativeScale + (sy > 0 ? TileOverlap * nativeScale : 0);

                using (var c = new SKCanvas(output))
                    c.DrawBitmap(outTile, new SKRect(ox, oy, ox + ow, oy + oh),
                                          new SKRect(dx, dy, dx + ow, dy + oh));

                progress?.Report((double)(ti + 1) / total);
            }
            return output;
        }, ct);
    }

    // ── Bicubic 폴백 ──────────────────────────────────────────────────────
    static Task<SKBitmap> BicubicAsync(
        SKBitmap src,
        int scaleFactor,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            int targetW = src.Width  * scaleFactor;
            int targetH = src.Height * scaleFactor;
            progress?.Report(0.5);
            var result = src.Resize(new SKImageInfo(targetW, targetH), new SKSamplingOptions(SKCubicResampler.Mitchell));
            progress?.Report(1.0);
            return result;
        }, ct);
    }

    // ── 이미지 저장 ───────────────────────────────────────────────────────
    public static void SaveImage(SKBitmap bmp, string path, OutputFormat fmt, int jpegQuality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image  = SKImage.FromBitmap(bmp);
        using var stream = File.OpenWrite(path);
        var encFmt = fmt switch
        {
            OutputFormat.Jpg  => SKEncodedImageFormat.Jpeg,
            OutputFormat.WebP => SKEncodedImageFormat.Webp,
            _                 => SKEncodedImageFormat.Png,
        };
        image.Encode(encFmt, jpegQuality).SaveTo(stream);
    }

    public static SKBitmap LoadBitmap(string path)
    {
        var bmp = SKBitmap.Decode(path);
        if (bmp is null) throw new InvalidOperationException($"이미지 로드 실패: {path}");
        return bmp;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────
    static List<(int x, int y, int w, int h)> BuildTiles(int imgW, int imgH, int tileSize, int overlap)
    {
        var tiles = new List<(int, int, int, int)>();
        int step = tileSize - overlap * 2;
        if (step <= 0) step = tileSize;

        for (int y = 0; y < imgH; y += step)
        for (int x = 0; x < imgW; x += step)
        {
            int sx = Math.Max(0, x - overlap);
            int sy = Math.Max(0, y - overlap);
            int ex = Math.Min(imgW, x + step + overlap);
            int ey = Math.Min(imgH, y + step + overlap);
            tiles.Add((sx, sy, ex - sx, ey - sy));
        }
        return tiles;
    }

    static DenseTensor<float> BitmapToTensor(SKBitmap bmp)
    {
        int h = bmp.Height, w = bmp.Width;
        var t = new DenseTensor<float>([1, 3, h, w]);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var c = bmp.GetPixel(x, y);
            t[0, 0, y, x] = c.Red   / 255f;
            t[0, 1, y, x] = c.Green / 255f;
            t[0, 2, y, x] = c.Blue  / 255f;
        }
        return t;
    }

    static SKBitmap TensorToBitmap(Tensor<float> t, int w, int h)
    {
        var bmp = new SKBitmap(w, h, SKColorType.Rgb888x, SKAlphaType.Opaque);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte r = Clamp(t[0, 0, y, x]);
            byte g = Clamp(t[0, 1, y, x]);
            byte b = Clamp(t[0, 2, y, x]);
            bmp.SetPixel(x, y, new SKColor(r, g, b));
        }
        return bmp;
    }

    static byte Clamp(float v) => (byte)Math.Clamp((int)(v * 255f + 0.5f), 0, 255);

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
