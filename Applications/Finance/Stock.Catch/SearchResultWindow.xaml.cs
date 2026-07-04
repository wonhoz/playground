using System.Windows;
using System.Windows.Controls;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

public partial class SearchResultWindow : Window
{
    /// <summary>선택된 종목(취소 시 null).</summary>
    public StockHit? Selected { get; private set; }

    public SearchResultWindow(string query, List<StockHit> hits)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);

        SearchInfo.Text = $"'{query}' 검색 결과 {hits.Count}건 — 종목을 선택하세요 (더블클릭 선택)";
        ResultList.ItemsSource = hits;
        ResultList.SelectedIndex = 0;
    }

    private void List_DoubleClick(object sender, RoutedEventArgs e) => Confirm();
    private void Ok_Click(object sender, RoutedEventArgs e) => Confirm();
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Confirm()
    {
        if (ResultList.SelectedItem is StockHit h)
        {
            Selected = h;
            DialogResult = true;
        }
    }
}
