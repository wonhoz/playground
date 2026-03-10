using WebShot.Models;

namespace WebShot.Services;

public class HistoryService
{
    private const int MaxEntries = 20;
    private readonly string _historyPath;
    private List<HistoryEntry> _entries = new();

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public HistoryService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "WebShot");
        Directory.CreateDirectory(dir);
        _historyPath = Path.Combine(dir, "history.json");
        Load();
    }

    public void Add(HistoryEntry entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        Save();
    }

    public void Remove(HistoryEntry entry)
    {
        _entries.Remove(entry);
        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_historyPath)) return;
            var json = File.ReadAllText(_historyPath);
            _entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new();
        }
        catch { _entries = new(); }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
        catch { }
    }
}
