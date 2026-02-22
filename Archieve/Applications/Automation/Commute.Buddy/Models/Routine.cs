namespace CommuteBuddy.Models;

public class Routine
{
    public bool         StartStayAwake   { get; set; }
    public bool         StopStayAwake    { get; set; }
    public List<string> AppsToLaunch    { get; set; } = [];
    public List<string> AppsToClose     { get; set; } = [];
    public bool         ShowNotification { get; set; } = true;
}
