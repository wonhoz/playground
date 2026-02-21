namespace BeatDrop.Engine;

public enum Difficulty { Easy, Normal, Hard }

/// <summary>
/// BPM 기반 자동 노트 패턴 생성.
/// </summary>
public static class PatternGenerator
{
    public static List<Note> Generate(double bpm, double duration, Difficulty difficulty, Random rng)
    {
        var notes = new List<Note>();
        double beatInterval = 60.0 / bpm;

        // 난이도별 설정
        double subdivision = difficulty switch
        {
            Difficulty.Easy => 1.0,    // 1비트마다
            Difficulty.Normal => 0.5,  // 반박마다
            _ => 0.25                  // 16분음표
        };

        double density = difficulty switch
        {
            Difficulty.Easy => 0.4,
            Difficulty.Normal => 0.55,
            _ => 0.7
        };

        double longNoteChance = difficulty switch
        {
            Difficulty.Easy => 0.08,
            Difficulty.Normal => 0.12,
            _ => 0.15
        };

        double doubleNoteChance = difficulty switch
        {
            Difficulty.Easy => 0.0,
            Difficulty.Normal => 0.1,
            _ => 0.2
        };

        double step = beatInterval * subdivision;
        double startDelay = 2.0; // 시작 전 여유

        for (double t = startDelay; t < duration - 1; t += step)
        {
            if (rng.NextDouble() > density) continue;

            int lane = rng.Next(4);

            // 롱노트
            bool isLong = rng.NextDouble() < longNoteChance;
            double dur = isLong ? beatInterval * (1 + rng.Next(3)) : 0;

            notes.Add(new Note(lane, t, isLong, dur));

            // 동시타 (더블 노트)
            if (rng.NextDouble() < doubleNoteChance)
            {
                int lane2;
                do { lane2 = rng.Next(4); } while (lane2 == lane);
                notes.Add(new Note(lane2, t));
            }
        }

        notes.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
        return notes;
    }
}
