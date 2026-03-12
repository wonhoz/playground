namespace Key.Map.Services;

/// ANSI 104-key 표준 레이아웃 정의
static class LayoutLibrary
{
    /// 키 단위 크기 (px). 키보드 총 폭 = 15 U = 690 px
    public const double U = 46.0;

    public static IReadOnlyList<KeyDef> AnsiKeys { get; } = Build();

    static List<KeyDef> Build()
    {
        var k = new List<KeyDef>();

        // ── Row 0: 기능키 행 (y = 0) ────────────────────────────────────
        double y0 = 0;
        k.Add(new("Escape",  "Esc",  0.00, y0));
        k.Add(new("F1",      "F1",   2.00, y0));
        k.Add(new("F2",      "F2",   3.00, y0));
        k.Add(new("F3",      "F3",   4.00, y0));
        k.Add(new("F4",      "F4",   5.00, y0));
        k.Add(new("F5",      "F5",   6.50, y0));
        k.Add(new("F6",      "F6",   7.50, y0));
        k.Add(new("F7",      "F7",   8.50, y0));
        k.Add(new("F8",      "F8",   9.50, y0));
        k.Add(new("F9",      "F9",  11.00, y0));
        k.Add(new("F10",     "F10", 12.00, y0));
        k.Add(new("F11",     "F11", 13.00, y0));
        k.Add(new("F12",     "F12", 14.00, y0));

        // ── Row 1: 숫자 행 (y = 1.25) ────────────────────────────────────
        double y1 = 1.25;
        (string code, string lbl)[] numRow =
        [
            ("OemTilde",  "`"), ("D1","1"), ("D2","2"), ("D3","3"), ("D4","4"),
            ("D5","5"), ("D6","6"), ("D7","7"), ("D8","8"), ("D9","9"),
            ("D0","0"), ("OemMinus","-"), ("OemPlus","=")
        ];
        for (int i = 0; i < numRow.Length; i++)
            k.Add(new(numRow[i].code, numRow[i].lbl, i, y1));
        k.Add(new("Back", "⌫", 13.00, y1, 2.00));

        // ── Row 2: QWERTY 행 (y = 2.25) ──────────────────────────────────
        double y2 = 2.25;
        k.Add(new("Tab",  "Tab", 0, y2, 1.50));
        string[] qRow = ["Q","W","E","R","T","Y","U","I","O","P"];
        for (int i = 0; i < qRow.Length; i++)
            k.Add(new(qRow[i], qRow[i], 1.50 + i, y2));
        k.Add(new("OemOpenBrackets",  "[",  11.50, y2));
        k.Add(new("OemCloseBrackets", "]",  12.50, y2));
        k.Add(new("OemBackslash",     "\\", 13.50, y2, 1.50));

        // ── Row 3: ASDF 행 (y = 3.25) ────────────────────────────────────
        double y3 = 3.25;
        k.Add(new("CapsLock", "Caps", 0, y3, 1.75));
        string[] aRow = ["A","S","D","F","G","H","J","K","L"];
        for (int i = 0; i < aRow.Length; i++)
            k.Add(new(aRow[i], aRow[i], 1.75 + i, y3));
        k.Add(new("OemSemicolon", ";", 10.75, y3));
        k.Add(new("OemQuotes",    "'", 11.75, y3));
        k.Add(new("Return", "Enter", 12.75, y3, 2.25));

        // ── Row 4: ZXCV 행 (y = 4.25) ────────────────────────────────────
        double y4 = 4.25;
        k.Add(new("LShiftKey", "Shift", 0, y4, 2.25));
        string[] zRow = ["Z","X","C","V","B","N","M"];
        for (int i = 0; i < zRow.Length; i++)
            k.Add(new(zRow[i], zRow[i], 2.25 + i, y4));
        k.Add(new("OemComma",    ",", 9.25,  y4));
        k.Add(new("OemPeriod",   ".", 10.25, y4));
        k.Add(new("OemQuestion", "/", 11.25, y4));
        k.Add(new("RShiftKey", "Shift", 12.25, y4, 2.75));

        // ── Row 5: 하단 행 (y = 5.25) ────────────────────────────────────
        double y5 = 5.25;
        k.Add(new("LControlKey", "Ctrl", 0.00,  y5, 1.25));
        k.Add(new("LWin",        "Win",  1.25,  y5, 1.25));
        k.Add(new("LMenu",       "Alt",  2.50,  y5, 1.25));
        k.Add(new("Space",       "Space",3.75,  y5, 6.25));
        k.Add(new("RMenu",       "Alt", 10.00,  y5, 1.25));
        k.Add(new("RWin",        "Win", 11.25,  y5, 1.25));
        k.Add(new("Apps",        "Menu",12.50,  y5, 1.25));
        k.Add(new("RControlKey", "Ctrl",13.75,  y5, 1.25));

        return k;
    }

    /// 단축키 문자열 → 키 코드 집합으로 변환
    public static HashSet<string> ParseShortcut(string shortcut)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var code in MapPart(part))
                result.Add(code);
        return result;
    }

    static string[] MapPart(string part) => part.Trim().ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL"           => ["LControlKey", "RControlKey"],
        "SHIFT"                        => ["LShiftKey", "RShiftKey"],
        "ALT"                          => ["LMenu", "RMenu"],
        "WIN" or "WINDOWS" or "WINKEY" => ["LWin", "RWin"],
        "SPACE"                        => ["Space"],
        "TAB"                          => ["Tab"],
        "ENTER" or "RETURN"            => ["Return"],
        "ESC" or "ESCAPE"              => ["Escape"],
        "BACKSPACE" or "BACK"          => ["Back"],
        "DELETE" or "DEL"              => ["Delete"],
        "CAPS" or "CAPSLOCK"           => ["CapsLock"],
        "+"                            => ["OemPlus"],
        "-"                            => ["OemMinus"],
        "["                            => ["OemOpenBrackets"],
        "]"                            => ["OemCloseBrackets"],
        "\\"                           => ["OemBackslash"],
        ";"                            => ["OemSemicolon"],
        "'"                            => ["OemQuotes"],
        ","                            => ["OemComma"],
        "."                            => ["OemPeriod"],
        "/"                            => ["OemQuestion"],
        "`"                            => ["OemTilde"],
        var p when p.Length == 1 && char.IsLetter(p[0]) => [p],
        var p when p.StartsWith("F") && int.TryParse(p[1..], out _) => [p],
        var p when p.Length == 1 && char.IsDigit(p[0]) => [$"D{p}"],
        var p => [p]
    };
}
