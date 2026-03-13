using ComicCast.Core;
using ComicCast.Models;
using ComicCast.Services;

namespace ComicCast.ViewModels;

public class LibraryBookItem : ViewModelBase
{
    private BitmapImage? _cover;
    public ComicBook   Book  { get; }
    public BitmapImage? Cover { get => _cover; set => SetField(ref _cover, value); }

    public string Title      => Book.Title;
    public string SeriesInfo => Book.Series.Length > 0
        ? $"{Book.Series}{(Book.Volume > 0 ? $" v{Book.Volume}" : "")}{(Book.Number > 0 ? $" #{Book.Number}" : "")}"
        : "";
    public string PageInfo   => $"{Book.LastPage + 1} / {Book.PageCount}p";
    public double ReadPercent => Book.ReadPercent;

    public LibraryBookItem(ComicBook book) => Book = book;
}

public class LibraryViewModel : ViewModelBase
{
    private readonly LibraryService      _library;
    private readonly ImageConvertService _converter;
    private readonly ArchiveService      _archive;

    private string  _searchQuery     = "";
    private string  _statusText      = "";
    private bool    _isScanning;
    private bool    _isConverting;
    private int     _convertProgress;
    private int     _convertTotal;
    private LibraryBookItem? _selected;

    public ObservableCollection<LibraryBookItem> Books { get; } = [];

    public string SearchQuery
    {
        get => _searchQuery;
        set { SetField(ref _searchQuery, value); ApplySearch(); }
    }
    public string  StatusText      { get => _statusText;      private set => SetField(ref _statusText, value); }
    public bool    IsScanning      { get => _isScanning;      private set => SetField(ref _isScanning, value); }
    public bool    IsConverting    { get => _isConverting;    private set => SetField(ref _isConverting, value); }
    public int     ConvertProgress { get => _convertProgress; private set => SetField(ref _convertProgress, value); }
    public int     ConvertTotal    { get => _convertTotal;    private set => SetField(ref _convertTotal, value); }

    public LibraryBookItem? SelectedBook
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public ICommand AddFolderCommand    { get; }
    public ICommand RefreshCommand      { get; }
    public ICommand RemoveBookCommand   { get; }
    public ICommand ConvertWebpCommand  { get; }

    public event Action<ComicBook>? OpenBookRequested;

    public LibraryViewModel(LibraryService library, ArchiveService archive, ImageConvertService converter)
    {
        _library   = library;
        _archive   = archive;
        _converter = converter;

        AddFolderCommand   = new RelayCommand(async () => await AddFolderAsync());
        RefreshCommand     = new RelayCommand(async () => await RefreshAsync());
        RemoveBookCommand  = new RelayCommand(RemoveSelected, () => _selected is not null);
        ConvertWebpCommand = new RelayCommand(async () => await ConvertSelectedAsync(),
            () => _selected is not null && !IsConverting);
    }

    public void Initialize()
    {
        _library.BookAdded   += OnBookAdded;
        _library.BookRemoved += OnBookRemoved;
        LoadBooks();
    }

    public void OpenSelected()
    {
        if (_selected is not null)
            OpenBookRequested?.Invoke(_selected.Book);
    }

    private void LoadBooks()
    {
        Books.Clear();
        foreach (var b in _library.GetAllBooks())
            Books.Add(new LibraryBookItem(b));
        StatusText = $"총 {Books.Count}권";
        _ = LoadThumbnailsAsync();
    }

    private void ApplySearch()
    {
        Books.Clear();
        var list = string.IsNullOrWhiteSpace(_searchQuery)
            ? _library.GetAllBooks()
            : _library.SearchBooks(_searchQuery);
        foreach (var b in list) Books.Add(new LibraryBookItem(b));
        StatusText = $"{Books.Count}권 표시";
        _ = LoadThumbnailsAsync();
    }

    private async Task LoadThumbnailsAsync()
    {
        var items = Books.ToList();
        foreach (var item in items)
        {
            if (item.Cover is not null) continue;
            item.Cover = await _converter.GetThumbnailAsync(item.Book);
        }
    }

    private async Task AddFolderAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description            = "만화 폴더 선택",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var folder = dialog.SelectedPath;
        _library.AddFolder(folder);
        await ScanFolderAsync(folder);
    }

    private async Task ScanFolderAsync(string folder)
    {
        IsScanning = true;
        StatusText = "스캔 중...";
        try
        {
            var progress = new Progress<string>(name => StatusText = $"스캔 중: {name}");
            await _library.ScanFolderAsync(folder, progress);
            StatusText = $"총 {Books.Count}권";
        }
        finally { IsScanning = false; }
    }

    private async Task RefreshAsync()
    {
        IsScanning = true;
        StatusText = "새로 고침 중...";
        try
        {
            foreach (var f in _library.GetFolders())
            {
                if (Directory.Exists(f.Path))
                    await _library.ScanFolderAsync(f.Path,
                        new Progress<string>(n => StatusText = $"스캔: {n}"));
            }
            LoadBooks();
        }
        finally { IsScanning = false; }
    }

    private void RemoveSelected()
    {
        if (_selected is null) return;
        _library.RemoveBook(_selected.Book.Id);
    }

    private async Task ConvertSelectedAsync()
    {
        if (_selected is null) return;
        var book = _selected.Book;
        IsConverting    = true;
        ConvertProgress = 0;
        ConvertTotal    = book.PageCount;
        StatusText      = $"WebP 변환 중: {book.Title}";
        try
        {
            var progress = new Progress<(int c, int t)>(v =>
            {
                ConvertProgress = v.c;
                ConvertTotal    = v.t;
            });
            await _converter.ConvertToWebpCbzAsync(book, 85, progress);
            StatusText = $"변환 완료: {book.Title}";
        }
        catch (Exception ex)
        {
            StatusText = $"변환 실패: {ex.Message}";
        }
        finally { IsConverting = false; }
    }

    private void OnBookAdded(ComicBook book)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            Books.Add(new LibraryBookItem(book));
            StatusText = $"총 {Books.Count}권";
        });
    }

    private void OnBookRemoved(int id)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var item = Books.FirstOrDefault(b => b.Book.Id == id);
            if (item is not null) Books.Remove(item);
            StatusText = $"총 {Books.Count}권";
        });
    }
}
