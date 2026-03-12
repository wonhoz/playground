using System.Text;
using SysClean.Models;

namespace SysClean.Services;

/// <summary>
/// 레지스트리 정리 전 .reg 형식으로 백업합니다.
/// 복원은 백업 파일을 더블클릭하면 됩니다.
/// </summary>
public static class BackupService
{
    public static string BackupFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "SysClean", "Backups");

    /// <summary>
    /// 이슈 목록에 해당하는 레지스트리 항목을 .reg 파일로 백업합니다.
    /// </summary>
    /// <returns>생성된 백업 파일 경로</returns>
    public static string BackupRegistryIssues(IEnumerable<RegistryIssue> issues)
    {
        Directory.CreateDirectory(BackupFolder);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(BackupFolder, $"registry_{timestamp}.reg");

        // .reg 파일은 UTF-16 LE with BOM
        using var w = new StreamWriter(path, false, new UnicodeEncoding(false, true));
        w.WriteLine("Windows Registry Editor Version 5.00");
        w.WriteLine();

        foreach (var group in issues.GroupBy(i => i.KeyPath))
            ExportKey(w, group.Key, group.ToList());

        return path;
    }

    private static void ExportKey(StreamWriter w, string keyPath, List<RegistryIssue> issues)
    {
        if (!keyPath.StartsWith(@"HKLM\")) return;
        var subPath = keyPath[@"HKLM\".Length..];

        // 설치경로/제거프로그램 카테고리는 서브키 전체를 백업
        bool isSubKeyDelete = issues.Any(i => i.Category is "설치 경로" or "제거 프로그램");
        if (isSubKeyDelete)
        {
            ExportSubtree(w, Registry.LocalMachine, subPath, $@"HKEY_LOCAL_MACHINE\{subPath}");
        }
        else
        {
            // 특정 값만 백업
            using var key = Registry.LocalMachine.OpenSubKey(subPath);
            if (key == null) return;

            w.WriteLine($@"[HKEY_LOCAL_MACHINE\{subPath}]");
            foreach (var issue in issues)
                WriteValue(w, key, issue.ValueName);
            w.WriteLine();
        }
    }

    private static void ExportSubtree(StreamWriter w, RegistryKey root, string subPath, string fullPath)
    {
        using var key = root.OpenSubKey(subPath);
        if (key == null) return;

        w.WriteLine($"[{fullPath}]");
        foreach (var valName in key.GetValueNames())
            WriteValue(w, key, valName);
        w.WriteLine();

        foreach (var sub in key.GetSubKeyNames())
            ExportSubtree(w, root, $@"{subPath}\{sub}", $@"{fullPath}\{sub}");
    }

    private static void WriteValue(StreamWriter w, RegistryKey key, string valueName)
    {
        try
        {
            var val = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (val == null) return;

            var kind = key.GetValueKind(valueName);
            var nameStr = valueName == "" ? "@" : $"\"{EscapeReg(valueName)}\"";

            switch (kind)
            {
                case RegistryValueKind.String:
                    w.WriteLine($"{nameStr}=\"{EscapeReg(val.ToString() ?? "")}\"");
                    break;

                case RegistryValueKind.DWord:
                    w.WriteLine($"{nameStr}=dword:{unchecked((uint)(int)val):x8}");
                    break;

                case RegistryValueKind.QWord:
                    var qBytes = BitConverter.GetBytes(unchecked((ulong)(long)val));
                    w.WriteLine($"{nameStr}=hex(b):{string.Join(",", qBytes.Select(b => b.ToString("x2")))}");
                    break;

                case RegistryValueKind.ExpandString:
                    var expBytes = Encoding.Unicode.GetBytes(val.ToString() + "\0");
                    WriteLongHex(w, nameStr, "hex(2)", expBytes);
                    break;

                case RegistryValueKind.MultiString:
                    var multiBytes = new List<byte>();
                    foreach (var s in (string[])val)
                        multiBytes.AddRange(Encoding.Unicode.GetBytes(s + "\0"));
                    multiBytes.AddRange(new byte[] { 0, 0 });
                    WriteLongHex(w, nameStr, "hex(7)", [.. multiBytes]);
                    break;

                case RegistryValueKind.Binary:
                    WriteLongHex(w, nameStr, "hex", (byte[])val);
                    break;
            }
        }
        catch { /* 읽기 실패 항목은 건너뜀 */ }
    }

    /// <summary>.reg 형식의 긴 hex 값을 줄바꿈(\)으로 나눠 씁니다.</summary>
    private static void WriteLongHex(StreamWriter w, string nameStr, string typePfx, byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            w.WriteLine($"{nameStr}={typePfx}:");
            return;
        }

        const int perLine = 25; // 줄당 바이트 수
        var sb = new StringBuilder($"{nameStr}={typePfx}:");

        for (int i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("x2"));
            if (i < bytes.Length - 1)
            {
                sb.Append(',');
                if ((i + 1) % perLine == 0)
                {
                    sb.Append('\\');
                    w.WriteLine(sb);
                    sb.Clear();
                    sb.Append("  ");
                }
            }
        }
        w.WriteLine(sb);
    }

    private static string EscapeReg(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public static void OpenBackupFolder()
    {
        Directory.CreateDirectory(BackupFolder);
        System.Diagnostics.Process.Start("explorer.exe", BackupFolder);
    }
}
