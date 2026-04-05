namespace CharPad.Data;

public record CharEntry(string Char, string Name, string Category);

public static class CharDatabase
{
    public static readonly List<CharEntry> All = BuildAll();

    // char 문자열 → CharEntry O(1) 조회용 (최근/즐겨찾기 탭 O(N²) 방지)
    public static readonly Dictionary<string, CharEntry> AllByChar = BuildByChar(All);

    private static Dictionary<string, CharEntry> BuildByChar(List<CharEntry> all)
    {
        var dict = new Dictionary<string, CharEntry>();
        foreach (var e in all) dict.TryAdd(e.Char, e);
        return dict;
    }

    private static List<CharEntry> BuildAll()
    {
        var list = new List<CharEntry>();

        // ── 화살표 ──────────────────────────────────────────────────
        AddRange(list, "arrow", new[]
        {
            ("→", "rightwards arrow"),
            ("←", "leftwards arrow"),
            ("↑", "upwards arrow"),
            ("↓", "downwards arrow"),
            ("↔", "left right arrow"),
            ("↕", "up down arrow"),
            ("↗", "north east arrow"),
            ("↘", "south east arrow"),
            ("↙", "south west arrow"),
            ("↖", "north west arrow"),
            ("⇒", "rightwards double arrow"),
            ("⇐", "leftwards double arrow"),
            ("⇑", "upwards double arrow"),
            ("⇓", "downwards double arrow"),
            ("⇔", "left right double arrow"),
            ("⇕", "up down double arrow"),
            ("➡", "black rightwards arrow"),
            ("⬅", "leftwards black arrow"),
            ("⬆", "upwards black arrow"),
            ("⬇", "downwards black arrow"),
            ("↩", "leftwards arrow with hook"),
            ("↪", "rightwards arrow with hook"),
            ("↵", "downwards arrow with corner leftwards"),
            ("⟵", "long leftwards arrow"),
            ("⟶", "long rightwards arrow"),
            ("⟷", "long left right arrow"),
            ("⟸", "long leftwards double arrow"),
            ("⟹", "long rightwards double arrow"),
            ("↺", "anticlockwise open circle arrow"),
            ("↻", "clockwise open circle arrow"),
            ("⤴", "arrow pointing rightwards then curving upwards"),
            ("⤵", "arrow pointing rightwards then curving downwards"),
        });

        // ── 수학 ────────────────────────────────────────────────────
        AddRange(list, "math", new[]
        {
            ("±", "plus-minus sign"),
            ("∓", "minus-or-plus sign"),
            ("×", "multiplication sign"),
            ("÷", "division sign"),
            ("·", "middle dot"),
            ("∞", "infinity"),
            ("∑", "n-ary summation"),
            ("∏", "n-ary product"),
            ("∂", "partial differential"),
            ("∇", "nabla"),
            ("√", "square root"),
            ("∛", "cube root"),
            ("∫", "integral"),
            ("∬", "double integral"),
            ("≈", "almost equal to"),
            ("≠", "not equal to"),
            ("≡", "identical to"),
            ("≤", "less-than or equal to"),
            ("≥", "greater-than or equal to"),
            ("≪", "much less-than"),
            ("≫", "much greater-than"),
            ("∝", "proportional to"),
            ("°", "degree sign"),
            ("∆", "increment"),
            ("⊕", "circled plus"),
            ("⊗", "circled times"),
            ("∈", "element of"),
            ("∉", "not an element of"),
            ("⊂", "subset of"),
            ("⊃", "superset of"),
            ("∪", "union"),
            ("∩", "intersection"),
            ("∅", "empty set"),
            ("¼", "vulgar fraction one quarter"),
            ("½", "vulgar fraction one half"),
            ("¾", "vulgar fraction three quarters"),
            ("π", "greek small letter pi"),
            ("φ", "greek small letter phi"),
            ("θ", "greek small letter theta"),
            ("λ", "greek small letter lambda"),
            ("μ", "greek small letter mu"),
            ("σ", "greek small letter sigma"),
            ("Σ", "greek capital letter sigma"),
            ("Π", "greek capital letter pi"),
            ("Ω", "greek capital letter omega"),
            ("α", "greek small letter alpha"),
            ("β", "greek small letter beta"),
            ("γ", "greek small letter gamma"),
            ("δ", "greek small letter delta"),
        });

        // ── 기호 ────────────────────────────────────────────────────
        AddRange(list, "symbol", new[]
        {
            ("©", "copyright sign"),
            ("™", "trade mark sign"),
            ("®", "registered sign"),
            ("§", "section sign"),
            ("¶", "pilcrow sign"),
            ("†", "dagger"),
            ("‡", "double dagger"),
            ("•", "bullet"),
            ("…", "horizontal ellipsis"),
            ("‰", "per mille sign"),
            ("′", "prime"),
            ("″", "double prime"),
            ("‴", "triple prime"),
            ("«", "left-pointing double angle quotation mark"),
            ("»", "right-pointing double angle quotation mark"),
            ("\u201C", "left double quotation mark"),
            ("\u201D", "right double quotation mark"),
            ("\u2018", "left single quotation mark"),
            ("\u2019", "right single quotation mark"),
            ("‹", "single left-pointing angle quotation mark"),
            ("›", "single right-pointing angle quotation mark"),
            ("—", "em dash"),
            ("–", "en dash"),
            ("✓", "check mark"),
            ("✗", "ballot x"),
            ("★", "black star"),
            ("☆", "white star"),
            ("♪", "eighth note"),
            ("♫", "beamed eighth notes"),
            ("♠", "black spade suit"),
            ("♥", "black heart suit"),
            ("♦", "black diamond suit"),
            ("♣", "black club suit"),
            ("☀", "black sun with rays"),
            ("☁", "cloud"),
            ("☂", "umbrella"),
            ("☃", "snowman"),
            ("⚡", "high voltage sign"),
            ("✨", "sparkles"),
            ("◆", "black diamond"),
            ("◇", "white diamond"),
            ("▲", "black up-pointing triangle"),
            ("▼", "black down-pointing triangle"),
            ("▶", "black right-pointing triangle"),
            ("◀", "black left-pointing triangle"),
            ("●", "black circle"),
            ("○", "white circle"),
        });

        // ── 통화 ────────────────────────────────────────────────────
        AddRange(list, "currency", new[]
        {
            ("$",  "dollar sign"),
            ("€",  "euro sign"),
            ("£",  "pound sign"),
            ("¥",  "yen sign"),
            ("₩",  "won sign"),
            ("₺",  "turkish lira sign"),
            ("₹",  "indian rupee sign"),
            ("₿",  "bitcoin sign"),
            ("₽",  "ruble sign"),
            ("¢",  "cent sign"),
            ("¤",  "currency sign"),
            ("₦",  "naira sign"),
            ("₴",  "hryvnia sign"),
            ("₱",  "philippine peso sign"),
            ("₫",  "dong sign"),
            ("฿",  "thai baht sign"),
            ("₡",  "colon sign"),
            ("₪",  "new shekel sign"),
        });

        // ── 위첨자/아래첨자 ─────────────────────────────────────────
        AddRange(list, "super", new[]
        {
            ("⁰", "superscript zero"),
            ("¹", "superscript one"),
            ("²", "superscript two"),
            ("³", "superscript three"),
            ("⁴", "superscript four"),
            ("⁵", "superscript five"),
            ("⁶", "superscript six"),
            ("⁷", "superscript seven"),
            ("⁸", "superscript eight"),
            ("⁹", "superscript nine"),
            ("ⁿ", "superscript latin small letter n"),
            ("ˣ", "modifier letter small x"),
            ("₀", "subscript zero"),
            ("₁", "subscript one"),
            ("₂", "subscript two"),
            ("₃", "subscript three"),
            ("₄", "subscript four"),
            ("₅", "subscript five"),
            ("₆", "subscript six"),
            ("₇", "subscript seven"),
            ("₈", "subscript eight"),
            ("₉", "subscript nine"),
            ("ₙ", "subscript latin small letter n"),
        });

        // ── 선/박스 그리기 ─────────────────────────────────────────
        AddRange(list, "box", new[]
        {
            ("─", "box drawings light horizontal"),
            ("│", "box drawings light vertical"),
            ("┌", "box drawings light down and right"),
            ("┐", "box drawings light down and left"),
            ("└", "box drawings light up and right"),
            ("┘", "box drawings light up and left"),
            ("├", "box drawings light vertical and right"),
            ("┤", "box drawings light vertical and left"),
            ("┬", "box drawings light down and horizontal"),
            ("┴", "box drawings light up and horizontal"),
            ("┼", "box drawings light vertical and horizontal"),
            ("═", "box drawings double horizontal"),
            ("║", "box drawings double vertical"),
            ("╔", "box drawings double down and right"),
            ("╗", "box drawings double down and left"),
            ("╚", "box drawings double up and right"),
            ("╝", "box drawings double up and left"),
            ("╠", "box drawings double vertical and right"),
            ("╣", "box drawings double vertical and left"),
            ("╦", "box drawings double down and horizontal"),
            ("╩", "box drawings double up and horizontal"),
            ("╬", "box drawings double vertical and horizontal"),
            ("╭", "box drawings light arc down and right"),
            ("╮", "box drawings light arc down and left"),
            ("╯", "box drawings light arc up and left"),
            ("╰", "box drawings light arc up and right"),
            ("┄", "box drawings light triple dash horizontal"),
            ("┆", "box drawings light triple dash vertical"),
            ("┅", "box drawings heavy triple dash horizontal"),
            ("┇", "box drawings heavy triple dash vertical"),
            ("━", "box drawings heavy horizontal"),
            ("┃", "box drawings heavy vertical"),
            ("░", "light shade"),
            ("▒", "medium shade"),
            ("▓", "dark shade"),
            ("█", "full block"),
            ("▄", "lower half block"),
            ("▀", "upper half block"),
            ("▌", "left half block"),
            ("▐", "right half block"),
        });

        // ── 이모지 ─────────────────────────────────────────────────
        AddRange(list, "emoji", new[]
        {
            ("😀", "grinning face"),
            ("😊", "smiling face with smiling eyes"),
            ("😂", "face with tears of joy"),
            ("🤔", "thinking face"),
            ("😎", "smiling face with sunglasses"),
            ("🤗", "hugging face"),
            ("😭", "loudly crying face"),
            ("🥳", "partying face"),
            ("😅", "grinning face with sweat"),
            ("😍", "smiling face with heart-eyes"),
            ("🙄", "face with rolling eyes"),
            ("😤", "face with steam from nose"),
            ("💡", "light bulb"),
            ("❤️", "red heart"),
            ("⭐", "star"),
            ("🔥", "fire"),
            ("✅", "white heavy check mark"),
            ("❌", "cross mark"),
            ("⚠️", "warning sign"),
            ("🎉", "party popper"),
            ("🚀", "rocket"),
            ("💻", "laptop computer"),
            ("📱", "mobile phone"),
            ("🔧", "wrench"),
            ("⚙️", "gear"),
            ("📋", "clipboard"),
            ("📌", "pushpin"),
            ("🔑", "key"),
            ("🎯", "direct hit"),
            ("👍", "thumbs up sign"),
            ("👀", "eyes"),
            ("🙏", "folded hands"),
            ("💪", "flexed biceps"),
            ("🌟", "glowing star"),
            ("🎵", "musical note"),
            ("📎", "paperclip"),
            ("🔍", "left-pointing magnifying glass"),
            ("💬", "speech balloon"),
            ("📅", "calendar"),
            ("🏆", "trophy"),
        });

        return list;
    }

    private static void AddRange(List<CharEntry> list, string category,
        IEnumerable<(string ch, string name)> items)
    {
        foreach (var (ch, name) in items)
            list.Add(new CharEntry(ch, name, category));
    }

    public static IEnumerable<CharEntry> GetByCategory(string category)
        => All.Where(e => e.Category == category);

    public static IEnumerable<CharEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var q = query.ToLower();
        // 정확 일치(0) → 이름 시작 일치(1) → 이름 포함 일치(2) 순 정렬
        return All
            .Where(e =>
                e.Char.Equals(query, StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e =>
                e.Char.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0 :
                e.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 1 : 2);
    }
}
