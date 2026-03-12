using Microsoft.Win32;

namespace SVG.Forge.ViewModels;

public class MainViewModel : BaseViewModel
{
    SvgDocument _document;
    ToolMode _tool = ToolMode.Select;
    SvgElement? _selected;
    SvgLayer? _selectedLayer;
    double _zoom = 1.0;
    string _status = "준비";
    bool _showGrid = true;

    // ── Document ─────────────────────────────────────────────────────
    public SvgDocument Document
    {
        get => _document;
        private set { Set(ref _document, value); Notify(nameof(Layers)); }
    }
    public ObservableCollection<SvgLayer> Layers => Document.Layers;

    // ── Selection ────────────────────────────────────────────────────
    public SvgElement? SelectedElement
    {
        get => _selected;
        set
        {
            Set(ref _selected, value);
            Notify(nameof(HasSelection));
            Notify(nameof(SelFillColor));   Notify(nameof(SelHasFill));
            Notify(nameof(SelStrokeColor)); Notify(nameof(SelHasStroke));
            Notify(nameof(SelStrokeWidth)); Notify(nameof(SelOpacity));
            Notify(nameof(SelX));           Notify(nameof(SelY));
            Notify(nameof(SelW));           Notify(nameof(SelH));
            Notify(nameof(SelText));        Notify(nameof(SelFontSize));
            Notify(nameof(IsTextSelected));
            DeleteCmd.RaiseCanExecuteChanged();
            DuplicateCmd.RaiseCanExecuteChanged();
            BringFwdCmd.RaiseCanExecuteChanged();
            SendBwdCmd.RaiseCanExecuteChanged();
        }
    }
    public bool HasSelection => _selected != null;
    public bool IsTextSelected => _selected?.ShapeType == SvgShapeType.Text;

    public SvgLayer? SelectedLayer
    {
        get => _selectedLayer;
        set => Set(ref _selectedLayer, value);
    }

    // ── Tool ─────────────────────────────────────────────────────────
    public ToolMode CurrentTool { get => _tool; set => Set(ref _tool, value); }

    // ── View ─────────────────────────────────────────────────────────
    public double ZoomLevel
    {
        get => _zoom;
        set { Set(ref _zoom, Math.Clamp(value, 0.05, 10.0)); Notify(nameof(ZoomText)); }
    }
    public string ZoomText => $"{ZoomLevel * 100:F0}%";
    public string Status   { get => _status;   set => Set(ref _status, value); }
    public bool   ShowGrid { get => _showGrid; set => Set(ref _showGrid, value); }

    // ── Selected element properties ──────────────────────────────────
    public Color  SelFillColor    { get => _selected?.FillColor    ?? Colors.SteelBlue; set { if (_selected != null) { _selected.FillColor = value;    MarkDirty(); Notify(nameof(SelFillColor)); } } }
    public bool   SelHasFill      { get => _selected?.HasFill      ?? true;             set { if (_selected != null) { _selected.HasFill = value;       MarkDirty(); } } }
    public Color  SelStrokeColor  { get => _selected?.StrokeColor  ?? Colors.DimGray;   set { if (_selected != null) { _selected.StrokeColor = value;   MarkDirty(); Notify(nameof(SelStrokeColor)); } } }
    public bool   SelHasStroke    { get => _selected?.HasStroke    ?? true;             set { if (_selected != null) { _selected.HasStroke = value;     MarkDirty(); } } }
    public double SelStrokeWidth  { get => _selected?.StrokeWidth  ?? 1.5;             set { if (_selected != null) { _selected.StrokeWidth = value;   MarkDirty(); } } }
    public double SelOpacity      { get => _selected?.Opacity      ?? 1.0;             set { if (_selected != null) { _selected.Opacity = value;       MarkDirty(); } } }
    public double SelX            { get => _selected?.X            ?? 0;               set { if (_selected != null) { _selected.X = value;             MarkDirty(); ElementChanged?.Invoke(_selected!); } } }
    public double SelY            { get => _selected?.Y            ?? 0;               set { if (_selected != null) { _selected.Y = value;             MarkDirty(); ElementChanged?.Invoke(_selected!); } } }
    public double SelW            { get => _selected?.W            ?? 0;               set { if (_selected != null) { _selected.W = value;             MarkDirty(); ElementChanged?.Invoke(_selected!); } } }
    public double SelH            { get => _selected?.H            ?? 0;               set { if (_selected != null) { _selected.H = value;             MarkDirty(); ElementChanged?.Invoke(_selected!); } } }
    public string SelText         { get => _selected?.Text         ?? "";              set { if (_selected != null) { _selected.Text = value;           MarkDirty(); ElementChanged?.Invoke(_selected!); } } }
    public double SelFontSize     { get => _selected?.FontSize     ?? 16;             set { if (_selected != null) { _selected.FontSize = value;       MarkDirty(); ElementChanged?.Invoke(_selected!); } } }

