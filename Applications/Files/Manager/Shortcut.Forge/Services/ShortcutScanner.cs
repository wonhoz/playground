namespace ShortcutForge.Services;

public static class ShortcutScanner
{
    /// <summary>폴더 내 모든 .lnk 파일을 스캔합니다. recurse=true면 하위 폴더 포함.</summary>
    public static IEnumerable<ShortcutEntry> Scan(string folder, bool recurse = false)
    {
        if (!Directory.Exists(folder)) yield break;

        var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk", option))
        {
            ShortcutEntry entry;
            try   { entry = ShellLinkService.Load(lnk); }
            catch { entry = new ShortcutEntry { Name = Path.GetFileNameWithoutExtension(lnk), LnkPath = lnk, Status = ShortcutStatus.Missing }; }

            // 아이콘 로드
            var iconSrc = string.IsNullOrEmpty(entry.IconPath)
                ? IconExtractor.Extract(entry.TargetPath)
                : IconExtractor.Extract(entry.IconPath, entry.IconIndex);
            entry.IconImage = iconSrc;

            yield return entry;
        }
    }

    /// <summary>알려진 바로가기 폴더 목록 (바탕화면, 시작 메뉴 등).</summary>
    public static IEnumerable<string> WellKnownFolders()
    {
        var paths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
        };
        return paths.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Distinct();
    }
}
