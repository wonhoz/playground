using System.Windows;

namespace BatchRename;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 아이콘 생성
        var resDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        IconGenerator.Generate(resDir);
    }
}
