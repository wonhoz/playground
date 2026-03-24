using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Prompt.Forge.Views;

public partial class VersionHistoryDialog : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public string? RestoredContent { get; private set; }

    record VersionEntry(int Id, string VersionLabel, string DateLabel, string Content);

    public VersionHistoryDialog(PromptItem current, List<PromptItem> history)
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        };

        TxtTitle.Text = $"히스토리: {current.Title}";

        var entries = history
            .OrderByDescending(h => h.Version)
            .Select(h => new VersionEntry(
                h.Id,
                $"v{h.Version}",
                h.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                h.Content))
            .ToList();

        if (entries.Count == 0)
        {
            TxtEmptyMsg.Visibility = System.Windows.Visibility.Visible;
            BtnRestore.IsEnabled = false;
        }
        else
        {
            TxtEmptyMsg.Visibility = System.Windows.Visibility.Collapsed;
            LstVersions.ItemsSource = entries;
            LstVersions.SelectedIndex = 0;
        }
    }

    void LstVersions_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LstVersions.SelectedItem is VersionEntry entry)
        {
            TxtPreview.Text = entry.Content;
            BtnRestore.IsEnabled = true;
        }
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (LstVersions.SelectedItem is not VersionEntry entry) return;
        RestoredContent = entry.Content;
        DialogResult = true;
    }

    void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
