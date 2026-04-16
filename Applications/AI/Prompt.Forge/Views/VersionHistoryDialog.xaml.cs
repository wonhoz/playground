using Prompt.Forge.Services;

namespace Prompt.Forge.Views;

public partial class VersionHistoryDialog : Window
{
    public string? RestoredContent { get; private set; }

    record VersionEntry(int Id, string VersionLabel, string DateLabel, string Content);

    Database? _db;
    readonly System.Collections.ObjectModel.ObservableCollection<VersionEntry> _entries = [];

    internal VersionHistoryDialog(PromptItem current, List<PromptItem> history, Database? db = null)
    {
        InitializeComponent();
        _db = db;

        Loaded += (_, _) => App.ApplyDarkTitleBar(this);

        TxtTitle.Text = $"히스토리: {current.Title}";

        foreach (var h in history.OrderByDescending(h => h.Version))
            _entries.Add(new VersionEntry(
                h.Id,
                $"v{h.Version}",
                h.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                h.Content));

        if (_entries.Count == 0)
        {
            TxtEmptyMsg.Visibility = System.Windows.Visibility.Visible;
            BtnRestore.IsEnabled = false;
        }
        else
        {
            TxtEmptyMsg.Visibility = System.Windows.Visibility.Collapsed;
            LstVersions.ItemsSource = _entries;
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
        _entries.Remove(entry);
        TxtPreview.Text = "";
        if (_entries.Count == 0)
        {
            TxtEmptyMsg.Visibility = System.Windows.Visibility.Visible;
            BtnRestore.IsEnabled = false;
            BtnDeleteVersion.IsEnabled = false;
        }
        else
        {
            LstVersions.SelectedIndex = 0;
        }
    }

    void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
