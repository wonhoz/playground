// WPF + UseWindowsForms 혼용 시 타입 모호성 해결
global using System.IO;
global using Application          = System.Windows.Application;
global using MessageBox           = System.Windows.MessageBox;
global using KeyEventArgs         = System.Windows.Input.KeyEventArgs;
global using RoutedEventArgs      = System.Windows.RoutedEventArgs;
global using HorizontalAlignment  = System.Windows.HorizontalAlignment;
global using Button               = System.Windows.Controls.Button;
