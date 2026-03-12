using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EnvGuard.Models;
using EnvGuard.Services;

namespace EnvGuard;

public partial class SnapshotDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private readonly List<EnvVariable> _currentVars;
    private List<(string FilePath, Snapshot Snapshot)> _snapshots = [];

    public SnapshotDialog(List<EnvVariable> currentVars)
    {
        InitializeComponent();
        _currentVars = currentVars;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        LoadSnapshots();
    }

    private void LoadSnapshots()
    {
        _snapshots = SnapshotService.GetSnapshots();
        LbSnapshots.Items.Clear();

        foreach (var (path, snap) in _snapshots)
        {
            var item = new ListBoxItem
            {
                Content = $"[{snap.CreatedAt:yyyy-MM-dd HH:mm:ss}]  {snap.Description}  ({snap.Entries.Count}개 변수)",
                Tag = path,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Padding = new Thickness(6, 4, 6, 4)
            };
            LbSnapshots.Items.Add(item);
        }
    }

    private void LbSnapshots_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LbDiff.Items.Clear();
        if (LbSnapshots.SelectedIndex < 0 || LbSnapshots.SelectedIndex >= _snapshots.Count) return;

        var (_, snap) = _snapshots[LbSnapshots.SelectedIndex];
        var diffs = EnvService.ComputeDiff(snap.Entries, _currentVars);

        foreach (var diff in diffs.Where(d => d.Kind != DiffKind.Unchanged))
        {
            var (prefix, color) = diff.Kind switch
            {
                DiffKind.Added => ("+ 추가", Color.FromRgb(80, 220, 120)),
                DiffKind.Removed => ("- 제거", Color.FromRgb(255, 90, 90)),
                DiffKind.Modified => ("~ 변경", Color.FromRgb(255, 210, 60)),
                _ => ("  동일", Color.FromRgb(120, 120, 140))
            };

            var text = diff.Kind == DiffKind.Modified
                ? $"[{prefix}] [{diff.Scope}] {diff.Name}\n    스냅샷: {Truncate(diff.OldValue, 80)}\n    현재값: {Truncate(diff.NewValue, 80)}"
                : $"[{prefix}] [{diff.Scope}] {diff.Name} = {Truncate(diff.Kind == DiffKind.Added ? diff.NewValue : diff.OldValue, 100)}";

            var item = new ListBoxItem
            {
                Content = text,
                Foreground = new SolidColorBrush(color),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Padding = new Thickness(4, 2, 4, 2)
            };
            LbDiff.Items.Add(item);
        }

        if (LbDiff.Items.Count == 0)
        {
            LbDiff.Items.Add(new ListBoxItem
            {
                Content = "변경사항 없음 — 현재 환경변수와 스냅샷이 동일합니다.",
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 140)),
                IsEnabled = false
            });
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (LbSnapshots.SelectedIndex < 0) return;

        var (_, snap) = _snapshots[LbSnapshots.SelectedIndex];

        var msg = EnvService.IsAdmin()
            ? "선택한 스냅샷으로 사용자 + 시스템 환경변수를 복원하시겠습니까?"
            : "선택한 스냅샷으로 사용자 환경변수만 복원합니다.\n(시스템 변수 복원은 관리자 권한 필요)";

        if (MessageBox.Show(msg, "스냅샷 복원", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        // Create snapshot of current state before restoring
        SnapshotService.CreateSnapshot("자동 - 복원 전 백업", _currentVars);

        try
        {
            SnapshotService.RestoreSnapshot(snap);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"복원 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (LbSnapshots.SelectedIndex < 0) return;

        var (path, snap) = _snapshots[LbSnapshots.SelectedIndex];

        if (MessageBox.Show($"스냅샷 '{snap.Description}'을 삭제하시겠습니까?",
            "스냅샷 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        SnapshotService.DeleteSnapshot(path);
        LoadSnapshots();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
