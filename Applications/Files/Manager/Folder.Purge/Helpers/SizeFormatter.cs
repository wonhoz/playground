namespace FolderPurge.Helpers;

public static class SizeFormatter
{
    public static string Format(long bytes) => bytes switch
    {
        0            => "0 B",
        < 1024       => $"{bytes} B",
        < 1048576    => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _            => $"{bytes / 1073741824.0:F2} GB"
    };
}
