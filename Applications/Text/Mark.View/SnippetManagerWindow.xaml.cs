using System.Windows;
using System.Windows.Controls;
using MarkView.Models;
using MarkView.Services;

namespace MarkView;

public partial class SnippetManagerWindow : Window
{
    private readonly AppSettings _settings;
    private int _selectedIndex = -1;
    private bool _isDirty;

    public SnippetManagerWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        var prev = _selectedIndex;
        SnippetList.Items.Clear();
        foreach (var s in _settings.CustomSnippets)
            SnippetList.Items.Add(new ListBoxItem { Content = s.Name, Tag = s });
        if (prev >= 0 && prev < SnippetList.Items.Count)
            SnippetList.SelectedIndex = prev;
    }

    private void SnippetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SnippetList.SelectedItem is not ListBoxItem item) { ClearEditor(); return; }
        _selectedIndex = SnippetList.SelectedIndex;
        var s = (UserSnippet)item.Tag!;
        TxtName.Text = s.Name;
        TxtContent.Text = s.Content;
        BtnDelete.IsEnabled = true;
        _isDirty = false;
        BtnSave.IsEnabled = false;
    }

    private void ClearEditor()
    {
        TxtName.Text = "";
        TxtContent.Text = "";
        BtnDelete.IsEnabled = false;
        _isDirty = false;
        BtnSave.IsEnabled = false;
    }

    private void TxtName_TextChanged(object sender, TextChangedEventArgs e) => MarkDirty();
    private void TxtContent_TextChanged(object sender, TextChangedEventArgs e) => MarkDirty();

    private void MarkDirty()
    {
        _isDirty = true;
        BtnSave.IsEnabled = _selectedIndex >= 0 && !string.IsNullOrWhiteSpace(TxtName.Text);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var s = new UserSnippet { Name = "새 스닛펫", Content = "" };
        _settings.CustomSnippets.Add(s);
        _settings.Save();
        RefreshList();
        _selectedIndex = _settings.CustomSnippets.Count - 1;
        SnippetList.SelectedIndex = _selectedIndex;
        TxtName.Focus();
        TxtName.SelectAll();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _settings.CustomSnippets.Count) return;
        var name = _settings.CustomSnippets[_selectedIndex].Name;
        if (MessageBox.Show($"'{name}' 스닛펫을 삭제하시겠습니까?",
            "삭제 확인", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        _settings.CustomSnippets.RemoveAt(_selectedIndex);
        _settings.Save();
        _selectedIndex = Math.Max(0, _selectedIndex - 1);
        RefreshList();
        if (SnippetList.Items.Count == 0) ClearEditor();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _settings.CustomSnippets.Count) return;
        _settings.CustomSnippets[_selectedIndex].Name = TxtName.Text.Trim();
        _settings.CustomSnippets[_selectedIndex].Content = TxtContent.Text;
        _settings.Save();
        RefreshList();
        _isDirty = false;
        BtnSave.IsEnabled = false;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var r = MessageBox.Show("저장되지 않은 변경이 있습니다. 저장하시겠습니까?",
                "닫기", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes) BtnSave_Click(sender, e);
            else if (r == MessageBoxResult.Cancel) return;
        }
        Close();
    }
}
