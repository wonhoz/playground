using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AudioCast.Services;

/// <summary>ffprobe로 읽은 원본 오디오 정보</summary>
public sealed record ProbeInfo(
    string FormatName,       // 예: "flac", "mov,mp4,m4a,3gp,3g2,mj2", "ogg"
    string Codec,            // 첫 오디오 스트림 codec_name
    int SampleRate,
    int BitsPerSample,       // 무손실 원본의 실제 비트 심도 (알 수 없으면 0)
    int BitRate,             // bps (알 수 없으면 0)
    double Duration,         // 초
    int? CoverStreamIndex,   // attached_pic 스트림 인덱스
    string? CoverCodec)
{
    static readonly HashSet<string> LosslessCodecs =
        ["flac", "alac", "ape", "wavpack", "tta", "mlp", "truehd", "wmalossless", "tak"];

    public bool IsLossless => Codec.StartsWith("pcm_") || LosslessCodecs.Contains(Codec);

    /// <summary>Ogg 계열은 태그가 컨테이너가 아닌 스트림에 저장됨</summary>
    public bool TagsOnStream => FormatName.Split(',').Contains("ogg");

    public int Kbps => BitRate / 1000;
}

public static class AudioProbe
{
    public static async Task<ProbeInfo> ProbeAsync(string ffprobePath, string file, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ffprobePath)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };
        foreach (var a in new[] { "-v", "error", "-print_format", "json", "-show_format", "-show_streams", file })
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("ffprobe를 실행할 수 없습니다.");
        var outTask = proc.StandardOutput.ReadToEndAsync(ct);
        var errTask = proc.StandardError.ReadToEndAsync(ct);
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            throw;
        }
        var json = await outTask;
        var err  = await errTask;
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(FirstLine(err) ?? "파일 정보를 읽을 수 없습니다.");

        return Parse(json);
    }

    static ProbeInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement? audio = null;
        int? coverIdx = null;
        string? coverCodec = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var s in streams.EnumerateArray())
            {
                var type = Str(s, "codec_type");
                if (type == "audio" && audio is null) audio = s;
                else if (type == "video" && coverIdx is null &&
                         s.TryGetProperty("disposition", out var disp) &&
                         disp.TryGetProperty("attached_pic", out var ap) && ap.GetInt32() == 1)
                {
                    coverIdx   = s.GetProperty("index").GetInt32();
                    coverCodec = Str(s, "codec_name");
                }
            }
        }
        if (audio is null) throw new InvalidOperationException("오디오 스트림이 없습니다.");

        var a      = audio.Value;
        var fmt    = root.TryGetProperty("format", out var f) ? f : default;
        var codec  = Str(a, "codec_name") ?? "";

        int bits = Int(a, "bits_per_raw_sample");
        if (bits == 0) bits = Int(a, "bits_per_sample");
        if (bits == 0 && Str(a, "sample_fmt") is string sf)
            bits = sf.StartsWith("s16") ? 16 : sf.StartsWith("s32") ? 24 : 0;

        int bitRate = Int(a, "bit_rate");
        if (bitRate == 0 && fmt.ValueKind == JsonValueKind.Object) bitRate = Int(fmt, "bit_rate");

        double duration = Dbl(a, "duration");
        if (duration <= 0 && fmt.ValueKind == JsonValueKind.Object) duration = Dbl(fmt, "duration");

        return new ProbeInfo(
            fmt.ValueKind == JsonValueKind.Object ? Str(fmt, "format_name") ?? "" : "",
            codec,
            Int(a, "sample_rate"),
            bits,
            bitRate,
            duration,
            coverIdx,
            coverCodec);
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static int Int(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.TryGetInt32(out var n) ? n : 0;
        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var p) ? p : 0;
    }

    static double Dbl(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String &&
        double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    internal static string? FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
