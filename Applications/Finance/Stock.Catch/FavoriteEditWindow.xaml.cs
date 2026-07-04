using System.Windows;
using Stock.Catch.Models;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>즐겨찾기 표시 이름 수정 다이얼로그. 저장 시 전달받은 <see cref="FavoriteStock"/>를 직접 갱신한다.</summary>
public partial class FavoriteEditWindow : Window
{
    private readonly FavoriteStock _fav;

    public FavoriteEditWindow(FavoriteStock fav)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _fav = fav;
        CodeText.Text = fav.Code;
        NameBox.Text = fav.Name;
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _fav.Name = NameBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
