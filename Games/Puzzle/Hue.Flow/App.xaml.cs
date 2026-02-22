using System.IO;
using System.Windows;

namespace HueFlow;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Resources");
            // 항상 재생성 — 공유 bin에서 다른 앱의 app.ico가 재사용되는 문제 방지
            IconGenerator.Generate(dir);
        }
        catch { }
    }
}
