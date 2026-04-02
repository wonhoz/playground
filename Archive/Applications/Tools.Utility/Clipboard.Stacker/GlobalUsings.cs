// WPF + UseWindowsForms 혼용 시 타입 모호성 해결
global using Application          = System.Windows.Application;
global using Clipboard            = System.Windows.Clipboard;
global using MessageBox           = System.Windows.MessageBox;
global using KeyEventArgs         = System.Windows.Input.KeyEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using RoutedEventArgs      = System.Windows.RoutedEventArgs;
global using Button               = System.Windows.Controls.Button;
global using HorizontalAlignment  = System.Windows.HorizontalAlignment;
