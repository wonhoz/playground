using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Timeline.Craft.Controls;
using Timeline.Craft.Services;

namespace Timeline.Craft;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm = new();
    bool _propChanging;  // 속성 패널 → 이벤트 업데이트 루프 방지

    public MainWindow()
    {
        DataContext = _vm;
        InitializeComponent();

        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(h, 20, ref v, sizeof(int));

            TLView.ViewModel = _vm;
            _vm.NewProject();

            _vm.PropertyChanged += (_, e) =>
            {
                if (!IsLoaded) return;
                if (e.PropertyName == nameof(MainViewModel.SelectedEvent))
                    LoadEventProps();
            };
        };
    }

    // ── 파일 메뉴 ─────────────────────────────────────────────────────────────

    void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        _vm.NewProject();
    }

    void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "타임라인 열기", Filter = "Timeline.Craft|*.tcf|JSON|*.json|모든 파일|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var proj = ProjectSerializer.Load(dlg.FileName);
            if (proj == null) throw new InvalidDataException("파일을 읽을 수 없습니다.");
            _vm.LoadProject(proj);
            _vm.CurrentPath = dlg.FileName;
            _vm.StatusText  = $"열기 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일 열기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_vm.CurrentPath)) { SaveAsFile_Click(sender, e); return; }
        DoSave(_vm.CurrentPath);
    }

    void SaveAsFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "다른 이름으로 저장",
            Filter     = "Timeline.Craft|*.tcf|JSON|*.json",
            FileName   = _vm.Title,
            DefaultExt = ".tcf"
        };
        if (dlg.ShowDialog() != true) return;
        _vm.CurrentPath = dlg.FileName;
        DoSave(dlg.FileName);
    }

    void DoSave(string path)
    {
        try
        {
            ProjectSerializer.Save(_vm.ToProject(), path);
            _vm.StatusText = $"저장 완료: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    bool ConfirmDiscard()
    {
        if (_vm.Events.Count == 0) return true;
        var r = MessageBox.Show("현재 작업을 저장하지 않고 닫겠습니까?",
            "확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return r == MessageBoxResult.Yes;
    }

    // ── 레인 / 이벤트 ─────────────────────────────────────────────────────────

    void AddLane_Click(object sender, RoutedEventArgs e) => _vm.AddLane();

    void RemoveLane_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is TimelineLane lane)
            _vm.RemoveLane(lane);
    }

    void AddEvent_Click(object sender, RoutedEventArgs e)
    {
        int lane = _vm.Lanes.Count > 0 ? 0 : -1;
        if (lane < 0) { MessageBox.Show("먼저 레인을 추가하세요.", "알림"); return; }
        var ev = _vm.AddEvent(DateTime.Today, DateTime.Today.AddDays(7), lane);
        TLView.RebuildAll();
        LoadEventProps();
    }

    void DeleteEvent_Click(object sender, RoutedEventArgs e)
    {
        _vm.DeleteSelected();
        TLView.RebuildAll();
    }

    // ── 속성 패널 ─────────────────────────────────────────────────────────────

    void LoadEventProps()
    {
        if (!IsLoaded) return;
        _propChanging = true;
        try
        {
            var ev = _vm.SelectedEvent;
            TxtEvTitle.Text  = ev?.Title   ?? "";
            TxtEvStart.Text  = ev?.Start.ToString("yyyy-MM-dd") ?? "";
            TxtEvEnd.Text    = ev?.End.ToString("yyyy-MM-dd")   ?? "";
            TxtEvColor.Text  = ev?.Color   ?? "";
            TxtEvNotes.Text  = ev?.Notes   ?? "";
            ChkMilestone.IsChecked = ev?.IsMilestone ?? false;
            UpdateColorPreview(ev?.Color ?? "#3B82F6");
        }
        finally { _propChanging = false; }
    }

    void EvProp_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded || _propChanging || _vm.SelectedEvent == null) return;

        _propChanging = true;
        try
        {
            var ev = _vm.SelectedEvent;
            ev.Title = TxtEvTitle.Text;
            ev.Notes = TxtEvNotes.Text;
            if (DateTime.TryParse(TxtEvStart.Text, out var s)) ev.Start = s;
            if (DateTime.TryParse(TxtEvEnd.Text,   out var d)) ev.End   = d;
            TLView.RebuildAll();
        }
        finally { _propChanging = false; }
    }

    void EvColor_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded || _propChanging || _vm.SelectedEvent == null) return;
        var col = TxtEvColor.Text.Trim();
        if (col.Length == 7 && col.StartsWith('#'))
        {
            _vm.SelectedEvent.Color = col;
            UpdateColorPreview(col);
            TLView.RebuildAll();
        }
    }

    void Milestone_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _propChanging || _vm.SelectedEvent == null) return;
        _vm.SelectedEvent.IsMilestone = ChkMilestone.IsChecked == true;
        TLView.RebuildAll();
    }

    void UpdateColorPreview(string hex)
    {
        try
        {
            ColorPreview.Background = (SolidColorBrush)new System.Windows.Media.BrushConverter()
                .ConvertFromString(hex)!;
        }
        catch { }
    }

    // ── 줌 / 스크롤 ───────────────────────────────────────────────────────────

    void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _vm.ZoomIn();
        TLView.RebuildAll();
    }

    void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _vm.ZoomOut();
        TLView.RebuildAll();
    }

    void TimelineScroll_Wheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Delta > 0) _vm.ZoomIn();
        else             _vm.ZoomOut();
        TLView.RebuildAll();
        e.Handled = true;
    }

    // ── PNG 내보내기 ──────────────────────────────────────────────────────────

    void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "PNG로 저장",
            Filter     = "PNG 이미지|*.png",
            FileName   = $"{_vm.Title}_{DateTime.Now:yyyyMMdd}.png",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() != true) return;

        var bmp = TLView.RenderToBitmap();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = new FileStream(dlg.FileName, FileMode.Create);
        enc.Save(fs);
        _vm.StatusText = $"PNG 저장 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
    }
}
