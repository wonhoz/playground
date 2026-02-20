using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using WorkspaceSwitcher.Models;

namespace WorkspaceSwitcher.Dialogs;

public partial class EditWorkspaceDialog : Window
{
    public Workspace Result { get; private set; }

    private string _selectedColor;
    private readonly ObservableCollection<WorkspaceApp> _apps;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private static readonly string[] Palette =
    [
        "#C0392B", "#E74C3C", "#D35400", "#E67E22", "#F39C12",
        "#27AE60", "#16A085", "#1ABC9C", "#2980B9", "#3498DB",
        "#8E44AD", "#9B59B6", "#2C3E50", "#5C5CFF", "#7F8C8D"
    ];

    public EditWorkspaceDialog(Workspace? src = null)
    {
        InitializeComponent();

        Result = src is null ? new Workspace() : new Workspace
        {
            Id    = src.Id,
            Name  = src.Name,
            Emoji = src.Emoji,
            Color = src.Color,
            Apps  = src.Apps.Select(a => new WorkspaceApp
            {
                Name  = a.Name,
                Path  = a.Path,
                Args  = a.Args,
                IsUrl = a.IsUrl
            }).ToList()
        };

        _selectedColor = Result.Color;
        _apps = new ObservableCollection<WorkspaceApp>(Result.Apps);

        NameBox.Text  = Result.Name;
        EmojiBox.Text = Result.Emoji;

        AppsList.ItemsSource = _apps;
        _apps.CollectionChanged += (_, _) => UpdatePreview();

        BuildColorPanel();
        UpdatePreview();

        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                int val = 1;
                DwmSetWindowAttribute(source.Handle, 20, ref val, sizeof(int));
            }
        };
    }

    // ── 색상 팔레트 ───────────────────────────────────────────────────────────

    private void BuildColorPanel()
    {
        ColorPanel.Children.Clear();
        foreach (var hex in Palette)
        {
            var isSelected = string.Equals(hex, _selectedColor, StringComparison.OrdinalIgnoreCase);
            var circle = new Border
            {
                Width = 26, Height = 26, CornerRadius = new CornerRadius(13),
                Margin = new Thickness(3),
                Background   = (Brush)new BrushConverter().ConvertFrom(hex)!,
                BorderBrush  = isSelected ? Brushes.White : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 2.5 : 0),
                Cursor = Cursors.Hand, Tag = hex
            };
            circle.MouseLeftButtonDown += (_, _) =>
            {
                _selectedColor = (string)circle.Tag;
                BuildColorPanel();
            };
            ColorPanel.Children.Add(circle);
        }
    }

    // ── 앱 목록 CRUD ──────────────────────────────────────────────────────────

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "실행 파일|*.exe|모든 파일|*.*",
            Title  = "실행 파일 선택"
        };
        if (dlg.ShowDialog() == true)
        {
            NewAppPathBox.Text = dlg.FileName;
        }
    }

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        var path = NewAppPathBox.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        var name = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? path : System.IO.Path.GetFileNameWithoutExtension(path);

        _apps.Add(new WorkspaceApp
        {
            Name  = name,
            Path  = path,
            IsUrl = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        });

        NewAppPathBox.Text = "";
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is WorkspaceApp app)
            _apps.Remove(app);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not WorkspaceApp app) return;
        int idx = _apps.IndexOf(app);
        if (idx > 0) _apps.Move(idx, idx - 1);
    }

    private void UpdatePreview()
    {
        PreviewText.Text = _apps.Count > 0
            ? $"실행 순서: {string.Join(" → ", _apps.Take(4).Select(a => a.Name))}{(_apps.Count > 4 ? " ..." : "")}"
            : "앱 경로나 URL을 추가하세요.";
    }

    // ── OK / Cancel ───────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("워크스페이스 이름을 입력하세요.", "Workspace.Switcher",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result.Name  = NameBox.Text.Trim();
        Result.Emoji = string.IsNullOrWhiteSpace(EmojiBox.Text) ? "💼" : EmojiBox.Text.Trim();
        Result.Color = _selectedColor;
        Result.Apps  = [.. _apps];

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
