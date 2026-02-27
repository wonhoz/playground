namespace ToastCast.Models;

public class Routine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "💡";
    public int IntervalMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;

    /// <summary>알림 팝업에서 완료 버튼을 기다리는 시간 (초). 시간 내 완료 클릭 시 달성, 만료/건너뛰기 시 미달성.</summary>
    public int CountdownSeconds { get; set; } = 30;

    /// <summary>다음 알림 시각 (런타임 전용)</summary>
    public DateTime NextFireAt { get; set; } = DateTime.MinValue;

    public static List<Routine> Defaults() =>
    [
        new Routine
        {
            Id = "eye-rest",
            Name = "눈 휴식",
            Description = "20-20-20 법칙: 20피트(6m) 이상 먼 곳을 20초간 바라보세요.",
            Icon = "👁",
            IntervalMinutes = 20,
            Enabled = true,
            CountdownSeconds = 20
        },
        new Routine
        {
            Id = "water",
            Name = "물 마시기",
            Description = "물 한 컵을 마셔 수분을 보충하세요.",
            Icon = "💧",
            IntervalMinutes = 60,
            Enabled = true,
            CountdownSeconds = 30
        },
        new Routine
        {
            Id = "stretch",
            Name = "스트레칭",
            Description = "목, 어깨, 허리를 가볍게 스트레칭하세요.",
            Icon = "🤸",
            IntervalMinutes = 90,
            Enabled = true,
            CountdownSeconds = 60
        },
        new Routine
        {
            Id = "posture",
            Name = "자세 교정",
            Description = "등을 곧게 펴고 모니터와 눈 높이를 맞추세요.",
            Icon = "🪑",
            IntervalMinutes = 30,
            Enabled = true,
            CountdownSeconds = 15
        }
    ];
}
