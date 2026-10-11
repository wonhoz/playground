using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AudioCast.Models;

namespace AudioCast.Services;

public sealed record ConvertOptions(
    FormatSpec Format,
    QualityOption Quality,
    bool SmartQuality,       // 같은 코덱·낮은 비트레이트면 스트림 복사, 손실 원본은 비트레이트 상한 제한
    bool KeepTags,           // 태그·앨범아트 유지
    bool Overwrite,
    string? OutputDir,       // null = 원본과 같은 폴더
    bool KeepStructure,      // 폴더 드롭 시 하위 폴더 구조 유지
    int Parallelism);

public sealed record ConversionResult(int Success, int Copied, int Skipped, int Failed, bool Cancelled)
{
    public int Total => Success + Copied + Skipped + Failed;
}

public static class AudioConverter
{
    // ─── 입력 필터 ───────────────────────────────────────────────────────────
    public static readonly (string Tag, string Label, string[] Exts)[] InputFilters =
    [
        ("All",   "자동 감지 (전체)",       []),
        ("MP3",   "MP3",                   [".mp3"]),
        ("FLAC",  "FLAC",                  [".flac"]),
        ("WAV",   "WAV",                   [".wav"]),
        ("M4A",   "M4A · AAC · ALAC",      [".m4a", ".aac", ".mp4a"]),
        ("OGG",   "OGG · OPUS",            [".ogg", ".oga", ".opus"]),
        ("WMA",   "WMA",                   [".wma"]),
        ("AIFF",  "AIFF",                  [".aiff", ".aif", ".aifc"]),
        ("APE",   "APE · WV · TTA",        [".ape", ".wv", ".tta"]),
        ("VIDEO", "영상 (오디오 추출)",      [".mp4", ".mkv", ".webm", ".mov", ".avi", ".m4v"]),
    ];

    static readonly string[] AudioExts =
        InputFilters.Where(f => f.Tag != "VIDEO").SelectMany(f => f.Exts)
                    .Concat([".mka", ".ac3", ".dts", ".amr", ".caf", ".dsf"]).ToArray();

    static string[] ExtsFor(string tag) =>
        tag == "All" ? AudioExts : InputFilters.FirstOrDefault(f => f.Tag == tag).Exts ?? AudioExts;

    static bool IsSupported(string path, string[] exts) =>
        exts.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static bool HasSupportedFiles(string[] paths, string filterTag)
    {
        var exts = ExtsFor(filterTag);
        return paths.Any(p => Directory.Exists(p) || IsSupported(p, exts));
    }

