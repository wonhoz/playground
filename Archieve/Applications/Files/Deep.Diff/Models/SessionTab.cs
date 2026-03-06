namespace DeepDiff.Models;

public class SessionTab : INotifyPropertyChanged
{
    private string _title = "";
    private UserControl? _content;

    public string Title
    {
        get => _title;
        set { _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); }
    }

    public UserControl? Content
    {
        get => _content;
        set { _content = value; PropertyChanged?.Invoke(this, new(nameof(Content))); }
    }

    public bool IsHome { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
