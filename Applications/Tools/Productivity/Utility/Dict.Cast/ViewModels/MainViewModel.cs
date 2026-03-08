namespace Dict.Cast.ViewModels;

using Dict.Cast.Models;
using Dict.Cast.Services;

public class MainViewModel : INotifyPropertyChanged
{
    readonly DictionaryService _dict;
    readonly AppDatabase       _db;

    string _searchText   = "";
    string _currentWord  = "";
    string _statusText   = "";
    bool   _isInWordlist;
    bool   _isBuilding;
    int    _buildProgress;
    string _buildMessage = "사전 초기화를 준비 중입니다...";

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(DictionaryService dict, AppDatabase db)
    {
        _dict = dict;
        _db   = db;
    }

    // ── 검색 ─────────────────────────────────────────────────────────────────

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnChanged(); }
    }

    public string CurrentWord
    {
        get => _currentWord;
        set { _currentWord = value; OnChanged(); OnChanged(nameof(HasResult)); OnChanged(nameof(WordHeading)); }
    }

    public string WordHeading => _currentWord.Length > 0
        ? _currentWord[0].ToString().ToUpperInvariant() + _currentWord[1..]
        : "";

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnChanged(); }
    }

    public bool IsInWordlist
    {
        get => _isInWordlist;
        set { _isInWordlist = value; OnChanged(); OnChanged(nameof(FavLabel)); }
    }

    public string FavLabel => _isInWordlist ? "★" : "☆";

    public bool HasResult => !string.IsNullOrEmpty(_currentWord);

    // ── 빌드 진행 ────────────────────────────────────────────────────────────

    public bool IsBuilding
    {
        get => _isBuilding;
        set { _isBuilding = value; OnChanged(); OnChanged(nameof(ShowResults)); OnChanged(nameof(ShowHint)); }
    }

    public int BuildProgress
    {
        get => _buildProgress;
        set { _buildProgress = value; OnChanged(); }
    }

    public string BuildMessage
    {
        get => _buildMessage;
        set { _buildMessage = value; OnChanged(); }
    }

    public bool ShowResults => !_isBuilding;
    public bool ShowHint    => !_isBuilding && !HasResult && string.IsNullOrEmpty(_statusText);

    // ── 컬렉션 ───────────────────────────────────────────────────────────────

    public ObservableCollection<SenseViewModel>  Senses      { get; } = new();
    public ObservableCollection<string>          History     { get; } = new();
    public ObservableCollection<string>          Suggestions { get; } = new();

    // ── 조회 ─────────────────────────────────────────────────────────────────

    public void Search(string word)
    {
        word = word.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(word)) { ClearResult(); return; }

        var senses = _dict.Lookup(word);
        Senses.Clear();
        Suggestions.Clear();

        if (senses.Count == 0)
        {
            CurrentWord = "";
            StatusText  = $"'{word}' — 결과 없음";
            OnChanged(nameof(ShowHint));
            return;
        }

        CurrentWord = word;
        StatusText  = "";

        // POS별로 번호 매기기
        var posIndex = new Dictionary<string, int>();
        foreach (var s in senses)
        {
            posIndex.TryGetValue(s.PartOfSpeech, out int n);
            posIndex[s.PartOfSpeech] = n + 1;
            Senses.Add(new SenseViewModel(s, n + 1));
        }

        _db.AddHistory(word);
        IsInWordlist = _db.IsInWordlist(word);
        RefreshHistory();
        OnChanged(nameof(ShowHint));
    }

    public void UpdateSuggestions(string prefix)
    {
        Suggestions.Clear();
        if (string.IsNullOrWhiteSpace(prefix)) return;
        foreach (var s in _dict.Suggest(prefix.Trim(), 8))
            Suggestions.Add(s);
    }

    public void ToggleWordlist()
    {
        if (string.IsNullOrEmpty(CurrentWord)) return;
        if (IsInWordlist) { _db.RemoveFromWordlist(CurrentWord); IsInWordlist = false; }
        else              { _db.AddToWordlist(CurrentWord);      IsInWordlist = true; }
    }

    public void ClearResult()
    {
        CurrentWord = "";
        Senses.Clear();
        Suggestions.Clear();
        StatusText  = "";
        IsInWordlist = false;
        OnChanged(nameof(ShowHint));
    }

    public void RefreshHistory()
    {
        History.Clear();
        foreach (var w in _db.GetRecentHistory(8)) History.Add(w);
    }

    public void ExportWordlist(bool anki)
    {
        var words = _db.GetWordlist();
        if (words.Count == 0) { StatusText = "단어장이 비어있습니다"; return; }

        var dlg = new Microsoft.Win32.SaveFileDialog();
        if (anki) { dlg.Filter = "Anki 덱 (*.txt)|*.txt"; dlg.FileName = "wordlist_anki.txt"; }
        else      { dlg.Filter = "CSV 파일 (*.csv)|*.csv"; dlg.FileName = "wordlist.csv"; }

        if (dlg.ShowDialog() != true) return;
        if (anki) _db.ExportWordlistToAnki(dlg.FileName);
        else      _db.ExportWordlistToCsv(dlg.FileName);
        StatusText = $"내보내기 완료";
    }

    void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
