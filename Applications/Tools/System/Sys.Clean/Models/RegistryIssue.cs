namespace SysClean.Models;

public class RegistryIssue
{
    public string Category { get; init; } = "";
    public string KeyPath { get; init; } = "";
    public string ValueName { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsFixed { get; set; }
}
