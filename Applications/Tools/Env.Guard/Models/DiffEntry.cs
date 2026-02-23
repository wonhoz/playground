namespace EnvGuard.Models;

public sealed class DiffEntry
{
    public string Name { get; set; } = "";
    public EnvScope Scope { get; set; }
    public DiffKind Kind { get; set; }
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
}

public enum DiffKind
{
    Added,
    Removed,
    Modified,
    Unchanged
}
