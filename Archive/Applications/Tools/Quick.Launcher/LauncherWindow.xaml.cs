using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickLauncher.Models;
using QuickLauncher.Services;

namespace QuickLauncher;

public partial class LauncherWindow : Window
{
    private List<LaunchItem> _results = [];

    public LauncherWindow()
    {
        InitializeComponent();
    }

    // ── 표시 / 숨기기 ─────────────────────────────────────────────────────────

    public void ShowLauncher()
    {
        // 화면 중앙 상단 1/4 지점에 위치
        var screen = SystemParameters.WorkArea;
        Left = (screen.Width  - Width) / 2;
        Top  =  screen.Height / 5;

        SearchBox.Text = "";
        UpdateResults("");
        Show();
        Activate();
        SearchBox.Focus();
    }

    public void HideLauncher()
    {
        Hide();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideLauncher();
    }

    // ── 검색 ──────────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = SearchBox.Text;
        PlaceholderText.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
        UpdateResults(q);
    }

    private void UpdateResults(string query)
    {
        var engine = ((App)Application.Current).Engine;
        _results = string.IsNullOrWhiteSpace(query) ? [] : engine.Search(query);

        if (_results.Count == 0)
        {
            ResultsPanel.Visibility  = Visibility.Collapsed;
            NoResultText.Visibility  = string.IsNullOrWhiteSpace(query) ? Visibility.Collapsed : Visibility.Visible;
            HintBar.Visibility       = Visibility.Collapsed;
        }
        else
        {
            ResultsPanel.ItemsSource = _results;
            ResultsPanel.Visibility  = Visibility.Visible;
            ResultsPanel.SelectedIndex = 0;
            NoResultText.Visibility  = Visibility.Collapsed;
            HintBar.Visibility       = Visibility.Visible;
        }
    }

    // ── 키보드 처리 ───────────────────────────────────────────────────────────

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideLauncher();
                e.Handled = true;
                break;

            case Key.Down:
                if (ResultsPanel.Items.Count > 0)
                {
                    ResultsPanel.SelectedIndex = Math.Min(
                        ResultsPanel.SelectedIndex + 1, ResultsPanel.Items.Count - 1);
                    ResultsPanel.ScrollIntoView(ResultsPanel.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (ResultsPanel.Items.Count > 0)
                {
                    ResultsPanel.SelectedIndex = Math.Max(ResultsPanel.SelectedIndex - 1, 0);
                    ResultsPanel.ScrollIntoView(ResultsPanel.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Enter:
                Launch(ResultsPanel.SelectedItem as LaunchItem ?? _results.FirstOrDefault());
                e.Handled = true;
                break;
        }
    }

    private void Results_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Launch(ResultsPanel.SelectedItem as LaunchItem);
            e.Handled = true;
        }
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Launch(ResultsPanel.SelectedItem as LaunchItem);
    }

    // ── 실행 ──────────────────────────────────────────────────────────────────

    private void Launch(LaunchItem? item)
    {
        if (item is null) return;
        HideLauncher();
        LaunchExecutor.Execute(item);
    }
}
