using System.IO;

namespace AudioCast.Services;

/// <summary>ffmpeg.exe / ffprobe.exe 경로 탐색 (지정 경로 → 앱 폴더 → PATH → 일반 설치 경로 → winget)</summary>
public static class FfmpegLocator
{
    public static string? Find(string? preferredPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
            return preferredPath;

        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local)) return local;

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* 잘못된 PATH 항목 무시 */ }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] common =
        [
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"scoop\shims\ffmpeg.exe"),
            Path.Combine(localAppData, @"Microsoft\WinGet\Links\ffmpeg.exe"),
        ];
        foreach (var path in common)
            if (File.Exists(path)) return path;

        var wingetPkgs = Path.Combine(localAppData, @"Microsoft\WinGet\Packages");
        if (Directory.Exists(wingetPkgs))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(wingetPkgs, "*FFmpeg*"))
                    foreach (var exe in Directory.GetFiles(dir, "ffmpeg.exe", SearchOption.AllDirectories))
                        return exe;
            }
            catch { /* 권한 문제 등 무시 */ }
        }
        return null;
    }

    /// <summary>ffmpeg.exe와 같은 폴더의 ffprobe.exe (winget Links 심볼릭 링크도 동일 폴더에 생성됨)</summary>
    public static string? FindProbe(string ffmpegPath)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpegPath) ?? "", "ffprobe.exe");
        return File.Exists(probe) ? probe : null;
    }
}
