namespace HexPeek.Services;

public record ParsedField(long Offset, int Length, string Name, string Description, System.Windows.Media.Color Color);

/// <summary>
/// 파일 시그니처를 감지하고 헤더 구조체를 파싱해 색상 하이라이트 필드 목록을 반환한다.
/// </summary>
public static class StructureParser
{
    public static string DetectFormat(HexDocument doc)
    {
        if (doc.Length < 4) return "Unknown";

        byte b0 = doc.ReadByte(0), b1 = doc.ReadByte(1),
             b2 = doc.ReadByte(2), b3 = doc.ReadByte(3);

        // PNG: 89 50 4E 47
        if (b0 == 0x89 && b1 == 0x50 && b2 == 0x4E && b3 == 0x47) return "PNG";
        // JPEG: FF D8 FF
        if (b0 == 0xFF && b1 == 0xD8 && b2 == 0xFF) return "JPEG";
        // ZIP/PK: 50 4B 03 04
        if (b0 == 0x50 && b1 == 0x4B) return "ZIP";
        // PE: MZ
        if (b0 == 0x4D && b1 == 0x5A) return "PE";
        // GIF: GIF8
        if (b0 == 0x47 && b1 == 0x49 && b2 == 0x46) return "GIF";
        // BMP: BM
        if (b0 == 0x42 && b1 == 0x4D) return "BMP";
        // ELF
        if (b0 == 0x7F && b1 == 0x45 && b2 == 0x4C && b3 == 0x46) return "ELF";
        // PDF: %PDF
        if (b0 == 0x25 && b1 == 0x50 && b2 == 0x44 && b3 == 0x46) return "PDF";

        return "Unknown";
    }

    public static List<ParsedField> Parse(HexDocument doc)
    {
        return DetectFormat(doc) switch
        {
            "PNG"  => ParsePng(doc),
            "JPEG" => ParseJpeg(doc),
            "ZIP"  => ParseZip(doc),
            "PE"   => ParsePe(doc),
            "GIF"  => ParseGif(doc),
            "BMP"  => ParseBmp(doc),
            _      => []
        };
    }

