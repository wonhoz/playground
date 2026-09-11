namespace KeyMacro.Models;

/// <summary>매크로와 별개로 앱 전체에 적용되는 설정</summary>
public sealed class AppSettings : ObservableObject
{
    private int _abortVk = 0x7B;      // F12
    private bool _abortCtrl = true;
    private bool _abortAlt = true;
    private bool _abortShift;
    private bool _abortWin;
    private bool _abortOnEsc = true;
    private string _timeSyncUrl = "https://www.google.com";
    private bool _timeSyncPrecise = true;

    /// <summary>실행 중인 매크로를 멈추는 전역 중단 키 (기본 Ctrl + Alt + F12)</summary>
    public int AbortVk { get => _abortVk; set { Set(ref _abortVk, value); Raise(nameof(AbortDisplay)); } }
    public bool AbortCtrl { get => _abortCtrl; set { Set(ref _abortCtrl, value); Raise(nameof(AbortDisplay)); } }
    public bool AbortAlt { get => _abortAlt; set { Set(ref _abortAlt, value); Raise(nameof(AbortDisplay)); } }
    public bool AbortShift { get => _abortShift; set { Set(ref _abortShift, value); Raise(nameof(AbortDisplay)); } }
    public bool AbortWin { get => _abortWin; set { Set(ref _abortWin, value); Raise(nameof(AbortDisplay)); } }

    /// <summary>Esc 로도 중단할지 — Esc 를 트리거 키로 쓰려면 꺼야 한다</summary>
    public bool AbortOnEsc { get => _abortOnEsc; set { Set(ref _abortOnEsc, value); Raise(nameof(AbortDisplay)); } }

    /// <summary>시간 동기화에 쓸 기준 사이트 주소 (응답의 Date 헤더를 읽는다)</summary>
    public string TimeSyncUrl { get => _timeSyncUrl; set => Set(ref _timeSyncUrl, value ?? ""); }

    /// <summary>초가 바뀌는 순간까지 추적해 오차를 줄이는 정밀 보정</summary>
    public bool TimeSyncPrecise { get => _timeSyncPrecise; set => Set(ref _timeSyncPrecise, value); }

    [JsonIgnore]
    public string AbortDisplay
    {
        get
        {
            var main = AbortVk == 0 ? "(없음)" : KeyNames.Combo(AbortVk, AbortCtrl, AbortAlt, AbortShift, AbortWin);
            return AbortOnEsc ? $"{main}  또는  Esc" : main;
        }
    }
}
