using System.Windows;

namespace HwxPdf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // exe 아이콘에 파일을 끌어다 놓으면 인자로 들어온다 → 바로 변환 시작
        new MainWindow(e.Args).Show();
    }
}
