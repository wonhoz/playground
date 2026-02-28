using System.Collections;
using System.Collections.Specialized;

namespace HexPeek.Models;

/// <summary>
/// WPF ListBox VirtualizingStackPanel과 함께 사용하는 가상화 행 컬렉션.
/// IList를 구현해 인덱서를 통해 필요한 행만 on-demand로 생성 → 수GB 파일도 메모리 효율적.
/// </summary>
public sealed class HexRowList : IList, INotifyCollectionChanged
{
    private readonly HexDocument _doc;

    public HexRowList(HexDocument doc) => _doc = doc;

    // ── IList 핵심 ────────────────────────────────────────────────────────
    public int Count => (int)Math.Min(int.MaxValue, _doc.RowCount);

    public object? this[int index]
    {
        get => _doc.GetRow(index);
        set => throw new NotSupportedException();
    }

    // ── INotifyCollectionChanged ──────────────────────────────────────────
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public void NotifyReset()
        => CollectionChanged?.Invoke(this,
               new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    // ── IList 나머지 (읽기 전용) ──────────────────────────────────────────
    public bool IsFixedSize  => false;
    public bool IsReadOnly   => true;
    public bool IsSynchronized => false;
    public object SyncRoot   => this;

    public bool Contains(object? value) => value is HexRow r && r.Offset < _doc.Length;
    public int  IndexOf(object? value)  => value is HexRow r ? (int)(r.Offset / 16) : -1;

    public void CopyTo(Array array, int index)
    {
        for (int i = 0; i < Count; i++) array.SetValue(this[i], index + i);
    }

    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < Count; i++) yield return this[i]!;
    }

    public int  Add(object? value)              => throw new NotSupportedException();
    public void Clear()                          => throw new NotSupportedException();
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value)            => throw new NotSupportedException();
    public void RemoveAt(int index)              => throw new NotSupportedException();
}
