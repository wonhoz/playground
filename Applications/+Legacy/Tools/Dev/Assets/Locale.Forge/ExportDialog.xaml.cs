using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using LocaleForge.Models;
using LocaleForge.Parsers;
using LocaleForge.ViewModels;
using Microsoft.Win32;

namespace LocaleForge;

public partial class ExportDialog : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly MainViewModel _vm;

    public ExportDialog(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;

        // 언어 목록 채우기
        foreach (var lang in _vm.Languages)
            CbLang.Items.Add(lang);
        if (CbLang.Items.Count > 0) CbLang.SelectedIndex = 0;
        CbFormat.SelectedIndex = 0;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var format = GetSelectedFormat();
        var ext = format switch
        {
            LocaleFileFormat.Json => ".json",
            LocaleFileFormat.Yaml => ".yaml",
            LocaleFileFormat.Resx => ".resx",
            LocaleFileFormat.Po => ".po",
            LocaleFileFormat.Properties => ".properties",
            _ => ".json"
        };

        var dlg = new SaveFileDialog
        {
            Title = "내보낼 파일 경로 선택",
            Filter = $"{format} 파일 (*{ext})|*{ext}",
            DefaultExt = ext,
            FileName = $"{CbLang.SelectedItem}{ext}"
        };
        if (dlg.ShowDialog() == true)
            TxtSavePath.Text = dlg.FileName;
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (CbLang.SelectedItem is not string langCode)
        {
            MessageBox.Show("언어를 선택하세요.", "Locale.Forge");
            return;
        }
        if (string.IsNullOrEmpty(TxtSavePath.Text))
        {
            MessageBox.Show("저장 경로를 지정하세요.", "Locale.Forge");
            return;
        }

        var format = GetSelectedFormat();
        var parser = ParserRegistry.Get(format);
        var entries = _vm.Entries
            .ToDictionary(e => e.Key, e => e.GetValue(langCode));

        try
        {
            parser.Save(TxtSavePath.Text, entries);
            MessageBox.Show($"내보내기 완료!\n{TxtSavePath.Text}", "Locale.Forge",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"내보내기 실패: {ex.Message}", "Locale.Forge",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private LocaleFileFormat GetSelectedFormat()
    {
        if (CbFormat.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return Enum.Parse<LocaleFileFormat>(tag);
        return LocaleFileFormat.Json;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
