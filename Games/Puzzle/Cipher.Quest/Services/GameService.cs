using System.Diagnostics;
using CipherQuest.Models;

namespace CipherQuest.Services;

public class GameService
{
    private int _chapterIdx = 0;
    private int _puzzleIdx  = 0;

    private readonly Stopwatch _timer = new();

    // ── 현재 상태 ────────────────────────────────────────────────────

    public Chapter       CurrentChapter => PuzzleData.All[_chapterIdx];
    public CipherPuzzle  CurrentPuzzle  => CurrentChapter.Puzzles[_puzzleIdx];
    public CipherType    Type           => CurrentChapter.Type;
    public int           ElapsedSeconds => (int)_timer.Elapsed.TotalSeconds;
    public bool          TimerRunning   => _timer.IsRunning;

    // 챕터별 현재 설정값
    public int    CaesarShift { get; set; } = 0;
    public string VigenereKey { get; set; } = "";
    public char[] SubMapping  { get; } = new char[26];  // '\0' = unknown
    public int    RailCount   { get; set; } = 2;
    public char   Rotor1 { get; set; } = 'A';
    public char   Rotor2 { get; set; } = 'A';
    public char   Rotor3 { get; set; } = 'A';

    // 각 퍼즐별 해결 여부 & 별점 [chapter][puzzle]
    private readonly int[,] _stars = new int[5, 3];

    // ── 내비게이션 ────────────────────────────────────────────────────

    public void GoToChapter(int idx)
    {
        _chapterIdx = Math.Clamp(idx, 0, PuzzleData.All.Count - 1);
        _puzzleIdx  = 0;
        ResetAttempt();
    }

    public bool NextPuzzle()
    {
        if (_puzzleIdx < CurrentChapter.Puzzles.Count - 1) { _puzzleIdx++; ResetAttempt(); return true; }
        return false;
    }

    public bool PrevPuzzle()
    {
        if (_puzzleIdx > 0) { _puzzleIdx--; ResetAttempt(); return true; }
        return false;
    }

    private void ResetAttempt()
    {
        _timer.Reset();
        CaesarShift = 0;
        VigenereKey = "";
        Array.Clear(SubMapping, 0, SubMapping.Length);
        RailCount = 2;
        Rotor1 = Rotor2 = Rotor3 = 'A';
    }

    public void StartTimer() { if (!_timer.IsRunning) _timer.Start(); }
    public void StopTimer()  => _timer.Stop();

    // ── 복호화 ───────────────────────────────────────────────────────

    public string Decrypt() => Type switch
    {
        CipherType.Caesar       => CipherEngine.CaesarDecrypt(CurrentPuzzle.CipherText, CaesarShift),
        CipherType.Vigenere     => CipherEngine.VigenereDecrypt(CurrentPuzzle.CipherText, VigenereKey),
        CipherType.Substitution => CipherEngine.SubstitutionDecrypt(CurrentPuzzle.CipherText, SubMapping),
        CipherType.RailFence    => CipherEngine.RailFenceDecrypt(CurrentPuzzle.CipherText, RailCount),
        CipherType.Enigma       => CipherEngine.EnigmaDecrypt(CurrentPuzzle.CipherText, Rotor1, Rotor2, Rotor3),
        _                       => "",
    };

    // ── 정답 확인 ────────────────────────────────────────────────────

    public bool CheckAnswer()
    {
        bool ok = Normalize(Decrypt()) == Normalize(CurrentPuzzle.PlainText);
        if (ok)
        {
            _timer.Stop();
            int s = ElapsedSeconds <= 60 ? 3 : ElapsedSeconds <= 120 ? 2 : 1;
            _stars[_chapterIdx, _puzzleIdx] = s;
        }
        return ok;
    }

    public int GetStars(int chapter, int puzzle) => _stars[chapter, puzzle];

    private static string Normalize(string s) =>
        new(s.ToUpper().Where(char.IsLetterOrDigit).ToArray());
}
