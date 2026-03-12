using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SvcGuard.Models;

public enum ServiceCategory
{
    SystemCritical,   // 시스템 필수
    Recommended,      // 권장
    SafeToDisable,    // 안전하게 비활성화 가능
    ThirdParty        // 서드파티
}

public class ServiceItem : INotifyPropertyChanged
{
    private string _status = "";
    private string _startType = "";

    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Account { get; set; } = "";
    public ServiceCategory Category { get; set; } = ServiceCategory.ThirdParty;

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); OnPropertyChanged(nameof(IsRunning)); }
    }

    public string StartType
    {
        get => _startType;
        set { _startType = value; OnPropertyChanged(); }
    }

    public bool IsRunning => _status == "Running";

    public string StatusColor => _status switch
    {
        "Running" => "#50E080",
        "Stopped" => "#FF5555",
        "Paused"  => "#FFB840",
        _         => "#808080"
    };

    public string CategoryLabel => Category switch
    {
        ServiceCategory.SystemCritical  => "시스템 필수",
        ServiceCategory.Recommended     => "권장",
        ServiceCategory.SafeToDisable   => "비활성화 가능",
        ServiceCategory.ThirdParty      => "서드파티",
        _                               => "알 수 없음"
    };

    public string CategoryColor => Category switch
    {
        ServiceCategory.SystemCritical  => "#FF5555",
        ServiceCategory.Recommended     => "#4A9EFF",
        ServiceCategory.SafeToDisable   => "#50E080",
        ServiceCategory.ThirdParty      => "#808080",
        _                               => "#808080"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
