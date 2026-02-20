namespace CommuteBuddy.Models;

public class RemoteWorkSettings
{
    public bool   Enabled      { get; set; }
    public string LocationName { get; set; } = "집";
    public int    StartHour    { get; set; } = 9;
    public int    StartMinute  { get; set; } = 0;
}
