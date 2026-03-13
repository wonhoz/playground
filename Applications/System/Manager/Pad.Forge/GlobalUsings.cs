global using System;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.IO;
global using System.Linq;
global using System.Runtime.InteropServices;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Input;
global using PadForge.Core;
global using PadForge.Models;
global using PadForge.Services;
global using PadForge.ViewModels;

// 모호성 해소 alias (WPF + WinForms 동시 활성화로 인한 충돌 방지)
global using WpfApplication = System.Windows.Application;
global using WpfColor       = System.Windows.Media.Color;
global using WpfBrush2      = System.Windows.Media.Brush;
global using WpfBinding     = System.Windows.Data.Binding;
