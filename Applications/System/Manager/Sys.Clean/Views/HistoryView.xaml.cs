using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SysClean.Models;
using SysClean.Services;

namespace SysClean.Views;

public partial class HistoryView : UserControl
{
    private readonly CleanHistoryService _history = new();

    public HistoryView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        var entries = _history.Load();
        HistoryPanel.Children.Clear();

        if (entries.Count == 0)
        {
            TbEmpty.Visibility = Visibility.Visible;
            HistoryPanel.Children.Add(TbEmpty);
            TbSummary.Text = "";
            BtnClear.IsEnabled = false;
            TbSubtitle.Text = "청소 작업 이력을 확인합니다.";
            return;
        }

        TbEmpty.Visibility = Visibility.Collapsed;
        BtnClear.IsEnabled = true;

        long totalCleaned = entries.Sum(e => e.CleanedBytes);
        TbSubtitle.Text = $"총 {entries.Count}회 청소 — 누적 {CleanTarget.FormatSize(totalCleaned)} 해제";
        TbSummary.Text = $"최근 {Math.Min(entries.Count, 100)}건 표시";

        foreach (var entry in entries)
            HistoryPanel.Children.Add(BuildCard(entry));
    }

    private static Border BuildCard(CleanHistoryEntry entry)
    {
        var relTime = CleanHistoryService.FormatRelativeTime(entry.Time);
        var absTime = entry.Time.ToString("yyyy-MM-dd  HH:mm");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = System.Windows.GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = $"🧹  {CleanTarget.FormatSize(entry.CleanedBytes)} 해제",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)System.Windows.Application.Current.FindResource("AccentGreen"))
        });
        left.Children.Add(new TextBlock
        {
            Text = $"{entry.ItemCount}개 항목 청소",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            Margin = new System.Windows.Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(left, 0);

        var right = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        right.Children.Add(new TextBlock
        {
            Text = relTime,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        });
        right.Children.Add(new TextBlock
        {
            Text = absTime,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new System.Windows.Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(right, 1);

        grid.Children.Add(left);
        grid.Children.Add(right);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            CornerRadius = new System.Windows.CornerRadius(6),
            Padding = new System.Windows.Thickness(14, 10, 14, 10),
            Margin = new System.Windows.Thickness(0, 0, 0, 6),
            Child = grid
        };
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "청소 기록을 모두 지우시겠습니까?",
            "기록 삭제",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.OK) return;

        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SysClean", "history.json");
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch { }

        Refresh();
    }
}
