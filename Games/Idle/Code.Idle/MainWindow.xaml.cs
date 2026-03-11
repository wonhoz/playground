using System.Windows;
using CodeIdle.Models;
using CodeIdle.ViewModels;

namespace CodeIdle;

public partial class MainWindow : Window
{
    private readonly GameViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (_, _) => App.ApplyDarkTitleBar(this);
    }

    public bool CanPrestige => _vm.State.Stage >= GameStage.IPO;

    private void BtnCode_Click(object sender, RoutedEventArgs e)
        => _vm.Click();

    private void BtnFixBug_Click(object sender, RoutedEventArgs e)
        => _vm.FixBug();

    private void BtnBuy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        var id = btn.Tag as string;
        var upgrade = _vm.Upgrades.FirstOrDefault(u => u.Id == id);
        if (upgrade != null) _vm.BuyUpgrade(upgrade);
    }

    private void BtnPrestige_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("회사를 매각하고 처음부터 시작하시겠습니까?\n클릭 파워가 영구적으로 강화됩니다.",
            "프레스티지", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _vm.Prestige();
        }
    }
}
