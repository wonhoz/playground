namespace KeyMacro.Models;

/// <summary>트리거 키 하나 + 그 키를 눌렀을 때 실행할 동작 시퀀스</summary>
public sealed class MacroRule : ObservableObject
{
    private string _name = "새 매크로";
    private bool _enabled = true;
    private int _triggerVk;
    private bool _ctrl, _alt, _shift, _win;
    private bool _suppressTrigger = true;
    private int _repeatCount = 1;
    private int _gapMs = 50;
    private bool _scheduleEnabled;
    private string _scheduleTime = "09:00:00";
    private bool _scheduleDaily = true;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get => _name; set => Set(ref _name, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    // ── 트리거 ──
    public int TriggerVk { get => _triggerVk; set { Set(ref _triggerVk, value); RaiseDisplay(); } }
    public bool Ctrl  { get => _ctrl;  set { Set(ref _ctrl, value);  RaiseDisplay(); } }
    public bool Alt   { get => _alt;   set { Set(ref _alt, value);   RaiseDisplay(); } }
    public bool Shift { get => _shift; set { Set(ref _shift, value); RaiseDisplay(); } }
    public bool Win   { get => _win;   set { Set(ref _win, value);   RaiseDisplay(); } }

    /// <summary>트리거 키의 원래 입력을 다른 프로그램에 전달하지 않음</summary>
    public bool SuppressTrigger { get => _suppressTrigger; set => Set(ref _suppressTrigger, value); }

    /// <summary>시퀀스 전체 반복 횟수</summary>
    public int RepeatCount { get => _repeatCount; set => Set(ref _repeatCount, Math.Max(1, value)); }

    /// <summary>동작과 동작 사이 기본 간격(ms)</summary>
    public int GapMs { get => _gapMs; set => Set(ref _gapMs, Math.Max(0, value)); }

    // ── 예약 실행 ──
    /// <summary>지정한 시각이 되면 키 입력 없이도 알아서 실행</summary>
    public bool ScheduleEnabled
    {
        get => _scheduleEnabled;
        set { Set(ref _scheduleEnabled, value); RaiseDisplay(); }
    }

    /// <summary>실행할 시각 (HH:mm:ss)</summary>
    public string ScheduleTime
    {
        get => _scheduleTime;
        set { Set(ref _scheduleTime, value ?? ""); RaiseDisplay(); }
    }

    /// <summary>true = 매일 반복, false = 한 번만 실행하고 예약 해제</summary>
    public bool ScheduleDaily
    {
        get => _scheduleDaily;
        set { Set(ref _scheduleDaily, value); RaiseDisplay(); }
    }

    /// <summary>예약 시각을 TimeSpan 으로 (형식이 잘못됐으면 null)</summary>
    [JsonIgnore]
    public TimeSpan? ScheduleAt =>
        TimeSpan.TryParseExact(ScheduleTime, new[] { @"hh\:mm\:ss", @"h\:mm\:ss", @"hh\:mm" },
            System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : null;

    public ObservableCollection<MacroAction> Actions { get; set; } = new();

    [JsonIgnore]
    public string TriggerDisplay =>
        TriggerVk == 0 ? "키 미지정" : KeyNames.Combo(TriggerVk, Ctrl, Alt, Shift, Win);

    [JsonIgnore]
    public string Summary
    {
        get
        {
            var text = Actions.Count == 0 ? "동작 없음" : $"동작 {Actions.Count}개";
            if (RepeatCount > 1) text += $" · {RepeatCount}회 반복";
            if (ScheduleEnabled && ScheduleAt is not null)
                text += $" · ⏰ {ScheduleTime}{(ScheduleDaily ? " 매일" : " 1회")}";
            return text;
        }
    }

    /// <summary>예약이 켜져 있고 시각도 올바른지</summary>
    [JsonIgnore]
    public bool HasSchedule => ScheduleEnabled && ScheduleAt is not null && Actions.Count > 0;

    public void RaiseDisplay()
    {
        Raise(nameof(TriggerDisplay));
        Raise(nameof(Summary));
    }
}
