namespace Timeline.Craft.Models;

sealed class TimelineEvent : INotifyPropertyChanged
{
    string _title   = "새 이벤트";
    string _notes   = "";
    string _color   = "#3B82F6";
    bool   _isMilestone;
    DateTime _start = DateTime.Today;
    DateTime _end   = DateTime.Today.AddDays(7);
    int  _laneIndex = 0;

    public int       Id           { get; set; }
    public string    Title        { get => _title; set { _title = value;  Notify(); } }
    public string    Notes        { get => _notes; set { _notes = value;  Notify(); } }
    public string    Color        { get => _color; set { _color = value;  Notify(); } }
    public bool      IsMilestone  { get => _isMilestone; set { _isMilestone = value; Notify(); } }
    public DateTime  Start        { get => _start; set { _start = value;  Notify(); } }
    public DateTime  End          { get => _end;   set { _end   = value;  Notify(); } }
    public int       LaneIndex    { get => _laneIndex; set { _laneIndex = value; Notify(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
