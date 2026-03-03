namespace TableCraft.Models;

/// <summary>컬럼 집계 결과</summary>
public class AggregateResult
{
    public string ColumnName { get; init; } = "";
    public long   Count      { get; init; }
    public long   Distinct   { get; init; }
    public long   Empty      { get; init; }
    public double Sum        { get; init; }
    public double Avg        { get; init; }
    public string Min        { get; init; } = "";
    public string Max        { get; init; } = "";
    public bool   IsNumeric  { get; init; }
}
