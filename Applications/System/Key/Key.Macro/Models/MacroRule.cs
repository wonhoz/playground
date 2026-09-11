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

    public ObservableCollection<MacroAction> Actions { get; set; } = new();

    [JsonIgnore]
    public string TriggerDisplay =>
        TriggerVk == 0 ? "키 미지정" : KeyNames.Combo(TriggerVk, Ctrl, Alt, Shift, Win);

    [JsonIgnore]
    public string Summary =>
        Actions.Count == 0 ? "동작 없음" : $"동작 {Actions.Count}개" + (RepeatCount > 1 ? $" · {RepeatCount}회 반복" : "");

    public void RaiseDisplay()
    {
        Raise(nameof(TriggerDisplay));
        Raise(nameof(Summary));
    }
}
