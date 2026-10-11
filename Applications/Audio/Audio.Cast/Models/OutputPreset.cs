namespace AudioCast.Models;

public enum OutputFormat { Mp3, Flac, Wav, M4aAac, M4aAlac, Ogg, Opus }

/// <summary>품질 옵션 — 손실 포맷은 Kbps, 무손실 포맷은 CdQuality 여부로 구분</summary>
public sealed record QualityOption(string Key, string Label, int Kbps = 0, bool Vbr = false, bool CdQuality = false)
{
    public override string ToString() => Label;
}

public sealed record FormatSpec(
    OutputFormat Format,
    string Tag,              // 설정 저장용 키
    string Label,            // 콤보박스 표시명
    string Extension,        // 출력 확장자
    string Muxer,            // ffmpeg -f 값 (임시 파일 확장자와 무관하게 컨테이너 지정)
    string Codec,            // ffprobe codec_name 기준 코덱명 (스트림 복사 판정용)
    bool Lossless,
    bool SupportsCover,
    QualityOption[] Qualities)
{
    public override string ToString() => Label;
}

public static class OutputPresets
{
    static readonly QualityOption[] LosslessQualities =
    [
        new("orig", "원본 해상도 유지"),
        new("cd",   "CD 품질 (16bit·44.1kHz)", CdQuality: true),
    ];

    public static readonly FormatSpec[] All =
    [
        new(OutputFormat.Mp3, "MP3", "MP3", ".mp3", "mp3", "mp3", false, true,
        [
            new("128", "128 kbps", 128),
            new("192", "192 kbps", 192),
            new("256", "256 kbps", 256),
            new("320", "320 kbps (최고)", 320),
            new("v0",  "VBR V0 (평균 ~245 kbps)", 245, Vbr: true),
        ]),
        new(OutputFormat.Flac,    "FLAC", "FLAC (무손실)",       ".flac", "flac", "flac", true, true,  LosslessQualities),
        new(OutputFormat.Wav,     "WAV",  "WAV (무압축)",        ".wav",  "wav",  "pcm",  true, false, LosslessQualities),
        new(OutputFormat.M4aAac,  "AAC",  "M4A · AAC",           ".m4a",  "ipod", "aac",  false, true,
        [
            new("128", "128 kbps", 128),
            new("192", "192 kbps", 192),
            new("256", "256 kbps (권장)", 256),
            new("320", "320 kbps", 320),
        ]),
        new(OutputFormat.M4aAlac, "ALAC", "M4A · ALAC (무손실)", ".m4a",  "ipod", "alac", true, true,  LosslessQualities),
        new(OutputFormat.Ogg,     "OGG",  "OGG · Vorbis",        ".ogg",  "ogg",  "vorbis", false, false,
        [
            new("128", "~128 kbps (q4)", 128, Vbr: true),
            new("192", "~192 kbps (q6)", 192, Vbr: true),
            new("256", "~256 kbps (q8)", 256, Vbr: true),
            new("320", "~320 kbps (q9)", 320, Vbr: true),
        ]),
        new(OutputFormat.Opus,    "OPUS", "OPUS",                ".opus", "opus", "opus", false, false,
        [
            new("96",  "96 kbps", 96),
            new("128", "128 kbps (권장)", 128),
            new("192", "192 kbps", 192),
            new("256", "256 kbps", 256),
        ]),
    ];

    public static FormatSpec Get(string? tag) =>
        All.FirstOrDefault(f => f.Tag == tag) ?? All[0];

    /// <summary>Vorbis 목표 비트레이트 → -q:a 값</summary>
    public static int VorbisQuality(int kbps) => kbps switch
    {
        <= 96  => 2,
        <= 128 => 4,
        <= 160 => 5,
        <= 192 => 6,
        <= 224 => 7,
        <= 256 => 8,
        <= 320 => 9,
        _      => 10
    };
}
