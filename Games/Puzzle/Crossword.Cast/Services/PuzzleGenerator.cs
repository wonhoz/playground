using CrosswordCast.Models;

namespace CrosswordCast.Services;

public static class PuzzleGenerator
{
    private const int N = Puzzle.N;

    public static Puzzle Generate(int seed = 0)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var rng   = new Random(seed + attempt);
            var words = WordDatabase.All.OrderBy(_ => rng.Next()).ToList();
            var result = TryBuild(words, rng);
            if (result is not null) return result;
        }
        // 최소 퍼즐 반환 (항상 성공하는 고정 배치)
        return BuildFallback();
    }

    // ── 백트래킹 배치 ────────────────────────────────────────────────

    private static Puzzle? TryBuild(List<WordEntry> words, Random rng)
    {
        var grid   = new char[N, N];
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
            grid[r, c] = '#';

        var placed = new List<(WordEntry entry, int row, int col, bool across)>();

        // 첫 단어: 중앙 가로
        var first = words[0];
        int fc = (N - first.Word.Length) / 2;
        PlaceWord(grid, first.Word, N / 2, fc, across: true);
        placed.Add((first, N / 2, fc, true));

        foreach (var entry in words.Skip(1))
        {
            TryAdd(grid, entry, placed, rng);
            if (placed.Count >= 14) break;
        }

        return placed.Count >= 7 ? BuildPuzzle(grid, placed) : null;
    }

    private static bool TryAdd(char[,] grid, WordEntry entry,
        List<(WordEntry, int, int, bool)> placed, Random rng)
    {
        var candidates = new List<(int row, int col, bool across)>();

        foreach (var (pw, pRow, pCol, pAcross) in placed)
        {
            for (int pi = 0; pi < pw.Word.Length; pi++)
            {
                for (int ei = 0; ei < entry.Word.Length; ei++)
                {
                    if (entry.Word[ei] != pw.Word[pi]) continue;

                    int row, col;
                    bool across;

                    if (pAcross)
                    {
                        col    = pCol + pi;
                        row    = pRow - ei;
                        across = false;
                    }
                    else
                    {
                        row    = pRow + pi;
                        col    = pCol - ei;
                        across = true;
                    }

                    if (CanPlace(grid, entry.Word, row, col, across))
                        candidates.Add((row, col, across));
                }
            }
        }

        if (candidates.Count == 0) return false;

        var (r, c, a) = candidates[rng.Next(candidates.Count)];
        PlaceWord(grid, entry.Word, r, c, a);
        placed.Add((entry, r, c, a));
        return true;
    }

    // ── 배치 가능 체크 ────────────────────────────────────────────────

    private static bool CanPlace(char[,] grid, string word, int row, int col, bool across)
    {
        int dr = across ? 0 : 1, dc = across ? 1 : 0;
        int er = row + dr * (word.Length - 1);
        int ec = col + dc * (word.Length - 1);

        if (row < 0 || col < 0 || er >= N || ec >= N) return false;

        // 앞/뒤 빈 공간
        int br = row - dr, bc = col - dc;
        if (br >= 0 && bc >= 0 && grid[br, bc] != '#') return false;
        if (er + dr < N && ec + dc < N && grid[er + dr, ec + dc] != '#') return false;

        for (int i = 0; i < word.Length; i++)
        {
            int r = row + dr * i;
            int c = col + dc * i;
            char cur = grid[r, c];

            if (cur == '#')
            {
                // 평행 인접 체크
                if (across)
                {
                    if (r > 0   && grid[r - 1, c] != '#') return false;
                    if (r < N-1 && grid[r + 1, c] != '#') return false;
                }
                else
                {
                    if (c > 0   && grid[r, c - 1] != '#') return false;
                    if (c < N-1 && grid[r, c + 1] != '#') return false;
                }
            }
            else if (cur != word[i]) return false;
        }
        return true;
    }

    private static void PlaceWord(char[,] grid, string word, int row, int col, bool across)
    {
        int dr = across ? 0 : 1, dc = across ? 1 : 0;
        for (int i = 0; i < word.Length; i++)
            grid[row + dr * i, col + dc * i] = word[i];
    }

    // ── 퍼즐 빌드 ────────────────────────────────────────────────────

    private static Puzzle BuildPuzzle(char[,] grid,
        List<(WordEntry entry, int row, int col, bool across)> placed)
    {
        var puzzle = new Puzzle();
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
            puzzle.Grid[r, c] = grid[r, c];

        // 번호 할당: 좌→우, 위→아래 순서로 각 단어 시작 셀에 번호
        int num = 1;
        var ordered = placed
            .OrderBy(p => p.row)
            .ThenBy(p => p.col)
            .ToList();

        foreach (var (entry, row, col, across) in ordered)
        {
            if (puzzle.Numbers[row, col] == 0)
                puzzle.Numbers[row, col] = num++;

            puzzle.Words.Add(new PlacedWord
            {
                Word    = entry.Word,
                HintKo  = entry.HintKo,
                Row     = row,
                Col     = col,
                Across  = across,
                Number  = puzzle.Numbers[row, col],
            });
        }
        return puzzle;
    }

    // ── 폴백 퍼즐 (생성 실패 시) ─────────────────────────────────────

    private static Puzzle BuildFallback()
    {
        var words = new[]
        {
            (new WordEntry("HEART", "마음"), 6, 4, true),
            (new WordEntry("EAGLE", "독수리"), 4, 6, false),
            (new WordEntry("APPLE", "사과"), 4, 4, true),
            (new WordEntry("TIGER", "호랑이"), 4, 8, false),
            (new WordEntry("ANGEL", "천사"), 8, 4, true),
            (new WordEntry("LIGHT", "빛"), 6, 7, false),
            (new WordEntry("BRAVE", "용감한"), 2, 4, false),
        };

        var grid = new char[N, N];
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
            grid[r, c] = '#';

        var placed = new List<(WordEntry, int, int, bool)>();
        foreach (var (e, r, c, a) in words)
        {
            PlaceWord(grid, e.Word, r, c, a);
            placed.Add((e, r, c, a));
        }
        return BuildPuzzle(grid, placed);
    }
}
