using System.IO;
using Music.Player.Models;

namespace Music.Player.Services
{
    public static class LibraryScanner
    {
        public static readonly string[] SupportedExtensions =
            [".mp3", ".wav", ".flac", ".m4a", ".wma", ".aac", ".ogg"];

        public static async Task<List<TrackInfo>> ScanFolderAsync(
            string folderPath,
            IProgress<(int current, int total, string fileName)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            var tracks = new List<TrackInfo>(files.Count);

            // 파일별 Task.Run 대신 단일 Task.Run으로 배치 처리 (스케줄링 오버헤드 감소)
            await Task.Run(() =>
            {
                for (int i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = files[i];
                    var track = TrackInfo.FromFile(file);
                    HistoryService.Instance.LoadFavoriteStatus(track);
                    tracks.Add(track);
                    progress?.Report((i + 1, files.Count, Path.GetFileName(file)));
                }
            }, cancellationToken);

            return tracks
                .OrderBy(t => t.DisplayArtist)
                .ThenBy(t => t.Album)
                .ThenBy(t => t.DisplayTitle)
                .ToList();
        }
    }
}
