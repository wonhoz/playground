using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using AmbientMixer.Models;

namespace AmbientMixer.Services;

/// <summary>
/// 8개 트랙의 오디오를 혼합·재생하는 핵심 서비스.
/// 슬립 타이머는 마지막 30초 동안 자동 페이드아웃.
/// </summary>
public sealed class MixerService : IDisposable
{
    private readonly WaveOutEvent _output = new() { DesiredLatency = 100 };
    private readonly MixingSampleProvider _mixer;
    private readonly Dictionary<AmbientTrack, VolumeSampleProvider> _channels = [];
    private readonly Dictionary<AmbientTrack, float> _trackVolumes  = [];

    private float _masterVolume   = 0.8f;
    private float _fadeMultiplier = 1.0f;

    private System.Timers.Timer? _sleepTimer;
    private int _sleepRemainSec;

    /// <summary>슬립 타이머 매 초 발생 — 남은 초 수 전달</summary>
    public event Action<int>? SleepTickChanged;
    /// <summary>슬립 타이머 만료 — 재생 자동 중단 후 발생</summary>
    public event Action? SleepExpired;

    public bool IsPlaying { get; private set; }

    public MixerService()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 1))
        {
            ReadFully = true,
        };

        foreach (AmbientTrack track in Enum.GetValues<AmbientTrack>())
        {
            _trackVolumes[track] = 0f;
            var provider = CreateProvider(track);
            var vol = new VolumeSampleProvider(provider) { Volume = 0f };
            _channels[track] = vol;
            _mixer.AddMixerInput(vol);
        }

        _output.Init(_mixer);
    }

    private static ISampleProvider CreateProvider(AmbientTrack track) => track switch
    {
        AmbientTrack.Rain       => new RainProvider(),
        AmbientTrack.Wind       => new WindProvider(),
        AmbientTrack.Wave       => new WaveProvider(),
        AmbientTrack.Bird       => new BirdProvider(),
        AmbientTrack.Cafe       => new CafeProvider(),
        AmbientTrack.Keyboard   => new KeyboardProvider(),
        AmbientTrack.Fire       => new FireProvider(),
        AmbientTrack.WhiteNoise => new WhiteNoiseProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(track)),
    };

    public float MasterVolume
    {
        get => _masterVolume;
        set { _masterVolume = Math.Clamp(value, 0f, 1f); RefreshAllVolumes(); }
    }

    /// <summary>개별 트랙 볼륨 설정 (0.0 ~ 1.0). 마스터·페이드 배율 자동 적용.</summary>
    public void SetTrackVolume(AmbientTrack track, float volume)
    {
        _trackVolumes[track] = Math.Clamp(volume, 0f, 1f);
        if (_channels.TryGetValue(track, out var ch))
            ch.Volume = _trackVolumes[track] * _masterVolume * _fadeMultiplier;
    }

    /// <summary>MixerSettings의 모든 볼륨을 한 번에 적용.</summary>
    public void ApplySettings(MixerSettings s)
    {
        _masterVolume = s.MasterVolume;
        foreach (AmbientTrack t in Enum.GetValues<AmbientTrack>())
            _trackVolumes[t] = s.GetVolume(t);
        RefreshAllVolumes();
    }

    private void RefreshAllVolumes()
    {
        foreach (AmbientTrack t in Enum.GetValues<AmbientTrack>())
        {
            if (_channels.TryGetValue(t, out var ch))
                ch.Volume = _trackVolumes[t] * _masterVolume * _fadeMultiplier;
        }
    }

    public void Play()  { if (!IsPlaying) { _output.Play();  IsPlaying = true;  } }
    public void Pause() { if (IsPlaying)  { _output.Pause(); IsPlaying = false; } }
    public void TogglePlay() { if (IsPlaying) Pause(); else Play(); }

    // ─────────────────────────────────────────────
    // 슬립 타이머
    // ─────────────────────────────────────────────

    public void StartSleepTimer(int minutes)
    {
        StopSleepTimer();
        if (minutes <= 0) return;

        _sleepRemainSec = minutes * 60;
        _fadeMultiplier = 1.0f;
        RefreshAllVolumes();

        _sleepTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _sleepTimer.Elapsed += OnSleepTick;
        _sleepTimer.Start();
    }

    private void OnSleepTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _sleepRemainSec = Math.Max(0, _sleepRemainSec - 1);
        SleepTickChanged?.Invoke(_sleepRemainSec);

        // 마지막 30초: 선형 페이드 아웃
        if (_sleepRemainSec <= 30)
        {
            _fadeMultiplier = _sleepRemainSec / 30.0f;
            RefreshAllVolumes();
        }

        if (_sleepRemainSec == 0)
        {
            StopSleepTimer();
            Pause();
            SleepExpired?.Invoke();
        }
    }

    public void StopSleepTimer()
    {
        _sleepTimer?.Stop();
        _sleepTimer?.Dispose();
        _sleepTimer = null;
        _fadeMultiplier = 1.0f;
        RefreshAllVolumes();
    }

    public void Dispose()
    {
        StopSleepTimer();
        _output.Stop();
        _output.Dispose();
    }
}
