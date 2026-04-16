namespace Prompt.Forge.Views;

public partial class BulkTagEditDialog : Window
{
    public List<string> TagsToAdd    { get; private set; } = [];
    public List<string> TagsToRemove { get; private set; } = [];

    public BulkTagEditDialog(int selectedCount)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            App.ApplyDarkTitleBar(this);
            TxtInfo.Text = $"{selectedCount}개 항목에 태그를 일괄 적용합니다.";
        };
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        TagsToAdd    = ParseTags(TxtAdd.Text);
        TagsToRemove = ParseTags(TxtRemove.Text);
        if (TagsToAdd.Count == 0 && TagsToRemove.Count == 0)
        {
            MessageBox.Show("추가하거나 제거할 태그를 입력하세요.",
                "입력 필요", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    static List<string> ParseTags(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
