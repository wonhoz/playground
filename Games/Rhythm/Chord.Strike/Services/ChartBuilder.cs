namespace ChordStrike.Services;

/// <summary>내장 채보 생성기</summary>
public static class ChartBuilder
{
    public static List<Chart> BuiltInCharts()
    {
        return
        [
            BuildEasy(),
            BuildMedium(),
            BuildHard(),
        ];
    }

    // ── 쉬움: 4레인 단음 위주 ─────────────────────────────────────
    private static Chart BuildEasy()
    {
        var notes = new List<Note>();
        // 4박자 반복 패턴 × 16마디
        int[] pattern = [0, 2, 1, 3, 0, 3, 2, 1];
        for (int bar = 0; bar < 16; bar++)
            for (int i = 0; i < pattern.Length; i++)
                notes.Add(new Note { Lane = pattern[i], Beat = bar * 4.0 + i * 0.5, Type = NoteType.Single });

        return new Chart { Title = "Easy Groove", Artist = "Built-in", BPM = 100, Notes = notes };
    }

    // ── 보통: 8레인, 화음 등장 ────────────────────────────────────
    private static Chart BuildMedium()
    {
        var notes = new List<Note>();
        // 양손 교대 패턴
        for (int bar = 0; bar < 20; bar++)
        {
            // 왼손 (0~3)
            notes.Add(new Note { Lane = bar % 4,       Beat = bar * 4.0 + 0, Type = NoteType.Single });
            notes.Add(new Note { Lane = (bar + 1) % 4, Beat = bar * 4.0 + 1, Type = NoteType.Single });
            // 오른손 (4~7)
            notes.Add(new Note { Lane = 4 + bar % 4,       Beat = bar * 4.0 + 2, Type = NoteType.Single });
            notes.Add(new Note { Lane = 4 + (bar + 1) % 4, Beat = bar * 4.0 + 3, Type = NoteType.Single });

            // 2마다 화음
            if (bar % 2 == 1)
            {
                notes.Add(new Note { Lane = 1, Beat = bar * 4.0 + 0.5, Type = NoteType.Single });
                notes.Add(new Note { Lane = 5, Beat = bar * 4.0 + 0.5, Type = NoteType.Single });
            }
        }
        return new Chart { Title = "Chord Awakening", Artist = "Built-in", BPM = 130, Notes = notes };
    }

    // ── 어려움: 8레인 + Hold 노트 + 빠른 아르페지오 ──────────────
    private static Chart BuildHard()
    {
        var notes = new List<Note>();
        double bpm = 160;
        for (int bar = 0; bar < 24; bar++)
        {
            // 빠른 연속음 (아르페지오)
            for (int i = 0; i < 8; i++)
                notes.Add(new Note { Lane = i, Beat = bar * 4.0 + i * 0.25, Type = NoteType.Single });

            // Hold 노트
            if (bar % 4 == 0)
            {
                notes.Add(new Note { Lane = 0, Beat = bar * 4.0 + 2, Type = NoteType.Hold, Duration = 1.5 });
                notes.Add(new Note { Lane = 7, Beat = bar * 4.0 + 2, Type = NoteType.Hold, Duration = 1.5 });
            }
        }
        return new Chart { Title = "Full Strike", Artist = "Built-in", BPM = bpm, Notes = notes };
    }
}
