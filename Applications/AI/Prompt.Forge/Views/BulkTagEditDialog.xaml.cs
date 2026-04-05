using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Prompt.Forge.Views;

public partial class BulkTagEditDialog : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public List<string> TagsToAdd    { get; private set; } = [];
    public List<string> TagsToRemove { get; private set; } = [];

    public BulkTagEditDialog(int selectedCount)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
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
