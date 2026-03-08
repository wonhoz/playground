namespace Tag.Forge.Models;

public class TrackInfo
{
    public string FilePath    { get; set; } = "";
    public string FileName    => Path.GetFileName(FilePath);
    public string Title       { get; set; } = "";
    public string Artist      { get; set; } = "";
    public string Album       { get; set; } = "";
    public string AlbumArtist { get; set; } = "";
    public uint   Track       { get; set; }
    public uint   Year        { get; set; }
    public string Genre       { get; set; } = "";
    public string Comment     { get; set; } = "";
    public byte[]? AlbumArt   { get; set; }
}