    // ── Events for canvas refresh ────────────────────────────────────
    public event Action? CanvasRefreshRequested;
    public event Action<SvgElement>? ElementChanged;
    public event Action<string>? ExportPngRequested;

    // ── Commands ─────────────────────────────────────────────────────
    public RelayCommand           NewCmd        { get; }
    public RelayCommand           OpenCmd       { get; }
    public RelayCommand           SaveCmd       { get; }
    public RelayCommand           SaveAsCmd     { get; }
    public RelayCommand           ExportSvgCmd  { get; }
    public RelayCommand           ExportPngCmd  { get; }
    public RelayCommand           DeleteCmd     { get; }
    public RelayCommand           DuplicateCmd  { get; }
    public RelayCommand           BringFwdCmd   { get; }
    public RelayCommand           SendBwdCmd    { get; }
    public RelayCommand           AddLayerCmd   { get; }
    public RelayCommand           ZoomInCmd     { get; }
    public RelayCommand           ZoomOutCmd    { get; }
    public RelayCommand           ZoomFitCmd    { get; }
    public RelayCommand<string>   SetToolCmd    { get; }

    public MainViewModel()
    {
        _document = SvgDocument.CreateDefault();
        _selectedLayer = _document.Layers.FirstOrDefault();

        NewCmd       = new(DoNew);
        OpenCmd      = new(DoOpen);
        SaveCmd      = new(DoSave);
        SaveAsCmd    = new(DoSaveAs);
        ExportSvgCmd = new(DoExportSvg);
        ExportPngCmd = new(DoExportPng);
        DeleteCmd    = new(DoDelete,    () => HasSelection);
        DuplicateCmd = new(DoDuplicate, () => HasSelection);
        BringFwdCmd  = new(DoBringFwd,  () => HasSelection);
        SendBwdCmd   = new(DoSendBwd,   () => HasSelection);
        AddLayerCmd  = new(DoAddLayer);
        ZoomInCmd    = new(() => ZoomLevel = Math.Min(10, ZoomLevel * 1.25));
        ZoomOutCmd   = new(() => ZoomLevel = Math.Max(0.05, ZoomLevel / 1.25));
        ZoomFitCmd   = new(() => ZoomLevel = 1.0);
        SetToolCmd   = new(t => { if (Enum.TryParse<ToolMode>(t, out var m)) CurrentTool = m; });
    }

    // ── File operations ──────────────────────────────────────────────
    void DoNew()
    {
        if (!ConfirmDiscard()) return;
        Document = SvgDocument.CreateDefault();
        SelectedElement = null;
        SelectedLayer = Document.Layers.FirstOrDefault();
        CanvasRefreshRequested?.Invoke();
        Status = "새 문서 생성됨";
    }

    void DoOpen()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Title = "SVG 파일 열기", Filter = "SVG 파일|*.svg|모든 파일|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            Document = SvgSerializer.Load(dlg.FileName);
            SelectedElement = null;
            SelectedLayer = Document.Layers.FirstOrDefault();
            CanvasRefreshRequested?.Invoke();
            Status = $"열기 완료: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"열기 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void DoSave() { if (Document.FilePath == null) DoSaveAs(); else SaveToFile(Document.FilePath); }

