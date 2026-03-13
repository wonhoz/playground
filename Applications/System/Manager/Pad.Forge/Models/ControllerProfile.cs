namespace PadForge.Models;

/// <summary>컨트롤러 매핑 프로파일 (JSON 저장 단위)</summary>
public class ControllerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "새 프로파일";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // 버튼 매핑 목록
    public List<ButtonMapping> Mappings { get; set; } = [];

    // 데드존 설정 (0.0 ~ 1.0)
    public float LeftStickDeadzone { get; set; } = 0.15f;
    public float RightStickDeadzone { get; set; } = 0.15f;
    public float TriggerDeadzone { get; set; } = 0.05f;

    // 스틱 감도 (1.0 = 기본)
    public float LeftStickSensitivity { get; set; } = 1.0f;
    public float RightStickSensitivity { get; set; } = 1.0f;

    // 앱별 자동 전환
    public List<AppProfile> AppProfiles { get; set; } = [];

    // ViGEm 가상 출력 활성화
    public bool ViGEmEnabled { get; set; } = false;
}

/// <summary>특정 앱 실행 시 자동으로 적용할 프로파일 매핑</summary>
public class AppProfile
{
    public string ProcessName { get; set; } = "";   // 예: "sekiro.exe"
    public string ProfileId { get; set; } = "";      // 적용할 ControllerProfile.Id
}
