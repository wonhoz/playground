namespace EarTrain.Models;

// ─── 음표 ─────────────────────────────────────────────────────────────────
public record Note(string Name, int MidiNote, bool IsBlack = false)
{
    /// <summary>MIDI 번호 → 주파수 (A4=440Hz 기준)</summary>
    public double Frequency => 440.0 * Math.Pow(2, (MidiNote - 69) / 12.0);
}

// ─── 인터벌 이름 ─────────────────────────────────────────────────────────
public record Interval(int Semitones, string Name, string KorName)
{
    public static readonly Interval[] All =
    [
        new(1,  "m2",  "단2도"),
        new(2,  "M2",  "장2도"),
        new(3,  "m3",  "단3도"),
        new(4,  "M3",  "장3도"),
        new(5,  "P4",  "완전4도"),
        new(6,  "TT",  "삼전음"),
        new(7,  "P5",  "완전5도"),
        new(8,  "m6",  "단6도"),
        new(9,  "M6",  "장6도"),
        new(10, "m7",  "단7도"),
        new(11, "M7",  "장7도"),
        new(12, "P8",  "옥타브"),
    ];
}

// ─── 화음 ─────────────────────────────────────────────────────────────────
public record ChordType(string Name, string KorName, int[] Semitones)
{
    public static readonly ChordType[] All =
    [
        new("Major",      "장3화음",  [0, 4, 7]),
        new("Minor",      "단3화음",  [0, 3, 7]),
        new("Diminished", "감3화음",  [0, 3, 6]),
        new("Augmented",  "증3화음",  [0, 4, 8]),
        new("Major7",     "장7화음",  [0, 4, 7, 11]),
        new("Dom7",       "속7화음",  [0, 4, 7, 10]),
        new("Minor7",     "단7화음",  [0, 3, 7, 10]),
    ];
}

// ─── 게임 모드 ────────────────────────────────────────────────────────────
public enum TrainMode { SingleNote, Interval, Chord, Melody }

// ─── 퀴즈 결과 ────────────────────────────────────────────────────────────
public record QuizResult(TrainMode Mode, string Correct, string Answered, bool IsCorrect, DateTime Time);

// ─── ELO 기록 ─────────────────────────────────────────────────────────────
public class EloRecord
{
    public string Key { get; set; } = "";   // e.g. "m3", "Major"
    public double Elo { get; set; } = 1200;
    public int Total { get; set; } = 0;
    public int Correct { get; set; } = 0;
    public double Accuracy => Total == 0 ? 0 : (double)Correct / Total * 100;
}
