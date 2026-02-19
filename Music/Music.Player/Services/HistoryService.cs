using System.IO;
using System.Text.Json;
using Music.Player.Models;

namespace Music.Player.Services
{
    public class HistoryService
    {
        private static readonly Lazy<HistoryService> _instance = new(() => new HistoryService());
        public static HistoryService Instance => _instance.Value;

        private readonly string _historyFilePath;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private Dictionary<string, PlayHistoryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

        private HistoryService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicPlayer");
            Directory.CreateDirectory(appDataPath);
            _historyFilePath = Path.Combine(appDataPath, "history.json");
            Load();
        }

        public void RecordPlay(TrackInfo track)
        {
            if (!_entries.TryGetValue(track.FilePath, out var entry))
            {
                entry = new PlayHistoryEntry { FilePath = track.FilePath };
                _entries[track.FilePath] = entry;
            }
            entry.Title = track.DisplayTitle;
            entry.Artist = track.DisplayArtist;
            entry.PlayCount++;
            entry.LastPlayedAt = DateTime.Now;
            Save();
        }

        public void ToggleFavorite(TrackInfo track)
        {
            if (!_entries.TryGetValue(track.FilePath, out var entry))
            {
                entry = new PlayHistoryEntry
                {
                    FilePath = track.FilePath,
                    Title = track.DisplayTitle,
                    Artist = track.DisplayArtist
                };
                _entries[track.FilePath] = entry;
            }
            entry.IsFavorite = !entry.IsFavorite;
            track.IsFavorite = entry.IsFavorite;
            Save();
        }

        public void LoadFavoriteStatus(TrackInfo track)
        {
            if (_entries.TryGetValue(track.FilePath, out var entry))
                track.IsFavorite = entry.IsFavorite;
        }

        public List<PlayHistoryEntry> GetRecentTracks(int count = 100)
            => _entries.Values
                .Where(e => e.PlayCount > 0)
                .OrderByDescending(e => e.LastPlayedAt)
                .Take(count)
                .ToList();

        public List<PlayHistoryEntry> GetMostPlayed(int count = 100)
            => _entries.Values
                .Where(e => e.PlayCount > 0)
                .OrderByDescending(e => e.PlayCount)
                .ThenByDescending(e => e.LastPlayedAt)
                .Take(count)
                .ToList();

        public List<PlayHistoryEntry> GetFavorites()
            => _entries.Values
                .Where(e => e.IsFavorite)
                .OrderBy(e => e.Artist)
                .ThenBy(e => e.Title)
                .ToList();

        private void Load()
        {
            try
            {
                if (!File.Exists(_historyFilePath)) return;
                var json = File.ReadAllText(_historyFilePath);
                var list = JsonSerializer.Deserialize<List<PlayHistoryEntry>>(json);
                if (list != null)
                    _entries = list.ToDictionary(e => e.FilePath, e => e, StringComparer.OrdinalIgnoreCase);
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    _entries.Values.ToList(),
                    _jsonOptions);
                File.WriteAllText(_historyFilePath, json);
            }
            catch { }
        }
    }
}