    /// <summary>드롭된 파일·폴더에서 대상 파일 수집 (폴더는 하위까지, 상대 폴더 경로 포함)</summary>
    public static List<FileJob> CollectFiles(string[] paths, string filterTag)
    {
        var exts = ExtsFor(filterTag);
        var jobs = new List<FileJob>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                var root     = Path.TrimEndingDirectorySeparator(p);
                var rootName = Path.GetFileName(root);
                var parent   = Path.GetDirectoryName(root) ?? root;
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (var f in files.Where(f => IsSupported(f, exts)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    if (!seen.Add(f)) continue;
                    // 드롭한 폴더 이름부터 포함 (예: Album\CD1)
                    var rel = Path.GetRelativePath(parent, Path.GetDirectoryName(f)!);
                    jobs.Add(new FileJob(f, string.IsNullOrEmpty(rootName) ? "" : rel));
                }
            }
            else if (File.Exists(p) && IsSupported(p, exts) && seen.Add(p))
            {
                jobs.Add(new FileJob(p, ""));
            }
        }
        return jobs;
    }

    // ─── 일괄 변환 ───────────────────────────────────────────────────────────
    public static async Task<ConversionResult> ConvertAsync(
        IReadOnlyList<FileJob> jobs, ConvertOptions opt, string ffmpegPath, CancellationToken ct)
    {
        var ffprobe  = FfmpegLocator.FindProbe(ffmpegPath);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 출력 경로는 실행 전에 순서대로 확정 (같은 이름 충돌 방지)
        foreach (var job in jobs)
            job.OutputPath = ResolveOutputPath(job, opt, reserved);

        try
        {
            await Parallel.ForEachAsync(jobs,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, opt.Parallelism), CancellationToken = ct },
                async (job, token) => await ConvertOneAsync(job, opt, ffmpegPath, ffprobe, token));
        }
        catch (OperationCanceledException) { }

        bool cancelled = ct.IsCancellationRequested;
        if (cancelled)
            foreach (var job in jobs.Where(j => j.Status is JobStatus.Pending or JobStatus.Running))
                job.Status = JobStatus.Cancelled;

        return new ConversionResult(
            jobs.Count(j => j.Status == JobStatus.Done),
            jobs.Count(j => j.Status == JobStatus.Copied),
            jobs.Count(j => j.Status == JobStatus.Skipped),
            jobs.Count(j => j.Status == JobStatus.Failed),
            cancelled);
    }

    static string ResolveOutputPath(FileJob job, ConvertOptions opt, HashSet<string> reserved)
    {
        var srcDir = Path.GetDirectoryName(job.SourcePath)!;
        var dir = opt.OutputDir is null
            ? srcDir
            : opt.KeepStructure && job.RelativeDir.Length > 0
                ? Path.Combine(opt.OutputDir, job.RelativeDir)
                : opt.OutputDir;

        var name = Path.GetFileNameWithoutExtension(job.SourcePath);
        var path = Path.Combine(dir, name + opt.Format.Extension);

        // 원본 자신·이번 일괄 작업의 다른 출력·(덮어쓰기 꺼짐 시) 기존 파일과 겹치면 번호 부여
        bool Taken(string p) =>
            string.Equals(Path.GetFullPath(p), Path.GetFullPath(job.SourcePath), StringComparison.OrdinalIgnoreCase) ||
            reserved.Contains(p) ||
            (!opt.Overwrite && File.Exists(p));

        for (int i = 1; Taken(path); i++)
            path = Path.Combine(dir, $"{name} ({i}){opt.Format.Extension}");

        reserved.Add(path);
        return path;
    }

    static async Task ConvertOneAsync(FileJob job, ConvertOptions opt, string ffmpeg, string? ffprobe, CancellationToken ct)
    {
        job.Status   = JobStatus.Running;
        job.Progress = 0;
        var output   = job.OutputPath!;
        var temp     = output + ".partial";

        try
        {
            ProbeInfo? info = ffprobe is null ? null : await AudioProbe.ProbeAsync(ffprobe, job.SourcePath, ct);
            var (args, note, copied) = BuildArgs(job.SourcePath, temp, info, opt);
            job.Detail = note;

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await RunFfmpegAsync(ffmpeg, args, info?.Duration ?? 0, p => job.Progress = p, ct);

            File.Move(temp, output, overwrite: true);
            job.Progress = 1;
            job.Status   = copied ? JobStatus.Copied : JobStatus.Done;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.Detail = "취소됨";
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Detail = ex.Message;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    // ─── ffmpeg 인자 구성 ─────────────────────────────────────────────────────
    internal static (List<string> Args, string Note, bool Copied) BuildArgs(
        string input, string output, ProbeInfo? info, ConvertOptions opt)
    {
        var fmt  = opt.Format;
        var q    = opt.Quality;
        var args = new List<string> { "-hide_banner", "-nostdin", "-v", "error", "-y", "-i", input, "-map", "0:a:0" };
        var notes = new List<string>();

        // 앨범아트 (attached_pic 스트림만 — 영상 트랙은 제외)
        bool coverOk = opt.KeepTags && fmt.SupportsCover && info?.CoverStreamIndex is int &&
                       (fmt.Format == OutputFormat.Mp3 || info.CoverCodec is "mjpeg" or "png");
        if (coverOk) args.AddRange(["-map", $"0:{info!.CoverStreamIndex}"]);

        // 태그
        if (!opt.KeepTags)            args.AddRange(["-map_metadata", "-1"]);
        else if (info?.TagsOnStream == true) args.AddRange(["-map_metadata", "0:s:a:0"]);
        else                          args.AddRange(["-map_metadata", "0"]);

        // 스트림 복사 판정 — 재인코딩 없이 원본 그대로 (완전 무손실)
        bool copy = opt.SmartQuality && info is not null && CanStreamCopy(info, fmt, q);
        bool lossySource = info is { IsLossless: false };

        if (copy)
        {
            args.AddRange(["-c:a", "copy"]);
            notes.Add("재인코딩 없이 스트림 복사 (무손실)");
        }
        else if (fmt.Lossless)
        {
            AddLosslessArgs(args, fmt, q, info);
            if (lossySource) notes.Add("손실 원본 → 무손실 포맷 (음질 향상 없음)");
            else if (info is not null)
                notes.Add(q.CdQuality ? "16bit · 44.1kHz" : $"{(info.BitsPerSample > 16 ? 24 : 16)}bit · {info.SampleRate / 1000.0:0.#}kHz");
        }
        else
        {
            int kbps = q.Kbps;
            // 음질 보호: 손실 원본보다 높은 비트레이트로 올려봐야 용량만 커짐 → 원본 이상인 가장 가까운 단계로 제한
            if (opt.SmartQuality && lossySource && info!.Kbps > 0 && kbps > info.Kbps)
            {
                var capped = fmt.Qualities.Where(o => o.Kbps >= info.Kbps).Select(o => o.Kbps).DefaultIfEmpty(kbps).Min();
                if (capped < kbps)
                {
                    notes.Add($"원본 {info.Kbps}kbps → {capped}kbps로 제한");
                    kbps = capped;
                }
            }
            AddLossyArgs(args, fmt, q, kbps);
            // 손실 코덱은 48kHz 초과 샘플레이트에서 비트만 낭비 → 48kHz로 (MP3·OPUS는 인코더가 자동 처리)
            if (info is { SampleRate: > 48000 } && fmt.Format is OutputFormat.M4aAac or OutputFormat.Ogg)
                args.AddRange(["-ar", "48000"]);
            if (lossySource && notes.Count == 0) notes.Add($"손실 → 손실 재인코딩 ({Describe(info!)} 원본)");
        }

        if (coverOk) args.AddRange(["-c:v", "copy", "-disposition:v:0", "attached_pic"]);

        switch (fmt.Format)
        {
            case OutputFormat.Mp3:
                args.AddRange(["-id3v2_version", "3", "-write_id3v1", "1"]);
                break;
            case OutputFormat.M4aAac:
            case OutputFormat.M4aAlac:
                args.AddRange(["-movflags", "+faststart"]);
                break;
        }

        args.AddRange(["-progress", "pipe:1", "-nostats", "-f", fmt.Muxer, output]);

        if (info is null) notes.Add("ffprobe 없음 — 기본 설정으로 변환");
        else if (notes.Count == 0) notes.Add($"{Describe(info)} 원본");
        return (args, string.Join(" · ", notes), copy);
    }

    static string Describe(ProbeInfo info)
    {
        var codec = info.Codec.StartsWith("pcm_") ? "PCM" : info.Codec.ToUpperInvariant();
        return info.IsLossless
            ? $"{codec} {(info.BitsPerSample > 0 ? $"{info.BitsPerSample}bit · " : "")}{info.SampleRate / 1000.0:0.#}kHz"
            : $"{codec} {info.Kbps}kbps";
    }

    static bool CanStreamCopy(ProbeInfo info, FormatSpec fmt, QualityOption q)
    {
        if (fmt.Lossless)
        {
            if (q.CdQuality) return false;
            return fmt.Format switch
            {
                OutputFormat.Flac    => info.Codec == "flac",
                OutputFormat.M4aAlac => info.Codec == "alac",
                // WAV는 리틀엔디언 PCM만 담을 수 있으므로 WAV 원본일 때만 복사
                OutputFormat.Wav     => info.Codec.StartsWith("pcm_") && info.FormatName == "wav",
                _ => false
            };
        }
        // 손실: 같은 코덱이고 원본 비트레이트가 목표 이하 (5% 여유) → 재인코딩해봐야 음질만 손해
        return info.Codec == fmt.Codec && info.Kbps > 0 && info.Kbps <= q.Kbps * 1.05;
    }

    static void AddLosslessArgs(List<string> args, FormatSpec fmt, QualityOption q, ProbeInfo? info)
    {
        // 손실 원본은 16bit로 충분, 무손실 고해상도 원본은 24bit 유지
        bool hiBits = !q.CdQuality && info is { IsLossless: true, BitsPerSample: > 16 };

        switch (fmt.Format)
        {
            case OutputFormat.Flac:
                args.AddRange(["-c:a", "flac", "-compression_level", "8", "-sample_fmt", hiBits ? "s32" : "s16"]);
                break;
            case OutputFormat.M4aAlac:
                args.AddRange(["-c:a", "alac", "-sample_fmt", hiBits ? "s32p" : "s16p"]);
                break;
            case OutputFormat.Wav:
                args.AddRange(["-c:a", hiBits ? "pcm_s24le" : "pcm_s16le"]);
                break;
        }

        if (q.CdQuality)
            args.AddRange(["-af", "aresample=44100:dither_method=triangular"]);
    }

    static void AddLossyArgs(List<string> args, FormatSpec fmt, QualityOption q, int kbps)
    {
        switch (fmt.Format)
        {
            case OutputFormat.Mp3:
                args.AddRange(["-c:a", "libmp3lame", "-compression_level", "2"]);   // LAME -q 2 (고품질 권장값)
                if (q.Vbr && kbps == q.Kbps) args.AddRange(["-q:a", "0"]);
                else args.AddRange(["-b:a", $"{kbps}k"]);
                break;
            case OutputFormat.M4aAac:
                args.AddRange(["-c:a", "aac", "-b:a", $"{kbps}k"]);
                break;
            case OutputFormat.Ogg:
                args.AddRange(["-c:a", "libvorbis", "-q:a", OutputPresets.VorbisQuality(kbps).ToString(CultureInfo.InvariantCulture)]);
                break;
            case OutputFormat.Opus:
                args.AddRange(["-c:a", "libopus", "-b:a", $"{kbps}k", "-vbr", "on"]);
                break;
        }
    }

    // ─── ffmpeg 실행 ─────────────────────────────────────────────────────────
    static async Task RunFfmpegAsync(string ffmpeg, List<string> args, double duration, Action<double> onProgress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg를 실행할 수 없습니다.");
        using var reg  = ct.Register(() => { try { proc.Kill(true); } catch { } });

        var errTask = proc.StandardError.ReadToEndAsync(CancellationToken.None);

        // -progress pipe:1 → "out_time_us=12345678" 형식 줄 단위 출력
        string? line;
        while ((line = await proc.StandardOutput.ReadLineAsync(CancellationToken.None)) is not null)
        {
            if (duration <= 0) continue;
            if (line.StartsWith("out_time_us=") || line.StartsWith("out_time_ms="))
            {
                if (long.TryParse(line.AsSpan(line.IndexOf('=') + 1), out var us) && us > 0)
                    onProgress(Math.Clamp(us / 1_000_000.0 / duration, 0, 0.99));
            }
        }

        await proc.WaitForExitAsync(CancellationToken.None);
        var err = await errTask;
        ct.ThrowIfCancellationRequested();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(AudioProbe.FirstLine(err) ?? $"ffmpeg 오류 (exit {proc.ExitCode})");
    }
}
