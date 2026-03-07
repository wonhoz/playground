namespace Timeline.Craft.Services;

static class ProjectSerializer
{
    static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented       = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Save(TimelineProject proj, string path)
    {
        var json = JsonSerializer.Serialize(proj, Opts);
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
    }

    public static TimelineProject? Load(string path)
    {
        var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        return JsonSerializer.Deserialize<TimelineProject>(json, Opts);
    }
}
