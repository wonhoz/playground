namespace TableCraft.Models;

public enum FilterOperator
{
    Contains, NotContains,
    Equals, NotEquals,
    StartsWith, EndsWith,
    GreaterThan, LessThan, Between,
    IsEmpty, IsNotEmpty,
    Regex
}

public class FilterCondition
{
    public int            ColumnIndex     { get; set; }
    public string         ColumnName      { get; set; } = "";
    public FilterOperator Operator        { get; set; } = FilterOperator.Contains;
    public string         Value           { get; set; } = "";
    public string         Value2          { get; set; } = "";   // Between의 두 번째 값
    public bool           CaseSensitive   { get; set; } = false;

    public string Summary => Operator switch
    {
        FilterOperator.Contains    => $"포함: {Value}",
        FilterOperator.NotContains => $"포함 안함: {Value}",
        FilterOperator.Equals      => $"= {Value}",
        FilterOperator.NotEquals   => $"≠ {Value}",
        FilterOperator.StartsWith  => $"시작: {Value}",
        FilterOperator.EndsWith    => $"끝: {Value}",
        FilterOperator.GreaterThan => $"> {Value}",
        FilterOperator.LessThan    => $"< {Value}",
        FilterOperator.Between     => $"{Value} ~ {Value2}",
        FilterOperator.IsEmpty     => "비어있음",
        FilterOperator.IsNotEmpty  => "비어있지 않음",
        FilterOperator.Regex       => $"정규식: {Value}",
        _                          => ""
    };
}
