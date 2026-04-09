using SysClean.Models;

namespace SysClean.Services;

public class CleanerService
{
    public List<CleanTarget> GetTargets()
    {
        var temp = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return
        [
            // ── Windows 시스템 ──────────────────────────────────────────
            new CleanTarget { IsGroup = true, Name = "Windows 시스템", Category = "system", CleanerId = "grp_system" },

            new CleanTarget
            {
                Name = "임시 파일",
                Description = $"사용자 임시 폴더 ({temp})",
                Category = "system", CleanerId = "win_temp",
                Paths = [temp, @"C:\Windows\Temp"]
            },
            new CleanTarget
            {
                Name = "휴지통",
                Description = "모든 드라이브의 휴지통",
                Category = "system", CleanerId = "recycle_bin",
                Paths = []
            },
            new CleanTarget
            {
                Name = "Windows 업데이트 캐시",
                Description = @"C:\Windows\SoftwareDistribution\Download",
                Category = "system", CleanerId = "wu_cache",
                Paths = [@"C:\Windows\SoftwareDistribution\Download"]
            },
            new CleanTarget
            {
                Name = "프리페치 파일",
                Description = @"C:\Windows\Prefetch — 앱 실행 가속 캐시",
                Category = "system", CleanerId = "prefetch",
                Paths = [@"C:\Windows\Prefetch"]
            },
            new CleanTarget
            {
                Name = "썸네일 캐시",
                Description = "파일 탐색기 이미지 미리보기 캐시",
                Category = "system", CleanerId = "thumbnail",
                Paths = [Path.Combine(localAppData, @"Microsoft\Windows\Explorer")]
            },
            new CleanTarget
            {
                Name = "최근 문서 기록",
                Description = "최근에 열었던 파일 목록",
                Category = "system", CleanerId = "recent_docs",
                Paths = [Path.Combine(appData, @"Microsoft\Windows\Recent")]
            },
            new CleanTarget
            {
                Name = "메모리 덤프 파일",
                Description = @"C:\Windows\Minidump — 시스템 충돌 덤프",
                Category = "system", CleanerId = "minidump",
                Paths = [@"C:\Windows\Minidump"]
            },

            // ── 브라우저 캐시 ──────────────────────────────────────────
            new CleanTarget { IsGroup = true, Name = "브라우저 캐시", Category = "browser_cache", CleanerId = "grp_cache" },

            new CleanTarget
            {
                Name = "Chrome 캐시",
                Description = "Google Chrome 브라우저 캐시",
                Category = "browser_cache", CleanerId = "chrome_cache",
                Paths = [Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"),
                         Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache")]
            },
            new CleanTarget
            {
                Name = "Edge 캐시",
                Description = "Microsoft Edge 브라우저 캐시",
                Category = "browser_cache", CleanerId = "edge_cache",
                Paths = [Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"),
                         Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache")]
            },
            new CleanTarget
            {
                Name = "Firefox 캐시",
                Description = "Mozilla Firefox 브라우저 캐시",
                Category = "browser_cache", CleanerId = "firefox_cache",
                Paths = FindFirefoxProfilePaths(appData, "cache2")
            },
            new CleanTarget
            {
                Name = "Brave 캐시",
                Description = "Brave 브라우저 캐시",
                Category = "browser_cache", CleanerId = "brave_cache",
                Paths = [Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
                         Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Code Cache")]
            },
            new CleanTarget
            {
                Name = "Vivaldi 캐시",
                Description = "Vivaldi 브라우저 캐시",
                Category = "browser_cache", CleanerId = "vivaldi_cache",
                Paths = [Path.Combine(localAppData, @"Vivaldi\User Data\Default\Cache"),
                         Path.Combine(localAppData, @"Vivaldi\User Data\Default\Code Cache")]
            },
            new CleanTarget
            {
                Name = "Opera 캐시",
                Description = "Opera 브라우저 캐시",
                Category = "browser_cache", CleanerId = "opera_cache",
                Paths = [Path.Combine(appData, @"Opera Software\Opera Stable\Cache"),
                         Path.Combine(localAppData, @"Programs\Opera\Cache")]
            },

            // ── 브라우저 기록/쿠키 ──────────────────────────────────────
            new CleanTarget { IsGroup = true, Name = "브라우저 기록 · 쿠키", Category = "browser_history", CleanerId = "grp_history",
                IsSelected = false },

            new CleanTarget
            {
                Name = "Chrome 방문 기록",
                Description = "Google Chrome 검색·방문 기록",
                Category = "browser_history", CleanerId = "chrome_history",
                IsSelected = false,
                Paths = [Path.Combine(localAppData, @"Google\Chrome\User Data\Default\History")]
            },
            new CleanTarget
            {
                Name = "Chrome 쿠키",
                Description = "Google Chrome 로그인 쿠키 (사이트에서 로그아웃됩니다)",
                Category = "browser_history", CleanerId = "chrome_cookies",
                IsSelected = false,
                Paths = [Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cookies"),
                         Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Network\Cookies")]
            },
            new CleanTarget
            {
                Name = "Edge 방문 기록",
                Description = "Microsoft Edge 검색·방문 기록",
                Category = "browser_history", CleanerId = "edge_history",
                IsSelected = false,
                Paths = [Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\History")]
            },
            new CleanTarget
            {
                Name = "Edge 쿠키",
                Description = "Microsoft Edge 로그인 쿠키 (사이트에서 로그아웃됩니다)",
                Category = "browser_history", CleanerId = "edge_cookies",
                IsSelected = false,
                Paths = [Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cookies"),
                         Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Network\Cookies")]
            },
            new CleanTarget
            {
                Name = "Firefox 방문 기록",
                Description = "Mozilla Firefox 방문 기록",
                Category = "browser_history", CleanerId = "firefox_history",
                IsSelected = false,
                Paths = FindFirefoxProfilePaths(appData, "places.sqlite")
            },

            // ── 개발자 캐시 ──────────────────────────────────────────
            new CleanTarget { IsGroup = true, Name = "개발자 캐시", Category = "dev_cache", CleanerId = "grp_dev",
                IsSelected = false },

            new CleanTarget
            {
                Name = "npm 캐시",
                Description = "Node.js 패키지 캐시",
                Category = "dev_cache", CleanerId = "npm_cache",
                IsSelected = false,
                Paths = [Path.Combine(appData, "npm-cache")]
            },
            new CleanTarget
            {
                Name = "NuGet 캐시",
                Description = ".NET 패키지 캐시",
                Category = "dev_cache", CleanerId = "nuget_cache",
                IsSelected = false,
                Paths = [Path.Combine(userProfile, @".nuget\packages")]
            },
            new CleanTarget
            {
                Name = "pip 캐시",
                Description = "Python 패키지 캐시",
                Category = "dev_cache", CleanerId = "pip_cache",
                IsSelected = false,
                Paths = [Path.Combine(localAppData, @"pip\cache")]
            },
            new CleanTarget
            {
                Name = "Gradle 캐시",
                Description = "Android/Java Gradle 빌드 캐시",
                Category = "dev_cache", CleanerId = "gradle_cache",
                IsSelected = false,
                Paths = [Path.Combine(userProfile, @".gradle\caches")]
            },
            new CleanTarget
            {
                Name = "Maven 캐시",
                Description = "Java Maven 빌드 캐시",
                Category = "dev_cache", CleanerId = "maven_cache",
                IsSelected = false,
                Paths = [Path.Combine(userProfile, @".m2\repository")]
            },
        ];
    }

    private static string[] FindFirefoxProfilePaths(string appData, string subPath)
    {
        var profilesBase = Path.Combine(appData, @"Mozilla\Firefox\Profiles");
        if (!Directory.Exists(profilesBase)) return [];

        return Directory.GetDirectories(profilesBase)
            .Select(d => Path.Combine(d, subPath))
            .ToArray();
    }

    public List<string> GetPreviewFiles(CleanTarget target, int maxFiles = 200)
    {
        if (target.IsGroup || target.Paths.Length == 0) return [];
        if (target.CleanerId == "recycle_bin") return ["(휴지통 — 파일 목록 표시 불가)"];

        var result = new List<string>();
        bool dbOnly = target.CleanerId == "thumbnail";
        bool sqliteOnly = target.CleanerId == "firefox_history";

        foreach (var path in target.Paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    result.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        if (dbOnly && ext != ".db") continue;
                        if (sqliteOnly && ext != ".sqlite") continue;
                        result.Add(f);
                        if (result.Count >= maxFiles) return result;
                    }
                }
            }
            catch { /* 접근 거부 무시 */ }
        }
        return result;
    }

    public async Task<long> ScanTargetAsync(CleanTarget target, CancellationToken ct)
    {
        if (target.IsGroup) return 0;

        return await Task.Run(() =>
        {
            long total = 0;

            if (target.CleanerId == "recycle_bin")
                return GetRecycleBinSize();

            foreach (var path in target.Paths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path))
                    {
                        total += new FileInfo(path).Length;
                    }
                    else if (Directory.Exists(path))
                    {
                        total += GetDirectorySize(path, ct);
                    }
                }
                catch { /* 접근 거부 무시 */ }
            }
            return total;
        }, ct);
    }

