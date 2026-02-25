using System.Text.Json.Serialization;

namespace EnvGuard.Models;

public sealed class Snapshot
{
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Description { get; set; } = "";
    public List<SnapshotEntry> Entries { get; set; } = [];
}

public sealed class SnapshotEntry
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnvScope Scope { get; set; }
}
