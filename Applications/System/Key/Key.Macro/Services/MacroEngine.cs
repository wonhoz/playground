using System.Collections.Concurrent;
using KeyMacro.Models;
using static KeyMacro.Services.NativeMethods;

namespace KeyMacro.Services;

/// <summary>키 입력을 감시하다가 일치하는 규칙의 동작 시퀀스를 실행한다</summary>
public sealed class MacroEngine : IDisposable
{
    /// <summary>실행 중인 매크로를 중단시키는 키 (Esc)</summary>
    public const int AbortVk = 0x1B;

    private readonly KeyboardHook _hook = new();
    private readonly BlockingCollection<MacroRule> _queue = new();
    private readonly HashSet<int> _heldTriggers = new();
    private readonly object _sync = new();

    private Thread? _worker;
    private volatile bool _abort;
    private volatile bool _busy;

    public MacroEngine() => _hook.KeyEvent += OnKey;

    /// <summary>규칙 목록 (UI 컬렉션을 그대로 참조)</summary>
    public IList<MacroRule> Rules { get; set; } = new List<MacroRule>();

    /// <summary>감시 중 여부</summary>
    public bool IsActive => _hook.IsInstalled;

    /// <summary>키 캡처 다이얼로그 등에서 잠시 트리거를 멈출 때 사용</summary>
    public bool Paused { get; set; }

    /// <summary>매크로가 실행되는 중인지</summary>
    public bool IsBusy => _busy;

    public event Action<string>? Log;
    public event Action? StateChanged;

    public void Start()
    {
        if (IsActive) return;
        _hook.Install();

        EnsureWorker();
        Raise("감시 시작 — 등록된 트리거 키를 기다립니다");
    }

    public void Stop()
    {
        if (!IsActive) return;
        _abort = true;
        _hook.Uninstall();
        lock (_sync) _heldTriggers.Clear();
        Raise("감시 중지");
    }

    /// <summary>감시와 무관하게 지정한 매크로를 즉시 한 번 실행 (테스트 실행)</summary>
    public void RunNow(MacroRule rule)
    {
        EnsureWorker();
        _queue.Add(rule);
    }

    private void EnsureWorker()
    {
        if (_worker is not null) return;
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "MacroRunner" };
        _worker.Start();
    }

    // ── 훅 콜백 (UI 스레드) ───────────────────────────────────────────
    private bool OnKey(HookedKey key)
    {
        if (Paused) return false;

        if (!key.IsDown)
        {
            lock (_sync) _heldTriggers.Remove(key.Vk);
            return false;
        }

        // 실행 중 Esc → 즉시 중단
        if (key.Vk == AbortVk && _busy)
        {
            _abort = true;
            Log?.Invoke("Esc — 실행 중단 요청");
            return false;
        }

        lock (_sync)
        {
            if (_heldTriggers.Contains(key.Vk)) return false;   // 키 누름 반복 이벤트 무시
        }

        var rule = Match(key.Vk);
        if (rule is null) return false;

        lock (_sync) _heldTriggers.Add(key.Vk);

        _queue.Add(rule);
        return rule.SuppressTrigger;
    }

    /// <summary>눌린 키 + 현재 수식 키 상태와 정확히 일치하는 규칙을 찾는다</summary>
    private MacroRule? Match(int vk)
    {
        bool ctrl  = Down(0x11), alt = Down(0x12), shift = Down(0x10);
        bool win   = Down(0x5B) || Down(0x5C);

        foreach (var rule in Rules)
        {
            if (!rule.Enabled || rule.TriggerVk != vk || rule.Actions.Count == 0) continue;
            if (rule.Ctrl == ctrl && rule.Alt == alt && rule.Shift == shift && rule.Win == win)
                return rule;
        }
        return null;

        static bool Down(int k) => (GetAsyncKeyState(k) & 0x8000) != 0;
    }

    // ── 실행 워커 ─────────────────────────────────────────────────────
    private void WorkerLoop()
    {
        foreach (var rule in _queue.GetConsumingEnumerable())
        {
            try { Run(rule); }
            catch (Exception ex) { Log?.Invoke($"실행 오류: {ex.Message}"); }
        }
    }

    private void Run(MacroRule rule)
    {
        _abort = false;
        _busy = true;
        StateChanged?.Invoke();
        Log?.Invoke($"▶ [{rule.TriggerDisplay}] {rule.Name} 실행");

        // 트리거에 쓰인 수식 키가 눌린 채로 남아 동작에 섞이는 것을 방지
        InputSender.ReleaseHeldModifiers();
        Thread.Sleep(20);

        var actions = rule.Actions.Select(a => a.Clone()).ToList();   // 실행 중 편집과 분리
        int repeat = Math.Max(1, rule.RepeatCount);

        for (int r = 0; r < repeat && !_abort; r++)
        {
            for (int i = 0; i < actions.Count && !_abort; i++)
            {
                if (EscPressed()) { _abort = true; break; }
                Execute(actions[i]);
                if (i < actions.Count - 1) Wait(rule.GapMs);
            }
            if (r < repeat - 1) Wait(rule.GapMs);
        }

        _busy = false;
        StateChanged?.Invoke();
        Log?.Invoke(_abort ? "■ 중단됨" : "✔ 완료");
    }

    private void Execute(MacroAction action)
    {
        switch (action.Kind)
        {
            case ActionKind.MouseClick:
                InputSender.Click(action.X, action.Y, action.Button, action.ClickCount);
                break;

            case ActionKind.MouseMove:
                InputSender.MoveTo(action.X, action.Y);
                break;

            case ActionKind.KeyStroke:
                SendKeyStroke(action);
                break;

            case ActionKind.Text:
                InputSender.SendText(action.Text);
                break;

            case ActionKind.Delay:
                Wait(action.DelayMs);
                break;
        }
    }

    private static void SendKeyStroke(MacroAction a)
    {
        var mods = new List<int>(4);
        if (a.Ctrl)  mods.Add(0x11);
        if (a.Alt)   mods.Add(0x12);
        if (a.Shift) mods.Add(0x10);
        if (a.Win)   mods.Add(0x5B);

        switch (a.KeyMode)
        {
            case KeyStrokeMode.Down:
                foreach (var m in mods) InputSender.KeyDown(m);
                InputSender.KeyDown(a.VirtualKey);
                break;

            case KeyStrokeMode.Up:
                InputSender.KeyUp(a.VirtualKey);
                for (int i = mods.Count - 1; i >= 0; i--) InputSender.KeyUp(mods[i]);
                break;

            default:
                foreach (var m in mods) InputSender.KeyDown(m);
                InputSender.KeyPress(a.VirtualKey);
                for (int i = mods.Count - 1; i >= 0; i--) InputSender.KeyUp(mods[i]);
                break;
        }
    }

    /// <summary>중단 요청에 빠르게 반응하도록 잘게 나눠 대기</summary>
    private void Wait(int ms)
    {
        const int slice = 20;
        for (int elapsed = 0; elapsed < ms && !_abort; elapsed += slice)
        {
            Thread.Sleep(Math.Min(slice, ms - elapsed));
            if (EscPressed()) _abort = true;
        }
    }

    /// <summary>Esc 물리 키 눌림 — 감시가 꺼진 상태의 테스트 실행도 중단할 수 있도록 직접 확인</summary>
    private static bool EscPressed() => (GetAsyncKeyState(AbortVk) & 0x8000) != 0;

    private void Raise(string message)
    {
        Log?.Invoke(message);
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        Stop();
        _queue.CompleteAdding();
        _hook.Dispose();
    }
}
