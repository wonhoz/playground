using ClipboardStacker.Models;

namespace ClipboardStacker.Services;

/// <summary>
/// 클립보드 히스토리를 FIFO 큐로 관리.
/// Push: 새 복사 → 앞에 추가 / Pop: 다음 붙여넣기 → 뒤에서 꺼냄.
/// </summary>
public class ClipboardStack
{
    private const int DefaultMax = 30;
    private readonly LinkedList<ClipEntry> _items = new();
    private int _maxHistory;

    public event Action? Changed;

    public IReadOnlyList<ClipEntry> Items    => [.. _items];
    public int                      Count    => _items.Count;
    public bool                     HasNext  => _items.Count > 0;

    public ClipboardStack(int maxHistory = DefaultMax)
    {
        _maxHistory = maxHistory;
    }

    /// <summary>새 텍스트를 스택 앞에 추가 (연속 동일 항목은 무시)</summary>
    public void Push(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_items.First?.Value.Text == text) return;

        _items.AddFirst(new ClipEntry(text));

        while (_items.Count > _maxHistory)
            _items.RemoveLast();

        Changed?.Invoke();
    }

    /// <summary>스택에서 가장 오래된 항목 꺼냄 (FIFO)</summary>
    public ClipEntry? PopNext()
    {
        var node = _items.Last;
        if (node is null) return null;
        _items.RemoveLast();
        Changed?.Invoke();
        return node.Value;
    }

    /// <summary>특정 항목 즉시 꺼냄</summary>
    public void Remove(ClipEntry entry)
    {
        var node = _items.Find(entry);
        if (node is not null)
        {
            _items.Remove(node);
            Changed?.Invoke();
        }
    }

    public void Clear()
    {
        _items.Clear();
        Changed?.Invoke();
    }

    public void SetMax(int max) => _maxHistory = max;
}
