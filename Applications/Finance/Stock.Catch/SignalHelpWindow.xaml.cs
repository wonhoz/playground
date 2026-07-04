using System.Windows;
using Stock.Catch.Services;

namespace Stock.Catch;

/// <summary>시그널 용어·등급·운용 가이드 도움말(실측 근거 요약 포함).</summary>
public partial class SignalHelpWindow : Window
{
    public SignalHelpWindow()
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
