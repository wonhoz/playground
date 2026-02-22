namespace AmbientMixer.Models;

public enum AmbientTrack
{
    Rain,       // 빗소리
    Wind,       // 바람
    Wave,       // 파도
    Bird,       // 새소리
    Cafe,       // 카페 소음
    Keyboard,   // 키보드 타이핑
    Fire,       // 모닥불
    WhiteNoise, // 화이트 노이즈
}

public static class AmbientTrackInfo
{
    public static string Emoji(AmbientTrack t) => t switch
    {
        AmbientTrack.Rain      => "☔",
        AmbientTrack.Wind      => "💨",
        AmbientTrack.Wave      => "🌊",
        AmbientTrack.Bird      => "🐦",
        AmbientTrack.Cafe      => "☕",
        AmbientTrack.Keyboard  => "⌨️",
        AmbientTrack.Fire      => "🔥",
        AmbientTrack.WhiteNoise => "〰",
        _                      => "🎵",
    };

    public static string Label(AmbientTrack t) => t switch
    {
        AmbientTrack.Rain      => "비",
        AmbientTrack.Wind      => "바람",
        AmbientTrack.Wave      => "파도",
        AmbientTrack.Bird      => "새소리",
        AmbientTrack.Cafe      => "카페",
        AmbientTrack.Keyboard  => "키보드",
        AmbientTrack.Fire      => "모닥불",
        AmbientTrack.WhiteNoise => "화이트 노이즈",
        _                      => t.ToString(),
    };
}
