using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace HwxPdf.Models;

public enum ConvertState { Waiting, Running, Done, Failed, Canceled }

public sealed class ConvertItem : INotifyPropertyChanged
{
    public ConvertItem(string source) => Source = source;

    public string Source { get; }
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
