// WPF + UseWindowsForms 혼용 시 타입 모호성 해결
global using System.IO;
global using Application         = System.Windows.Application;
global using MessageBox          = System.Windows.MessageBox;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using Button              = System.Windows.Controls.Button;
global using DragEventArgs       = System.Windows.DragEventArgs;
global using DragDropEffects     = System.Windows.DragDropEffects;
global using DataFormats         = System.Windows.DataFormats;
// Color는 IconGenerator(System.Drawing.Color)와 충돌 → 파일별 처리
