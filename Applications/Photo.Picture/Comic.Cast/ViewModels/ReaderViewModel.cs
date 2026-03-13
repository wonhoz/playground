using ComicCast.Core;
using ComicCast.Models;
using ComicCast.Services;

namespace ComicCast.ViewModels;

public class ReaderViewModel : ViewModelBase
{
    private readonly ArchiveService  _archive;
    private readonly LibraryService  _library;

    private ReadingSession? _session;
    private BitmapImage?    _leftPage;
    private BitmapImage?    _rightPage;
    private bool            _isLoading;
    private string          _statusText = "";
    private ViewMode        _viewMode   = ViewMode.Single;
    private double          _zoom       = 1.0;
    private CancellationTokenSource _cts = new();

    public BitmapImage? LeftPage    { get => _leftPage;   private set => SetField(ref _leftPage,   value); }
    public BitmapImage? RightPage   { get => _rightPage;  private set => SetField(ref _rightPage,  value); }
    public bool         IsLoading   { get => _isLoading;  private set => SetField(ref _isLoading,  value); }
    public string       StatusText  { get => _statusText; private set => SetField(ref _statusText, value); }
    public double       Zoom        { get => _zoom;       set => SetField(ref _zoom, Math.Clamp(value, 0.25, 4.0)); }
    public bool         IsDouble    => _viewMode != ViewMode.Single;

    public ViewMode ViewMode
    {
        get => _viewMode;
        set { SetField(ref _viewMode, value); OnPropertyChanged(nameof(IsDouble)); _ = LoadCurrentPageAsync(); }
    }

    public ICommand PrevCommand    { get; }
    public ICommand NextCommand    { get; }
    public ICommand ZoomInCommand  { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ZoomFitCommand { get; }
    public ICommand SingleModeCommand  { get; }
    public ICommand DoubleMangaCommand { get; }
    public ICommand DoubleWebCommand   { get; }

    public int CurrentPage  => (_session?.Current ?? 0) + 1;
    public int TotalPages   => _session?.Pages.Count ?? 0;
    public string BookTitle => _session?.Book.Title ?? "";

    public ReaderViewModel(ArchiveService archive, LibraryService library)
    {
        _archive = archive;
        _library = library;

        PrevCommand    = new RelayCommand(() => NavigatePrev(), () => _session?.HasPrev == true);
        NextCommand    = new RelayCommand(() => NavigateNext(), () => _session?.HasNext == true);
        ZoomInCommand  = new RelayCommand(() => Zoom += 0.25);
        ZoomOutCommand = new RelayCommand(() => Zoom -= 0.25);
        ZoomFitCommand = new RelayCommand(() => Zoom = 1.0);
        SingleModeCommand  = new RelayCommand(() => ViewMode = ViewMode.Single);
        DoubleMangaCommand = new RelayCommand(() => ViewMode = ViewMode.DoubleManga);
        DoubleWebCommand   = new RelayCommand(() => ViewMode = ViewMode.DoubleWebtoon);
    }

    public async Task OpenBookAsync(ComicBook book)
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        var pages = await Task.Run(() => _archive.GetPages(book.FilePath));
        _session = new ReadingSession
        {
            Book     = book,
            Pages    = pages,
            Current  = Math.Max(0, book.LastPage),
            ViewMode = _viewMode,
        };

        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(BookTitle));
        await LoadCurrentPageAsync();
    }

    private void NavigatePrev()
    {
        if (_session is null) return;
        int step = IsDouble ? 2 : 1;
        _session.Current = Math.Max(0, _session.Current - step);
        _ = LoadCurrentPageAsync();
    }

    private void NavigateNext()
    {
        if (_session is null) return;
        int step = IsDouble ? 2 : 1;
        _session.Current = Math.Min(_session.Pages.Count - 1, _session.Current + step);
        _ = LoadCurrentPageAsync();
    }

    private async Task LoadCurrentPageAsync()
    {
        if (_session is null) return;
        IsLoading = true;
        var ct = _cts.Token;

        try
        {
            var book = _session.Book;
            var idx  = _session.Current;

            if (!IsDouble)
            {
                LeftPage  = await _archive.LoadBitmapAsync(book.FilePath, _session.Pages[idx], ct);
                RightPage = null;
            }
            else
            {
                // 망가: 오른쪽이 앞, 왼쪽이 뒤
                // 웹툰: 왼쪽이 앞
                bool manga = _viewMode == ViewMode.DoubleManga;
                int  a     = manga ? idx + 1 : idx;
                int  b     = manga ? idx     : idx + 1;

                var taskA = a < _session.Pages.Count
                    ? _archive.LoadBitmapAsync(book.FilePath, _session.Pages[a], ct)
                    : Task.FromResult<BitmapImage>(null!);
                var taskB = b < _session.Pages.Count
                    ? _archive.LoadBitmapAsync(book.FilePath, _session.Pages[b], ct)
                    : Task.FromResult<BitmapImage>(null!);

                await Task.WhenAll(taskA, taskB);
                LeftPage  = taskA.Result;
                RightPage = taskB.Result;
            }

            OnPropertyChanged(nameof(CurrentPage));
            StatusText = $"{CurrentPage} / {TotalPages}";

            // 진행 저장
            _library.UpdateProgress(book.Id, idx);
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsLoading = false;
        }
    }

    public void HandleKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Left:  case Key.PageUp:   NavigatePrev(); break;
            case Key.Right: case Key.PageDown: NavigateNext(); break;
            case Key.OemPlus:  case Key.Add:      Zoom += 0.25; break;
            case Key.OemMinus: case Key.Subtract: Zoom -= 0.25; break;
            case Key.D0: case Key.NumPad0: Zoom = 1.0; break;
        }
    }
}
