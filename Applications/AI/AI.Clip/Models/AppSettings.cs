using System.Text.Json;

namespace AiClip.Models
{
    public class AppSettings
    {
        public string ApiKey { get; set; } = "";
        public string TranslateTargetLanguage { get; set; } = "Korean";
        public string CodeTargetLanguage { get; set; } = "Python";

        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AiClip", "settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath)) ?? new();
            }
            catch { }
            return new();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(this, _jsonOptions));
            }
            catch { }
        }
    }
}
