using System.Runtime.InteropServices;
using System.Windows.Interop;
using Prompt.Forge.Services;

namespace Prompt.Forge.Views;

public partial class VersionHistoryDialog : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public string? RestoredContent { get; private set; }

    record VersionEntry(int Id, string VersionLabel, string DateLabel, string Content);

    Database? _db;

    internal VersionHistoryDialog(PromptItem current, List<PromptItem> history, Database? db = null)
    {
        InitializeComponent();
        _db = db;

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
            BtnDeleteVersion.IsEnabled = _db != null;
        }
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (LstVersions.SelectedItem is not VersionEntry entry) return;
        RestoredContent = entry.Content;
        DialogResult = true;
    }

    void DeleteVersion_Click(object sender, RoutedEventArgs e)
    {
        if (LstVersions.SelectedItem is not VersionEntry entry || _db == null) return;
        var r = MessageBox.Show($"v{entry.VersionLabel} 버전을 삭제하시겠습니까?",
            "버전 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (r != MessageBoxResult.Yes) return;
        _db.DeleteHistoryItem(entry.Id);
        var items = (LstVersions.ItemsSource as System.Collections.Generic.List<VersionEntry>)?.ToList() ?? [];
        items.RemoveAll(x => x.Id == entry.Id);
        LstVersions.ItemsSource = null;
        LstVersions.ItemsSource = items;
        TxtPreview.Text = "";
        BtnRestore.IsEnabled = false;
        BtnDeleteVersion.IsEnabled = false;
        if (items.Count == 0)
            TxtEmptyMsg.Visibility = System.Windows.Visibility.Visible;
    }

    void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
