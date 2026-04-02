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

    // ── 세션 로드 ──────────────────────────────────────────────────────────
    public void LoadModel(UpscaleModelType modelType)
    {
        if (modelType == UpscaleModelType.Bicubic)
        {
            _session?.Dispose();
            _session      = null;
            _loadedModel  = UpscaleModelType.Bicubic;
            _tileSize     = DefaultTileSize;
            _tileOverlap  = DefaultTileOverlap;
            _fixedTileSize = false;
            return;
        }

        var path = ModelManager.GetModelPath(modelType);
        if (path is null || !File.Exists(path))
            throw new FileNotFoundException($"모델 파일을 찾을 수 없습니다: {modelType.ModelFileName()}\n경로: {ModelManager.ModelDir}");

        _session?.Dispose();

        var opts = new SessionOptions();
        opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        // CPU EP를 명시적으로 등록 → ORT가 DML/CUDA를 자동 등록하지 않음
        // (EP 목록이 비어있으면 Windows에서 시스템 directml.dll을 자동 감지·등록)
        opts.AppendExecutionProvider_CPU(1);
        _session     = new InferenceSession(path, opts);
        _loadedModel = modelType;

        // 모델 입력 크기 감지 — 고정 크기(예: waifu2x 64×64) vs 동적 크기
        var inputDims = _session.InputMetadata.Values.First().Dimensions;
        if (inputDims.Length >= 4 && inputDims[2] > 0 && inputDims[3] > 0)
        {
            _fixedTileSize = true;
            _tileSize      = (int)inputDims[2];
            _tileOverlap   = Math.Max(2, _tileSize / 16);
        }
        else
        {
            _fixedTileSize = false;
            _tileSize      = DefaultTileSize;
            _tileOverlap   = DefaultTileOverlap;
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
            int tileSize    = _tileSize;
            int tileOverlap = _tileOverlap;
            bool fixedSize  = _fixedTileSize;
            var tiles = BuildTiles(src.Width, src.Height, tileSize, tileOverlap);
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
                // 고정 크기 모델: 타일이 기대 크기보다 작을 경우 zero-padding
                var tensor = fixedSize
                    ? BitmapToTensorPadded(tileBmp, tileSize, tileSize)
                    : BitmapToTensor(tileBmp);

                // 추론
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), tensor)
                };
                using var results = session.Run(inputs);
                var outTensor = results.First().AsTensor<float>();

                // 타일 결과 → SKBitmap
                // 출력 텐서 실제 차원 (3D [C,H,W] 또는 4D [N,C,H,W])
                int oRank   = outTensor.Dimensions.Length;
                int actualW = outTensor.Dimensions[oRank - 1];
                int actualH = outTensor.Dimensions[oRank - 2];

                int tw = sw * nativeScale;
                int th = sh * nativeScale;
                // 패딩된 입력에서 나온 출력 → 실제 타일 크기로 크롭
                int useW = Math.Min(tw, actualW);
                int useH = Math.Min(th, actualH);
                using var rawTile = TensorToBitmap(outTensor, useW, useH);

                // 출력이 기대 크기와 다르면 리사이즈 (nativeScale 불일치 보정)
                SKBitmap outTile;
                if (useW == tw && useH == th)
                    outTile = rawTile.Copy();
                else
                    outTile = rawTile.Resize(new SKImageInfo(tw, th),
                                             new SKSamplingOptions(SKCubicResampler.Mitchell));

                using (outTile)
                {
                    // 오버랩 제거 후 복사
                    int ox = sx > 0 ? tileOverlap * nativeScale : 0;
                    int oy = sy > 0 ? tileOverlap * nativeScale : 0;
                    int ow = tw - ox - (sx + sw < src.Width  ? tileOverlap * nativeScale : 0);
                    int oh = th - oy - (sy + sh < src.Height ? tileOverlap * nativeScale : 0);

                    int dx = sx * nativeScale + (sx > 0 ? tileOverlap * nativeScale : 0);
                    int dy = sy * nativeScale + (sy > 0 ? tileOverlap * nativeScale : 0);

                    using var c = new SKCanvas(output);
                    c.DrawBitmap(outTile, new SKRect(ox, oy, ox + ow, oy + oh),
                                          new SKRect(dx, dy, dx + ow, dy + oh));
                }

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

    // 고정 크기 모델용: 타일이 targetH×targetW보다 작으면 zero-padding
    static DenseTensor<float> BitmapToTensorPadded(SKBitmap bmp, int targetH, int targetW)
    {
        var t = new DenseTensor<float>([1, 3, targetH, targetW]);
        int h = Math.Min(bmp.Height, targetH);
        int w = Math.Min(bmp.Width,  targetW);
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
        var bmp  = new SKBitmap(w, h, SKColorType.Rgb888x, SKAlphaType.Opaque);
        int rank = t.Dimensions.Length;
        bool is4D = rank == 4;
        // 채널 수 (4D: dim[1], 3D: dim[0])
        int nCh = t.Dimensions[is4D ? 1 : 0];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float fR = is4D ? t[0, 0, y, x] : t[0, y, x];
            float fG = nCh >= 2 ? (is4D ? t[0, 1, y, x] : t[1, y, x]) : fR;
            float fB = nCh >= 3 ? (is4D ? t[0, 2, y, x] : t[2, y, x]) : fR;
            bmp.SetPixel(x, y, new SKColor(Clamp(fR), Clamp(fG), Clamp(fB)));
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
