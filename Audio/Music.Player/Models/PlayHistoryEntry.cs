namespace Music.Player.Models
{
    public class PlayHistoryEntry
    {
        public string FilePath { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public int PlayCount { get; set; } = 0;
        public DateTime LastPlayedAt { get; set; } = DateTime.MinValue;
        public bool IsFavorite { get; set; } = false;

        public string LastPlayedText => LastPlayedAt == DateTime.MinValue
            ? "없음"
            : LastPlayedAt.ToString("MM-dd HH:mm");
    }
}
