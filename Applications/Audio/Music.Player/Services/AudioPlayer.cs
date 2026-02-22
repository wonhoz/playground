using NAudio.Wave;

namespace Music.Player.Services
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer? _wavePlayer;
        private AudioFileReader? _audioFile;
        private bool _disposed;

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
            _wavePlayer = new WaveOutEvent();
            _wavePlayer.Init(_audioFile);
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
