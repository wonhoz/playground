using Microsoft.Diagnostics.Runtime;
using ClrModuleInfo = Microsoft.Diagnostics.Runtime.ModuleInfo;

namespace CrashView.Services;

public static class DumpAnalyzer
{
    /// <summary>.dmp 파일을 분석하여 DumpInfo를 반환합니다.</summary>
    public static async Task<DumpInfo> AnalyzeAsync(string dumpPath,
        IProgress<string>? progress = null, string? symbolPath = null)
    {
        return await Task.Run(() => Analyze(dumpPath, progress, symbolPath));
    }

    private static DumpInfo Analyze(string dumpPath, IProgress<string>? progress, string? symbolPath)
    {
        var info = new DumpInfo
        {
            FilePath   = dumpPath,
            FileName   = Path.GetFileName(dumpPath),
            FileSize   = new FileInfo(dumpPath).Length,
            AnalyzedAt = DateTime.Now
        };

        progress?.Report("덤프 파일 로드 중...");

        try
        {
            using var dataTarget = DataTarget.LoadDump(dumpPath);
            try { info.Architecture = dataTarget.DataReader.Architecture.ToString(); }
            catch { info.Architecture = "Unknown"; }

            progress?.Report("모듈 목록 수집 중...");
            LoadModules(dataTarget, info);

            progress?.Report("CLR 런타임 감지 중...");
            var clr = dataTarget.ClrVersions.FirstOrDefault();
            if (clr != null)
            {
                info.IsManaged = true;
                progress?.Report("관리 코드 스택 분석 중...");
                using var runtime = clr.CreateRuntime();
                LoadManagedThreads(runtime, info);
                LoadHeapInfo(runtime, info);
            }
            else
            {
                progress?.Report("네이티브 스레드 수집 중...");
                LoadNativeThreads(dataTarget, info);
            }

            // 예외 정보: 크래시 스레드에서 가져옴
            var crashThread = info.Threads.FirstOrDefault(t => t.IsCrash);
            if (crashThread != null)
            {
                info.CrashStack = crashThread.Stack;
                // 관리 예외인 경우 첫 번째 관리 프레임에서 타입 추출
                var exFrame = crashThread.Stack.FirstOrDefault(f => f.IsManaged && f.Method.Contains("throw", StringComparison.OrdinalIgnoreCase));
                if (exFrame != null) info.ExceptionType = exFrame.Method;
            }

            progress?.Report("분석 완료");
        }
        catch (Exception ex)
        {
            info.ExceptionMessage = $"분석 오류: {ex.Message}";
            progress?.Report($"오류: {ex.Message}");
        }

        return info;
    }

    // ── 모듈 ──────────────────────────────────────────────────────────
    private static void LoadModules(DataTarget target, DumpInfo info)
    {
        foreach (ClrModuleInfo mod in target.EnumerateModules())
        {
            info.Modules.Add(new Models.ModuleInfo
            {
                Name        = Path.GetFileName(mod.FileName ?? ""),
                FilePath    = mod.FileName ?? "",
                BaseAddress = mod.ImageBase,
                Size        = (ulong)(mod.IndexFileSize > 0 ? mod.IndexFileSize : 0),
                Version     = mod.Version?.ToString() ?? "",
                IsManaged   = false   // ClrMD Runtime에서 별도로 확인
            });
        }
    }

    // ── 관리 스레드 (ClrMD) ───────────────────────────────────────────
    private static void LoadManagedThreads(ClrRuntime runtime, DumpInfo info)
    {
        int crashIdx = -1;
        for (int i = 0; i < runtime.Threads.Length; i++)
        {
            var t = runtime.Threads[i];
            var thread = new ThreadInfo
            {
                ThreadId = t.OSThreadId,
                State    = t.IsAlive ? "실행 중" : "종료",
                IsCrash  = t.CurrentException != null
            };

            int frameIdx = 0;
            foreach (var frame in t.EnumerateStackTrace())
            {
                var method = frame.Method;
                thread.Stack.Add(new StackFrame
                {
                    Index      = frameIdx++,
                    ModuleName = method?.Type?.Module?.Name ?? "?",
                    Method     = method?.Signature ?? frame.ToString() ?? "?",
                    Offset     = frame.StackPointer,
                    IsManaged  = method != null
                });
            }

            // 예외 정보
            if (t.CurrentException != null)
            {
                info.ExceptionType    = t.CurrentException.Type?.Name ?? "";
                info.ExceptionMessage = t.CurrentException.Message ?? "";
                info.ExceptionAddress = t.CurrentException.Address;
                crashIdx = i;
            }

            info.Threads.Add(thread);
        }

        // 모듈 관리 여부 업데이트
        var managedModules = new HashSet<string>(runtime.EnumerateModules().Select(m => m.Name ?? ""), StringComparer.OrdinalIgnoreCase);
        foreach (var mod in info.Modules)
            if (managedModules.Contains(mod.FilePath)) mod.IsManaged = true;
    }

