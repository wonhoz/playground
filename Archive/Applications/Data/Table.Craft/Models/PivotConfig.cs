namespace TableCraft.Models;

public enum PivotAgg { Count, Sum, Avg, Min, Max }

public class PivotConfig
{
    public int      RowColumnIndex { get; set; } = -1;
    public int      ColColumnIndex { get; set; } = -1;
    public int      ValColumnIndex { get; set; } = -1;
    public PivotAgg Aggregation    { get; set; } = PivotAgg.Count;
}
