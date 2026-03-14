using System.IO;

namespace IconMaker.Services;

/// <summary>
/// PNG-embedded ICO 파일 인코더 (ICONDIR + ICONDIRENTRY + PNG 데이터)
/// </summary>
public static class IcoEncoder
{
    public static byte[] Encode(IReadOnlyList<(int size, byte[] png)> images)
    {
        int count = images.Count;
        int headerSize = 6 + count * 16;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // ICONDIR
        bw.Write((ushort)0);     // reserved
        bw.Write((ushort)1);     // type = 1 (icon)
        bw.Write((ushort)count);

        // 각 이미지의 오프셋 계산
        int offset = headerSize;
        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = offset;
            offset += images[i].png.Length;
        }

        // ICONDIRENTRY × count
        for (int i = 0; i < count; i++)
        {
            int sz = images[i].size;
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // bWidth  (0 = 256)
            bw.Write((byte)(sz >= 256 ? 0 : sz)); // bHeight (0 = 256)
            bw.Write((byte)0);                     // bColorCount
            bw.Write((byte)0);                     // bReserved
            bw.Write((ushort)1);                   // wPlanes
            bw.Write((ushort)32);                  // wBitCount
            bw.Write((uint)images[i].png.Length);  // dwBytesInRes
            bw.Write((uint)offsets[i]);            // dwImageOffset
        }

        // PNG 바이너리 데이터
        foreach (var (_, png) in images)
            bw.Write(png);

        return ms.ToArray();
    }
}
