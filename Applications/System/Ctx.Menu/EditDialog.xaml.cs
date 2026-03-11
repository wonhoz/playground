using System.Windows;

namespace CtxMenu;

public partial class EditDialog : Window
{
    public ShellEntry Result { get; private set; } = new();

    private readonly string? _originalKeyName;

    // 새 항목 추가용
    public EditDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        TxtTitle.Text = "새 항목 추가";
        CmbTarget.SelectedIndex = 0;
    }

    // 기존 항목 편집용
    public EditDialog(Window owner, ShellEntry entry)
    {
        InitializeComponent();
        Owner = owner;
        TxtTitle.Text = "항목 편집";

        _originalKeyName = entry.KeyName;
        TxtDisplayName.Text = entry.DisplayName;
        TxtKeyName.Text      = entry.KeyName;
        TxtCommand.Text      = entry.Command;
        TxtIconPath.Text     = entry.IconPath;
        RadSystem.IsChecked  = entry.Scope == RegistryScope.System;
        RadUser.IsChecked    = entry.Scope == RegistryScope.User;

        CmbTarget.SelectedIndex = entry.TargetType switch
        {
            TargetType.AllFiles   => 0,
            TargetType.Folder     => 1,
            TargetType.Background => 2,
            TargetType.Drive      => 3,
            TargetType.Extension  => 4,
            _                     => 0,
        };

        if (entry.TargetType == TargetType.Extension)
            TxtExt.Text = entry.ExtFilter;
    }

    // ── 이벤트 ────────────────────────────────────────────────────
    private void TxtDisplayName_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // 키 이름이 아직 비어있으면 표시 이름을 기반으로 자동 생성
        if (string.IsNullOrWhiteSpace(_originalKeyName) && TxtKeyName != null)
        {
            var auto = TxtDisplayName.Text.Trim()
                .Replace(" ", ".")
                .Replace("/", "_")
                .Replace("\\", "_");
            TxtKeyName.Text = auto;
        }
    }

    private void CmbTarget_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var isExt = CmbTarget.SelectedIndex == 4;
        LblExt.Visibility = isExt ? Visibility.Visible  : Visibility.Collapsed;
        TxtExt.Visibility = isExt ? Visibility.Visible  : Visibility.Collapsed;
    }

    private void BtnBrowseCommand_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "실행 파일 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            TxtCommand.Text = $"\"{dlg.FileName}\" \"%1\"";
    }

    private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "아이콘 파일 선택",
            Filter = "아이콘/실행 파일 (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|모든 파일 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            TxtIconPath.Text = dlg.FileName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                ? dlg.FileName
                : $"{dlg.FileName},0";
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Text = "";

        var keyName = TxtKeyName.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyName))
        {
            TxtError.Text = "키 이름을 입력하세요.";
            TxtKeyName.Focus();
            return;
        }

        var targetType = CmbTarget.SelectedIndex switch
        {
            0 => TargetType.AllFiles,
            1 => TargetType.Folder,
            2 => TargetType.Background,
            3 => TargetType.Drive,
            4 => TargetType.Extension,
            _ => TargetType.AllFiles,
        };

        if (targetType == TargetType.Extension && string.IsNullOrWhiteSpace(TxtExt.Text))
        {
            TxtError.Text = "확장자를 입력하세요. (예: .py)";
            TxtExt.Focus();
            return;
        }

        Result = new ShellEntry
        {
            KeyName     = keyName,
            DisplayName = TxtDisplayName.Text.Trim(),
            Command     = TxtCommand.Text.Trim(),
            IconPath    = TxtIconPath.Text.Trim(),
            TargetType  = targetType,
            Scope       = RadSystem.IsChecked == true ? RegistryScope.System : RegistryScope.User,
            IsEnabled   = true,
            ExtFilter   = targetType == TargetType.Extension ? TxtExt.Text.Trim() : "",
        };

        // 기존 편집 시 original key name 전달용
        if (_originalKeyName != null)
            Result.RegistryPath = _originalKeyName; // 임시로 원래 키이름 전달

        DialogResult = true;
    }
}
