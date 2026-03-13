using PadForge.Models;

namespace PadForge.Services;

/// <summary>프로파일 JSON 저장·불러오기 서비스</summary>
public class ProfileService
{
    private static readonly string ProfileDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "PadForge", "Profiles");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        PropertyNameCaseInsensitive = true,
        Converters             = { new JsonStringEnumConverter() }
    };

    public ObservableCollection<ControllerProfile> Profiles { get; } = [];
    public ControllerProfile? ActiveProfile { get; private set; }

    public event Action<ControllerProfile>? ProfileActivated;

    public ProfileService()
    {
        Directory.CreateDirectory(ProfileDir);
        LoadAll();
    }

    private void LoadAll()
    {
        foreach (var file in Directory.EnumerateFiles(ProfileDir, "*.json"))
        {
            try
            {
                var json    = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<ControllerProfile>(json, JsonOpts);
                if (profile is not null) Profiles.Add(profile);
            }
            catch { /* 손상된 파일 무시 */ }
        }

        if (Profiles.Count == 0)
            Profiles.Add(CreateDefault());
    }

    public void Save(ControllerProfile profile)
    {
        profile.UpdatedAt = DateTime.Now;
        var path = GetPath(profile.Id);
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOpts), System.Text.Encoding.UTF8);
    }

    public void Delete(ControllerProfile profile)
    {
        var path = GetPath(profile.Id);
        if (File.Exists(path)) File.Delete(path);
        Profiles.Remove(profile);
    }

    public void Activate(ControllerProfile profile)
    {
        ActiveProfile = profile;
        ProfileActivated?.Invoke(profile);
    }

    public void Export(ControllerProfile profile, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOpts), System.Text.Encoding.UTF8);
    }

    public ControllerProfile? Import(string path)
    {
        var json    = File.ReadAllText(path);
        var profile = JsonSerializer.Deserialize<ControllerProfile>(json, JsonOpts);
        if (profile is null) return null;

        profile.Id   = Guid.NewGuid().ToString();  // 충돌 방지
        Profiles.Add(profile);
        Save(profile);
        return profile;
    }

    private static ControllerProfile CreateDefault()
    {
        var p = new ControllerProfile { Name = "기본 프로파일" };
        return p;
    }

    private static string GetPath(string id)
        => Path.Combine(ProfileDir, $"{id}.json");
}
