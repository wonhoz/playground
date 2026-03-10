using EnvGuard.Models;
using Microsoft.Win32;

namespace EnvGuard.Services;

public static class EnvService
{
    public static List<EnvVariable> GetAll()
    {
        var list = new List<EnvVariable>();
        AddFromTarget(list, EnvironmentVariableTarget.User, EnvScope.User);
        AddFromTarget(list, EnvironmentVariableTarget.Machine, EnvScope.System);
        return list.OrderBy(v => v.Scope).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddFromTarget(List<EnvVariable> list, EnvironmentVariableTarget target, EnvScope scope)
    {
        var vars = Environment.GetEnvironmentVariables(target);
        foreach (string key in vars.Keys)
        {
            list.Add(new EnvVariable
            {
                Name = key,
                Value = vars[key]?.ToString() ?? "",
                Scope = scope
            });
        }
    }

    public static void SetVariable(string name, string value, EnvScope scope)
    {
        var target = scope == EnvScope.User ? EnvironmentVariableTarget.User : EnvironmentVariableTarget.Machine;
        Environment.SetEnvironmentVariable(name, value, target);
    }

    public static void DeleteVariable(string name, EnvScope scope)
    {
        var target = scope == EnvScope.User ? EnvironmentVariableTarget.User : EnvironmentVariableTarget.Machine;
        Environment.SetEnvironmentVariable(name, null, target);
    }

    public static List<string> GetPathEntries(EnvScope scope)
    {
        var target = scope == EnvScope.User ? EnvironmentVariableTarget.User : EnvironmentVariableTarget.Machine;
        var path = Environment.GetEnvironmentVariable("PATH", target) ?? "";
        return path.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    public static void SetPathEntries(List<string> entries, EnvScope scope)
    {
        var value = string.Join(';', entries);
        SetVariable("PATH", value, scope);
    }

    public static bool IsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    public static List<DiffEntry> ComputeDiff(List<SnapshotEntry> old, List<EnvVariable> current)
    {
        var result = new List<DiffEntry>();
        var oldDict = old.ToDictionary(e => (e.Name, e.Scope), e => e.Value);
        var curDict = current.ToDictionary(e => (e.Name, e.Scope), e => e.Value);

        foreach (var key in oldDict.Keys.Union(curDict.Keys).Distinct())
        {
            var hasOld = oldDict.TryGetValue(key, out var oldVal);
            var hasCur = curDict.TryGetValue(key, out var curVal);

            if (hasOld && hasCur)
            {
                result.Add(new DiffEntry
                {
                    Name = key.Name, Scope = key.Scope,
                    Kind = oldVal == curVal ? DiffKind.Unchanged : DiffKind.Modified,
                    OldValue = oldVal ?? "", NewValue = curVal ?? ""
                });
            }
            else if (hasOld)
            {
                result.Add(new DiffEntry
                {
                    Name = key.Name, Scope = key.Scope, Kind = DiffKind.Removed,
                    OldValue = oldVal ?? ""
                });
            }
            else
            {
                result.Add(new DiffEntry
                {
                    Name = key.Name, Scope = key.Scope, Kind = DiffKind.Added,
                    NewValue = curVal ?? ""
                });
            }
        }

        return result.OrderBy(d => d.Kind == DiffKind.Unchanged ? 1 : 0)
                     .ThenBy(d => d.Scope).ThenBy(d => d.Name).ToList();
    }
}
