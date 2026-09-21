using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace AIPForge.Services;

/// <summary>Advanced Installer 설치본(AdvancedInstaller.com · advinst.exe) 탐색 및 CLI 실행</summary>
public static class AdvInstaller
{
    private const string CliName = "AdvancedInstaller.com";
    private const string GuiName = "advinst.exe";

    static AdvInstaller() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>
    /// CLI 경로 탐색: 설정값 → 레지스트리 → Program Files 아래 가장 높은 버전.
    /// </summary>
    public static string? LocateCli(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && File.Exists(preferred)) return preferred;

        foreach (var root in new[] { @"SOFTWARE\WOW6432Node\Caphyon\Advanced Installer", @"SOFTWARE\Caphyon\Advanced Installer" })
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(root);
                if (key?.GetValue("Advanced Installer Path") is string dir)
                {
                    var candidate = Path.Combine(dir, "bin", "x86", CliName);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch
            {
                // 레지스트리 접근 실패 시 폴더 탐색으로 진행
            }
        }

        var found = new List<(Version ver, string path)>();
        foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                   Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
        {
            var caphyon = Path.Combine(pf, "Caphyon");
            if (!System.IO.Directory.Exists(caphyon)) continue;
            foreach (var dir in System.IO.Directory.GetDirectories(caphyon, "Advanced Installer*"))
            {
                var cli = Path.Combine(dir, "bin", "x86", CliName);
                if (!File.Exists(cli)) continue;
                var name = Path.GetFileName(dir).Replace("Advanced Installer", "").Trim();
                found.Add((Version.TryParse(name, out var v) ? v : new Version(0, 0), cli));
            }
        }
        return found.OrderByDescending(f => f.ver).Select(f => f.path).FirstOrDefault();
    }

    public static string? GuiPath(string? cliPath)
    {
        if (cliPath == null) return null;
        var gui = Path.Combine(Path.GetDirectoryName(cliPath)!, GuiName);
        return File.Exists(gui) ? gui : null;
    }

    /// <summary>설치된 AI 버전 (폴더명 기준, 예: "18.3")</summary>
    public static string? InstalledVersion(string? cliPath)
    {
        if (cliPath == null) return null;
        var dir = new DirectoryInfo(Path.GetDirectoryName(cliPath)!).Parent?.Parent;
        return dir?.Name.Replace("Advanced Installer", "").Trim();
    }

    public static void OpenInGui(string cliPath, string aipPath)
    {
        var gui = GuiPath(cliPath) ?? throw new FileNotFoundException("advinst.exe를 찾을 수 없습니다.");
        Process.Start(new ProcessStartInfo(gui, $"\"{aipPath}\"") { UseShellExecute = true });
    }

    /// <summary>
    /// CLI 실행 — 출력 줄마다 <paramref name="onLine"/> 호출, 종료 코드 반환.
    /// 취소 시 프로세스 트리를 종료하고 -1 반환.
    /// </summary>
    public static async Task<int> RunAsync(string cliPath, string arguments, Action<string> onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(cliPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdout = PumpAsync(proc.StandardOutput.BaseStream, onLine);
        var stderr = PumpAsync(proc.StandardError.BaseStream, onLine);
        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* 이미 종료됨 */ }
            await Task.WhenAll(stdout, stderr);
            return -1;
        }
        await Task.WhenAll(stdout, stderr);
        return proc.ExitCode;
    }

    public static string BuildArguments(string aipPath, bool rebuild, string? buildName)
    {
        var args = $"{(rebuild ? "/rebuild" : "/build")} \"{aipPath}\"";
        if (!string.IsNullOrEmpty(buildName)) args += $" -buildslist \"{buildName}\"";
        return args;
    }

    public static string RefreshSyncArguments(string aipPath) => $"/edit \"{aipPath}\" /RefreshSync";

    /// <summary>
    /// 줄 단위로 읽되 인코딩을 줄마다 판별 — AI 18.x CLI는 UTF-8을 쓰지만
    /// 버전·환경에 따라 OEM(CP949)로 나오는 경우를 대비해 UTF-8 실패 시 CP949로 해석.
    /// </summary>
    private static async Task PumpAsync(Stream stream, Action<string> onLine)
    {
        var strictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
        var oem = Encoding.GetEncoding(949);
        var line = new List<byte>(256);
        var buffer = new byte[4096];

        void Flush()
        {
            var bytes = line.ToArray();
            line.Clear();
            string text;
            try { text = strictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { text = oem.GetString(bytes); }
            onLine(text.TrimEnd('\r', ' '));
        }

        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == (byte)'\n') Flush();
                else line.Add(buffer[i]);
            }
        }
        if (line.Count > 0) Flush();
    }
}
