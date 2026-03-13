using ComicCast.Core;
using ComicCast.Models;
using ComicCast.Services;

namespace ComicCast.ViewModels;

public enum AppTab { Library, Reader }

public class MainViewModel : ViewModelBase
{
    private AppTab _currentTab = AppTab.Library;

    public AppTab CurrentTab
    {
        get => _currentTab;
        set { SetField(ref _currentTab, value); OnPropertyChanged(nameof(IsLibraryTab)); OnPropertyChanged(nameof(IsReaderTab)); }
    }

    public bool IsLibraryTab => _currentTab == AppTab.Library;
    public bool IsReaderTab  => _currentTab == AppTab.Reader;

    public LibraryViewModel Library { get; }
    public ReaderViewModel  Reader  { get; }

    public ICommand ShowLibraryCommand { get; }
    public ICommand ShowReaderCommand  { get; }

    public MainViewModel(LibraryViewModel library, ReaderViewModel reader)
    {
        Library = library;
        Reader  = reader;

        ShowLibraryCommand = new RelayCommand(() => CurrentTab = AppTab.Library);
        ShowReaderCommand  = new RelayCommand(() => CurrentTab = AppTab.Reader);

        Library.OpenBookRequested += OnOpenBook;
    }

    private async void OnOpenBook(ComicBook book)
    {
        await Reader.OpenBookAsync(book);
        CurrentTab = AppTab.Reader;
    }
}
