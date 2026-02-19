using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using TagLib;

namespace Music.Player.Models
{
    public class TrackInfo : INotifyPropertyChanged
    {
        private bool _isFavorite;

        public string FilePath { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public BitmapImage? AlbumArt { get; set; }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string DisplayTitle => string.IsNullOrEmpty(Title) ? Path.GetFileNameWithoutExtension(FilePath) : Title;
        public string DisplayArtist => string.IsNullOrEmpty(Artist) ? "Unknown Artist" : Artist;
        public string DurationText => Duration.ToString(@"mm\:ss");

        public static TrackInfo FromFile(string filePath)
        {
            var track = new TrackInfo { FilePath = filePath };

            try
            {
                using var file = TagLib.File.Create(filePath);
                track.Title = file.Tag.Title ?? "";
                track.Artist = file.Tag.FirstPerformer ?? "";
                track.Album = file.Tag.Album ?? "";
                track.Duration = file.Properties.Duration;

                // Extract album art
                if (file.Tag.Pictures.Length > 0)
                {
                    var picture = file.Tag.Pictures[0];
                    using var ms = new MemoryStream(picture.Data.Data);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    track.AlbumArt = bitmap;
                }
            }
            catch
            {
                // If metadata extraction fails, use file info
                track.Title = Path.GetFileNameWithoutExtension(filePath);
            }

            return track;
        }
    }
}
