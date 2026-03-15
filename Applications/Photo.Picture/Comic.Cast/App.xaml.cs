using ComicCast.Services;
using ComicCast.ViewModels;
using ComicCast.Views;

namespace ComicCast;

public partial class App : WpfApplication
{
    private LibraryService? _library;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 서비스 초기화
        var archive   = new ArchiveService();
        _library      = new LibraryService(archive);
        var converter = new ImageConvertService(archive);
        _library.Initialize();

        // ViewModel 생성
        var libraryVm = new LibraryViewModel(_library, archive, converter);
        var readerVm  = new ReaderViewModel(archive, _library);
        var mainVm    = new MainViewModel(libraryVm, readerVm);

        libraryVm.Initialize();

        var mainWindow = new MainWindow { DataContext = mainVm };
        MainWindow = mainWindow;
        mainWindow.Show();

        // 드래그앤드롭으로 직접 파일 열기 지원 — 창이 완전히 표시된 후 실행
        if (e.Args.Length > 0 && File.Exists(e.Args[0]))
        {
            _ = OpenFileArg(e.Args[0], readerVm, mainVm, archive, _library);
        }
    }

    private static async Task OpenFileArg(string path, ReaderViewModel reader,
        MainViewModel main, ArchiveService archive, LibraryService library)
    {
        var book = new Models.ComicBook
        {
            FilePath    = path,
            Title       = Path.GetFileNameWithoutExtension(path),
            ArchiveType = ArchiveService.DetectType(path),
        };
        var pages = await Task.Run(() => archive.GetPages(path));
        book.PageCount = pages.Count;

        await reader.OpenBookAsync(book);
        main.CurrentTab = AppTab.Reader;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _library?.Dispose();
        base.OnExit(e);
    }
}
