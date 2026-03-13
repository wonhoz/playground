using ComicCast.ViewModels;

namespace ComicCast.Views;

public partial class ReaderView
{
    public ReaderView() => InitializeComponent();

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is ReaderViewModel vm)
            vm.HandleKeyDown(e.Key);
    }
}
