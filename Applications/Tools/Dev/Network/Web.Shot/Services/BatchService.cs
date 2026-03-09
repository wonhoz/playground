using WebShot.Models;

namespace WebShot.Services;

/// <summary>배치 캡처: TXT 파일에서 URL 목록 로드 → 순차 캡처</summary>
public class BatchService
{
    private readonly CaptureService  _capture;
    private readonly HistoryService  _history;
    private bool _cancelled;

    public event Action<int, int, string>? Progress;   // (current, total, url)
    public event Action<string, Exception>? ItemFailed; // (url, error)

    public BatchService(CaptureService capture, HistoryService history)
    {
        _capture = capture;
        _history = history;
    }

    /// <summary>TXT 파일에서 URL 목록을 읽어 순차 캡처. 반환값: 성공 건수</summary>
    public async Task<int> RunAsync(string urlListFile, CaptureSettings baseSettings)
    {
        _cancelled = false;
        var urls = ReadUrlsFromFile(urlListFile);
        int success = 0;

        for (int i = 0; i < urls.Count; i++)
        {
            if (_cancelled) break;

            var url = urls[i];
            Progress?.Invoke(i + 1, urls.Count, url);

            try
            {
                var settings = new CaptureSettings
                {
                    Url           = url,
                    ViewportWidth = baseSettings.ViewportWidth,
                    DelayMs       = baseSettings.DelayMs,
                    CapturePdf    = baseSettings.CapturePdf,
                    OutputFolder  = baseSettings.OutputFolder
                };

                string filePath;
                string fileType;
                if (settings.CapturePdf)
                {
                    filePath = await _capture.CaptureFullPagePdfAsync(settings);
                    fileType = "pdf";
                }
                else
                {
                    filePath = await _capture.CaptureFullPagePngAsync(settings);
                    fileType = "png";
                }

                _history.Add(new HistoryEntry
                {
                    Url           = url,
                    FilePath      = filePath,
                    FileType      = fileType,
                    ViewportWidth = settings.ViewportWidth
                });
                success++;
            }
            catch (Exception ex)
            {
                ItemFailed?.Invoke(url, ex);
            }
        }

        return success;
    }

    public void Cancel() => _cancelled = true;

    private static List<string> ReadUrlsFromFile(string path)
    {
        if (!File.Exists(path)) return new();
        return File.ReadAllLines(path)
                   .Select(l => l.Trim())
                   .Where(l => l.Length > 0 && !l.StartsWith('#'))
                   .ToList();
    }
}
