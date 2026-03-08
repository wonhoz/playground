namespace Layout.Forge.Services;

using Layout.Forge.Models;

/// <summary>ANSI 104키 레이아웃 정의</summary>
public static class KeyboardLayout
{
    static KeyDef K(string id, string label, ushort sc, double w = 1.0) => new(id, label, sc, w);
    static KeyDef Gap(double w = 0.5) => new("", "", 0, w);

    // ── 메인 키보드 행 ───────────────────────────────────────────────────

    public static readonly IReadOnlyList<KeyDef> Row0 = new KeyDef[]
    {
        K("Esc","Esc",0x0001),
        Gap(0.5),
        K("F1","F1",0x003B), K("F2","F2",0x003C), K("F3","F3",0x003D), K("F4","F4",0x003E),
        Gap(0.5),
        K("F5","F5",0x003F), K("F6","F6",0x0040), K("F7","F7",0x0041), K("F8","F8",0x0042),
        Gap(0.5),
        K("F9","F9",0x0043), K("F10","F10",0x0044), K("F11","F11",0x0057), K("F12","F12",0x0058),
    };

    public static readonly IReadOnlyList<KeyDef> Row1 = new KeyDef[]
    {
        K("`","`",0x0029), K("1","1",0x0002), K("2","2",0x0003), K("3","3",0x0004),
        K("4","4",0x0005), K("5","5",0x0006), K("6","6",0x0007), K("7","7",0x0008),
        K("8","8",0x0009), K("9","9",0x000A), K("0","0",0x000B), K("-","-",0x000C),
        K("=","=",0x000D), K("Backspace","⌫ Backspace",0x000E,2.0),
    };

    public static readonly IReadOnlyList<KeyDef> Row2 = new KeyDef[]
    {
        K("Tab","Tab ⇥",0x000F,1.5), K("Q","Q",0x0010), K("W","W",0x0011), K("E","E",0x0012),
        K("R","R",0x0013), K("T","T",0x0014), K("Y","Y",0x0015), K("U","U",0x0016),
        K("I","I",0x0017), K("O","O",0x0018), K("P","P",0x0019),
        K("[","[",0x001A), K("]","]",0x001B), K("\\","\\",0x002B,1.5),
    };

    public static readonly IReadOnlyList<KeyDef> Row3 = new KeyDef[]
    {
        K("CapsLock","Caps Lock",0x003A,1.75),
        K("A","A",0x001E), K("S","S",0x001F), K("D","D",0x0020), K("F","F",0x0021),
        K("G","G",0x0022), K("H","H",0x0023), K("J","J",0x0024), K("K","K",0x0025),
        K("L","L",0x0026), K(";",";",0x0027), K("'","'",0x0028),
        K("Enter","Enter ↵",0x001C,2.25),
    };

    public static readonly IReadOnlyList<KeyDef> Row4 = new KeyDef[]
    {
        K("LShift","⇧ Shift",0x002A,2.25),
        K("Z","Z",0x002C), K("X","X",0x002D), K("C","C",0x002E), K("V","V",0x002F),
        K("B","B",0x0030), K("N","N",0x0031), K("M","M",0x0032),
        K(",",",",0x0033), K(".",".  ",0x0034), K("/","/",0x0035),
        K("RShift","⇧ Shift",0x0036,2.75),
    };

    public static readonly IReadOnlyList<KeyDef> Row5 = new KeyDef[]
    {
        K("LCtrl","Ctrl",0x001D,1.5), K("LWin","⊞",0xE05B,1.25), K("LAlt","Alt",0x0038,1.25),
        K("Space","",0x0039,6.25),
        K("RAlt","Alt",0xE038,1.25), K("RWin","⊞",0xE05C,1.25), K("Menu","☰",0xE05D,1.25),
        K("RCtrl","Ctrl",0xE01D,1.5),
    };

    // ── 네비게이션 클러스터 ──────────────────────────────────────────────

