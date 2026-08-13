namespace StayAwake
{
    /// <summary>
    /// 업무시간 스케줄 — 지정한 요일·시간대에만 활동 시뮬레이션이 동작하도록 제어
    /// (예: 평일 09:00~17:00 → 해당 구간에만 절전 방지)
    /// </summary>
    public class WorkSchedule
    {
        /// <summary>요일 비트마스크 — bit N = (int)DayOfWeek N (0:일 ~ 6:토)</summary>
        public const int MaskWeekdays = 0b0111110; // 월~금 (62)
        public const int MaskWeekend = 0b1000001;  // 토·일 (65)
        public const int MaskEveryDay = 0b1111111; // 매일 (127)

        private static readonly string[] DayNames = { "일", "월", "화", "수", "목", "금", "토" };

        public bool IsEnabled { get; set; }
        public int StartHour { get; set; } = 9;
        public int StartMinute { get; set; }
        public int EndHour { get; set; } = 17;
        public int EndMinute { get; set; }
        public int DaysMask { get; set; } = MaskWeekdays;

        private int StartOfDayMinutes => StartHour * 60 + StartMinute;
        private int EndOfDayMinutes => EndHour * 60 + EndMinute;

        public bool IsDayEnabled(DayOfWeek day) => (DaysMask & (1 << (int)day)) != 0;

        /// <summary>
        /// 지정 시각이 업무시간 구간에 속하는지 판정
        /// 시작 &gt; 종료인 경우 자정을 넘는 구간(예: 22:00~06:00)으로 해석하며,
        /// 요일 판정은 구간이 시작된 날짜 기준으로 수행한다.
        /// </summary>
        public bool IsWithin(DateTime now)
        {
            if (DaysMask == 0) return false;

            var start = StartOfDayMinutes;
            var end = EndOfDayMinutes;
            var current = now.Hour * 60 + now.Minute;

            // 시작 = 종료 → 해당 요일 종일 동작
            if (start == end) return IsDayEnabled(now.DayOfWeek);

            if (start < end)
                return IsDayEnabled(now.DayOfWeek) && current >= start && current < end;

            // 자정을 넘는 구간 — 시작 이후는 오늘 요일, 종료 이전은 전날 요일 기준
            if (current >= start) return IsDayEnabled(now.DayOfWeek);
            if (current < end) return IsDayEnabled(now.AddDays(-1).DayOfWeek);
            return false;
        }

        /// <summary>
        /// 다음 업무시간 시작 시각 (현재 구간 밖일 때만 의미 있음)
        /// 최대 8일까지 분 단위로 탐색하며, 해당 구간이 없으면 null
        /// </summary>
        public DateTime? NextStart(DateTime now)
        {
            if (DaysMask == 0) return null;

            // 종일 모드 — 활성 요일의 자정이 곧 시작 시각
            if (StartOfDayMinutes == EndOfDayMinutes)
            {
                for (var i = 1; i <= 7; i++)
                {
                    var day = now.Date.AddDays(i);
                    if (IsDayEnabled(day.DayOfWeek)) return day;
                }
                return null;
            }

            for (var i = 0; i <= 7; i++)
            {
                var candidate = now.Date.AddDays(i).AddMinutes(StartOfDayMinutes);
                if (candidate <= now) continue;
                if (IsWithin(candidate)) return candidate;
            }
            return null;
        }

        /// <summary>
        /// 현재 업무시간 구간의 종료 시각 (구간 안일 때만 의미 있음)
        /// </summary>
        public DateTime? CurrentEnd(DateTime now)
        {
            if (StartOfDayMinutes == EndOfDayMinutes) return null; // 종일 모드는 종료 시각 없음

            var end = now.Date.AddMinutes(EndOfDayMinutes);
            return end > now ? end : end.AddDays(1);
        }

        /// <summary>요일 요약 — "평일" / "매일" / "주말" / "월·수·금"</summary>
        public string DaysSummary()
        {
            if (DaysMask == 0) return "없음";
            if (DaysMask == MaskEveryDay) return "매일";
            if (DaysMask == MaskWeekdays) return "평일";
            if (DaysMask == MaskWeekend) return "주말";

            var names = Enumerable.Range(0, 7)
                .Where(d => (DaysMask & (1 << d)) != 0)
                .Select(d => DayNames[d]);
            return string.Join("·", names);
        }

        /// <summary>전체 요약 — "평일 09:00~17:00"</summary>
        public string Summary() =>
            $"{DaysSummary()} {StartHour:D2}:{StartMinute:D2}~{EndHour:D2}:{EndMinute:D2}";

        /// <summary>다음 시작 시각 요약 — "월 09:00" (없으면 null)</summary>
        public string? NextStartSummary(DateTime now)
        {
            var next = NextStart(now);
            if (next == null) return null;
            return $"{DayNames[(int)next.Value.DayOfWeek]} {next.Value:HH:mm}";
        }
    }
}
