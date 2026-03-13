using PadForge.Models;
using PadForge.ViewModels;

namespace PadForge.Views;

public partial class AppProfileView
{
    public AppProfileView() => InitializeComponent();

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MappingEditorViewModel vm)
            vm.Profile.AppProfiles.Add(new AppProfile());
    }
}
