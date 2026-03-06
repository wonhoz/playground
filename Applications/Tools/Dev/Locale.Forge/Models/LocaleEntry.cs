using System.ComponentModel;

namespace LocaleForge.Models;

public class LocaleEntry : INotifyPropertyChanged
{
    private string _key = string.Empty;
    private readonly Dictionary<string, string> _values = new();
    private bool _isMissing;
    private bool _isUnused;

    public string Key
    {
        get => _key;
        set { _key = value; OnPropertyChanged(nameof(Key)); }
    }

    public bool IsMissing
    {
        get => _isMissing;
        set { _isMissing = value; OnPropertyChanged(nameof(IsMissing)); }
    }

    public bool IsUnused
    {
        get => _isUnused;
        set { _isUnused = value; OnPropertyChanged(nameof(IsUnused)); }
    }

    public string GetValue(string langCode) =>
        _values.TryGetValue(langCode, out var v) ? v : string.Empty;

    public void SetValue(string langCode, string value)
    {
        _values[langCode] = value;
        OnPropertyChanged($"Value_{langCode}");
    }

    public bool HasValue(string langCode) => _values.ContainsKey(langCode) && !string.IsNullOrEmpty(_values[langCode]);

    public IReadOnlyDictionary<string, string> AllValues => _values;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
