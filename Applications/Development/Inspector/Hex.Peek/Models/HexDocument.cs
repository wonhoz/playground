namespace HexPeek.Models;

/// <summary>
/// 파일을 MemoryMappedFile 또는 바이트 배열로 로드해 바이트 단위 접근을 제공한다.
/// 50MB 이하는 byte[] 직접 로드, 초과는 MemoryMappedFile 사용.
/// </summary>
public sealed class HexDocument : IDisposable
{
    private MemoryMappedFile?         _mmf;
    private MemoryMappedViewAccessor? _view;
    private byte[]?                   _buffer;
    private bool                      _disposed;

    private const long MmfThreshold = 50L * 1024 * 1024; // 50 MB

    public string FilePath { get; private set; } = "";
    public long   Length   { get; private set; }
    public long   RowCount => (Length + 15) / 16;
    public bool   IsLoaded => Length > 0;
    public bool   IsDirty  { get; private set; }

    // ── 로드 ──────────────────────────────────────────────────────────────
    public static HexDocument Load(string path)
    {
        var doc = new HexDocument();
        doc.LoadFile(path);
        return doc;
    }

    private void LoadFile(string path)
    {
        FilePath = path;
        Length   = new FileInfo(path).Length;

        if (Length == 0) return;

        if (Length <= MmfThreshold)
        {
            _buffer = File.ReadAllBytes(path);
        }
        else
        {
            _mmf  = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }
    }

    // ── 바이트 읽기 ───────────────────────────────────────────────────────
    public byte ReadByte(long offset)
    {
        if (offset < 0 || offset >= Length) return 0;
        return _buffer != null ? _buffer[offset] : _view!.ReadByte(offset);
    }

    public int ReadBytes(long offset, byte[] dest, int count)
    {
        if (offset >= Length) return 0;
        int actual = (int)Math.Min(count, Length - offset);
        if (actual <= 0) return 0;

        if (_buffer != null)
            Buffer.BlockCopy(_buffer, (int)offset, dest, 0, actual);
        else
            _view!.ReadArray(offset, dest, 0, actual);

        return actual;
    }

    // ── 바이트 쓰기 (byte[] 모드 전용) ───────────────────────────────────
    public void WriteByte(long offset, byte value)
    {
        if (_buffer == null || offset < 0 || offset >= Length) return;
        _buffer[offset] = value;
        IsDirty = true;
    }

    public void Save()
    {
        if (_buffer == null || !IsDirty) return;
        File.WriteAllBytes(FilePath, _buffer);
        IsDirty = false;
    }

    public void SaveAs(string newPath)
    {
        if (_buffer == null) return;
        File.WriteAllBytes(newPath, _buffer);
        FilePath = newPath;
        IsDirty  = false;
    }

    // ── 행 단위 접근 ──────────────────────────────────────────────────────
    public HexRow GetRow(long rowIndex)
    {
        long offset = rowIndex * 16;
        var  bytes  = new byte[16];
        int  count  = ReadBytes(offset, bytes, 16);
        return new HexRow(offset, bytes, count);
    }

    // ── IDisposable ───────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view?.Dispose();
        _mmf?.Dispose();
        _buffer = null;
    }
}
