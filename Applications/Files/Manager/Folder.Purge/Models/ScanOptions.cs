namespace FolderPurge.Models;

public class ScanOptions
{
    public bool ScanEmptyFolders { get; set; } = true;
    public bool ScanVsArtifacts { get; set; } = true;
    public bool ScanEmptyFiles { get; set; } = false;
    public bool UseRecycleBin { get; set; } = true;
    public bool PreviewOnly { get; set; } = false;

    // 제외할 폴더명 (대소문자 무시)
    public HashSet<string> ExcludedFolderNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".svn", ".hg", "node_modules", "__pycache__"
    };

    // VS 아티팩트로 간주할 폴더명
    public HashSet<string> VsArtifactNames { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj"
    };
}
