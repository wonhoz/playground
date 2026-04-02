using System.Text.Json.Serialization;

namespace ClipboardStacker.Models;

public class AppSettings
{
    /// <summary>최대 스택 히스토리 개수</summary>
    public int MaxHistory { get; set; } = 30;

    /// <summary>팝업 단축키 (기본: Ctrl+Shift+V)</summary>
    public int PopupHotkeyMods { get; set; } = 0x0006; // MOD_CONTROL | MOD_SHIFT
    public int PopupHotkeyVk   { get; set; } = 0x56;   // V
    public string PopupHotkeyText { get; set; } = "Ctrl+Shift+V";

    /// <summary>붙여넣기 시 텍스트 변환 모드</summary>
    public TransformMode Transform { get; set; } = TransformMode.None;

    /// <summary>즐겨찾기 (핀) 목록</summary>
    public List<PinnedItem> Pinned { get; set; } = [];
}

public class PinnedItem
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransformMode
{
    None,
    Upper,
    Lower,
    Trim,
}
