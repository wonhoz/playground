namespace ToastCast.Models;

/// <summary>루틴 알림 달성 기록 (1건 = 1번의 알림 완료)</summary>
public class RoutineRecord
{
    public string RoutineId { get; set; } = "";
    public string RoutineName { get; set; } = "";
    public DateTime FiredAt { get; set; } = DateTime.Now;
    public bool Skipped { get; set; } = false;   // 유휴 상태로 스킵
    public bool Dismissed { get; set; } = false;  // 사용자가 '완료' 클릭
}
