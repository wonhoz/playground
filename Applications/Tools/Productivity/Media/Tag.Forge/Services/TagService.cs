namespace Tag.Forge.Services;

public class TagService
{
    static readonly HashSet<string> SupportedExts = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".flac", ".ogg", ".m4a", ".aac", ".wma", ".opus" };

    public TrackInfo Load(string path)
    {
        using var f = TagLib.File.Create(path);
        var t = f.Tag;
        return new TrackInfo
        {
            FilePath    = path,
            Title       = t.Title         ?? "",
            Artist      = t.FirstPerformer ?? "",
            Album       = t.Album          ?? "",
            AlbumArtist = t.FirstAlbumArtist ?? "",
            Track       = t.Track,
            Year        = t.Year,
            Genre       = t.FirstGenre    ?? "",
            Comment     = t.Comment       ?? "",
            AlbumArt    = t.Pictures?.Length > 0 ? t.Pictures[0].Data.Data : null,
        };
    }

    public void Save(TrackInfo info)
    {
        using var f = TagLib.File.Create(info.FilePath);
        var t = f.Tag;
        t.Title        = info.Title;
        t.Performers   = string.IsNullOrEmpty(info.Artist)      ? [] : [info.Artist];
        t.Album        = info.Album;
        t.AlbumArtists = string.IsNullOrEmpty(info.AlbumArtist) ? [] : [info.AlbumArtist];
        t.Track        = info.Track;
        t.Year         = info.Year;
        t.Genres       = string.IsNullOrEmpty(info.Genre)       ? [] : [info.Genre];
        t.Comment      = info.Comment;
        if (info.AlbumArt != null)
        {
            t.Pictures = [new TagLib.Picture(new TagLib.ByteVector(info.AlbumArt))];
        }
        f.Save();
    }

    public IEnumerable<string> ScanFolder(string folder) =>
        Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                 .Where(p => SupportedExts.Contains(Path.GetExtension(p)));
}
