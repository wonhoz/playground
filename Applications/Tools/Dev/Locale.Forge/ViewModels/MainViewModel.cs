using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using LocaleForge.Models;
using LocaleForge.Parsers;

namespace LocaleForge.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // 로드된 언어 파일 목록
    public ObservableCollection<LocaleFile> LoadedFiles { get; } = new();

    // 모든 키 통합 뷰 (언어 컬럼별로 값을 담음)
    public ObservableCollection<LocaleEntry> Entries { get; } = new();

    // 현재 언어 코드 목록 (컬럼 헤더용)
    public ObservableCollection<string> Languages { get; } = new();

    private string _statusText = "파일을 드래그&드롭하거나 열기 버튼으로 불러오세요.";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    private int _missingCount;
    public int MissingCount
    {
        get => _missingCount;
        set { _missingCount = value; OnPropertyChanged(nameof(MissingCount)); OnPropertyChanged(nameof(StatsText)); }
    }

    private int _unusedCount;
    public int UnusedCount
    {
        get => _unusedCount;
        set { _unusedCount = value; OnPropertyChanged(nameof(UnusedCount)); OnPropertyChanged(nameof(StatsText)); }
    }

    public string StatsText => $"키: {Entries.Count}  누락: {MissingCount}  미사용: {UnusedCount}";

    // 파일 로드
    public bool LoadFile(string filePath)
    {
        try
        {
            var format = LocaleFile.DetectFormat(filePath);
            var parser = ParserRegistry.Get(format);
            var entries = parser.Parse(filePath);

            // 언어 코드 감지 (파일명 기준: en.json / messages_ko.properties 등)
            var langCode = DetectLanguageCode(filePath);

            // 이미 같은 경로면 교체
            var existing = LoadedFiles.FirstOrDefault(f => f.FilePath == filePath);
            if (existing != null)
                LoadedFiles.Remove(existing);

            var file = new LocaleFile
            {
                FilePath = filePath,
                LanguageCode = langCode,
                Format = format,
                Entries = entries
            };
            LoadedFiles.Add(file);

            if (!Languages.Contains(langCode))
                Languages.Add(langCode);

            RebuildEntries();
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"오류: {ex.Message}";
            return false;
        }
    }

    public void RemoveFile(LocaleFile file)
    {
        LoadedFiles.Remove(file);
        // 해당 언어 코드를 쓰는 파일이 더 없으면 언어 제거
        if (!LoadedFiles.Any(f => f.LanguageCode == file.LanguageCode))
            Languages.Remove(file.LanguageCode);
        RebuildEntries();
    }

    public void RebuildEntries()
    {
        Entries.Clear();

        // 모든 키 수집
        var allKeys = LoadedFiles
            .SelectMany(f => f.Entries.Keys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        foreach (var key in allKeys)
        {
            var entry = new LocaleEntry { Key = key };
            foreach (var file in LoadedFiles)
            {
                if (file.Entries.TryGetValue(key, out var val))
                    entry.SetValue(file.LanguageCode, val);
            }
            Entries.Add(entry);
        }

        RefreshMissingFlags();
    }

    public void RefreshMissingFlags()
    {
        int missing = 0;
        foreach (var entry in Entries)
        {
            bool hasMissing = Languages.Any(lang => !entry.HasValue(lang));
            entry.IsMissing = hasMissing;
            if (hasMissing) missing++;
        }
        MissingCount = missing;
        StatusText = $"파일 {LoadedFiles.Count}개, 키 {Entries.Count}개 로드됨.";
    }

    public void MarkUnused(HashSet<string> usedKeys)
    {
        int unused = 0;
        foreach (var entry in Entries)
        {
            entry.IsUnused = !usedKeys.Contains(entry.Key);
            if (entry.IsUnused) unused++;
        }
        UnusedCount = unused;
    }

    public void SaveFile(LocaleFile file)
    {
        var parser = ParserRegistry.Get(file.Format);
        // 현재 Entries에서 해당 언어 값 추출
        var updated = new Dictionary<string, string>();
        foreach (var entry in Entries)
        {
            var val = entry.GetValue(file.LanguageCode);
            if (!string.IsNullOrEmpty(val) || file.Entries.ContainsKey(entry.Key))
                updated[entry.Key] = val;
        }
        parser.Save(file.FilePath, updated);
        file.Entries = updated;
    }

    public void SetEntryValue(string key, string langCode, string value)
    {
        var entry = Entries.FirstOrDefault(e => e.Key == key);
        if (entry != null)
        {
            entry.SetValue(langCode, value);
            RefreshMissingFlags();
        }
    }

    public void AddKey(string key)
    {
        if (Entries.Any(e => e.Key == key)) return;
        var entry = new LocaleEntry { Key = key };
        entry.IsMissing = Languages.Count > 0;
        // 정렬 위치 찾기
        int idx = Entries.Count;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (string.Compare(Entries[i].Key, key, StringComparison.Ordinal) > 0)
            { idx = i; break; }
        }
        Entries.Insert(idx, entry);
        RefreshMissingFlags();
    }

    public void DeleteKey(string key)
    {
        var entry = Entries.FirstOrDefault(e => e.Key == key);
        if (entry != null) Entries.Remove(entry);
        RefreshMissingFlags();
    }

    private static string DetectLanguageCode(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        // 예: en, ko, ja, zh-CN, messages_en, strings_ko
        var parts = name.Split('_', '-', '.');
        // 마지막 파트가 언어 코드처럼 보이면 사용
        var last = parts[^1].ToLowerInvariant();
        if (IsLikelyLangCode(last)) return last;
        if (parts.Length > 1)
        {
            var second = parts[^2].ToLowerInvariant();
            if (IsLikelyLangCode(second)) return $"{second}-{last}";
        }
        return name;
    }

    private static bool IsLikelyLangCode(string s) =>
        s.Length is 2 or 3 && s.All(char.IsLetter);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
