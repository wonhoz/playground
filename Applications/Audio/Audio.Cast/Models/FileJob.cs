using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace AudioCast.Models;

public enum JobStatus { Pending, Running, Done, Copied, Skipped, Failed, Cancelled }

/// <summary>변환 목록의 한 항목 (WPF 바인딩은 백그라운드 스레드의 PropertyChanged도 자동 마샬링)</summary>
public sealed class FileJob : INotifyPropertyChanged
{
    public FileJob(string sourcePath, string relativeDir)
    {
        SourcePath  = sourcePath;
        RelativeDir = relativeDir;
        FileName    = Path.GetFileName(sourcePath);
    }

    public string SourcePath  { get; }
    /// <summary>드롭한 폴더 기준 상대 폴더 (하위 폴더 구조 유지용, 파일 단독 드롭 시 빈 문자열)</summary>
    public string RelativeDir { get; }
    public string FileName    { get; }
    public string? OutputPath { get; set; }

    JobStatus _status = JobStatus.Pending;
    public JobStatus Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnChanged(); OnChanged(nameof(StatusText)); OnChanged(nameof(IsActive)); } }
    }

    double _progress;
    /// <summary>0.0 ~ 1.0</summary>
    public double Progress
    {
        get => _progress;
        set { if (Math.Abs(_progress - value) > 0.001) { _progress = value; OnChanged(); OnChanged(nameof(StatusText)); } }
    }

    string _detail = "";
    public string Detail
    {
        get => _detail;
        set { if (_detail != value) { _detail = value; OnChanged(); } }
    }

    public bool IsActive => _status == JobStatus.Running;

    public string StatusText => _status switch
    {
        JobStatus.Pending   => "대기",
        JobStatus.Running   => $"{_progress * 100:0}%",
        JobStatus.Done      => "완료",
        JobStatus.Copied    => "복사",
        JobStatus.Skipped   => "건너뜀",
        JobStatus.Failed    => "실패",
        JobStatus.Cancelled => "취소",
        _                   => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
