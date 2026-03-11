using System.Windows;
using SvcGuard.ViewModels;

namespace SvcGuard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) =>
        {
            App.ApplyDarkTitleBar(this);
            await _vm.LoadServicesAsync();
        };
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        => await _vm.LoadServicesAsync();

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.StartAsync(_vm.SelectedService);
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.StopAsync(_vm.SelectedService);
    }

    private async void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.RestartAsync(_vm.SelectedService);
    }

    private async void BtnAuto_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.SetStartTypeAsync(_vm.SelectedService, "Automatic");
    }

    private async void BtnManual_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.SetStartTypeAsync(_vm.SelectedService, "Manual");
    }

    private async void BtnDisable_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.SetStartTypeAsync(_vm.SelectedService, "Disabled");
    }

    private async void BtnDelayed_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedService != null) await _vm.SetStartTypeAsync(_vm.SelectedService, "Automatic (Delayed Start)");
    }

    private async void BtnGaming_Click(object sender, RoutedEventArgs e)
        => await _vm.ApplyGamingPresetAsync();

    private async void BtnDev_Click(object sender, RoutedEventArgs e)
        => await _vm.ApplyDevPresetAsync();
}
