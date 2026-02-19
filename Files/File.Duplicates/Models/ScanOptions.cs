namespace FileDuplicates.Models;

public class ScanOptions
{
    public List<string> Folders           { get; set; } = [];
    public bool         IncludeSubfolders { get; set; } = true;
    public bool         EnableHashScan    { get; set; } = true;
    public bool         EnableImageScan   { get; set; } = true;
    /// <summary>Hamming distance threshold for image similarity (0–64). Default 10.</summary>
    public int          SimilarityThreshold { get; set; } = 10;
}
