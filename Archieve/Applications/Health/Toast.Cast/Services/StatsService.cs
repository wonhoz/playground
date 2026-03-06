using System.Text.Json;
using ToastCast.Models;

namespace ToastCast.Services;

public static class StatsService
{
    private static readonly string RecordsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ToastCast", "records.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static List<RoutineRecord>? _cache;

    private static List<RoutineRecord> LoadAll()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(RecordsPath))
            {
                var json = File.ReadAllText(RecordsPath);
                _cache = JsonSerializer.Deserialize<List<RoutineRecord>>(json, JsonOpts) ?? [];
                return _cache;
            }
        }
        catch { /* 손상 시 빈 목록 */ }
        _cache = [];
        return _cache;
    }

    public static void AddRecord(RoutineRecord record)
    {
        var all = LoadAll();
        all.Add(record);

        // 90일 이상 된 기록 자동 정리
        var cutoff = DateTime.Now.AddDays(-90);
        _cache = all.Where(r => r.FiredAt >= cutoff).ToList();

        SaveAll(_cache);
    }

    private static void SaveAll(List<RoutineRecord> records)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecordsPath)!);
            File.WriteAllText(RecordsPath, JsonSerializer.Serialize(records, JsonOpts));
        }
        catch { /* 저장 실패 무시 */ }
    }

    /// <summary>이번 주(월~일) 루틴별 달성률을 반환합니다.</summary>
    public static Dictionary<string, WeeklyRoutineStat> GetWeeklyStats(List<Routine> routines)
    {
        var now = DateTime.Now;
        var weekStart = now.Date.AddDays(-((int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1));
        var weekEnd = weekStart.AddDays(7);

        var records = LoadAll()
            .Where(r => r.FiredAt >= weekStart && r.FiredAt < weekEnd && !r.Skipped)
            .ToList();

        var result = new Dictionary<string, WeeklyRoutineStat>();
        foreach (var routine in routines.Where(r => r.Enabled))
        {
            var routineRecords = records.Where(r => r.RoutineId == routine.Id).ToList();
            var daysElapsed = Math.Max(1, (int)(now.Date - weekStart).TotalDays + 1);
            var expectedPerDay = 24 * 60 / routine.IntervalMinutes;
            var expected = daysElapsed * expectedPerDay;
            var achieved = routineRecords.Count(r =>  r.Dismissed);
            var missed   = routineRecords.Count(r => !r.Dismissed);

            result[routine.Id] = new WeeklyRoutineStat
            {
                RoutineName = routine.Name,
                Icon        = routine.Icon,
                Achieved    = achieved,
                Missed      = missed,
                Expected    = expected,
                Rate        = expected > 0 ? (double)achieved / expected : 0
            };
        }
        return result;
    }

    public static void InvalidateCache() => _cache = null;
}

public class WeeklyRoutineStat
{
    public string RoutineName { get; set; } = "";
    public string Icon        { get; set; } = "";
    public int    Achieved    { get; set; }
    public int    Missed      { get; set; }
    public int    Expected    { get; set; }
    public double Rate        { get; set; }
}
