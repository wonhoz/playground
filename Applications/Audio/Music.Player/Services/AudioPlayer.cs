using NAudio.Wave;

namespace Music.Player.Services
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer? _wavePlayer;
        private AudioFileReader? _audioFile;
        private VarispeedSampleProvider? _speedProvider;
        private bool _disposed;
        private float _currentSpeed = 1.0f;

        public event EventHandler? PlaybackStopped;
        public event EventHandler<TimeSpan>? PositionChanged;

        private System.Timers.Timer? _positionTimer;

        public PlaybackState PlaybackState => _wavePlayer?.PlaybackState ?? PlaybackState.Stopped;
        public TimeSpan CurrentPosition => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => _audioFile?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => _audioFile?.Volume ?? 1f;
            set
            {
                if (_audioFile != null)
                    _audioFile.Volume = Math.Clamp(value, 0f, 1f);
            }
        }

        /// <summary>재생 속도 (0.5x ~ 2.0x). 트랙 전환 시에도 유지된다.</summary>
        public float Speed
        {
            get => _currentSpeed;
            set
            {
                _currentSpeed = Math.Clamp(value, 0.5f, 2.0f);
                if (_speedProvider != null)
                    _speedProvider.Speed = _currentSpeed;
            }
        }

        public void Load(string filePath)
        {
            // Unsubscribe from old PlaybackStopped event before stopping
            // to prevent triggering end-of-track logic during track change
            if (_wavePlayer != null)
            {
                _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
            }

            Stop();
            DisposeAudio();

            _audioFile = new AudioFileReader(filePath);
            _speedProvider = new VarispeedSampleProvider(_audioFile, _currentSpeed);
            _wavePlayer = new WaveOutEvent();
            _wavePlayer.Init(_speedProvider);
            _wavePlayer.PlaybackStopped += OnPlaybackStopped;

            _positionTimer = new System.Timers.Timer(100);
            _positionTimer.AutoReset = true;
            _positionTimer.Elapsed += (s, e) => PositionChanged?.Invoke(this, CurrentPosition);
        }

        public void Play()
        {
            if (_wavePlayer == null) return;

            _wavePlayer.Play();
            _positionTimer?.Start();
        }

        public void Pause()
        {
            _wavePlayer?.Pause();
            _positionTimer?.Stop();
        }

        public void Stop()
        {
            _wavePlayer?.Stop();
            _positionTimer?.Stop();
            if (_audioFile != null)
                _audioFile.Position = 0;
        }

        public void Seek(TimeSpan position)
        {
            if (_audioFile != null)
            {
                _audioFile.CurrentTime = position;
                _speedProvider?.Reset();
                PositionChanged?.Invoke(this, position);
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            _positionTimer?.Stop();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        private void DisposeAudio()
        {
            if (_wavePlayer != null)
            {
                _wavePlayer.PlaybackStopped -= OnPlaybackStopped;
                _wavePlayer.Dispose();
                _wavePlayer = null;
            }

            _audioFile?.Dispose();
            _audioFile = null;
            _speedProvider = null; // VarispeedSampleProvider는 Dispose 불필요

            _positionTimer?.Dispose();
            _positionTimer = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            DisposeAudio();
            GC.SuppressFinalize(this);
        }
    }
}
