namespace Brush.Scale.Services;

public class ModelDownloadService
{
    static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
        DefaultRequestHeaders = { { "User-Agent", "Brush.Scale/1.0" } },
    };

    /// <summary>
    /// 모델을 다운로드하여 models 폴더에 저장합니다.
    /// progress: 0.0~1.0 (바이트 기준), -1 = 크기 미확인 중
    /// </summary>
    public static async Task DownloadAsync(
        ModelInfo info,
        IProgress<(double ratio, long downloaded, long total)>? progress = null,
        CancellationToken ct = default)
    {
        ModelManager.EnsureModelDir();
        var destPath = Path.Combine(ModelManager.ModelDir, info.FileName);
        var tmpPath  = destPath + ".download";

        try
        {
            using var resp = await _http.GetAsync(
                info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? info.ExpectedBytes;

            await using var src  = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst  = File.OpenWrite(tmpPath);

            var  buf        = new byte[81920];
            long downloaded = 0;
            int  read;

            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                downloaded += read;
                double ratio = total > 0 ? (double)downloaded / total : -1;
                progress?.Report((ratio, downloaded, total));
            }
        }
        catch
        {
            // 실패 시 임시 파일 제거
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            throw;
        }

        // 완료 시 최종 이름으로 이동
        if (File.Exists(destPath)) File.Delete(destPath);
        File.Move(tmpPath, destPath);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)  return "크기 확인 중...";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }
}
