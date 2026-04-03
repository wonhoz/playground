using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Brush.Scale.Services;

public class UpscaleService : IDisposable
{
    const int DefaultTileSize    = 256;
    const int DefaultTileOverlap = 16;

    InferenceSession? _session;
    UpscaleModelType  _loadedModel   = UpscaleModelType.Bicubic;
    int               _tileSize      = DefaultTileSize;
    int               _tileOverlap   = DefaultTileOverlap;
    bool              _fixedTileSize = false;
    int               _borderOut     = 0;  // 모델 출력 경계 크롭 픽셀 수 (출력 좌표, 각 변 기준)
    int               _borderIn      = 0;  // = _borderOut / NativeScale (입력 좌표)

    // ── 세션 로드 ──────────────────────────────────────────────────────────
    public void LoadModel(UpscaleModelType modelType)
    {
        if (modelType == UpscaleModelType.Bicubic)
        {
            _session?.Dispose();
            _session       = null;
            _loadedModel   = UpscaleModelType.Bicubic;
            _tileSize      = DefaultTileSize;
            _tileOverlap   = DefaultTileOverlap;
            _fixedTileSize = false;
            _borderOut     = 0;
            _borderIn      = 0;
            return;
        }

        var path = ModelManager.GetModelPath(modelType);
        if (path is null || !File.Exists(path))
            throw new FileNotFoundException($"모델 파일을 찾을 수 없습니다: {modelType.ModelFileName()}\n경로: {ModelManager.ModelDir}");

        _session?.Dispose();

        var opts = new SessionOptions();
        opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        // CPU EP를 명시적으로 등록 → ORT가 DML/CUDA를 자동 등록하지 않음
        opts.AppendExecutionProvider_CPU(1);
        _session     = new InferenceSession(path, opts);
        _loadedModel = modelType;

        // 모델 입력 크기 감지 — 고정 크기(예: RealESRGAN 64×64) vs 동적 크기
        var inputDims = _session.InputMetadata.Values.First().Dimensions;
        if (inputDims.Length >= 4 && inputDims[2] > 0 && inputDims[3] > 0)
        {
            _fixedTileSize = true;
            _tileSize      = (int)inputDims[2];
            _tileOverlap   = Math.Max(2, _tileSize / 16);
            _borderOut     = 0;
            _borderIn      = 0;
        }
        else
        {
            _fixedTileSize = false;
            _tileSize      = DefaultTileSize;
            _tileOverlap   = DefaultTileOverlap;

            // 경계 크롭 자동 감지
            // 예: waifu2x-cunet → input 64 → output 56 → borderOut=(128-56)/2=36, borderIn=18
            _borderOut = 0;
            _borderIn  = 0;
            try
            {
                int ns     = modelType.NativeScale();
                const int testH = 64;
                var td = new DenseTensor<float>([1, 3, testH, testH]);
                var testInputs = new List<NamedOnnxValue> {
                    NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), td)
                };
                using var testResult = _session.Run(testInputs);
                var dims = testResult.First().AsTensor<float>().Dimensions;
                int outH = dims[dims.Length - 2];
                _borderOut = Math.Max(0, (ns * testH - outH) / 2);
                _borderIn  = ns > 0 ? _borderOut / ns : 0;
            }
            catch { /* 감지 실패 시 borderOut=0 유지 */ }
        }
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
            var session    = _session!;
            int nativeScale = _loadedModel.NativeScale();
            int outW = src.Width  * nativeScale;
            int outH = src.Height * nativeScale;
            var output = new SKBitmap(outW, outH, SKColorType.Rgb888x, SKAlphaType.Opaque);

            int tileSize   = _tileSize;
            int borderIn   = _borderIn;
            int borderOut  = _borderOut;
            bool fixedSize = _fixedTileSize;
            string inputName = session.InputMetadata.Keys.First();

            if (borderIn > 0)
            {
                // ── 경계 크롭 모델 (waifu2x-cunet 등) ───────────────────────────
                // 소스를 borderIn만큼 복제 패딩 후, 오버랩 없는 타일로 처리.
                // 타일 위치 sx(패딩 좌표) → 출력 위치 sx*nativeScale (오프셋 불필요).
                int step = Math.Max(1, tileSize - 2 * borderIn);
                using var padded = PadBitmap(src, borderIn);
                var tiles = BuildTilesPadded(padded.Width, padded.Height, tileSize, step);
                int total = tiles.Count;

                for (int ti = 0; ti < total; ti++)
                {
                    ct.ThrowIfCancellationRequested();
                    var (sx, sy, sw, sh) = tiles[ti];

                    using var tileBmp = new SKBitmap(sw, sh);
                    using (var c = new SKCanvas(tileBmp))
                        c.DrawBitmap(padded, new SKRect(sx, sy, sx + sw, sy + sh),
                                             new SKRect(0,  0,  sw,       sh));

                    var tensor = BitmapToTensor(tileBmp);
                    var inputs = new List<NamedOnnxValue> {
                        NamedOnnxValue.CreateFromTensor(inputName, tensor)
                    };
                    using var results = session.Run(inputs);
                    var outTensor = results.First().AsTensor<float>();

                    int oRank   = outTensor.Dimensions.Length;
                    int actualW = outTensor.Dimensions[oRank - 1];
                    int actualH = outTensor.Dimensions[oRank - 2];

                    int dx = sx * nativeScale;
                    int dy = sy * nativeScale;
                    int dw = Math.Min(actualW, outW - dx);
                    int dh = Math.Min(actualH, outH - dy);
                    if (dw <= 0 || dh <= 0) continue;

                    using var rawTile = TensorToBitmap(outTensor, dw, dh);
                    using var canvas  = new SKCanvas(output);
                    canvas.DrawBitmap(rawTile, new SKRect(0, 0, dw, dh),
                                               new SKRect(dx, dy, dx + dw, dy + dh));

                    progress?.Report((double)(ti + 1) / total);
                }
            }
            else
            {
                // ── 경계 없는 모델 (RealESRGAN 등) ──────────────────────────────
                // 기존 오버랩 타일링: 출력이 정확히 nativeScale배.
                int tileOverlap = _tileOverlap;
                var tiles = BuildTiles(src.Width, src.Height, tileSize, tileOverlap);
                int total = tiles.Count;

                for (int ti = 0; ti < total; ti++)
                {
                    ct.ThrowIfCancellationRequested();
                    var (sx, sy, sw, sh) = tiles[ti];

                    using var tileBmp = new SKBitmap(sw, sh);
                    using (var c = new SKCanvas(tileBmp))
                        c.DrawBitmap(src, new SKRect(sx, sy, sx + sw, sy + sh),
                                          new SKRect(0,  0,  sw,       sh));

                    var tensor = fixedSize
                        ? BitmapToTensorPadded(tileBmp, tileSize, tileSize)
                        : BitmapToTensor(tileBmp);

                    var inputs = new List<NamedOnnxValue> {
                        NamedOnnxValue.CreateFromTensor(inputName, tensor)
                    };
                    using var results   = session.Run(inputs);
                    var outTensor = results.First().AsTensor<float>();

                    int oRank   = outTensor.Dimensions.Length;
                    int actualW = outTensor.Dimensions[oRank - 1];
                    int actualH = outTensor.Dimensions[oRank - 2];

                    int tw = sw * nativeScale;
                    int th = sh * nativeScale;
                    int useW = Math.Min(tw, actualW);
                    int useH = Math.Min(th, actualH);
                    using var rawTile = TensorToBitmap(outTensor, useW, useH);

                    SKBitmap outTile;
                    if (useW == tw && useH == th)
                        outTile = rawTile.Copy();
                    else
                        outTile = rawTile.Resize(new SKImageInfo(tw, th),
                                                 new SKSamplingOptions(SKCubicResampler.Mitchell));

                    using (outTile)
                    {
                        int ox = sx > 0 ? tileOverlap * nativeScale : 0;
                        int oy = sy > 0 ? tileOverlap * nativeScale : 0;
                        int ow = tw - ox - (sx + sw < src.Width  ? tileOverlap * nativeScale : 0);
                        int oh = th - oy - (sy + sh < src.Height ? tileOverlap * nativeScale : 0);

                        int dx = sx * nativeScale + (sx > 0 ? tileOverlap * nativeScale : 0);
                        int dy = sy * nativeScale + (sy > 0 ? tileOverlap * nativeScale : 0);

                        using var canvas = new SKCanvas(output);
                        canvas.DrawBitmap(outTile, new SKRect(ox, oy, ox + ow, oy + oh),
                                                   new SKRect(dx, dy, dx + ow, dy + oh));
                    }

                    progress?.Report((double)(ti + 1) / total);
                }
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

    /// <summary>소스 이미지를 각 변에 pad픽셀만큼 복제 패딩한 새 비트맵 반환.</summary>
    static SKBitmap PadBitmap(SKBitmap src, int pad)
    {
        int srcW = src.Width, srcH = src.Height;
        int W = srcW + 2 * pad, H = srcH + 2 * pad;
        var bmp = new SKBitmap(W, H, src.ColorType, src.AlphaType);

        using (var canvas = new SKCanvas(bmp))
        {
            // 중앙에 원본 복사
            canvas.DrawBitmap(src, pad, pad);
            // 좌/우 엣지: 1픽셀 폭 스트립을 pad 폭으로 늘리기 (nearest-neighbor — 1px 소스라 보간 무관)
            canvas.DrawBitmap(src, new SKRect(0, 0, 1, srcH),         new SKRect(0,          pad, pad,          pad + srcH));
            canvas.DrawBitmap(src, new SKRect(srcW - 1, 0, srcW, srcH), new SKRect(pad + srcW, pad, W,            pad + srcH));
            // 상/하 엣지
            canvas.DrawBitmap(src, new SKRect(0, 0, srcW, 1),           new SKRect(pad, 0,          pad + srcW, pad));
            canvas.DrawBitmap(src, new SKRect(0, srcH - 1, srcW, srcH), new SKRect(pad, pad + srcH, pad + srcW, H));
            // 4 코너
            canvas.DrawBitmap(src, new SKRect(0,        0,        1,    1),    new SKRect(0,          0,          pad,   pad));
            canvas.DrawBitmap(src, new SKRect(srcW - 1, 0,        srcW, 1),    new SKRect(pad + srcW, 0,          W,     pad));
            canvas.DrawBitmap(src, new SKRect(0,        srcH - 1, 1,    srcH), new SKRect(0,          pad + srcH, pad,   H));
            canvas.DrawBitmap(src, new SKRect(srcW - 1, srcH - 1, srcW, srcH), new SKRect(pad + srcW, pad + srcH, W,     H));
        }
        return bmp;
    }

    /// <summary>경계 크롭 모델용 타일 목록. 모든 타일이 정확히 tileSize×tileSize.</summary>
    static List<(int x, int y, int w, int h)> BuildTilesPadded(int padW, int padH, int tileSize, int step)
    {
        var xs = BuildAxisPositions(padW, tileSize, step);
        var ys = BuildAxisPositions(padH, tileSize, step);
        var tiles = new List<(int, int, int, int)>();
        foreach (var y in ys)
        foreach (var x in xs)
            tiles.Add((x, y, Math.Min(tileSize, padW - x), Math.Min(tileSize, padH - y)));
        return tiles;
    }

    static List<int> BuildAxisPositions(int dim, int tileSize, int step)
    {
        var pos = new List<int>();
        if (dim <= tileSize)
        {
            pos.Add(0);
            return pos;
        }
        for (int x = 0; x + tileSize <= dim; x += step)
            pos.Add(x);
        if (pos.Count == 0 || pos[^1] + tileSize < dim)
            pos.Add(dim - tileSize);
        return pos;
    }

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

    static unsafe DenseTensor<float> BitmapToTensor(SKBitmap bmp)
    {
        int h = bmp.Height, w = bmp.Width;
        var t = new DenseTensor<float>([1, 3, h, w]);

        SKBitmap? converted = bmp.ColorType != SKColorType.Bgra8888
            ? bmp.Copy(SKColorType.Bgra8888)
            : null;
        var src = converted ?? bmp;
        try
        {
            byte* ptr      = (byte*)src.GetPixels().ToPointer();
            int   rowBytes = src.RowBytes;
            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + y * rowBytes;
                for (int x = 0; x < w; x++)
                {
                    byte* p = row + x * 4;  // BGRA: p[0]=B, p[1]=G, p[2]=R
                    t[0, 0, y, x] = p[2] / 255f;
                    t[0, 1, y, x] = p[1] / 255f;
                    t[0, 2, y, x] = p[0] / 255f;
                }
            }
        }
        finally { converted?.Dispose(); }
        return t;
    }

    // 고정 크기 모델용: 타일이 targetH×targetW보다 작으면 zero-padding
    static unsafe DenseTensor<float> BitmapToTensorPadded(SKBitmap bmp, int targetH, int targetW)
    {
        var t  = new DenseTensor<float>([1, 3, targetH, targetW]);
        int h  = Math.Min(bmp.Height, targetH);
        int w  = Math.Min(bmp.Width,  targetW);

        SKBitmap? converted = bmp.ColorType != SKColorType.Bgra8888
            ? bmp.Copy(SKColorType.Bgra8888)
            : null;
        var src = converted ?? bmp;
        try
        {
            byte* ptr      = (byte*)src.GetPixels().ToPointer();
            int   rowBytes = src.RowBytes;
            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + y * rowBytes;
                for (int x = 0; x < w; x++)
                {
                    byte* p = row + x * 4;
                    t[0, 0, y, x] = p[2] / 255f;
                    t[0, 1, y, x] = p[1] / 255f;
                    t[0, 2, y, x] = p[0] / 255f;
                }
            }
        }
        finally { converted?.Dispose(); }
        return t;
    }

    static unsafe SKBitmap TensorToBitmap(Tensor<float> t, int w, int h)
    {
        var bmp  = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque));
        byte* ptr = (byte*)bmp.GetPixels().ToPointer();
        int rowBytes = bmp.RowBytes;
        int rank  = t.Dimensions.Length;
        bool is4D = rank == 4;
        int nCh   = t.Dimensions[is4D ? 1 : 0];

        for (int y = 0; y < h; y++)
        {
            byte* row = ptr + y * rowBytes;
            for (int x = 0; x < w; x++)
            {
                float fR = is4D ? t[0, 0, y, x] : t[0, y, x];
                float fG = nCh >= 2 ? (is4D ? t[0, 1, y, x] : t[1, y, x]) : fR;
                float fB = nCh >= 3 ? (is4D ? t[0, 2, y, x] : t[2, y, x]) : fR;
                byte* p = row + x * 4;
                p[2] = Clamp(fR);  // R
                p[1] = Clamp(fG);  // G
                p[0] = Clamp(fB);  // B
                p[3] = 255;        // A
            }
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
