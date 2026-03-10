namespace Layout.Forge.Services;

using System.Runtime.InteropServices;

/// <summary>
/// Windows Scancode Map 레지스트리 읽기/쓰기.
/// HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layout\Scancode Map
/// 관리자 권한 필요.
/// </summary>
public static class ScancodeService
{
    const string RegPath  = @"SYSTEM\CurrentControlSet\Control\Keyboard Layout";
    const string RegValue = "Scancode Map";

    /// <summary>현재 레지스트리의 매핑을 읽어 반환. 없으면 빈 딕셔너리.</summary>
    public static Dictionary<ushort, ushort> Read()
    {
        var result = new Dictionary<ushort, ushort>();
        using var key = Registry.LocalMachine.OpenSubKey(RegPath);
        if (key?.GetValue(RegValue) is not byte[] bytes || bytes.Length < 16) return result;

        int count = BitConverter.ToInt32(bytes, 8) - 1; // null terminator 제외
        for (int i = 0; i < count; i++)
        {
            int off = 12 + i * 4;
            if (off + 4 > bytes.Length) break;
            ushort dst = BitConverter.ToUInt16(bytes, off);
            ushort src = BitConverter.ToUInt16(bytes, off + 2);
            if (src != 0) result[src] = dst;
        }
        return result;
    }

    /// <summary>매핑을 레지스트리에 적용. 재부팅 후 적용됨.</summary>
    public static void Apply(IReadOnlyDictionary<ushort, ushort> mappings)
    {
        int count  = mappings.Count + 1; // +1 null terminator
        var bytes  = new byte[8 + 4 + count * 4];

        // header: 8 zero bytes
        // count
        bytes[8] = (byte)( count        & 0xFF);
        bytes[9] = (byte)((count >> 8)  & 0xFF);

        int off = 12;
        foreach (var (src, dst) in mappings)
        {
            bytes[off++] = (byte)( dst        & 0xFF);
            bytes[off++] = (byte)((dst >> 8)  & 0xFF);
            bytes[off++] = (byte)( src        & 0xFF);
            bytes[off++] = (byte)((src >> 8)  & 0xFF);
        }
        // null terminator: already zero

        using var regKey = Registry.LocalMachine.OpenSubKey(RegPath, writable: true)
            ?? throw new InvalidOperationException("레지스트리 키를 쓰기 모드로 열 수 없습니다.\n관리자 권한으로 실행했는지 확인하세요.");
        regKey.SetValue(RegValue, bytes, RegistryValueKind.Binary);
    }

    /// <summary>Scancode Map 항목 삭제 (기본값 복원). 재부팅 후 적용됨.</summary>
    public static void Clear()
    {
        using var regKey = Registry.LocalMachine.OpenSubKey(RegPath, writable: true);
        regKey?.DeleteValue(RegValue, throwOnMissingValue: false);
    }
}
