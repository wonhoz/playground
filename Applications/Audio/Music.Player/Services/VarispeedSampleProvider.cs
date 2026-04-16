using NAudio.Wave;

namespace Music.Player.Services
{
    /// <summary>
    /// 선형 보간 리샘플링으로 재생 속도를 변경한다 (피치도 함께 변함).
    /// 0.5x ~ 2.0x 범위를 지원하며 버퍼 경계의 연속성을 유지한다.
    /// </summary>
    public class VarispeedSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float _speed = 1.0f;
        private float[] _srcBuf = [];
        private float[] _leftover = [];
        private int _leftoverCount;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public float Speed
        {
            get => _speed;
            set
            {
                _speed = Math.Clamp(value, 0.5f, 2.0f);
                _leftoverCount = 0; // 속도 변경 시 잔여 버퍼 초기화
            }
        }

        public VarispeedSampleProvider(ISampleProvider source, float initialSpeed = 1.0f)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _leftover = new float[_channels * 4];
            _speed = Math.Clamp(initialSpeed, 0.5f, 2.0f);
        }

        /// <summary>Seek 후 호출하여 잔여 버퍼를 초기화한다.</summary>
        public void Reset() => _leftoverCount = 0;

        public int Read(float[] buffer, int offset, int count)
        {
            float speed = _speed;

            // 1x 속도는 패스스루
            if (Math.Abs(speed - 1.0f) < 0.001f)
            {
                _leftoverCount = 0;
                return _source.Read(buffer, offset, count);
            }

            int ch = _channels;
            int outputFrames = count / ch;

            // 이번 Read 에서 소스로부터 새로 읽어야 할 프레임 수
            int srcFromSource = Math.Max(0,
                (int)Math.Ceiling(outputFrames * speed) + 1 - _leftoverCount);
            int totalNeeded = _leftoverCount + srcFromSource;
            int totalSamples = totalNeeded * ch;

            if (_srcBuf.Length < totalSamples)
                _srcBuf = new float[totalSamples + ch * 4];

            // 이전 잔여 프레임을 버퍼 앞에 복사
            if (_leftoverCount > 0)
                Array.Copy(_leftover, 0, _srcBuf, 0, _leftoverCount * ch);

            // 소스에서 새 샘플 읽기
            int newSamples = srcFromSource > 0
                ? _source.Read(_srcBuf, _leftoverCount * ch, srcFromSource * ch)
                : 0;
            int totalFrames = _leftoverCount + newSamples / ch;

            if (totalFrames == 0) return 0;

            // 선형 보간으로 출력 샘플 생성
            int written = 0;
            for (int i = 0; i < outputFrames; i++)
            {
                double srcPos = i * speed;
                int f0 = (int)srcPos;
                if (f0 >= totalFrames) break;

                int f1 = Math.Min(f0 + 1, totalFrames - 1);
                double frac = srcPos - f0;

                for (int c = 0; c < ch; c++)
                {
                    float s0 = _srcBuf[f0 * ch + c];
                    float s1 = _srcBuf[f1 * ch + c];
                    buffer[offset + i * ch + c] = (float)(s0 + frac * (s1 - s0));
                }
                written++;
            }

            // 다음 Read 를 위한 잔여 프레임 보존
            double nextSrcPos = written * speed;
            int nextFrame = (int)Math.Floor(nextSrcPos);
            _leftoverCount = Math.Max(0, totalFrames - nextFrame);
            if (_leftoverCount > 0)
            {
                int leftoverSamples = _leftoverCount * ch;
                if (_leftover.Length < leftoverSamples)
                    _leftover = new float[leftoverSamples + ch * 4];
                Array.Copy(_srcBuf, nextFrame * ch, _leftover, 0, leftoverSamples);
            }

            return written * ch;
        }
    }
}
