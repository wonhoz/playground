using MouseFlick.Models;

namespace MouseFlick.Services;

internal static class ProfileManager
{
    private static readonly List<GestureProfile> _builtinPresets = BuildPresets();

    public static IReadOnlyList<GestureProfile> BuiltinPresets => _builtinPresets;

    /// <summary>프로세스 이름에 맞는 활성 프로필 반환 (비기본 프로필 우선)</summary>
    public static GestureProfile GetActiveProfile(AppSettings settings, string processName)
    {
        foreach (var p in settings.Profiles)
        {
            if (p.IsDefault) continue;
            if (p.ProcessNames.Any(n => processName.Contains(n, StringComparison.OrdinalIgnoreCase)))
                return p;
        }
        return settings.Profiles.FirstOrDefault(p => p.IsDefault)
            ?? new GestureProfile { IsDefault = true, Name = "기본" };
    }

    public static GestureAction? FindAction(GestureProfile profile, string gesture) =>
        profile.Actions.FirstOrDefault(a =>
            string.Equals(a.Gesture, gesture, StringComparison.OrdinalIgnoreCase));

    // ── 내장 프리셋 ──────────────────────────────────────────────────────────
    private static List<GestureProfile> BuildPresets() =>
    [
        new()
        {
            Id = "default", Name = "기본", IsDefault = true,
            ProcessNames = [],
            Actions =
            [
                new() { Gesture = "L",  Description = "뒤로",     KeyCombo = "Alt+Left"  },
                new() { Gesture = "R",  Description = "앞으로",    KeyCombo = "Alt+Right" },
                new() { Gesture = "U",  Description = "맨 위로",   KeyCombo = "Ctrl+Home" },
                new() { Gesture = "D",  Description = "맨 아래로", KeyCombo = "Ctrl+End"  },
            ]
        },
        new()
        {
            Id = "chrome", Name = "Chrome / Edge",
            ProcessNames = ["chrome", "msedge"],
            Actions =
            [
                new() { Gesture = "L",  Description = "뒤로",      KeyCombo = "Alt+Left"     },
                new() { Gesture = "R",  Description = "앞으로",     KeyCombo = "Alt+Right"    },
                new() { Gesture = "U",  Description = "새 탭",      KeyCombo = "Ctrl+T"       },
                new() { Gesture = "D",  Description = "탭 닫기",    KeyCombo = "Ctrl+W"       },
                new() { Gesture = "UD", Description = "탭 복구",    KeyCombo = "Ctrl+Shift+T" },
                new() { Gesture = "LR", Description = "새로고침",   KeyCombo = "F5"           },
            ]
        },
        new()
        {
            Id = "firefox", Name = "Firefox",
            ProcessNames = ["firefox"],
            Actions =
            [
                new() { Gesture = "L",  Description = "뒤로",      KeyCombo = "Alt+Left"     },
                new() { Gesture = "R",  Description = "앞으로",     KeyCombo = "Alt+Right"    },
                new() { Gesture = "U",  Description = "새 탭",      KeyCombo = "Ctrl+T"       },
                new() { Gesture = "D",  Description = "탭 닫기",    KeyCombo = "Ctrl+W"       },
                new() { Gesture = "UD", Description = "탭 복구",    KeyCombo = "Ctrl+Shift+T" },
                new() { Gesture = "LR", Description = "새로고침",   KeyCombo = "F5"           },
            ]
        },
        new()
        {
            Id = "devenv", Name = "Visual Studio",
            ProcessNames = ["devenv"],
            Actions =
            [
                new() { Gesture = "L", Description = "뒤로",      KeyCombo = "Ctrl+Minus"       },
                new() { Gesture = "R", Description = "앞으로",     KeyCombo = "Ctrl+Shift+Minus" },
                new() { Gesture = "U", Description = "실행 취소",  KeyCombo = "Ctrl+Z"           },
                new() { Gesture = "D", Description = "다시 실행",  KeyCombo = "Ctrl+Y"           },
            ]
        },
        new()
        {
            Id = "notepadpp", Name = "Notepad++",
            ProcessNames = ["notepad++"],
            Actions =
            [
                new() { Gesture = "L", Description = "뒤로",      KeyCombo = "Alt+Left"  },
                new() { Gesture = "R", Description = "앞으로",     KeyCombo = "Alt+Right" },
                new() { Gesture = "U", Description = "실행 취소",  KeyCombo = "Ctrl+Z"    },
                new() { Gesture = "D", Description = "다시 실행",  KeyCombo = "Ctrl+Y"    },
            ]
        },
        new()
        {
            Id = "code", Name = "VS Code",
            ProcessNames = ["code"],
            Actions =
            [
                new() { Gesture = "L", Description = "뒤로",      KeyCombo = "Alt+Left"  },
                new() { Gesture = "R", Description = "앞으로",     KeyCombo = "Alt+Right" },
                new() { Gesture = "U", Description = "실행 취소",  KeyCombo = "Ctrl+Z"    },
                new() { Gesture = "D", Description = "다시 실행",  KeyCombo = "Ctrl+Y"    },
            ]
        },
    ];
}
