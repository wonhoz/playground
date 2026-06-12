using System.IO;
using System.Text.Json;

namespace StockRush.Services;

public static class SaveService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Playground", "Stock.Rush");
    private static readonly string FilePath = Path.Combine(Dir, "save.json");

    public static SaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
        }
        catch { /* 손상 시 초기화 */ }
        return new SaveData();
    }

    public static void Save(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 저장 실패 무시 */ }
    }
}
