namespace CrashView.Models;

public class DumpInfo
{
    public string FilePath      { get; set; } = "";
    public string FileName      { get; set; } = "";
    public long   FileSize      { get; set; }
    public DateTime AnalyzedAt  { get; set; }

    // 예외 정보
    public string ExceptionType    { get; set; } = "";
    public string ExceptionMessage { get; set; } = "";
    public ulong  ExceptionAddress { get; set; }
    public uint   ExceptionCode    { get; set; }
    public string ExceptionCodeName => ExceptionCode switch
    {
        0xC0000005 => "ACCESS_VIOLATION",
        0xC0000094 => "INTEGER_DIVIDE_BY_ZERO",
        0xC00000FD => "STACK_OVERFLOW",
        0xC0000135 => "DLL_NOT_FOUND",
        0xC0000409 => "STACK_BUFFER_OVERRUN",
        0x80000003 => "BREAKPOINT",
        0x40010006 => "CTRL_C",
        _          => $"0x{ExceptionCode:X8}"
    };

    // 구조 정보
    public List<ThreadInfo>  Threads  { get; set; } = [];
    public List<ModuleInfo>  Modules  { get; set; } = [];
    public List<StackFrame>  CrashStack { get; set; } = [];

    // OS / 프로세스 정보
    public string ProcessName    { get; set; } = "";
    public uint   ProcessId      { get; set; }
    public string OsVersion      { get; set; } = "";
    public string Architecture   { get; set; } = "";
    public bool   IsManaged      { get; set; }

    // 메모리 정보 (ClrMD)
    public long HeapSize       { get; set; }
    public long Gen0Size        { get; set; }
    public long Gen1Size        { get; set; }
    public long Gen2Size        { get; set; }
    public long LohSize         { get; set; }
}

public class StackFrame
{
    public int    Index      { get; set; }
    public string ModuleName { get; set; } = "";
    public string Method     { get; set; } = "";
    public ulong  Offset     { get; set; }
    public string SourceFile { get; set; } = "";
    public int    LineNumber { get; set; }
    public bool   IsManaged  { get; set; }

    public string Display => string.IsNullOrEmpty(SourceFile)
        ? $"[{Index:D2}] {ModuleName}!{Method} +0x{Offset:X}"
        : $"[{Index:D2}] {ModuleName}!{Method} at {System.IO.Path.GetFileName(SourceFile)}:{LineNumber}";
    public Brush  FrameBrush => IsManaged
        ? new SolidColorBrush(Color.FromRgb(0x6E, 0xFF, 0x6E))
        : new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
}

public class ModuleInfo
{
    public string Name        { get; set; } = "";
    public string FilePath    { get; set; } = "";
    public ulong  BaseAddress { get; set; }
    public ulong  Size        { get; set; }
    public string Version     { get; set; } = "";
    public bool   IsSigned    { get; set; }
    public bool   IsManaged   { get; set; }

    public string SizeDisplay => Size < 1024 * 1024
        ? $"{Size / 1024} KB" : $"{Size / 1024 / 1024} MB";
    public Brush  ManagedBrush => IsManaged
        ? new SolidColorBrush(Color.FromRgb(0x6E, 0xFF, 0x6E))
        : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
}

public class ThreadInfo
{
    public uint          ThreadId   { get; set; }
    public string        State      { get; set; } = "";
    public List<StackFrame> Stack   { get; set; } = [];
    public bool          IsCrash    { get; set; }

    public Brush StateBrush => IsCrash
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55))
        : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
}
