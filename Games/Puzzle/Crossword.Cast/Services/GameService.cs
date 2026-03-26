using CrosswordCast.Models;

namespace CrosswordCast.Services;

public class GameService
{
    private int _seed;

    public int CurrentSeed => _seed;

    public Puzzle? CurrentPuzzle { get; private set; }

    private char[,] _user = new char[Puzzle.N, Puzzle.N];

    public void NewPuzzle(int? seed = null)
    {
        _seed         = seed ?? (int)DateTime.Now.Ticks;
        CurrentPuzzle = PuzzleGenerator.Generate(_seed);
        _user         = new char[Puzzle.N, Puzzle.N];
    }

    public void Clear() => _user = new char[Puzzle.N, Puzzle.N];

    public char GetUserCell(int r, int c)  => _user[r, c];

    public void SetUserCell(int r, int c, char ch)
    {
        if (CurrentPuzzle?.Grid[r, c] == '#') return;
        _user[r, c] = ch;
    }

    public bool IsCorrect(int r, int c)
    {
        var ch = _user[r, c];
        return ch != '\0' && ch == CurrentPuzzle?.Grid[r, c];
    }

    public bool IsCompleted()
    {
        if (CurrentPuzzle is null) return false;
        for (int r = 0; r < Puzzle.N; r++)
        for (int c = 0; c < Puzzle.N; c++)
        {
            if (CurrentPuzzle.Grid[r, c] == '#') continue;
            if (_user[r, c] != CurrentPuzzle.Grid[r, c]) return false;
        }
        return true;
    }

    public PlacedWord? FindWord(int r, int c, bool across) =>
        CurrentPuzzle?.Words.FirstOrDefault(w => w.Across == across && w.Contains(r, c));

    public bool IsWordCompleted(PlacedWord w)
    {
        int dr = w.Across ? 0 : 1, dc = w.Across ? 1 : 0;
        for (int i = 0; i < w.Word.Length; i++)
            if (_user[w.Row + dr * i, w.Col + dc * i] != w.Word[i]) return false;
        return true;
    }

    public (int done, int total) WordProgress()
    {
        if (CurrentPuzzle is null) return (0, 0);
        return (CurrentPuzzle.Words.Count(IsWordCompleted), CurrentPuzzle.Words.Count);
    }

    public void RevealWord(PlacedWord w)
    {
        int dr = w.Across ? 0 : 1, dc = w.Across ? 1 : 0;
        for (int i = 0; i < w.Word.Length; i++)
            _user[w.Row + dr * i, w.Col + dc * i] = w.Word[i];
    }

    public char[,] GetUserGrid()
    {
        var copy = new char[Puzzle.N, Puzzle.N];
        Array.Copy(_user, copy, _user.Length);
        return copy;
    }

    public void SetUserGrid(char[,] grid)
    {
        Array.Copy(grid, _user, grid.Length);
    }
}
