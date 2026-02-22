namespace CommuteBuddy.Models;

public class AppSettings
{
    public List<Location>     Locations     { get; set; } = [];
    public string             StayAwakePath { get; set; } = "";
    public RemoteWorkSettings RemoteWork    { get; set; } = new();

    public static AppSettings CreateDefault() => new()
    {
        Locations =
        [
            new()
            {
                Name  = "회사",
                Emoji = "🏢",
                ArrivalRoutine   = new() { StartStayAwake = true,  ShowNotification = true },
                DepartureRoutine = new() { StopStayAwake  = true,  ShowNotification = true },
            },
            new()
            {
                Name  = "집",
                Emoji = "🏠",
                ArrivalRoutine   = new() { ShowNotification = true },
                DepartureRoutine = new() { ShowNotification = true },
            },
            new()
            {
                Name  = "카페",
                Emoji = "☕",
                ArrivalRoutine   = new() { ShowNotification = true },
                DepartureRoutine = new() { ShowNotification = true },
            },
        ],
        RemoteWork = new() { Enabled = false, LocationName = "집", StartHour = 9, StartMinute = 0 },
    };
}
