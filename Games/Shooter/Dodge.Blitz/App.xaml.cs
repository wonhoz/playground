using System.Windows;

namespace DodgeBlitz;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        IconGenerator.EnsureIcon();
    }
}
