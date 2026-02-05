using System.IO;
using System.Text.Json;
using Music.Player.Models;

namespace Music.Player.Services
{
    public static class PlaylistService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicPlayer");

        private static readonly string StateFilePath = Path.Combine(AppDataPath, "playlist.json");

        public static void SaveState(PlaylistState state)
        {
            try
            {
                Directory.CreateDirectory(AppDataPath);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(state, options);
                File.WriteAllText(StateFilePath, json);
            }
            catch
            {
                // Silently fail - saving state is not critical
            }
        }

        public static PlaylistState? LoadState()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                    return null;

                var json = File.ReadAllText(StateFilePath);
                var state = JsonSerializer.Deserialize<PlaylistState>(json);

                // Validate file paths still exist
                if (state != null)
                {
                    state.FilePaths = state.FilePaths.Where(File.Exists).ToList();

                    // Reset index if it's out of bounds
                    if (state.CurrentTrackIndex >= state.FilePaths.Count)
                        state.CurrentTrackIndex = state.FilePaths.Count > 0 ? 0 : -1;
                }

                return state;
            }
            catch
            {
                return null;
            }
        }
    }
}
