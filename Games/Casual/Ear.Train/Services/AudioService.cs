using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EarTrain.Services;

/// <summary>사인파로 음표를 생성해 재생하는 서비스</summary>
public class AudioService : IDisposable
{
    private readonly WaveOutEvent _waveOut = new();
    private readonly MixingSampleProvider _mixer;

    public AudioService()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
        {
            ReadFully = true
        };
        _waveOut.Init(_mixer);
        _waveOut.Play();
    }

    /// <summary>단일 음표 재생</summary>
    public void PlayNote(double frequency, double durationSec = 1.0, double amplitude = 0.3)
    {
        var sig = new SignalGenerator(44100, 2)
        {
            Type = SignalGeneratorType.Sin,
            Frequency = frequency,
            Gain = amplitude
        };
        var env = new EnvelopeSampleProvider(sig.ToMono(), durationSec, 0.05, 0.1, 0.7f, 0.15);
        _mixer.AddMixerInput(env);
    }

    /// <summary>음표들을 순차적으로 재생 (멜로디)</summary>
    public async Task PlayMelodyAsync(IEnumerable<(double Hz, double Dur)> notes, CancellationToken ct = default)
    {
        foreach (var (hz, dur) in notes)
        {
            ct.ThrowIfCancellationRequested();
            PlayNote(hz, dur);
            await Task.Delay(TimeSpan.FromSeconds(dur + 0.05), ct);
        }
    }

    /// <summary>화음: 동시에 여러 음표 재생</summary>
    public void PlayChord(IEnumerable<double> frequencies, double durationSec = 1.5)
    {
        foreach (var hz in frequencies)
            PlayNote(hz, durationSec, 0.2);
    }

    public void Dispose()
    {
        _waveOut.Stop();
        _waveOut.Dispose();
    }
}

// ─── 간단한 Envelope Provider (ADSR) ─────────────────────────────────────
file class EnvelopeSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _totalSamples;
    private readonly int _attackSamples;
    private readonly int _decaySamples;
    private readonly float _sustainLevel;
    private readonly int _releaseSamples;
    private int _position = 0;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public EnvelopeSampleProvider(ISampleProvider source, double totalSec,
        double attack, double decay, float sustain, double release)
    {
        _source = source;
        int rate = source.WaveFormat.SampleRate;
        _totalSamples = (int)(totalSec * rate);
        _attackSamples = (int)(attack * rate);
        _decaySamples = (int)(decay * rate);
        _sustainLevel = sustain;
        _releaseSamples = (int)(release * rate);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (_position >= _totalSamples) return 0;
        int toRead = Math.Min(count, _totalSamples - _position);
        int read = _source.Read(buffer, offset, toRead);

        int releaseStart = _totalSamples - _releaseSamples;
        for (int i = 0; i < read; i++)
        {
            int pos = _position + i;
            float gain;
            if (pos < _attackSamples)
                gain = (float)pos / _attackSamples;
            else if (pos < _attackSamples + _decaySamples)
                gain = 1f - (1f - _sustainLevel) * (float)(pos - _attackSamples) / _decaySamples;
            else if (pos < releaseStart)
                gain = _sustainLevel;
            else
                gain = _sustainLevel * (1f - (float)(pos - releaseStart) / _releaseSamples);

            buffer[offset + i] *= gain;
        }
        _position += read;
        return read;
    }
}

// ─── Mono 변환 헬퍼 ──────────────────────────────────────────────────────
file static class SampleProviderExtensions
{
    public static ISampleProvider ToMono(this ISampleProvider source)
    {
        if (source.WaveFormat.Channels == 1) return source;
        return new StereoToMonoSampleProvider(source);
    }
}
