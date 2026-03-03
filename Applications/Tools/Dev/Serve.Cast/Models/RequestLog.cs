namespace ServeCast.Models;

/// <summary>실시간 요청 로그 항목</summary>
public class RequestLog
{
    public DateTime   Timestamp  { get; init; } = DateTime.Now;
    public string     Method     { get; init; } = "";
    public string     Path       { get; init; } = "";
    public int        StatusCode { get; set;  }
    public long       ElapsedMs  { get; set;  }
    public long       BytesSent  { get; set;  }

    public string TimeStr     => Timestamp.ToString("HH:mm:ss");
    public string ElapsedStr  => ElapsedMs < 1000 ? $"{ElapsedMs}ms" : $"{ElapsedMs / 1000.0:F1}s";
    public string BytesStr    => BytesSent switch
    {
        < 1024              => $"{BytesSent}B",
        < 1024 * 1024       => $"{BytesSent / 1024.0:F1}KB",
        _                   => $"{BytesSent / (1024.0 * 1024):F1}MB"
    };
    public string StatusColor => StatusCode switch
    {
        >= 500 => "#f38ba8",
        >= 400 => "#fab387",
        >= 300 => "#f9e2af",
        >= 200 => "#a6e3a1",
        _      => "#cdd6f4"
    };
}
