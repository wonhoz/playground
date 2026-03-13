using System.Globalization;
using System.Windows.Media;
using CrosswordCast.Models;
using CrosswordCast.Services;

namespace CrosswordCast.Views;

/// <summary>크로스워드 격자 렌더러 + 입력 처리 (WPF FrameworkElement)</summary>
public class CrosswordGrid : System.Windows.FrameworkElement
{
    private const int    N    = Puzzle.N;
    private const double Cell = 36;

    public GameService?  Game      { get; set; }
    public event Action? Changed;

    private int  _selRow = -1, _selCol = -1;
    private bool _selAcross = true;

    // ── 브러시 캐시 ──────────────────────────────────────────────────

    private static readonly SolidColorBrush BrBlock    = new(WpfColor.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly SolidColorBrush BrEmpty    = new(WpfColor.FromRgb(0xF5, 0xF5, 0xF5));
    private static readonly SolidColorBrush BrWord     = new(WpfColor.FromRgb(0xBB, 0xDE, 0xFB));
    private static readonly SolidColorBrush BrCursor   = new(WpfColor.FromRgb(0x19, 0x76, 0xD2));
    private static readonly SolidColorBrush BrOk       = new(WpfColor.FromRgb(0x1B, 0x5E, 0x20));
    private static readonly SolidColorBrush BrLetter   = new(WpfColor.FromRgb(0x1A, 0x1A, 0x1A));
    private static readonly SolidColorBrush BrNumColor = new(WpfColor.FromRgb(0x55, 0x55, 0x55));
    private static readonly Pen             PenBorder  = new(new SolidColorBrush(WpfColor.FromRgb(0xCC, 0xCC, 0xCC)), 0.5);
    private static readonly Typeface        TfBold     = new(new FontFamily("Segoe UI"),
                                                              FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private static readonly Typeface        TfNormal   = new("Segoe UI");

    public CrosswordGrid()
    {
        Width     = N * Cell + 1;
        Height    = N * Cell + 1;
        Focusable = true;
        Cursor    = Cursors.IBeam;
    }

    // ── 렌더링 ───────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(WpfColor.FromRgb(0x10, 0x10, 0x10)), null,
            new Rect(0, 0, Width, Height));

        if (Game?.CurrentPuzzle is not { } puzzle) return;

        var selWord = _selRow >= 0 ? Game.FindWord(_selRow, _selCol, _selAcross) : null;

        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            char answer = puzzle.Grid[r, c];
            var rect = new Rect(c * Cell + 0.5, r * Cell + 0.5, Cell - 0.5, Cell - 0.5);

            if (answer == '#')
            {
                dc.DrawRectangle(BrBlock, null, rect);
                continue;
            }

            // 배경
            Brush bg;
            if (r == _selRow && c == _selCol)
                bg = BrCursor;
            else if (selWord?.Contains(r, c) == true)
                bg = BrWord;
            else
                bg = BrEmpty;

            dc.DrawRectangle(bg, PenBorder, rect);

            // 번호
            int num = puzzle.Numbers[r, c];
            if (num > 0)
            {
                var ft = MakeText(num.ToString(), 8, BrNumColor, TfNormal);
                dc.DrawText(ft, new Point(c * Cell + 2, r * Cell + 1));
            }

            // 사용자 글자
            char ch = Game.GetUserCell(r, c);
            if (ch != '\0')
            {
                var brush = Game.IsCorrect(r, c) ? BrOk : BrLetter;
                var ft    = MakeText(ch.ToString(), 18, brush, TfBold);
                dc.DrawText(ft, new Point(
                    c * Cell + (Cell - ft.Width)  / 2,
                    r * Cell + (Cell - ft.Height) / 2 + 2));
            }
        }
    }

    private static FormattedText MakeText(string text, double size, Brush brush, Typeface tf) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, size, brush, 96);

    // ── 마우스 입력 ──────────────────────────────────────────────────

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();
        var pos = e.GetPosition(this);
        int c = (int)(pos.X / Cell);
        int r = (int)(pos.Y / Cell);
        if (r < 0 || r >= N || c < 0 || c >= N) return;
        if (Game?.CurrentPuzzle?.Grid[r, c] == '#') return;

        if (r == _selRow && c == _selCol)
        {
            // 같은 셀 재클릭 → 방향 전환
            _selAcross = !_selAcross;
            if (Game?.FindWord(r, c, _selAcross) is null)
                _selAcross = !_selAcross; // 되돌리기
        }
        else
        {
            _selRow    = r;
            _selCol    = c;
            if (Game?.FindWord(r, c, _selAcross) is null)
                _selAcross = !_selAcross;
        }

        InvalidateVisual();
        Changed?.Invoke();
    }

    // ── 키보드 입력 ──────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Game is null || _selRow < 0) return;

        if (e.Key is >= Key.A and <= Key.Z)
        {
            char ch = (char)('A' + (e.Key - Key.A));
            Game.SetUserCell(_selRow, _selCol, ch);
            MoveCursor(forward: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            if (Game.GetUserCell(_selRow, _selCol) != '\0')
                Game.SetUserCell(_selRow, _selCol, '\0');
            else
                MoveCursor(forward: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            Game.SetUserCell(_selRow, _selCol, '\0');
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            _selAcross = !_selAcross;
            if (Game.FindWord(_selRow, _selCol, _selAcross) is null)
                _selAcross = !_selAcross;
            e.Handled = true;
        }

        InvalidateVisual();
        Changed?.Invoke();
    }

    private void MoveCursor(bool forward)
    {
        int dr = _selAcross ? 0 : 1;
        int dc = _selAcross ? 1 : 0;
        int nr = _selRow + (forward ? dr : -dr);
        int nc = _selCol + (forward ? dc : -dc);
        if (nr >= 0 && nr < N && nc >= 0 && nc < N &&
            Game?.CurrentPuzzle?.Grid[nr, nc] != '#')
        {
            _selRow = nr;
            _selCol = nc;
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    public void Refresh() => InvalidateVisual();

    public void ResetSelection()
    {
        _selRow = _selCol = -1;
        InvalidateVisual();
    }

    public PlacedWord? SelectedWord =>
        _selRow >= 0 ? Game?.FindWord(_selRow, _selCol, _selAcross) : null;
}
