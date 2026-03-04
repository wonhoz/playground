namespace MouseFlick.Models;

public sealed class GestureAction
{
    public string Gesture     { get; set; } = "";  // "L", "LR", "UD" 등
    public string Description { get; set; } = "";  // 사람이 읽는 설명
    public string KeyCombo    { get; set; } = "";  // "Alt+Left", "Ctrl+W" 등
}
