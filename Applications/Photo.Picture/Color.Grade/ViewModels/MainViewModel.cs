namespace Color.Grade.ViewModels;

using System.Windows.Media.Imaging;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Notify([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    // ── 이미지 ────────────────────────────────────────────────────────────

    BitmapSource? _originalFull;
    BitmapSource? _originalPreview;

    BitmapSource? _before;
    public BitmapSource? Before { get => _before; set { _before = value; Notify(); Notify(nameof(HasImage)); } }

    BitmapSource? _after;
    public BitmapSource? After { get => _after; set { _after = value; Notify(); } }

    public bool HasImage => _originalFull != null;

    // ── 조정값 ────────────────────────────────────────────────────────────

    public ImageAdjustments Adjustments { get; } = new();

    // 슬라이더는 -100 ~ +100 범위, 내부에서 스케일 변환
    double _exposure;
    public double Exposure
    {
        get => _exposure;
        set { _exposure = value; Adjustments.Exposure = value / 50.0; Notify(); SchedulePreview(); }
    }

    double _contrast;
    public double Contrast
    {
        get => _contrast;
        set { _contrast = value; Adjustments.Contrast = value / 100.0; Notify(); SchedulePreview(); }
    }

    double _saturation;
    public double Saturation
    {
        get => _saturation;
        set { _saturation = value; Adjustments.Saturation = value / 100.0; Notify(); SchedulePreview(); }
    }

    double _temperature;
    public double Temperature
    {
        get => _temperature;
        set { _temperature = value; Adjustments.Temperature = value / 100.0; Notify(); SchedulePreview(); }
    }

    double _highlights;
    public double Highlights
    {
        get => _highlights;
        set { _highlights = value; Adjustments.Highlights = value / 100.0; Notify(); SchedulePreview(); }
    }

    double _shadows;
    public double Shadows
    {
        get => _shadows;
        set { _shadows = value; Adjustments.Shadows = value / 100.0; Notify(); SchedulePreview(); }
    }

    // ── LUT ──────────────────────────────────────────────────────────────

    public ObservableCollection<LutInfo> Presets { get; } = new();

    LutInfo? _selectedLut;
    public LutInfo? SelectedLut
    {
        get => _selectedLut;
        set
        {
            _selectedLut = value;
            _currentLut3d = null;
            if (value?.FilePath != null)
                _currentLut3d = LutParser.Parse(value.FilePath);
            Notify();
            SchedulePreview();
        }
    }

    Lut3D? _currentLut3d;

    // ── 상태 ─────────────────────────────────────────────────────────────

    string _status = "이미지를 끌어다 놓거나 열기 버튼을 클릭하세요";
    public string Status { get => _status; set { _status = value; Notify(); } }

    bool _isBusy;
    public bool IsBusy { get => _isBusy; set { _isBusy = value; Notify(); } }

    // ── 배치 ─────────────────────────────────────────────────────────────

    string _currentPath = "";
    public string CurrentPath { get => _currentPath; set { _currentPath = value; Notify(); } }

    // ── 초기화 ────────────────────────────────────────────────────────────

    public MainViewModel()
    {
        foreach (var p in LutLibrary.BuiltIn)
            Presets.Add(p);
    }

    // ── 이미지 로드 ───────────────────────────────────────────────────────

    public void LoadImage(string path)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource    = new Uri(path);
            bi.CacheOption  = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            _originalFull    = bi;
            _originalPreview = ImageProcessor.ResizeForPreview(bi);
            Before           = _originalPreview;
            CurrentPath      = path;
            Status           = $"로드됨: {System.IO.Path.GetFileName(path)}  ({bi.PixelWidth}×{bi.PixelHeight})";
            UpdatePreview();
        }
        catch (Exception ex)
        {
            Status = $"로드 실패: {ex.Message}";
        }
    }

    public void AddCustomLut(string path)
    {
        var info = new LutInfo
        {
            Name     = $"[외부] {System.IO.Path.GetFileNameWithoutExtension(path)}",
            IsBuiltIn = false,
            FilePath = path,
        };
        Presets.Add(info);
        SelectedLut = info;
        Status = $"LUT 로드됨: {info.Name}";
    }

    // ── 미리보기 ─────────────────────────────────────────────────────────

    CancellationTokenSource? _previewCts;

    void SchedulePreview()
    {
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var ct = _previewCts.Token;
        Task.Run(async () =>
        {
            await Task.Delay(60, ct); // 60ms 디바운스
            if (ct.IsCancellationRequested) return;
            System.Windows.Application.Current?.Dispatcher.Invoke(UpdatePreview);
        }, ct);
    }

    public void UpdatePreview()
    {
        if (_originalPreview == null) return;
        try
        {
            var result = ImageProcessor.ProcessFull(_originalPreview, Adjustments, _selectedLut, _currentLut3d);
            result.Freeze();
            After = result;
        }
        catch { }
    }

    // ── 초기화 ────────────────────────────────────────────────────────────

    public void ResetAdjustments()
    {
        Exposure    = 0;
        Contrast    = 0;
        Saturation  = 0;
        Temperature = 0;
        Highlights  = 0;
        Shadows     = 0;
        SelectedLut = Presets.FirstOrDefault();
    }

    // ── 내보내기 (단일) ──────────────────────────────────────────────────

    public async Task ExportAsync(string outPath, int quality = 90)
    {
        if (_originalFull == null) return;
        IsBusy = true;
        Status = "내보내기 중...";
        try
        {
            await Task.Run(() =>
            {
                var result = ImageProcessor.ProcessFull(_originalFull!, Adjustments, _selectedLut, _currentLut3d);
                result.Freeze();
                Save(result, outPath, quality);
            });
            Status = $"내보내기 완료: {System.IO.Path.GetFileName(outPath)}";
        }
        catch (Exception ex)
        {
            Status = $"내보내기 실패: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // ── 배치 내보내기 ────────────────────────────────────────────────────

    public async Task BatchExportAsync(string inputFolder, string outputFolder, string ext, int quality, CancellationToken ct)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp" };
        var files = Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly)
                             .Where(f => supported.Contains(System.IO.Path.GetExtension(f)))
                             .ToList();
        IsBusy = true;
        int done = 0;
        Directory.CreateDirectory(outputFolder);
        foreach (var f in files)
        {
            if (ct.IsCancellationRequested) break;
            Status = $"배치 처리 중: {done}/{files.Count}  {System.IO.Path.GetFileName(f)}";
            try
            {
                await Task.Run(() =>
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource   = new Uri(f);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();
                    var result = ImageProcessor.ProcessFull(bi, Adjustments, _selectedLut, _currentLut3d);
                    result.Freeze();
                    var outFile = System.IO.Path.Combine(outputFolder,
                        System.IO.Path.GetFileNameWithoutExtension(f) + ext);
                    Save(result, outFile, quality);
                }, ct);
                done++;
            }
            catch { }
        }
        IsBusy = false;
        Status = ct.IsCancellationRequested
            ? $"배치 취소됨: {done}/{files.Count} 완료"
            : $"배치 완료: {done}개 처리됨";
    }

    // ── 파일 저장 ────────────────────────────────────────────────────────

    static void Save(System.Windows.Media.Imaging.BitmapSource bmp, string path, int quality)
    {
        using var fs = System.IO.File.Create(path);
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        System.Windows.Media.Imaging.BitmapEncoder enc = ext switch
        {
            ".png"  => new System.Windows.Media.Imaging.PngBitmapEncoder(),
            ".bmp"  => new System.Windows.Media.Imaging.BmpBitmapEncoder(),
            ".tiff" or ".tif" => new System.Windows.Media.Imaging.TiffBitmapEncoder(),
            _       => new System.Windows.Media.Imaging.JpegBitmapEncoder
                        { QualityLevel = quality }
        };
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        enc.Save(fs);
    }
}
