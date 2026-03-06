namespace HexPeek.Models;

/// <summary>
/// HexView 한 행 = 16 바이트 데이터 + 표시용 포맷 문자열
/// </summary>
public sealed class HexRow
{
    public long   Offset { get; }
    public byte[] Bytes  { get; }   // 항상 16바이트 배열 (마지막 행은 Count만큼 유효)
    public int    Count  { get; }   // 실제 유효 바이트 수

    public HexRow(long offset, byte[] bytes, int count)
    {
        Offset = offset;
        Bytes  = bytes;
        Count  = count;
    }

    // ── 표시 문자열 ───────────────────────────────────────────────────────
    public string OffsetText => Offset.ToString("X8");

    public string HexText
    {
        get
        {
            var sb = new StringBuilder(50);
            for (int i = 0; i < 16; i++)
            {
                if (i == 8) sb.Append("  ");
                else if (i > 0) sb.Append(' ');

                if (i < Count) sb.Append(Bytes[i].ToString("X2"));
                else           sb.Append("  ");
            }
            return sb.ToString();
        }
    }

    public string AsciiText
    {
        get
        {
            var chars = new char[Count];
            for (int i = 0; i < Count; i++)
            {
                byte b = Bytes[i];
                chars[i] = (b >= 0x20 && b < 0x7F) ? (char)b : '.';
            }
            return new string(chars);
        }
    }

    // ── 동등성 (ScrollIntoView 등에서 사용) ──────────────────────────────
    public override bool Equals(object? obj) => obj is HexRow r && r.Offset == Offset;
    public override int  GetHashCode()       => Offset.GetHashCode();
}
