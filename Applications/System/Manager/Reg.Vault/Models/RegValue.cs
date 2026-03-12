using Microsoft.Win32;

namespace RegVault.Models;

public class RegValue
{
    public string Name        { get; }
    public RegistryValueKind Kind { get; }
    public object? RawData    { get; }
    public string KindDisplay { get; }
    public string DataDisplay { get; }
    public string DisplayName => string.IsNullOrEmpty(Name) ? "(기본값)" : Name;

    public RegValue(string name, RegistryValueKind kind, object? rawData)
    {
        Name       = name;
        Kind       = kind;
        RawData    = rawData;
        KindDisplay = FormatKind(kind);
        DataDisplay = FormatData(kind, rawData);
    }

    private static string FormatKind(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String       => "REG_SZ",
        RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
        RegistryValueKind.MultiString  => "REG_MULTI_SZ",
        RegistryValueKind.DWord        => "REG_DWORD",
        RegistryValueKind.QWord        => "REG_QWORD",
        RegistryValueKind.Binary       => "REG_BINARY",
        RegistryValueKind.None         => "REG_NONE",
        _                              => "REG_UNKNOWN"
    };

    private static string FormatData(RegistryValueKind kind, object? data)
    {
        if (data == null) return "(없음)";
        return kind switch
        {
            RegistryValueKind.MultiString when data is string[] arr =>
                string.Join(" | ", arr),
            RegistryValueKind.Binary when data is byte[] bytes =>
                BitConverter.ToString(bytes).Replace("-", " "),
            RegistryValueKind.DWord when data is int i =>
                $"0x{(uint)i:X8} ({(uint)i})",
            RegistryValueKind.QWord when data is long l =>
                $"0x{(ulong)l:X16} ({(ulong)l})",
            _ => data.ToString() ?? ""
        };
    }
}
