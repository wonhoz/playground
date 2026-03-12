namespace Sched.Cast.Services;

using System.Diagnostics;
using System.Xml;
using Sched.Cast.Models;

/// <summary>
/// Windows Task Scheduler v2 COM을 dynamic으로 래핑.
/// NuGet 패키지 없이 내장 taskschd.dll 사용.
/// </summary>
public class TaskSchedulerService
{
    const string FolderPath = "\\SchedCast";
    const string FolderName = "SchedCast";

    // ── COM 상수 ────────────────────────────────────────────────────────
    const int TASK_CREATE_OR_UPDATE = 6;
    const int TASK_LOGON_INTERACTIVE_TOKEN = 3;
    const int TASK_RUNLEVEL_HIGHEST = 1;
    const int TASK_ACTION_EXEC  = 0;
    const int TASK_TRIGGER_TIME   = 1;
    const int TASK_TRIGGER_DAILY  = 2;
    const int TASK_TRIGGER_WEEKLY = 3;
    const int TASK_TRIGGER_BOOT   = 8;
    const int TASK_TRIGGER_LOGON  = 9;

    // ── 작업 목록 ─────────────────────────────────────────────────────

    public List<TaskInfo> GetTasks()
    {
        var result = new List<TaskInfo>();
        try
        {
            dynamic ts     = CreateScheduler();
            dynamic folder = GetOrCreateFolder(ts);
            dynamic tasks  = folder.GetTasks(0);
            int count = tasks.Count;
            for (int i = 1; i <= count; i++)
            {
                try { result.Add(ComTaskToInfo(tasks[i])); } catch { }
            }
        }
        catch { }
        return result;
    }

    // ── 작업 제어 ─────────────────────────────────────────────────────

    public void RunTask(string name)
        => Schtasks($"/run /tn \"{FolderPath}\\{name}\"");

    public void StopTask(string name)
        => Schtasks($"/end /tn \"{FolderPath}\\{name}\"");

    public void DeleteTask(string name)
        => Schtasks($"/delete /tn \"{FolderPath}\\{name}\" /f");

    public void SetEnabled(string name, bool enabled)
        => Schtasks(enabled
            ? $"/change /tn \"{FolderPath}\\{name}\" /ENABLE"
            : $"/change /tn \"{FolderPath}\\{name}\" /DISABLE");

    // ── 작업 등록 ─────────────────────────────────────────────────────

