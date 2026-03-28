using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;
using WebShot.Models;

namespace WebShot.Services;

/// <summary>WebView2 CDP로 전체 페이지 스크린샷/PDF 캡처</summary>
public class CaptureService
{
    private readonly WebView2 _webView;

    public CaptureService(WebView2 webView)
    {
        _webView = webView;
    }

    /// <summary>전체 페이지 PNG 캡처 → 파일 저장. 반환값: 저장된 파일 경로</summary>
    public async Task<string> CaptureFullPagePngAsync(CaptureSettings settings)
    {
        Directory.CreateDirectory(settings.OutputFolder);

        await SetViewportWidthAsync(settings.ViewportWidth);
        await NavigateAndWaitAsync(settings.Url, settings.DelayMs);

        try
        {
            var cdpParams = new
            {
                format              = "png",
                captureBeyondViewport = true,
                fromSurface         = true
            };
            var paramsJson = JsonSerializer.Serialize(cdpParams);
            var resultJson = await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Page.captureScreenshot", paramsJson);

            using var result = JsonDocument.Parse(resultJson);
            var base64 = result.RootElement.GetProperty("data").GetString()
                         ?? throw new InvalidOperationException("캡처 데이터 없음");

            var bytes    = Convert.FromBase64String(base64);
            var fileName = BuildFileName(settings.Url, "png");
            var filePath = Path.Combine(settings.OutputFolder, fileName);
            await File.WriteAllBytesAsync(filePath, bytes);
            return filePath;
        }
        finally
        {
            // 캡처 후 viewport 고정 해제 → 미리보기가 창 크기에 맞게 복원됨
            await ClearViewportOverrideAsync();
        }
    }

    /// <summary>전체 페이지 PDF 저장. 반환값: 저장된 파일 경로</summary>
    public async Task<string> CaptureFullPagePdfAsync(CaptureSettings settings)
    {
        Directory.CreateDirectory(settings.OutputFolder);

        await SetViewportWidthAsync(settings.ViewportWidth);
        await NavigateAndWaitAsync(settings.Url, settings.DelayMs);

        var fileName = BuildFileName(settings.Url, "pdf");
        var filePath = Path.Combine(settings.OutputFolder, fileName);

        try
        {
            var pdfSettings = _webView.CoreWebView2.Environment.CreatePrintSettings();
            pdfSettings.ShouldPrintBackgrounds   = true;
            pdfSettings.ShouldPrintSelectionOnly = false;

            await _webView.CoreWebView2.PrintToPdfAsync(filePath, pdfSettings);
            return filePath;
        }
        finally
        {
            await ClearViewportOverrideAsync();
        }
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────

    private async Task ClearViewportOverrideAsync()
    {
        await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Emulation.clearDeviceMetricsOverride", "{}");
    }

    private async Task SetViewportWidthAsync(int width)
    {
        // CDP: Emulation.setDeviceMetricsOverride로 너비 지정, 높이는 임시로 크게
        var p = JsonSerializer.Serialize(new
        {
            width,
            height          = 900,
            deviceScaleFactor = 1,
            mobile          = false
        });
        await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
            "Emulation.setDeviceMetricsOverride", p);
    }

    private async Task NavigateAndWaitAsync(string url, int delayMs)
    {
        var tcs = new TaskCompletionSource<bool>();
        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            _webView.CoreWebView2.NavigationCompleted -= OnCompleted;
            if (!e.IsSuccess)
                tcs.TrySetException(new InvalidOperationException($"페이지 로드 실패 ({e.WebErrorStatus})"));
            else
                tcs.TrySetResult(true);
        }
        _webView.CoreWebView2.NavigationCompleted += OnCompleted;
        _webView.CoreWebView2.Navigate(url);

        // 30초 타임아웃
        var timeout = Task.Delay(30_000);
        if (await Task.WhenAny(tcs.Task, timeout) == timeout)
        {
            _webView.CoreWebView2.NavigationCompleted -= OnCompleted;
            throw new TimeoutException("페이지 로드 타임아웃 (30초 초과)");
        }
        await tcs.Task; // 예외 전파
        await Task.Delay(delayMs);
    }

    private static string BuildFileName(string url, string ext)
    {
        // URL에서 파일명 친화적 문자열 추출
        var host  = TryGetHost(url);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{stamp}_{host}.{ext}";
    }

    private static string TryGetHost(string url)
    {
        try
        {
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;
            return Uri.TryCreate(url, UriKind.Absolute, out var u)
                ? u.Host.Replace(".", "_")
                : "page";
        }
        catch { return "page"; }
    }
}
