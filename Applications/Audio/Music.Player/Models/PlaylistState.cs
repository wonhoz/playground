namespace Music.Player.Models
{
    public class PlaylistState
    {
        public List<string> FilePaths { get; set; } = new();
        public int CurrentTrackIndex { get; set; } = -1;
        public double CurrentPositionSeconds { get; set; }
        public bool IsShuffleEnabled { get; set; }
        public bool IsRepeatEnabled { get; set; }
    }
}
