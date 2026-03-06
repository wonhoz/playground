using DeepDiff.Models;

namespace DeepDiff.Services;

public class HexDiffService
{
    public const int BytesPerRow = 16;
    public const int MaxRows     = 65536; // 1 MB

    public record HexDiffResult(List<HexDiffRow> Rows, long LeftSize, long RightSize, int DiffRows);

    public HexDiffResult Compare(string leftPath, string rightPath)
    {
        var lb = ReadBytes(leftPath);
        var rb = ReadBytes(rightPath);
        return BuildRows(lb, rb, lb.Length, rb.Length);
    }

    public HexDiffResult CompareBytes(byte[] left, byte[] right)
        => BuildRows(left, right, left.Length, right.Length);

    private static byte[] ReadBytes(string path)
    {
        if (!File.Exists(path)) return [];
        var fi = new FileInfo(path);
        long len = Math.Min(fi.Length, (long)MaxRows * BytesPerRow);
        var buf = new byte[len];
        using var fs = File.OpenRead(path);
        fs.ReadExactly(buf);
        return buf;
    }

    private static HexDiffResult BuildRows(byte[] lb, byte[] rb, long lLen, long rLen)
    {
        long maxLen = Math.Max(lb.Length, rb.Length);
        var rows = new List<HexDiffRow>((int)(maxLen / BytesPerRow) + 1);
        int diffRows = 0;

        for (long addr = 0; addr < maxLen; addr += BytesPerRow)
        {
            var lRow = GetRow(lb, addr);
            var rRow = GetRow(rb, addr);
            var differs = new bool[BytesPerRow];
            bool rowDiff = false;

            for (int b = 0; b < BytesPerRow; b++)
            {
                // 실제 데이터 범위 외 패딩 바이트는 차이 없음으로 처리
                bool lValid = addr + b < lLen;
                bool rValid = addr + b < rLen;
                differs[b] = lValid != rValid || (lValid && lRow[b] != rRow[b]);
                if (differs[b]) rowDiff = true;
            }

            string lDisp = FormatRow(lRow, addr, lLen);
            string rDisp = FormatRow(rRow, addr, rLen);
            rows.Add(new(addr, lRow, rRow, differs, lDisp, rDisp, rowDiff));
            if (rowDiff) diffRows++;
        }

        return new(rows, lLen, rLen, diffRows);
    }

    private static byte[] GetRow(byte[] data, long addr)
    {
        var row = new byte[BytesPerRow];
        for (int i = 0; i < BytesPerRow; i++)
        {
            long idx = addr + i;
            row[i] = idx < data.Length ? data[idx] : (byte)0;
        }
        return row;
    }

    private static string FormatRow(byte[] row, long addr, long dataLen)
    {
        var sb = new System.Text.StringBuilder(72);
        sb.Append("  ");
        sb.Append(addr.ToString("X8"));
        sb.Append("  ");
        sb.Append(FormatHex(row, dataLen, addr));
        sb.Append("   ");
        sb.Append(FormatAscii(row, dataLen, addr));
        sb.Append("  ");
        return sb.ToString();
    }

    public static string FormatHex(byte[] row, long dataLen, long addr)
    {
        var sb = new System.Text.StringBuilder(50);
        for (int i = 0; i < BytesPerRow; i++)
        {
            if (i > 0) sb.Append(' ');
            if (i == 8) sb.Append(' ');
            if (addr + i < dataLen) sb.Append(row[i].ToString("X2"));
            else sb.Append("  ");
        }
        return sb.ToString();
    }

    public static string FormatAscii(byte[] row, long dataLen, long addr)
    {
        var sb = new System.Text.StringBuilder(16);
        for (int i = 0; i < BytesPerRow; i++)
        {
            long idx = addr + i;
            if (idx < dataLen)
            {
                char c = row[i] is >= 0x20 and < 0x7F ? (char)row[i] : '.';
                sb.Append(c);
            }
            else sb.Append(' ');
        }
        return sb.ToString();
    }
}
