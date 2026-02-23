namespace LogLens.Models;

public sealed class LogEntry
{
    public int LineNumber { get; init; }
    public string Text { get; init; } = "";
    public DateTime? Timestamp { get; init; }
    public LogLevel Level { get; init; } = LogLevel.None;
}

public enum LogLevel
{
    None,
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}
