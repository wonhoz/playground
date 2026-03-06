namespace Mosaic.Forge.Services;

sealed class TileEntry
{
    public required string FilePath { get; init; }
    public double LabL   { get; init; }
    public double LabA   { get; init; }
    public double LabB   { get; init; }
    public int    Index  { get; init; }
    public int    UseCount { get; set; }
}