    private static long GetDirectorySize(string path, CancellationToken ct)
    {
        long size = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { size += new FileInfo(f).Length; } catch { }
            }
        }
        catch { }
        return size;
    }

    private static long GetRecycleBinSize()
    {
        long total = 0;
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            var recyclePath = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
            if (!Directory.Exists(recyclePath)) continue;
            try
            {
                total += GetDirectorySize(recyclePath, CancellationToken.None);
            }
            catch { }
        }
        return total;
    }

    public async Task<(long cleaned, int errors)> CleanTargetAsync(CleanTarget target, CancellationToken ct)
    {
        if (target.IsGroup) return (0, 0);

        return await Task.Run(() =>
        {
            long cleaned = 0;
            int errors = 0;

            if (target.CleanerId == "recycle_bin")
            {
                EmptyRecycleBin();
                return (target.Size > 0 ? target.Size : 0, 0);
            }

            foreach (var path in target.Paths)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(path))
                    {
                        var size = new FileInfo(path).Length;
                        File.Delete(path);
                        cleaned += size;
                    }
                    else if (Directory.Exists(path))
                    {
                        (long s, int e) = CleanDirectory(path, target.CleanerId, ct);
                        cleaned += s;
                        errors += e;
                    }
                }
                catch { errors++; }
            }
            return (cleaned, errors);
        }, ct);
    }

    private static (long, int) CleanDirectory(string path, string cleanerId, CancellationToken ct)
    {
        long cleaned = 0;
        int errors = 0;

        // 썸네일 캐시는 .db 파일만 삭제
        bool dbOnly = cleanerId == "thumbnail";
        // Firefox history는 sqlite 파일만
        bool sqliteOnly = cleanerId == "firefox_history";

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ext = Path.GetExtension(file).ToLower();
                if (dbOnly && ext != ".db") continue;
                if (sqliteOnly && ext != ".sqlite") continue;

                var size = new FileInfo(file).Length;
                File.Delete(file);
                cleaned += size;
            }
            catch { errors++; }
        }

        if (!dbOnly && !sqliteOnly)
        {
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                                         .OrderByDescending(d => d.Length))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                        Directory.Delete(dir);
                }
                catch { }
            }
        }

        return (cleaned, errors);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath,
        uint dwFlags);

    private static void EmptyRecycleBin()
    {
        const uint SHERB_NOCONFIRMATION = 0x00000001;
        const uint SHERB_NOPROGRESSUI   = 0x00000002;
        const uint SHERB_NOSOUND        = 0x00000004;
        SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
    }
}
