using System.IO;
using System.Text.Json;

namespace Photo.Video.Organizer.Services
{
    public class AppSettings
    {
        public string? LastDestinationPath { get; set; }
        public int FolderStructureIndex { get; set; } = 0;
        public string CustomPattern { get; set; } = "yyyy/MM/dd";
        public bool AutoRotate { get; set; } = false;
        public bool SaveLog { get; set; } = false;
        public bool MoveFiles { get; set; } = false;
        public List<string> RecentDestinations { get; set; } = new();

        public void AddRecentDestination(string path)
        {
            RecentDestinations.Remove(path);
            RecentDestinations.Insert(0, path);
            if (RecentDestinations.Count > 5)
                RecentDestinations.RemoveRange(5, RecentDestinations.Count - 5);
        }

        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PhotoVideoOrganizer",
            "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }
    }
}
