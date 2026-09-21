namespace AIPForge.Services;

/// <summary>덮어쓰기 전 원본 백업 — 공유 폴더를 어지럽히지 않도록 로컬 앱 데이터에 보관</summary>
public static class Backup
{
    private const int KeepPerFile = 20;

    public static string Dir => Path.Combine(AppSettings.DataDir, "backups");

    public static string? Create(string path)
    {
        if (!File.Exists(path)) return null;
        System.IO.Directory.CreateDirectory(Dir);
        var name = Path.GetFileName(path);
        var target = Path.Combine(Dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{name}");
        File.Copy(path, target, overwrite: true);
        Prune(name);
        return target;
    }

    private static void Prune(string name)
    {
        var old = new DirectoryInfo(Dir).GetFiles($"*_{name}")
            .OrderByDescending(f => f.Name)
            .Skip(KeepPerFile);
        foreach (var f in old)
        {
            try { f.Delete(); } catch { /* 사용 중이면 다음 기회에 */ }
        }
    }
}
