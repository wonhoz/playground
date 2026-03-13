namespace PadForge.Models;

public enum ActionType
{
    None,
    KeyPress,       // 키보드 키 누르기
    KeySequence,    // 키 시퀀스 (매크로)
    MouseButton,    // 마우스 버튼
    MouseScroll,    // 마우스 스크롤
    TextType,       // 텍스트 타이핑
    VirtualButton,  // ViGEm 가상 버튼 출력
    OpenApp,        // 앱 실행
}

public enum MouseButton { Left, Right, Middle }
public enum ScrollDirection { Up, Down, Left, Right }

public class MappingAction
{
    public ActionType Type { get; set; } = ActionType.None;

    // KeyPress / KeySequence
    public string? KeyCode { get; set; }            // e.g. "VK_RETURN", "Ctrl+C"
    public List<string> KeySequence { get; set; } = [];

    // MouseButton
    public MouseButton Mouse { get; set; }
    public ScrollDirection ScrollDir { get; set; }
    public int ScrollAmount { get; set; } = 3;

    // TextType
    public string? Text { get; set; }

    // VirtualButton (ViGEm)
    public XButtons VirtualXButton { get; set; }

    // OpenApp
    public string? AppPath { get; set; }

    [JsonIgnore]
    public string DisplayName => Type switch
    {
        ActionType.None         => "없음",
        ActionType.KeyPress     => $"키: {KeyCode}",
        ActionType.KeySequence  => $"시퀀스: {string.Join("+", KeySequence)}",
        ActionType.MouseButton  => $"마우스: {Mouse}",
        ActionType.MouseScroll  => $"스크롤: {ScrollDir} ×{ScrollAmount}",
        ActionType.TextType     => $"텍스트: {Text}",
        ActionType.VirtualButton=> $"가상 버튼: {VirtualXButton}",
        ActionType.OpenApp      => $"앱 실행: {Path.GetFileName(AppPath)}",
        _                       => "알 수 없음"
    };
}
