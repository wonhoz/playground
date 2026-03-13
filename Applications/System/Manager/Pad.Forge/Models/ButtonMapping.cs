namespace PadForge.Models;

/// <summary>게임패드 입력 하나와 동작 하나의 매핑</summary>
public class ButtonMapping
{
    public GamepadInput Input { get; set; }
    public MappingAction Action { get; set; } = new();
    public bool HoldEnabled { get; set; }       // 홀드 동작 사용 여부
    public MappingAction? HoldAction { get; set; }  // 홀드 시 동작

    [JsonIgnore]
    public string InputLabel => Input.ToString();
}
