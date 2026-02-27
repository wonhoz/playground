using System.IO;
using System.Reflection;
using GlyphMap.Models;

namespace GlyphMap.Services;

/// <summary>
/// UnicodeData.txt + Blocks.txt 파싱 및 검색 서비스.
/// 백그라운드에서 한 번 로드 후 메모리에 캐시.
/// </summary>
public sealed class UnicodeDataService
{
    private List<GlyphEntry> _all = [];
    private List<(int Start, int End, string Name)> _blocks = [];

    public IReadOnlyList<GlyphEntry> All => _all;
    public IReadOnlyList<(int Start, int End, string Name)> Blocks => _blocks;

    public bool IsLoaded { get; private set; }
    public event Action? Loaded;

    // ──────────────────────────────────────────────────────────────────────
    // 로드
    // ──────────────────────────────────────────────────────────────────────
    public async Task LoadAsync()
    {
        await Task.Run(() =>
        {
            _blocks = ParseBlocks();
            _all    = ParseUnicodeData(_blocks);
            IsLoaded = true;
        });
        Loaded?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Blocks.txt 파싱
    // ──────────────────────────────────────────────────────────────────────
    private static List<(int Start, int End, string Name)> ParseBlocks()
    {
        var result = new List<(int, int, string)>(350);
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("GlyphMap.Resources.Blocks.txt")!;
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
            // 형식: 0000..007F; Basic Latin
            var semi = line.IndexOf(';');
            if (semi < 0) continue;
            var range = line[..semi].Trim();
            var name  = line[(semi + 1)..].Trim();
            var dots  = range.IndexOf("..", StringComparison.Ordinal);
            if (dots < 0) continue;
            var start = Convert.ToInt32(range[..dots], 16);
            var end   = Convert.ToInt32(range[(dots + 2)..], 16);
            result.Add((start, end, name));
        }
        return result;
    }

    // ──────────────────────────────────────────────────────────────────────
    // UnicodeData.txt 파싱
    // ──────────────────────────────────────────────────────────────────────
    private static List<GlyphEntry> ParseUnicodeData(
        List<(int Start, int End, string Name)> blocks)
    {
        var result = new List<GlyphEntry>(35_000);
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("GlyphMap.Resources.UnicodeData.txt")!;
        using var reader = new StreamReader(stream);

        string? line;
        int rangeStart = -1;
        string rangeBaseName = "";
        string rangeCat = "";

        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrEmpty(line)) continue;
            var fields = line.Split(';');
            if (fields.Length < 3) continue;

            var cp   = Convert.ToInt32(fields[0], 16);
            var name = fields[1];
            var cat  = fields[2];

            // 범위 시작 (예: <CJK Unified Ideograph, First>)
            if (name.EndsWith(", First>", StringComparison.OrdinalIgnoreCase))
            {
                rangeStart    = cp;
                rangeBaseName = name[1..name.IndexOf(',')].Trim();
                rangeCat      = cat;
                continue;
            }
            // 범위 끝
            if (name.EndsWith(", Last>", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = rangeStart; i <= cp; i++)
                {
                    var iName  = $"{rangeBaseName}-{i:X4}";
                    var iBlock = FindBlock(blocks, i);
                    result.Add(new GlyphEntry
                    {
                        CodePoint = i,
                        Name      = iName,
                        Category  = rangeCat,
                        Block     = iBlock,
                        SearchKey = iName.ToLowerInvariant()
                    });
                }
                rangeStart = -1;
                continue;
            }

            var block = FindBlock(blocks, cp);
            result.Add(new GlyphEntry
            {
                CodePoint = cp,
                Name      = name,
                Category  = cat,
                Block     = block,
                SearchKey = name.ToLowerInvariant()
            });
        }

        return result;
    }

    private static string FindBlock(List<(int Start, int End, string Name)> blocks, int cp)
    {
        // 이진 탐색으로 블록 찾기
        int lo = 0, hi = blocks.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var (s, e, _) = blocks[mid];
            if (cp < s)      hi = mid - 1;
            else if (cp > e) lo = mid + 1;
            else             return blocks[mid].Name;
        }
        return "Unknown";
    }

    // ──────────────────────────────────────────────────────────────────────
    // 검색 (퍼지: 쿼리 토큰이 이름에 모두 포함되면 매치)
    // ──────────────────────────────────────────────────────────────────────
    public IEnumerable<GlyphEntry> Search(string query, int limit = 500)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        // U+ 또는 코드포인트 숫자 검색
        if (query.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
            query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hexStr = query.StartsWith("U+", StringComparison.OrdinalIgnoreCase)
                ? query[2..] : query[2..];
            if (int.TryParse(hexStr, System.Globalization.NumberStyles.HexNumber,
                             null, out var cp))
            {
                var exact = _all.FirstOrDefault(g => g.CodePoint == cp);
                return exact != null ? [exact] : [];
            }
        }

        var lower  = query.ToLowerInvariant();
        var tokens = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return _all
            .Where(g => tokens.All(t => g.SearchKey.Contains(t)))
            .Take(limit);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 블록별 문자 목록 (카테고리 트리 클릭 시)
    // ──────────────────────────────────────────────────────────────────────
    public IReadOnlyList<GlyphEntry> GetByBlock(string blockName)
    {
        return _all.Where(g => g.Block == blockName).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────
    // 코드포인트로 단일 항목 조회
    // ──────────────────────────────────────────────────────────────────────
    public GlyphEntry? GetByCodePoint(int cp)
        => _all.FirstOrDefault(g => g.CodePoint == cp);

    // ──────────────────────────────────────────────────────────────────────
    // 카테고리 그룹 (사이드바 섹션 구분용)
    // ──────────────────────────────────────────────────────────────────────
    public static string GetCategoryGroup(string blockName) => blockName switch
    {
        var n when n.Contains("Emoji")   || n.Contains("Dingbat")        => "이모지 & 기호",
        var n when n.Contains("CJK")     || n.Contains("Hangul")
                || n.Contains("Katakana")|| n.Contains("Hiragana")
                || n.Contains("Bopomofo")|| n.Contains("Kana")           => "동아시아",
        var n when n.Contains("Latin")   || n.Contains("Greek")
                || n.Contains("Cyrillic")|| n.Contains("Armenian")
                || n.Contains("Hebrew")  || n.Contains("Arabic")
                || n.Contains("IPA")                                      => "유럽 & 중동",
        var n when n.Contains("Arrow")   || n.Contains("Box Drawing")
                || n.Contains("Block")   || n.Contains("Geometric")
                || n.Contains("Miscellaneous Symbol")                     => "기호 & 도형",
        var n when n.Contains("Mathematical") || n.Contains("Letterlike")
                || n.Contains("Number")  || n.Contains("Superscript")    => "수학 & 숫자",
        var n when n.Contains("Musical") || n.Contains("Playing Card")
                || n.Contains("Mahjong") || n.Contains("Domino")         => "게임 & 음악",
        var n when n.Contains("Supplemental") || n.Contains("Ancient")
                || n.Contains("Cuneiform")|| n.Contains("Hieroglyph")    => "고대 문자",
        var n when n.Contains("Basic Latin")                              => "기본 라틴",
        _                                                                  => "기타"
    };
}
