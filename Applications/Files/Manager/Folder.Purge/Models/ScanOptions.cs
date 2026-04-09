namespace FolderPurge.Models;

public class ScanOptions
{
    public bool ScanEmptyFolders { get; set; } = true;
    public bool ScanVsArtifacts { get; set; } = true;
    public bool ScanEmptyFiles { get; set; } = false;
    public bool UseRecycleBin { get; set; } = true;
    public bool PreviewOnly { get; set; } = false;

    // 최근 N일 이내 수정된 폴더는 탐지에서 제외 (LastWriteTime 기준)
    public bool ExcludeRecentFolders { get; set; } = false;
    public int MinAgeDays { get; set; } = 7;

    // 제외할 폴더명 (대소문자 무시) — BuildOptions()에서 AppSettings 값으로 채워짐
    public HashSet<string> ExcludedFolderNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // VS 아티팩트로 간주할 폴더명 — BuildOptions()에서 AppSettings 값으로 채워짐
    public HashSet<string> VsArtifactNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // VS 아티팩트로 간주할 파일 확장자 — BuildOptions()에서 AppSettings 값으로 채워짐
    public HashSet<string> VsArtifactFileExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
