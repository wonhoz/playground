using AIPForge.Models;
using AIPForge.Services;

namespace AIPForge.Views;

/// <summary>ProductVersion 변경 (단일 · 일괄 공용)</summary>
public partial class VersionDialog : Window
{
    public string Version => TxtVersion.Text.Trim();
    public bool NewProductCode => ChkProductCode.IsChecked == true;

    public VersionDialog(string targetDescription, string currentVersion, bool newProductCode)
    {
        InitializeComponent();
        TxtTarget.Text = targetDescription;
        TxtVersion.Text = AipDocument.IncrementVersion(currentVersion);
        ChkProductCode.IsChecked = newProductCode;
        DarkTitle.Apply(this);
        Loaded += (_, _) => { TxtVersion.Focus(); TxtVersion.SelectAll(); };
    }

    private void BtnIncrement_Click(object sender, RoutedEventArgs e)
    {
        TxtVersion.Text = AipDocument.IncrementVersion(Version);
        TxtVersion.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!AipDocument.IsValidVersion(Version, out var error))
        {
            TxtError.Text = error;
            TxtError.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}
