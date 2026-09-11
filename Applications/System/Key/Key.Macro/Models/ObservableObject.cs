namespace KeyMacro.Models;

/// <summary>INotifyPropertyChanged 기본 구현 (모델 공통 베이스)</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value,
        [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    /// <summary>지정한 속성(기본: 호출자) 변경 알림. 파생 표시용 속성 갱신에도 사용</summary>
    public void Raise([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