    // ── PNG ───────────────────────────────────────────────────────────────
    private static List<ParsedField> ParsePng(HexDocument doc)
    {
        var cyan   = System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4);
        var purple = System.Windows.Media.Color.FromRgb(0xAB, 0x47, 0xBC);
        var green  = System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50);
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00);

        var fields = new List<ParsedField>
        {
            new(0,  8, "PNG Signature", "\\x89PNG\\r\\n\\x1a\\n", cyan),
            new(8,  4, "IHDR Length",   "13 (0x0D)", purple),
            new(12, 4, "IHDR Type",     "\"IHDR\"", purple),
            new(16, 4, "Width",         "픽셀 너비", orange),
            new(20, 4, "Height",        "픽셀 높이", orange),
            new(24, 1, "Bit Depth",     "채널당 비트 수", green),
            new(25, 1, "Color Type",    "0=Gray 2=RGB 3=Palette 4=GrayA 6=RGBA", green),
            new(26, 1, "Compression",   "0=Deflate", green),
            new(27, 1, "Filter",        "0=Adaptive", green),
            new(28, 1, "Interlace",     "0=None 1=Adam7", green),
            new(29, 4, "IHDR CRC",      "CRC-32", purple),
        };

        // 청크들 파싱
        long pos = 33;
        int  maxChunks = 30;
        while (pos + 12 <= doc.Length && maxChunks-- > 0)
        {
            var lenBytes = new byte[4];
            doc.ReadBytes(pos, lenBytes, 4);
            int  chunkLen  = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];
            var  typeBytes = new byte[4];
            doc.ReadBytes(pos + 4, typeBytes, 4);
            string chunkType = Encoding.ASCII.GetString(typeBytes);

            fields.Add(new(pos,     4, $"{chunkType} Length", chunkLen.ToString(), purple));
            fields.Add(new(pos + 4, 4, $"{chunkType} Type",   chunkType, cyan));
            if (chunkLen > 0 && pos + 8 + chunkLen <= doc.Length)
                fields.Add(new(pos + 8, chunkLen, $"{chunkType} Data", $"{chunkLen} bytes", orange));
            fields.Add(new(pos + 8 + chunkLen, 4, $"{chunkType} CRC", "CRC-32", purple));

            if (chunkType == "IEND") break;
            pos += 12 + chunkLen;
        }

        return fields;
    }

    // ── JPEG ──────────────────────────────────────────────────────────────
    private static List<ParsedField> ParseJpeg(HexDocument doc)
    {
        var cyan   = System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4);
        var purple = System.Windows.Media.Color.FromRgb(0xAB, 0x47, 0xBC);
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00);

        var fields = new List<ParsedField>
        {
            new(0, 2, "SOI Marker", "FF D8 — Start of Image", cyan),
        };

        long pos = 2;
        int  maxSegs = 50;
        while (pos + 4 <= doc.Length && maxSegs-- > 0)
        {
            if (doc.ReadByte(pos) != 0xFF) break;
            byte marker = doc.ReadByte(pos + 1);
            string name = marker switch
            {
                0xE0 => "APP0",  0xE1 => "APP1 (EXIF)",
                0xDB => "DQT",   0xC0 => "SOF0",
                0xC4 => "DHT",   0xDA => "SOS",
                0xD9 => "EOI",   _    => $"FF {marker:X2}"
            };

            if (marker == 0xD9) { fields.Add(new(pos, 2, "EOI Marker", "End of Image", cyan)); break; }

            var segLenBytes = new byte[2];
            doc.ReadBytes(pos + 2, segLenBytes, 2);
            int segLen = (segLenBytes[0] << 8) | segLenBytes[1];

            fields.Add(new(pos,     2, $"{name} Marker", $"FF {marker:X2}", cyan));
            fields.Add(new(pos + 2, 2, $"{name} Length", segLen.ToString(), purple));
            if (segLen > 2)
                fields.Add(new(pos + 4, segLen - 2, $"{name} Data", $"{segLen - 2} bytes", orange));

            pos += 2 + segLen;
        }

        return fields;
    }

    // ── ZIP ───────────────────────────────────────────────────────────────
    private static List<ParsedField> ParseZip(HexDocument doc)
    {
        var cyan   = System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4);
        var purple = System.Windows.Media.Color.FromRgb(0xAB, 0x47, 0xBC);
        var green  = System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50);
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00);

        var fields = new List<ParsedField>();
        long pos = 0;

        while (pos + 30 <= doc.Length)
        {
            // Local file header signature: 50 4B 03 04
            var sig = new byte[4];
            doc.ReadBytes(pos, sig, 4);
            if (sig[0] != 0x50 || sig[1] != 0x4B) break;

            if (sig[2] == 0x03 && sig[3] == 0x04) // Local file header
            {
                var hdr = new byte[30];
                doc.ReadBytes(pos, hdr, 30);
                int compLen   = (hdr[18] | (hdr[19] << 8) | (hdr[20] << 16) | (hdr[21] << 24));
                int uncompLen = (hdr[22] | (hdr[23] << 8) | (hdr[24] << 16) | (hdr[25] << 24));
                int fnLen     = hdr[26] | (hdr[27] << 8);
                int extraLen  = hdr[28] | (hdr[29] << 8);

                var fnBytes = new byte[fnLen];
                doc.ReadBytes(pos + 30, fnBytes, fnLen);
                string fname = Encoding.UTF8.GetString(fnBytes);

                fields.Add(new(pos,      4, "LFH Signature",    "50 4B 03 04", cyan));
                fields.Add(new(pos + 4,  2, "Version",          hdr[4].ToString(), purple));
                fields.Add(new(pos + 6,  2, "Flags",            (hdr[6] | (hdr[7] << 8)).ToString("X4"), purple));
                fields.Add(new(pos + 8,  2, "Compression",      hdr[8].ToString(), green));
                fields.Add(new(pos + 14, 4, "CRC-32",           $"{hdr[14] | (hdr[15] << 8) | (hdr[16] << 16) | (hdr[17] << 24):X8}", purple));
                fields.Add(new(pos + 18, 4, "Compressed Size",  compLen.ToString(), orange));
                fields.Add(new(pos + 22, 4, "Uncompressed Size",uncompLen.ToString(), orange));
                fields.Add(new(pos + 26, 2, "Filename Length",  fnLen.ToString(), green));
                fields.Add(new(pos + 30, fnLen, "Filename",     fname, orange));
                if (compLen > 0)
                    fields.Add(new(pos + 30 + fnLen + extraLen, compLen, "File Data", $"{compLen} bytes", green));

                pos += 30 + fnLen + extraLen + compLen;
            }
            else if (sig[2] == 0x01 && sig[3] == 0x02) // Central directory
            {
                fields.Add(new(pos, 4, "Central Directory", "50 4B 01 02", cyan));
                break;
            }
            else break;
        }

        return fields;
    }

    // ── PE ────────────────────────────────────────────────────────────────
    private static List<ParsedField> ParsePe(HexDocument doc)
    {
        var cyan   = System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4);
        var purple = System.Windows.Media.Color.FromRgb(0xAB, 0x47, 0xBC);
        var green  = System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50);
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00);

        var fields = new List<ParsedField>
        {
            new(0,  2, "MZ Signature",       "4D 5A", cyan),
            new(2,  2, "Last Page Bytes",     "", purple),
            new(4,  2, "Pages",              "", purple),
            new(60, 4, "PE Header Offset",   "", orange),
        };

        var peOffBytes = new byte[4];
        doc.ReadBytes(60, peOffBytes, 4);
        long peOff = peOffBytes[0] | ((long)peOffBytes[1] << 8) | ((long)peOffBytes[2] << 16) | ((long)peOffBytes[3] << 24);

        if (peOff > 0 && peOff + 24 <= doc.Length)
        {
            fields.Add(new(peOff,     4, "PE Signature",     "50 45 00 00", cyan));
            fields.Add(new(peOff + 4, 2, "Machine",          "", green));
            fields.Add(new(peOff + 6, 2, "Number of Sections","", green));
            fields.Add(new(peOff + 8, 4, "Timestamp",        "", purple));
            fields.Add(new(peOff + 16,2, "Optional Hdr Size","", purple));
            fields.Add(new(peOff + 18,2, "Characteristics",  "", orange));
        }

        return fields;
    }

    // ── GIF ───────────────────────────────────────────────────────────────
    private static List<ParsedField> ParseGif(HexDocument doc)
    {
        var cyan   = System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4);
        var purple = System.Windows.Media.Color.FromRgb(0xAB, 0x47, 0xBC);
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00);

        return
        [
            new(0, 6, "GIF Signature", "GIF87a or GIF89a", cyan),
            new(6, 2, "Canvas Width",  "픽셀 너비", orange),
            new(8, 2, "Canvas Height", "픽셀 높이", orange),
            new(10,1, "Packed Field",  "GCT, Color Resolution, Sort, Size", purple),
            new(11,1, "Background Color Index", "", purple),
            new(12,1, "Pixel Aspect Ratio",     "", purple),
        ];
    }

    // ── BMP ───────────────────────────────────────────────────────────────
    private static List<ParsedField> ParseBmp(HexDocument doc)
    {
        var cyan   = System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xD4);
        var purple = System.Windows.Media.Color.FromRgb(0xAB, 0x47, 0xBC);
        var orange = System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00);
        var green  = System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50);

        return
        [
            new(0,  2,  "BM Signature",  "42 4D", cyan),
            new(2,  4,  "File Size",     "바이트", orange),
            new(6,  4,  "Reserved",      "", purple),
            new(10, 4,  "Pixel Offset",  "바이트", orange),
            new(14, 4,  "DIB Hdr Size",  "바이트", purple),
            new(18, 4,  "Width",         "픽셀", orange),
            new(22, 4,  "Height",        "픽셀 (음수=top-down)", orange),
            new(26, 2,  "Color Planes",  "", green),
            new(28, 2,  "Bits per Pixel","", green),
            new(30, 4,  "Compression",   "", purple),
        ];
    }
}
