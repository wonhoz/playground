using NAudio.Wave;

namespace AmbientMixer.Services;

// ─────────────────────────────────────────────────────────────────────────────
// 공통: 각 환경음은 ISampleProvider를 구현하는 무한 생성기.
// MixingSampleProvider + VolumeSampleProvider로 조합.
// WaveFormat: 44100 Hz, 32-bit float, Mono
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>☔ 빗소리 — 백색 노이즈에 저역 통과 필터 적용</summary>
public sealed class RainProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private float _lp;  // 저역 통과 누산기

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float noise = (float)(_rng.NextDouble() * 2 - 1);
            // 두 단계 저역 통과 필터 → 부드럽고 균일한 빗소리
            _lp = noise * 0.04f + _lp * 0.96f;
            buf[offset + i] = _lp * 8f;   // 증폭 보정
        }
        return count;
    }
}

/// <summary>💨 바람 — 느린 진폭 변조(0.15 Hz) × 저역 필터 노이즈</summary>
public sealed class WindProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private float  _lp;
    private double _t;

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _t += 1.0 / 44100;
            // 0.12 Hz 사인파 = ~8초 주기의 바람 세기 변화
            double env = 0.4 + 0.6 * Math.Abs(Math.Sin(Math.PI * 0.12 * _t));
            float noise = (float)(_rng.NextDouble() * 2 - 1);
            _lp = noise * 0.015f + _lp * 0.985f;
            buf[offset + i] = _lp * (float)env * 14f;
        }
        return count;
    }
}

/// <summary>🌊 파도 — 주기적(~9초) 빌드업 & 해소 × 광대역 노이즈</summary>
public sealed class WaveProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private double _t;

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _t += 1.0 / 44100;
            // 여러 주기의 파도를 겹쳐서 자연스럽게
            double w1 = Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * _t / 9.0),  2.5);
            double w2 = Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * _t / 13.0), 2.0) * 0.5;
            double env = Math.Clamp(w1 + w2, 0, 1);
            float noise = (float)(_rng.NextDouble() * 2 - 1);
            buf[offset + i] = noise * (float)env * 0.9f;
        }
        return count;
    }
}

/// <summary>🐦 새소리 — 랜덤 간격의 짧은 치르프(2000~4500 Hz)</summary>
public sealed class BirdProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private double _t, _nextChirp;
    private double _chirpStart, _chirpFreq, _chirpDur;
    private bool   _chirping;

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _t += 1.0 / 44100;

            if (!_chirping && _t >= _nextChirp)
            {
                _chirping   = true;
                _chirpStart = _t;
                _chirpFreq  = 2200 + _rng.NextDouble() * 2300;  // 2200~4500 Hz
                _chirpDur   = 0.06 + _rng.NextDouble() * 0.18;  // 60~240 ms
            }

            float sample = 0f;
            if (_chirping)
            {
                double ct = _t - _chirpStart;
                if (ct >= _chirpDur)
                {
                    _chirping  = false;
                    _nextChirp = _t + 0.4 + _rng.NextDouble() * 3.5; // 0.4~4초 간격
                }
                else
                {
                    // 종형 엔벨로프 × 주파수 상승 치르프
                    double env  = Math.Sin(Math.PI * ct / _chirpDur);
                    double freq = _chirpFreq * (1 + ct * 120);
                    sample = (float)(Math.Sin(2 * Math.PI * freq * ct) * env * 0.45);
                }
            }
            buf[offset + i] = sample;
        }
        return count;
    }
}

/// <summary>☕ 카페 소음 — 대역 통과 필터 무르무르 + 간헐적 대화 버스트</summary>
public sealed class CafeProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private float  _lp1, _lp2, _hp;
    private double _t;
    private double _nextBurst;
    private int    _burstRem;

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _t += 1.0 / 44100;

            float noise = (float)(_rng.NextDouble() * 2 - 1);
            // 대역 통과 (200~3000 Hz): 저역 통과 2회 + 고역 통과 1회
            _lp1 = noise * 0.12f + _lp1 * 0.88f;
            _lp2 = _lp1  * 0.18f + _lp2 * 0.82f;
            float band = _lp2 - (_hp = _lp2 * 0.015f + _hp * 0.985f);

            // 간헐적 대화 버스트 (목소리 시뮬레이션)
            if (_burstRem == 0 && _t >= _nextBurst)
            {
                _nextBurst = _t + 1.0 + _rng.NextDouble() * 4.0;
                _burstRem  = (int)(44100 * (0.2 + _rng.NextDouble() * 0.8));
            }
            float burstMul = _burstRem > 0 ? 2.0f : 1.0f;
            if (_burstRem > 0) _burstRem--;

            buf[offset + i] = band * burstMul * 3f;
        }
        return count;
    }
}

/// <summary>⌨️ 키보드 타이핑 — 40~120 ms 간격 임펄스 + 12 ms 클릭 감쇠</summary>
public sealed class KeyboardProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private double _t, _nextClick;
    private int    _clickRem;
    private const double ClickDurSec = 0.010; // 10 ms

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _t += 1.0 / 44100;

            if (_clickRem == 0 && _t >= _nextClick)
            {
                _nextClick = _t + 0.040 + _rng.NextDouble() * 0.100; // 40~140 ms
                _clickRem  = (int)(44100 * ClickDurSec);

                // 연속 타이핑 버스트 확률 (짧게 2~4개 연속)
                if (_rng.NextDouble() < 0.3)
                    _nextClick = _t + 0.012 + _rng.NextDouble() * 0.020;
            }

            float sample = 0f;
            if (_clickRem > 0)
            {
                double elapsed = ClickDurSec - (_clickRem / 44100.0);
                double env = Math.Exp(-elapsed * 500);
                sample = (float)((_rng.NextDouble() * 2 - 1) * env * 0.55);
                _clickRem--;
            }
            buf[offset + i] = sample;
        }
        return count;
    }
}

/// <summary>🔥 모닥불 — 저역 필터 노이즈 + 랜덤 크래클</summary>
public sealed class FireProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();
    private float  _lp;
    private double _t, _nextCrackle;
    private int    _crackleRem;
    private const double CrackleDurSec = 0.004;

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _t += 1.0 / 44100;

            float noise = (float)(_rng.NextDouble() * 2 - 1);
            _lp = noise * 0.04f + _lp * 0.96f;
            float sample = _lp * 4f;

            if (_crackleRem == 0 && _t >= _nextCrackle)
            {
                _nextCrackle = _t + 0.08 + _rng.NextDouble() * 0.9;
                _crackleRem  = (int)(44100 * CrackleDurSec);
            }

            if (_crackleRem > 0)
            {
                double elapsed = CrackleDurSec - (_crackleRem / 44100.0);
                double env = Math.Exp(-elapsed * 1200);
                sample += (float)((_rng.NextDouble() * 2 - 1) * env * 0.7);
                _crackleRem--;
            }
            buf[offset + i] = sample;
        }
        return count;
    }
}

/// <summary>〰 화이트 노이즈 — 균일 주파수 분포</summary>
public sealed class WhiteNoiseProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private readonly Random _rng = new();

    public int Read(float[] buf, int offset, int count)
    {
        for (int i = 0; i < count; i++)
            buf[offset + i] = (float)(_rng.NextDouble() * 2 - 1) * 0.55f;
        return count;
    }
}
