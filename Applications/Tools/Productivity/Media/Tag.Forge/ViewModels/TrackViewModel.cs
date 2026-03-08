namespace Tag.Forge.ViewModels;

public class TrackViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public TrackInfo Info { get; }

    public TrackViewModel(TrackInfo info) => Info = info;

    public string FilePath => Info.FilePath;
    public string FileName => Info.FileName;

    public string Title
    {
        get => Info.Title;
        set { if (Info.Title != value) { Info.Title = value; Modified = true; Notify(); } }
    }
    public string Artist
    {
        get => Info.Artist;
        set { if (Info.Artist != value) { Info.Artist = value; Modified = true; Notify(); } }
    }
    public string Album
    {
        get => Info.Album;
        set { if (Info.Album != value) { Info.Album = value; Modified = true; Notify(); } }
    }
    public string AlbumArtist
    {
        get => Info.AlbumArtist;
        set { if (Info.AlbumArtist != value) { Info.AlbumArtist = value; Modified = true; Notify(); } }
    }
    public uint Track
    {
        get => Info.Track;
        set { if (Info.Track != value) { Info.Track = value; Modified = true; Notify(); } }
    }
    public uint Year
    {
        get => Info.Year;
        set { if (Info.Year != value) { Info.Year = value; Modified = true; Notify(); } }
    }
    public string Genre
    {
        get => Info.Genre;
        set { if (Info.Genre != value) { Info.Genre = value; Modified = true; Notify(); } }
    }
    public string Comment
    {
        get => Info.Comment;
        set { if (Info.Comment != value) { Info.Comment = value; Modified = true; Notify(); } }
    }

    bool _modified;
    public bool Modified
    {
        get => _modified;
        set
        {
            Info.AlbumArt = Info.AlbumArt; // 갱신 방어용
            _modified = value;
            Notify();
            Notify(nameof(RowColor));
        }
    }

    /// <summary>수정 여부 시각화용 행 좌측 강조색</summary>
    public string RowColor => _modified ? "#FBBF24" : "Transparent";
}
