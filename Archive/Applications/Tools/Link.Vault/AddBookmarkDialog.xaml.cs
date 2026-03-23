using System.Runtime.InteropServices;
using System.Windows;
using LinkVault.Models;
using LinkVault.Services;

namespace LinkVault;

public partial class AddBookmarkDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly DatabaseService _db;
    private readonly SnapshotService _snapshot;
    private readonly Bookmark? _edit;

    public AddBookmarkDialog(DatabaseService db, SnapshotService snapshot, Bookmark? edit = null)
    {
        InitializeComponent();
        _db       = db;
        _snapshot = snapshot;
        _edit     = edit;

        Loaded += OnLoaded;

        if (edit != null)
        {
            Title       = "북마크 편집";
            TxtUrl.Text   = edit.Url;
            TxtTitle.Text = edit.Title;
            TxtDesc.Text  = edit.Description;
            TxtTags.Text  = edit.Tags;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var dark = 1;
        DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        TxtUrl.Focus();
    }

    private async void BtnFetch_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        TxtUrl.Text = url;

        BtnFetch.IsEnabled = false;
        BtnFetch.Content = "...";
        var meta = await _snapshot.FetchMetaAsync(url);
        if (string.IsNullOrEmpty(TxtTitle.Text)) TxtTitle.Text = meta.Title;
        if (string.IsNullOrEmpty(TxtDesc.Text))  TxtDesc.Text  = meta.Description;
        BtnFetch.Content   = "가져오기";
        BtnFetch.IsEnabled = true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) { MessageBox.Show("URL을 입력하세요."); return; }
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        var title = TxtTitle.Text.Trim();
        if (string.IsNullOrEmpty(title)) title = url;

        if (_edit != null)
        {
            _edit.Url         = url;
            _edit.Title       = title;
            _edit.Description = TxtDesc.Text.Trim();
            _edit.Tags        = TxtTags.Text.Trim();
            _db.Update(_edit);
        }
        else
        {
            _db.Insert(new Bookmark
            {
                Url         = url,
                Title       = title,
                Description = TxtDesc.Text.Trim(),
                Tags        = TxtTags.Text.Trim()
            });
        }

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
