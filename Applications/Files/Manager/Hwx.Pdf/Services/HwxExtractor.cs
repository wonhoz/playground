using System.IO;
using System.Text;

namespace HwxPdf.Services;

/// <summary>
/// 핸디소프트 전자결재 문서(.hwx)에서 본문 한글 문서를 꺼낸다.
/// hwx = "Handysoft Approval Document File" 헤더(결재 정보) + HWP 5.0 원본(OLE 복합 파일).
/// </summary>
public static class HwxExtractor
{
    private static readonly byte[] HwxMagic = Encoding.ASCII.GetBytes("Handysoft Approval Document File");
    private static readonly byte[] OleMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    public static bool IsHwx(string path)
    {
        Span<byte> head = stackalloc byte[32];
        using var fs = File.OpenRead(path);
        return fs.Read(head) == head.Length && head.SequenceEqual(HwxMagic);
    }

    /// <summary>본문을 임시 파일로 꺼내고 그 경로를 돌려준다 (.hwp 또는 .hwpx).</summary>
    public static string ExtractToTemp(string hwxPath, string tempDir)
    {
        var data = File.ReadAllBytes(hwxPath);
        if (!data.AsSpan().StartsWith(HwxMagic))
            throw new InvalidDataException("핸디소프트 결재 문서(hwx) 형식이 아닙니다.");

        // 본문은 보통 HWP 5.0(OLE). 혹시 HWPX(zip) 로 들어 있는 경우도 받아 준다.
        var (offset, ext) = (data.AsSpan().IndexOf(OleMagic), ".hwp");
        if (offset < 0)
            (offset, ext) = (data.AsSpan().IndexOf(ZipMagic), ".hwpx");
        if (offset < 0)
            throw new InvalidDataException("hwx 안에서 본문 한글 문서를 찾지 못했습니다.");

        Directory.CreateDirectory(tempDir);
        var tmp = Path.Combine(tempDir, $"{Guid.NewGuid():N}{ext}");
        File.WriteAllBytes(tmp, data[offset..]);
        return tmp;
    }
}
