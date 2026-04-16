using System.IO;
using System.Text.Json;

namespace MarkView.Services;

public class AppSettings
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarkView", "settings.json");

    public string Theme { get; set; } = "dark";
    public List<string> RecentFiles { get; set; } = [];
    public List<string> PinnedFiles { get; set; } = [];
    public List<string> OpenFiles { get; set; } = [];
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public double EditorFontSize { get; set; } = 13;
    public bool IsEditMode { get; set; } = false;
    public bool IsTocVisible { get; set; } = false;
    public double TocWidth { get; set; } = 220;
    public double EditorSplitRatio { get; set; } = 0.5;
    public int ActiveTabIndex { get; set; } = 0;
    public double PreviewFontSize { get; set; } = 15;
    public bool IsMaximized { get; set; } = false;
    public bool IsFocusMode { get; set; } = false;
    public bool IsWordWrap { get; set; } = true;
    public string ExportDir { get; set; } = "";
    public string PdfPageSize { get; set; } = "a4"; // a4, letter, legal
    public double PdfMarginCm { get; set; } = 1.0;
    public int AutoSaveIntervalSec { get; set; } = 30; // 30, 60, 120
    public Dictionary<string, int> FileCursorPositions { get; set; } = [];
    public Dictionary<string, double> FileScrollPositions { get; set; } = [];
    public Dictionary<string, List<int>> Bookmarks { get; set; } = [];
    // 문서별 마지막 내보내기 경로 (doc.FilePath → last export full path)
    public Dictionary<string, string> LastExportPaths { get; set; } = [];

    private const int MaxRecentFiles = 20;

    public void AddRecentFile(string path)
    {
        RecentFiles.Remove(path);
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
    }

    public void TogglePin(string path)
    {
        if (PinnedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            PinnedFiles.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        else
            PinnedFiles.Insert(0, path);
    }

    public bool IsPinned(string path) =>
        PinnedFiles.Contains(path, StringComparer.OrdinalIgnoreCase);

    public static AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // 손상된 설정 파일은 백업 후 초기화
            try
            {
                var backup = _path + ".bak";
                File.Copy(_path, backup, overwrite: true);
                File.Delete(_path);
            }
            catch { }
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