    // ── 네이티브 스레드 ────────────────────────────────────────────────
    private static void LoadNativeThreads(DataTarget target, DumpInfo info)
    {
        // DataTarget에서 직접 스레드 정보를 가져옴 (ClrMD Native API)
        // 간략화: 최소 1개 스레드 항목 생성
        info.Threads.Add(new ThreadInfo
        {
            ThreadId = 0,
            State    = "네이티브 (CLR 없음)",
            IsCrash  = true
        });
    }

    // ── 힙 정보 ──────────────────────────────────────────────────────
    private static void LoadHeapInfo(ClrRuntime runtime, DumpInfo info)
    {
        try
        {
            var heap = runtime.Heap;
            if (!heap.CanWalkHeap) return;

            long gen0 = 0, gen1 = 0, gen2 = 0, loh = 0;
            foreach (var seg in heap.Segments)
            {
                switch (seg.Kind)
                {
                    case GCSegmentKind.Generation0: gen0 += (long)seg.Length; break;
                    case GCSegmentKind.Generation1: gen1 += (long)seg.Length; break;
                    case GCSegmentKind.Generation2: gen2 += (long)seg.Length; break;
                    case GCSegmentKind.Large:       loh  += (long)seg.Length; break;
                }
            }
            info.Gen0Size  = gen0;
            info.Gen1Size  = gen1;
            info.Gen2Size  = gen2;
            info.LohSize   = loh;
            info.HeapSize  = gen0 + gen1 + gen2 + loh;
        }
        catch { /* 힙 워크 불가 */ }
    }

    // ── 리포트 ────────────────────────────────────────────────────────
    public static string ExportMarkdown(DumpInfo info)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Crash Report: {info.FileName}");
        sb.AppendLine($"\n**분석 일시**: {info.AnalyzedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**파일 크기**: {info.FileSize / 1024} KB");
        sb.AppendLine($"**아키텍처**: {info.Architecture}");
        sb.AppendLine($"**관리 코드**: {(info.IsManaged ? "예 (.NET)" : "아니오 (네이티브)")}");

        if (!string.IsNullOrEmpty(info.ExceptionType))
        {
            sb.AppendLine($"\n## 예외");
            sb.AppendLine($"- **타입**: {info.ExceptionType}");
            sb.AppendLine($"- **메시지**: {info.ExceptionMessage}");
            sb.AppendLine($"- **코드**: {info.ExceptionCodeName}");
        }

        if (info.CrashStack.Count > 0)
        {
            sb.AppendLine("\n## 콜스택");
            sb.AppendLine("```");
            foreach (var f in info.CrashStack.Take(30)) sb.AppendLine(f.Display);
            sb.AppendLine("```");
        }

        if (info.IsManaged && info.HeapSize > 0)
        {
            sb.AppendLine("\n## GC 힙");
            sb.AppendLine($"| 영역 | 크기 |");
            sb.AppendLine("|------|------|");
            sb.AppendLine($"| Gen0 | {info.Gen0Size / 1024} KB |");
            sb.AppendLine($"| Gen1 | {info.Gen1Size / 1024} KB |");
            sb.AppendLine($"| Gen2 | {info.Gen2Size / 1024} KB |");
            sb.AppendLine($"| LOH  | {info.LohSize  / 1024} KB |");
        }

        if (info.Modules.Count > 0)
        {
            sb.AppendLine($"\n## 로드된 모듈 ({info.Modules.Count}개)");
            sb.AppendLine("| 이름 | 버전 | 크기 | 관리 |");
            sb.AppendLine("|------|------|------|------|");
            foreach (var m in info.Modules.Take(30))
                sb.AppendLine($"| {m.Name} | {m.Version} | {m.SizeDisplay} | {(m.IsManaged ? "✓" : "")} |");
        }

        return sb.ToString();
    }
}
