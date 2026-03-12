using System.Text;

namespace WinEvent.Services;

public static class ExportService
{
    public static void ExportCsv(IEnumerable<EventItem> items, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("시간,레벨,EventID,소스,로그,메시지");
        foreach (var e in items)
        {
            var msg = e.MessageFull.Replace("\"", "\"\"").Replace("\r\n", " ").Replace("\n", " ");
            sb.AppendLine($"\"{e.TimeDisplay}\",\"{e.LevelTag}\",{e.EventId},\"{e.ProviderName}\",\"{e.LogName}\",\"{msg}\"");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void ExportJson(IEnumerable<EventItem> items, string path)
    {
        var list = items.Select(e => new
        {
            time     = e.TimeDisplay,
            level    = e.LevelTag,
            eventId  = e.EventId,
            source   = e.ProviderName,
            logName  = e.LogName,
            message  = e.MessageFull
        });
        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(true));
    }
}
