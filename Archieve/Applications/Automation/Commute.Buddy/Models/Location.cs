namespace CommuteBuddy.Models;

public class Location
{
    public string       Name             { get; set; } = "";
    public string       Emoji            { get; set; } = "📍";
    public List<string> Ssids            { get; set; } = [];
    public Routine      ArrivalRoutine   { get; set; } = new();
    public Routine      DepartureRoutine { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => $"{Emoji} {Name}";
}
