using System.Text.Json.Serialization;

namespace AmbientMixer.Models;

public class MixerSettings
{
    public float MasterVolume    { get; set; } = 0.8f;
    public int   SleepTimerMins  { get; set; } = 0;  // 0 = 꺼짐
    public bool  StartMinimized  { get; set; } = false;

    /// <summary>각 트랙 볼륨 (0.0 ~ 1.0)</summary>
    public Dictionary<string, float> TrackVolumes { get; set; } = [];

    /// <summary>저장된 프리셋 목록</summary>
    public List<Preset> Presets { get; set; } = DefaultPresets();

    public float GetVolume(AmbientTrack t) =>
        TrackVolumes.TryGetValue(t.ToString(), out var v) ? v : 0f;

    public void SetVolume(AmbientTrack t, float v) =>
        TrackVolumes[t.ToString()] = v;

    private static List<Preset> DefaultPresets() =>
    [
        new Preset
        {
            Name = "카페 모드",
            Volumes = new()
            {
                [nameof(AmbientTrack.Cafe)]      = 0.70f,
                [nameof(AmbientTrack.Keyboard)]  = 0.55f,
                [nameof(AmbientTrack.WhiteNoise)] = 0.15f,
            },
        },
        new Preset
        {
            Name = "숲속 모드",
            Volumes = new()
            {
                [nameof(AmbientTrack.Rain)] = 0.25f,
                [nameof(AmbientTrack.Wind)] = 0.35f,
                [nameof(AmbientTrack.Bird)] = 0.75f,
                [nameof(AmbientTrack.WhiteNoise)] = 0.10f,
            },
        },
        new Preset
        {
            Name = "비 오는 날",
            Volumes = new()
            {
                [nameof(AmbientTrack.Rain)] = 0.80f,
                [nameof(AmbientTrack.Wind)] = 0.25f,
                [nameof(AmbientTrack.Fire)] = 0.45f,
            },
        },
    ];
}

public class Preset
{
    public string                   Name    { get; set; } = "";
    public Dictionary<string, float> Volumes { get; set; } = [];
}
