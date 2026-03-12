using System.Windows;
using Microsoft.Win32;
using RegVault.Models;

namespace RegVault;

public partial class ValueEditDialog : Window
{
    private readonly RegValue _original;
    public object? NewValue { get; private set; }

    public ValueEditDialog(RegValue original)
    {
        InitializeComponent();
        App.ApplyDarkTitlebar(this);
        _original = original;

        TxtName.Text  = string.IsNullOrEmpty(original.Name) ? "(기본값)" : original.Name;
        TxtKind.Text  = original.KindDisplay;
        TxtValue.Text = original.Kind == RegistryValueKind.MultiString && original.RawData is string[] arr
            ? string.Join(Environment.NewLine, arr)
            : original.RawData?.ToString() ?? "";
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NewValue = _original.Kind switch
            {
                RegistryValueKind.DWord =>
                    TxtValue.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? (int)Convert.ToUInt32(TxtValue.Text, 16)
                        : int.Parse(TxtValue.Text),
                RegistryValueKind.QWord =>
                    TxtValue.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? (long)Convert.ToUInt64(TxtValue.Text, 16)
                        : long.Parse(TxtValue.Text),
                RegistryValueKind.Binary =>
                    TxtValue.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(h => Convert.ToByte(h, 16)).ToArray(),
                RegistryValueKind.MultiString =>
                    TxtValue.Text.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None),
                _ => TxtValue.Text
            };

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"입력값 변환 오류:\n{ex.Message}", "Reg.Vault",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;
}
