using NAudio.MediaFoundation;
using NAudio.Wave;

namespace EchoText.Services;

public static class ExportService
{
    /// <summary>WAV 파일을 MP3로 변환 (Windows Media Foundation 사용, 외부 DLL 불필요).</summary>
    public static async Task WavToMp3Async(string wavPath, string mp3Path, int bitRateKbps = 128)
    {
        await Task.Run(() =>
        {
            MediaFoundationApi.Startup();
            try
            {
                using var reader = new AudioFileReader(wavPath);
                MediaFoundationEncoder.EncodeToMp3(reader, mp3Path, bitRateKbps * 1000);
            }
            finally
            {
                MediaFoundationApi.Shutdown();
            }
        });
    }

    /// <summary>텍스트를 챕터 단위로 분할.</summary>
    public static List<(string title, string text)> SplitChapters(string text, string delimiter)
    {
        var sep = delimiter
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t");

        var parts = text.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<(string, string)>();

        for (int i = 0; i < parts.Length; i++)
        {
            var chunk = parts[i].Trim();
            if (string.IsNullOrEmpty(chunk)) continue;

            // 제목: 첫 줄 (최대 40자)
            var firstLine = chunk.Split('\n')[0].Trim();
            var title = firstLine.Length > 40 ? firstLine[..40] + "…" : firstLine;
            result.Add(($"챕터 {result.Count + 1}: {title}", chunk));
        }

        return result;
    }

    /// <summary>파일명에 사용 가능한 문자열로 정제.</summary>
    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
