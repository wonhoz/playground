namespace CommuteBuddy.Models;

public class CommuteEntry
{
    public DateTime Timestamp    { get; set; }
    public string   LocationName { get; set; } = "";
    public string   Direction    { get; set; } = ""; // "arrived" | "left"
}

public class MonthlyLog
{
    public int                Year    { get; set; }
    public int                Month   { get; set; }
    public List<CommuteEntry> Entries { get; set; } = [];
}
