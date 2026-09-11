namespace KeyMacro.Models;

/// <summary>매크로가 수행할 단위 동작의 종류</summary>
public enum ActionKind
{
    MouseClick,  // 특정 좌표 마우스 클릭
    MouseMove,   // 특정 좌표로 커서 이동만
    KeyStroke,   // 키보드 키 입력
    Text,        // 문자열 그대로 입력
    Delay        // 대기
}

public enum ClickButton { Left, Right, Middle }

/// <summary>키 입력 방식 — 누르고 떼기 / 누른 채로 / 떼기</summary>
public enum KeyStrokeMode { Press, Down, Up }

/// <summary>매크로 시퀀스의 한 단계</summary>
public sealed class MacroAction : ObservableObject
{
    private ActionKind _kind = ActionKind.MouseClick;
    private int _x, _y;
    private ClickButton _button = ClickButton.Left;
    private int _clickCount = 1;
    private int _virtualKey;
    private bool _ctrl, _alt, _shift, _win;
    private KeyStrokeMode _keyMode = KeyStrokeMode.Press;
    private string _text = "";
    private int _delayMs = 100;

    public ActionKind Kind { get => _kind; set { Set(ref _kind, value); RaiseDisplay(); } }

    // ── 마우스 ──
    public int X { get => _x; set { Set(ref _x, value); RaiseDisplay(); } }
    public int Y { get => _y; set { Set(ref _y, value); RaiseDisplay(); } }
    public ClickButton Button { get => _button; set { Set(ref _button, value); RaiseDisplay(); } }
    public int ClickCount { get => _clickCount; set { Set(ref _clickCount, value); RaiseDisplay(); } }

    // ── 키보드 ──
    public int VirtualKey { get => _virtualKey; set { Set(ref _virtualKey, value); RaiseDisplay(); } }
    public bool Ctrl { get => _ctrl; set { Set(ref _ctrl, value); RaiseDisplay(); } }
    public bool Alt { get => _alt; set { Set(ref _alt, value); RaiseDisplay(); } }
    public bool Shift { get => _shift; set { Set(ref _shift, value); RaiseDisplay(); } }
    public bool Win { get => _win; set { Set(ref _win, value); RaiseDisplay(); } }
    public KeyStrokeMode KeyMode { get => _keyMode; set { Set(ref _keyMode, value); RaiseDisplay(); } }

    // ── 텍스트 / 지연 ──
    public string Text { get => _text; set { Set(ref _text, value ?? ""); RaiseDisplay(); } }
    public int DelayMs { get => _delayMs; set { Set(ref _delayMs, value); RaiseDisplay(); } }

    /// <summary>목록에 표시할 한 줄 요약 (아이콘 + 사람이 읽는 설명)</summary>
    [JsonIgnore]
    public string Display => Kind switch
    {
        ActionKind.MouseClick => $"{ButtonLabel} 클릭{(ClickCount > 1 ? $" x{ClickCount}" : "")}  ({X}, {Y})",
        ActionKind.MouseMove  => $"커서 이동  ({X}, {Y})",
        ActionKind.KeyStroke  => $"{KeyNames.Combo(VirtualKey, Ctrl, Alt, Shift, Win)}  {KeyModeLabel}",
        ActionKind.Text       => $"텍스트 입력  \"{Preview(Text)}\"",
        ActionKind.Delay      => $"{DelayMs}ms 대기",
        _ => "?"
    };

    [JsonIgnore]
    public string Glyph => Kind switch
    {
        ActionKind.MouseClick => "\U0001F5B1",  // 마우스
        ActionKind.MouseMove  => "\u2196",
        ActionKind.KeyStroke  => "\u2328",  // 키보드
        ActionKind.Text       => "\U0001F4DD",
        ActionKind.Delay      => "\u23F1",  // 타이머
        _ => "\u2753"
    };

    [JsonIgnore]
    public string ButtonLabel => Button switch
    {
        ClickButton.Left => "왼쪽",
        ClickButton.Right => "오른쪽",
        _ => "가운데"
    };

    [JsonIgnore]
    public string KeyModeLabel => KeyMode switch
    {
        KeyStrokeMode.Down => "누른 채로",
        KeyStrokeMode.Up => "떼기",
        _ => "입력"
    };

    private static string Preview(string s)
    {
        var one = s.Replace("\r", "").Replace("\n", "\u21B5");
        return one.Length <= 24 ? one : one[..24] + "...";
    }

    private void RaiseDisplay()
    {
        Raise(nameof(Display));
        Raise(nameof(Glyph));
        Raise(nameof(ButtonLabel));
        Raise(nameof(KeyModeLabel));
    }

    /// <summary>값만 복사 (이벤트 구독은 복사하지 않음)</summary>
    public MacroAction Clone() => new()
    {
        Kind = Kind, X = X, Y = Y, Button = Button, ClickCount = ClickCount,
        VirtualKey = VirtualKey, Ctrl = Ctrl, Alt = Alt, Shift = Shift, Win = Win,
        KeyMode = KeyMode, Text = Text, DelayMs = DelayMs
    };
}