    void DoSaveAs()
    {
        var dlg = new SaveFileDialog
        {
            Title = "SVG 파일 저장", Filter = "SVG 파일|*.svg",
            FileName = Document.FilePath != null ? Path.GetFileNameWithoutExtension(Document.FilePath) : "새문서"
        };
        if (dlg.ShowDialog() == true) SaveToFile(dlg.FileName);
    }

    void SaveToFile(string path)
    {
        try
        {
            SvgSerializer.Save(Document, path);
            Document.FilePath = path;
            Document.IsDirty = false;
            Status = $"저장 완료: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void DoExportSvg()
    {
        var dlg = new SaveFileDialog { Title = "SVG 내보내기", Filter = "SVG 파일|*.svg", FileName = "export" };
        if (dlg.ShowDialog() != true) return;
        try { SvgSerializer.Save(Document, dlg.FileName); Status = "SVG 내보내기 완료"; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    void DoExportPng()
    {
        var dlg = new SaveFileDialog { Title = "PNG 내보내기", Filter = "PNG 파일|*.png", FileName = "export" };
        if (dlg.ShowDialog() == true) ExportPngRequested?.Invoke(dlg.FileName);
    }

    // ── Element management ───────────────────────────────────────────
    public SvgElement AddElement(SvgElement el)
    {
        var layer = SelectedLayer ?? Document.Layers.FirstOrDefault();
        if (layer == null)
        {
            layer = new SvgLayer { Name = "레이어 1" };
            Document.Layers.Add(layer);
            SelectedLayer = layer;
        }
        el.Name = $"{el.ShapeType} {layer.Elements.Count + 1}";
        layer.Elements.Add(el);
        MarkDirty();
        SelectedElement = el;
        Status = $"{el.Name} 추가됨";
        return el;
    }

    void DoDelete()
    {
        if (_selected == null) return;
        foreach (var l in Document.Layers) if (l.Elements.Remove(_selected)) break;
        SelectedElement = null;
        MarkDirty();
        CanvasRefreshRequested?.Invoke();
        Status = "요소 삭제됨";
    }

    void DoDuplicate()
    {
        if (_selected == null) return;
        var clone = _selected.Clone();
        AddElement(clone);
        CanvasRefreshRequested?.Invoke();
        Status = "요소 복제됨";
    }

    void DoBringFwd()
    {
        if (_selected == null) return;
        foreach (var l in Document.Layers)
        {
            var i = l.Elements.IndexOf(_selected);
            if (i >= 0 && i < l.Elements.Count - 1) { l.Elements.Move(i, i + 1); break; }
        }
        MarkDirty();
        CanvasRefreshRequested?.Invoke();
    }

    void DoSendBwd()
    {
        if (_selected == null) return;
        foreach (var l in Document.Layers)
        {
            var i = l.Elements.IndexOf(_selected);
            if (i > 0) { l.Elements.Move(i, i - 1); break; }
        }
        MarkDirty();
        CanvasRefreshRequested?.Invoke();
    }

    void DoAddLayer()
    {
        var layer = new SvgLayer { Name = $"레이어 {Document.Layers.Count + 1}" };
        Document.Layers.Add(layer);
        SelectedLayer = layer;
        MarkDirty();
    }

    void MarkDirty() => Document.IsDirty = true;

    bool ConfirmDiscard()
    {
        if (!Document.IsDirty) return true;
        return MessageBox.Show("변경 사항을 저장하지 않고 닫으시겠습니까?",
            "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    // Refresh property notifications for selected element after external changes
    public void RefreshSelectionProperties()
    {
        Notify(nameof(SelX)); Notify(nameof(SelY));
        Notify(nameof(SelW)); Notify(nameof(SelH));
    }
}
