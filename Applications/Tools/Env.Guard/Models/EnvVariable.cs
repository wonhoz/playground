namespace EnvGuard.Models;

public sealed class EnvVariable
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public EnvScope Scope { get; set; }
    public bool IsPath => Name.Equals("PATH", StringComparison.OrdinalIgnoreCase)
                       || Name.Equals("Path", StringComparison.OrdinalIgnoreCase);
}

public enum EnvScope
{
    User,
    System
}
