using System.Text.Json.Serialization;

namespace SoundBoard.Models;

public class SoundButton
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public string Name       { get; set; } = "New Sound";
    public string Emoji      { get; set; } = "🔊";
    /// <summary>사용자 오디오 파일 경로. 비어있으면 BuiltInKey 사용.</summary>
    public string FilePath   { get; set; } = "";
    /// <summary>내장 사운드 키. "airhorn", "applause" 등.</summary>
    public string BuiltInKey { get; set; } = "";
    /// <summary>버튼 배경색 (hex).</summary>
    public string Color      { get; set; } = "#2C3E50";
    public int    HotkeyVk   { get; set; } = 0;
    public int    HotkeyMods { get; set; } = 0;
    public string HotkeyText { get; set; } = "";

    [JsonIgnore] public bool IsBuiltIn  => !string.IsNullOrEmpty(BuiltInKey);
    [JsonIgnore] public bool HasHotkey  => HotkeyVk != 0;
    [JsonIgnore] public bool HasSound   => IsBuiltIn || !string.IsNullOrEmpty(FilePath);
}
