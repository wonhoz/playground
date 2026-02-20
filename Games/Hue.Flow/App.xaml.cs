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
            if (!File.Exists(Path.Combine(dir, "app.ico")))
                IconGenerator.Generate(dir);
        }
        catch { }
    }
}
