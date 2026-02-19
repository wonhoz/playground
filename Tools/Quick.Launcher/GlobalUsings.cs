// WPF + UseWindowsForms 혼용 시 발생하는 타입 모호성 해결
global using Application          = System.Windows.Application;
global using KeyEventArgs         = System.Windows.Input.KeyEventArgs;
global using RoutedEventArgs      = System.Windows.RoutedEventArgs;
global using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
global using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
global using DragEventArgs        = System.Windows.DragEventArgs;
global using Clipboard            = System.Windows.Clipboard;
global using MessageBox           = System.Windows.MessageBox;
