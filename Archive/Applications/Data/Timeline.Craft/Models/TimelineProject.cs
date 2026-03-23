namespace Timeline.Craft.Models;

sealed class TimelineProject
{
    public string              Title   { get; set; } = "새 타임라인";
    public List<TimelineLane>  Lanes   { get; set; } = [];
    public List<TimelineEvent> Events  { get; set; } = [];
    public DateTime            ViewStart { get; set; } = DateTime.Today.AddDays(-7);
    public double              PixelsPerDay { get; set; } = 30;
}
