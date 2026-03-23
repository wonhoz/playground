namespace DeepDiff.Views;

public partial class HomeView : UserControl
{
    private readonly MainWindow _main;

    public HomeView(MainWindow main)
    {
        _main = main;
        InitializeComponent();
    }

    private void CardFolder_Click(object sender, MouseButtonEventArgs e)
        => _main.OpenCompare(CompareMode.Folder);

    private void CardText_Click(object sender, MouseButtonEventArgs e)
        => _main.OpenCompare(CompareMode.Text);

    private void CardImage_Click(object sender, MouseButtonEventArgs e)
        => _main.OpenCompare(CompareMode.Image);

    private void CardHex_Click(object sender, MouseButtonEventArgs e)
        => _main.OpenCompare(CompareMode.Hex);

    private void CardClipboard_Click(object sender, MouseButtonEventArgs e)
        => _main.OpenCompare(CompareMode.Clipboard);
}
