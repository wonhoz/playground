// WPF + UseWindowsForms 혼용 시 발생하는 타입 모호성 해결
global using Application              = System.Windows.Application;
global using MessageBox               = System.Windows.MessageBox;
global using Clipboard                = System.Windows.Clipboard;
global using KeyEventArgs             = System.Windows.Input.KeyEventArgs;
global using RoutedEventArgs          = System.Windows.RoutedEventArgs;
global using TextChangedEventArgs     = System.Windows.Controls.TextChangedEventArgs;
global using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
global using Window                   = System.Windows.Window;
global using Visibility               = System.Windows.Visibility;
global using MessageBoxButton         = System.Windows.MessageBoxButton;
global using MessageBoxImage          = System.Windows.MessageBoxImage;
global using MessageBoxResult         = System.Windows.MessageBoxResult;
