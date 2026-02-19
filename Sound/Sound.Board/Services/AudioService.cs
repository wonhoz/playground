using System.IO;
using NAudio.Wave;
using SoundBoard.Models;

namespace SoundBoard.Services;

public class AudioService : IDisposable
{
    private readonly List<WaveOutEvent> _pool = [];
    private readonly object _lock = new();
    private float _volume = 0.8f;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            lock (_lock)
                foreach (var wo in _pool)
                    wo.Volume = _volume;
        }
    }

    public bool OverlapSounds { get; set; } = true;

    public void Play(SoundButton btn)
    {
        if (!btn.HasSound) return;
        if (!OverlapSounds) StopAll();

        try
        {
            IWaveProvider provider;
            IDisposable   cleanup;

            if (!string.IsNullOrEmpty(btn.FilePath) && File.Exists(btn.FilePath))
            {
                var reader = new AudioFileReader(btn.FilePath);
                provider = reader;
                cleanup  = reader;
            }
            else
            {
                var pcm    = SoundSynthesizer.Generate(btn.BuiltInKey);
                var format = new WaveFormat(44100, 16, 1);
                var ms     = new MemoryStream(pcm);
                var raw    = new RawSourceWaveStream(ms, format);
                provider   = raw;
                cleanup    = raw;
            }

            var wo = new WaveOutEvent { Volume = _volume };
            wo.Init(provider);
            wo.PlaybackStopped += (_, _) =>
            {
                lock (_lock) _pool.Remove(wo);
                wo.Dispose();
                cleanup.Dispose();
            };
            lock (_lock) _pool.Add(wo);
            wo.Play();
        }
        catch { /* 재생 오류는 무시 */ }
    }

    public void StopAll()
    {
        lock (_lock)
            foreach (var wo in _pool.ToList())
                wo.Stop();
    }

    public void Dispose()
    {
        StopAll();
        lock (_lock)
            foreach (var wo in _pool)
                wo.Dispose();
    }
}