    public void RegisterTask(TaskInfo info)
    {
        // XML 기반으로 schtasks 등록 (COM 등록은 XML 생성 후 import 방식)
        var xml = BuildTaskXml(info);
        var tmp = Path.Combine(Path.GetTempPath(), $"sched_cast_{Guid.NewGuid():N}.xml");
        File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
        try
        {
            Schtasks($"/create /tn \"{FolderPath}\\{info.Name}\" /xml \"{tmp}\" /f");
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    static dynamic CreateScheduler()
    {
        var t = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Task Scheduler COM을 로드할 수 없습니다");
        dynamic svc = Activator.CreateInstance(t)!;
        svc.Connect(null, null, null, null);
        return svc;
    }

    static dynamic GetOrCreateFolder(dynamic ts)
    {
        try { return ts.GetFolder(FolderPath); }
        catch
        {
            var root = ts.GetFolder("\\");
            return root.CreateFolder(FolderName, "");
        }
    }

    static TaskInfo ComTaskToInfo(dynamic task)
    {
        int state = 0;
        try { state = (int)task.State; } catch { }

        DateTime? lastRun = null, nextRun = null;
        try { var d = (DateTime)task.LastRunTime; if (d.Year > 1900) lastRun = d; } catch { }
        try { var d = (DateTime)task.NextRunTime; if (d.Year > 1900) nextRun = d; } catch { }

        int lastResult = 0;
        try { lastResult = (int)task.LastTaskResult; } catch { }

        bool enabled = true;
        try { enabled = (bool)task.Enabled; } catch { }

        string exe = "", args = "", workDir = "";
        try
        {
            dynamic actions = task.Definition.Actions;
            int cnt = actions.Count;
            for (int i = 1; i <= cnt; i++)
            {
                dynamic a = actions[i];
                if ((int)a.Type == TASK_ACTION_EXEC)
                {
                    exe     = (string)a.Path    ?? "";
                    args    = (string)a.Arguments ?? "";
                    workDir = (string)a.WorkingDirectory ?? "";
                    break;
                }
            }
        }
        catch { }

        string desc = "";
        try { desc = (string)task.Definition.RegistrationInfo.Description ?? ""; } catch { }

        return new TaskInfo
        {
            Name        = (string)task.Name ?? "",
            Path        = (string)task.Path ?? "",
            Description = desc,
            State       = (SchedTaskState)Math.Clamp(state, 0, 4),
            LastRun     = lastRun,
            NextRun     = nextRun,
            LastResult  = lastResult,
            Enabled     = enabled,
            ExePath     = exe,
            Arguments   = args,
            WorkDir     = workDir,
        };
    }

    static void Schtasks(string args)
    {
        var p = Process.Start(new ProcessStartInfo
        {
            FileName               = "schtasks.exe",
            Arguments              = args,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        });
        p?.WaitForExit(10_000);
        int exit = p?.ExitCode ?? -1;
        if (exit != 0)
            throw new InvalidOperationException($"schtasks 오류 (exit {exit}): {args}");
    }

    static string BuildTaskXml(TaskInfo info)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
        sb.Append("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
        sb.Append("<RegistrationInfo>");
        sb.Append($"<Description>{XmlEncode(info.Description)}</Description>");
        sb.Append("</RegistrationInfo>");
        sb.Append("<Principals><Principal id=\"Author\">");
        sb.Append("<LogonType>InteractiveToken</LogonType>");
        if (info.RunAsHighest) sb.Append("<RunLevel>HighestAvailable</RunLevel>");
        sb.Append("</Principal></Principals>");
        sb.Append("<Settings><Enabled>");
        sb.Append(info.Enabled ? "true" : "false");
        sb.Append("</Enabled><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
        sb.Append("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
        sb.Append("</Settings>");
        sb.Append("<Triggers>");
        switch (info.TriggerType)
        {
            case TriggerKind.Once:
                sb.Append($"<TimeTrigger><StartBoundary>{info.StartTime:s}</StartBoundary></TimeTrigger>");
                break;
            case TriggerKind.Daily:
                sb.Append($"<CalendarTrigger><StartBoundary>{(DateTime.Today + info.DailyTime):s}</StartBoundary>");
                sb.Append("<ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger>");
                break;
            case TriggerKind.Weekly:
                var day = info.WeeklyDay.ToString();
                sb.Append($"<CalendarTrigger><StartBoundary>{(DateTime.Today + info.WeeklyTime):s}</StartBoundary>");
                sb.Append($"<ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek><{day}/></DaysOfWeek></ScheduleByWeek></CalendarTrigger>");
                break;
            case TriggerKind.AtLogon:
                sb.Append("<LogonTrigger/>");
                break;
            case TriggerKind.AtStartup:
                sb.Append("<BootTrigger/>");
                break;
        }
        sb.Append("</Triggers>");
        sb.Append("<Actions Context=\"Author\">");
        sb.Append($"<Exec><Command>{XmlEncode(info.ExePath)}</Command>");
        if (!string.IsNullOrWhiteSpace(info.Arguments))
            sb.Append($"<Arguments>{XmlEncode(info.Arguments)}</Arguments>");
        if (!string.IsNullOrWhiteSpace(info.WorkDir))
            sb.Append($"<WorkingDirectory>{XmlEncode(info.WorkDir)}</WorkingDirectory>");
        sb.Append("</Exec></Actions></Task>");
        return sb.ToString();
    }

    static string XmlEncode(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
