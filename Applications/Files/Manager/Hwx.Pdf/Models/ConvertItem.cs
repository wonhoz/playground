using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HwxPdf.Models;

public enum ConvertState { Waiting, Running, Done, Failed, Canceled }

/// <summary>변환 방향 — 원본 확장자로 정한다 (.pdf → 한글, 나머지 → PDF)</summary>
public enum ConvertDirection { ToPdf, ToHwp }

public sealed class ConvertItem : INotifyPropertyChanged
{
    public ConvertItem(string source)
    {
        Source = source;
        Direction = Path.GetExtension(source).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? ConvertDirection.ToHwp
            : ConvertDirection.ToPdf;
    }

    public string Source { get; }
    public ConvertDirection Direction { get; }

    /// <summary>만들 파일 확장자 (.pdf · .hwp · .hwpx) — 목록에 넣을 때 옵션으로 정한다</summary>
    public string TargetExtension
    {
        get => _targetExtension;
        set { _targetExtension = value; OnChanged(); OnChanged(nameof(TargetLabel)); }
    }
    private string _targetExtension = ".pdf";

    public string TargetLabel => "→ " + TargetExtension.TrimStart('.').ToUpperInvariant();
    public string FileName => Path.GetFileName(Source);
    public string Folder => Path.GetDirectoryName(Source) ?? "";

    private ConvertState _state;
    public ConvertState State
    {
        get => _state;
        set { _state = value; OnChanged(); OnChanged(nameof(StateText)); }
    }

    public string StateText => State switch
    {
        ConvertState.Waiting => "대기",
        ConvertState.Running => "변환 중",
        ConvertState.Done => "완료",
        ConvertState.Failed => "실패",
        _ => "취소",
    };

    private string _message = "";
    public string Message
    {
        get => _message;
        set { _message = value; OnChanged(); }
    }

    public string? OutputPath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
