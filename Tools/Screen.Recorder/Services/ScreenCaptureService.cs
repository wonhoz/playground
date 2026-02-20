using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ScreenRecorder.Services;

public sealed class ScreenCaptureService : IDisposable
{
    [DllImport("gdi32.dll")]
    private static extern int BitBlt(nint hdcDest, int xDest, int yDest, int width, int height,
        nint hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern nint CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, nint lpInitData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(nint hdc);

    private const int SRCCOPY = 0x00CC0020;

    private readonly Int32Rect _region;
    private readonly int _fps;
    private readonly string _outputPath;
    private readonly bool _isGif;
    private readonly List<string> _framePaths = [];
    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private bool _paused;
    private string? _tempDir;

    public ScreenCaptureService(Int32Rect region, int fps, string outputPath, bool isGif)
    {
        _region = region;
        _fps = fps;
        _outputPath = outputPath;
        _isGif = isGif;
    }

    public Task StartAsync()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ScreenRecorder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _cts = new CancellationTokenSource();
        _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
        return Task.CompletedTask;
    }

    private void CaptureLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(1000.0 / _fps);
        var frameIndex = 0;

        while (!ct.IsCancellationRequested)
        {
            if (_paused)
            {
                Thread.Sleep(50);
                continue;
            }

            var framePath = Path.Combine(_tempDir!, $"frame_{frameIndex:D6}.png");
            CaptureFrame(framePath);
            _framePaths.Add(framePath);
            frameIndex++;

            // 프레임 간격 유지
            Thread.Sleep(interval);
        }
    }

    private void CaptureFrame(string savePath)
    {
        using var bmp = new Bitmap(_region.Width, _region.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(_region.X, _region.Y, 0, 0,
            new System.Drawing.Size(_region.Width, _region.Height),
            CopyPixelOperation.SourceCopy);
        bmp.Save(savePath, ImageFormat.Png);
    }

    public void Pause() => _paused = true;

    public void Resume() => _paused = false;

    public async Task<string> StopAsync()
    {
        _cts?.Cancel();
        if (_captureTask is not null)
            await _captureTask;

        // 인코딩
        if (_isGif)
            await EncodeGifAsync();
        else
            await EncodeMp4Async();

        return _outputPath;
    }

    private async Task EncodeMp4Async()
    {
        await EncoderService.EncodeToMp4Async(_framePaths, _fps, _outputPath);
    }

    private async Task EncodeGifAsync()
    {
        await EncoderService.EncodeToGifAsync(_framePaths, _fps, _outputPath);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        // 임시 파일 정리
        if (_tempDir is not null && Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); }
            catch { /* 무시 */ }
        }
    }
}
