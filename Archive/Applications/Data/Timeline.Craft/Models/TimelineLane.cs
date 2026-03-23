namespace Timeline.Craft.Models;

sealed class TimelineLane : INotifyPropertyChanged
{
    string _name  = "레인";
    string _color = "#444466";

    public int    Index { get; set; }
    public string Name  { get => _name;  set { _name  = value; Notify(); } }
    public string Color { get => _color; set { _color = value; Notify(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
