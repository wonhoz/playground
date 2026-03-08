namespace Layout.Forge.Services;

using Layout.Forge.Models;

public static class ProfileService
{
    static readonly string ProfileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Layout.Forge", "Profiles");

    static ProfileService() => Directory.CreateDirectory(ProfileDir);

    public static void Save(KeyProfile profile)
    {
        var path = Path.Combine(ProfileDir, Sanitize(profile.Name) + ".json");
        var dto  = new ProfileDto
        {
            Name     = profile.Name,
            Mappings = profile.Mappings.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)
        };
        File.WriteAllText(path, JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static List<KeyProfile> LoadAll()
    {
        var result = new List<KeyProfile>();
        foreach (var file in Directory.GetFiles(ProfileDir, "*.json"))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<ProfileDto>(File.ReadAllText(file));
                if (dto == null) continue;
                result.Add(new KeyProfile
                {
                    Name     = dto.Name,
                    Mappings = dto.Mappings.ToDictionary(
                        kv => ushort.Parse(kv.Key),
                        kv => kv.Value)
                });
            }
            catch { }
        }
        return result;
    }

    public static void Delete(KeyProfile profile)
    {
        var path = Path.Combine(ProfileDir, Sanitize(profile.Name) + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    static string Sanitize(string name)
        => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    class ProfileDto
    {
        public string Name { get; set; } = "";
        public Dictionary<string, ushort> Mappings { get; set; } = new();
    }
}