    public static readonly IReadOnlyList<KeyDef> NavRow0 = new KeyDef[]
    {
        K("PrtSc","PrtSc",0xE037), K("ScrLk","ScrLk",0x0046), K("Pause","Pause",0x0045),
    };

    public static readonly IReadOnlyList<KeyDef> NavRow1 = new KeyDef[]
    {
        K("Ins","Ins",0xE052), K("Home","Home",0xE047), K("PgUp","PgUp",0xE049),
    };

    public static readonly IReadOnlyList<KeyDef> NavRow2 = new KeyDef[]
    {
        K("Del","Del",0xE053), K("End","End",0xE04F), K("PgDn","PgDn",0xE051),
    };

    public static readonly IReadOnlyList<KeyDef> NavRow3 = new KeyDef[]
    {
        Gap(1.0), K("Up","↑",0xE048), Gap(1.0),
    };

    public static readonly IReadOnlyList<KeyDef> NavRow4 = new KeyDef[]
    {
        K("Left","←",0xE04B), K("Down","↓",0xE050), K("Right","→",0xE04D),
    };

    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> MainRows = new[]
        { Row0, Row1, Row2, Row3, Row4, Row5 };

    public static readonly IReadOnlyList<IReadOnlyList<KeyDef>> NavRows = new[]
        { NavRow0, NavRow1, NavRow2, NavRow3, NavRow4 };

    // ── 리매핑 대상 목록 ────────────────────────────────────────────────

    public static readonly IReadOnlyList<KeyTarget> Targets = BuildTargets();

    static IReadOnlyList<KeyTarget> BuildTargets()
    {
        var list = new List<KeyTarget>
        {
            new(0x0000, "── 비활성화 (키 막기) ──"),
            new(0x001D, "Left Ctrl"),    new(0xE01D, "Right Ctrl"),
            new(0x0038, "Left Alt"),     new(0xE038, "Right Alt"),
            new(0x002A, "Left Shift"),   new(0x0036, "Right Shift"),
            new(0xE05B, "Left Win"),     new(0xE05C, "Right Win"),
            new(0xE05D, "Menu"),         new(0x003A, "Caps Lock"),
            new(0x0001, "Esc"),          new(0x000F, "Tab"),
            new(0x001C, "Enter"),        new(0x000E, "Backspace"),
            new(0x0039, "Space"),
            new(0xE052, "Insert"),       new(0xE053, "Delete"),
            new(0xE047, "Home"),         new(0xE04F, "End"),
            new(0xE049, "Page Up"),      new(0xE051, "Page Down"),
            new(0xE048, "↑ Up"),         new(0xE050, "↓ Down"),
            new(0xE04B, "← Left"),       new(0xE04D, "→ Right"),
        };

        // F1-F12
        ushort[] fsc = { 0x003B,0x003C,0x003D,0x003E,0x003F,0x0040,
                         0x0041,0x0042,0x0043,0x0044,0x0057,0x0058 };
        for (int i = 0; i < 12; i++) list.Add(new(fsc[i], $"F{i+1}"));

        // A-Z
        ushort[] abc = { 0x001E,0x0030,0x002E,0x0020,0x0012,0x0021,
                         0x0022,0x0023,0x0017,0x0024,0x0025,0x0026,
                         0x0032,0x0031,0x0018,0x0019,0x0010,0x0013,
                         0x001F,0x0014,0x0016,0x002F,0x0011,0x002D,
                         0x0015,0x002C };
        for (int i = 0; i < 26; i++) list.Add(new(abc[i], ((char)('A' + i)).ToString()));

        // 0-9
        ushort[] nums = { 0x000B,0x0002,0x0003,0x0004,0x0005,
                          0x0006,0x0007,0x0008,0x0009,0x000A };
        for (int i = 0; i < 10; i++) list.Add(new(nums[i], i.ToString()));

        return list.AsReadOnly();
    }

    /// <summary>ScanCode → 표시 이름</summary>
    public static string GetKeyName(ushort sc)
        => Targets.FirstOrDefault(t => t.ScanCode == sc)?.Name
        ?? $"0x{sc:X4}";
}
