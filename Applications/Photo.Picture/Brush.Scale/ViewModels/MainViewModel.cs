using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush.Scale.Services;
using SkiaSharp;

namespace Brush.Scale.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    readonly UpscaleService _svc = new();
    bool _settingsDirty = false;

    // ── 이미지 상태 ───────────────────────────────────────────────────────
    string? _inputPath;
    public string? InputPath
    {
        get => _inputPath;
        set { _inputPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImage)); OnPropertyChanged(nameof(InputFileName)); }
    }

    public bool HasImage => _inputPath is not null;
    public string InputFileName => _inputPath is null ? "" : Path.GetFileName(_inputPath);

    BitmapSource? _originalBitmap;
    public BitmapSource? OriginalBitmap
    {
        get => _originalBitmap;
        set { _originalBitmap = value; OnPropertyChanged(); }
    }

    BitmapSource? _resultBitmap;
    public BitmapSource? ResultBitmap
    {
        get => _resultBitmap;
        set { _resultBitmap = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasResult)); OnPropertyChanged(nameof(ResultInfo)); }
    }

    public bool HasResult => _resultBitmap is not null;
    public string ResultInfo => _resultBitmap is null ? "" :
        $"{_resultBitmap.PixelWidth} × {_resultBitmap.PixelHeight}";

    // ── 모델 / 설정 ───────────────────────────────────────────────────────
    public ObservableCollection<ModelItem> Models            { get; } = [];
    public ObservableCollection<ModelItem> DownloadableModels { get; } = [];

    ModelItem? _selectedModel;
    public ModelItem? SelectedModel
    {
        get => _selectedModel;
        set
        {
            _selectedModel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModelNotAvailable));
            SaveSettings();
        }
    }

    public bool ModelNotAvailable =>
        _selectedModel is not null &&
        !ModelManager.IsAvailable(_selectedModel.ModelType);

    public ObservableCollection<int> ScaleFactors { get; } = [2, 4, 8];

    int _scaleFactor = 4;
    public int ScaleFactor
    {
        get => _scaleFactor;
        set { _scaleFactor = value; OnPropertyChanged(); SaveSettings(); }
    }

    public ObservableCollection<OutputFormatItem> OutputFormats { get; } = [];

    OutputFormatItem? _selectedFormat;
    public OutputFormatItem? SelectedFormat
    {
        get => _selectedFormat;
        set { _selectedFormat = value; OnPropertyChanged(); SaveSettings(); }
    }

    int _jpegQuality = 95;
    public int JpegQuality
    {
        get => _jpegQuality;
        set { _jpegQuality = value; OnPropertyChanged(); SaveSettings(); }
    }

    // ── 배치 ──────────────────────────────────────────────────────────────
    string _batchInputDir  = "";
    public string BatchInputDir
    {
        get => _batchInputDir;
        set { _batchInputDir = value; OnPropertyChanged(); SaveSettings(); }
    }

    string _batchOutputDir = "";
    public string BatchOutputDir
    {
        get => _batchOutputDir;
        set { _batchOutputDir = value; OnPropertyChanged(); SaveSettings(); }
    }

    string _outputPattern = "{name}_{scale}x";
    public string OutputPattern
    {
        get => _outputPattern;
        set { _outputPattern = value; OnPropertyChanged(); SaveSettings(); }
    }

    bool _batchRecursive = false;
    public bool BatchRecursive
    {
        get => _batchRecursive;
        set { _batchRecursive = value; OnPropertyChanged(); SaveSettings(); }
    }

    // ── 진행 ──────────────────────────────────────────────────────────────
    bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); }
    }
    public bool IsIdle => !_isBusy;

    double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    string _statusText = "이미지를 드래그하거나 클립보드에서 붙여넣기 하세요";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    CancellationTokenSource? _cts;

    // ── 이벤트 ────────────────────────────────────────────────────────────
    public event Action<int>? BatchCompleted;

    // ── 커맨드 ────────────────────────────────────────────────────────────
    public ICommand CancelCommand { get; }

    public MainViewModel()
    {
        // 모델 목록
        foreach (var (t, avail) in ModelManager.GetModelStatus())
        {
            var item = new ModelItem(t, avail);
            Models.Add(item);
            if (t != UpscaleModelType.Bicubic)
                DownloadableModels.Add(item);
        }
        _selectedModel = Models.FirstOrDefault();

        // 출력 형식
        foreach (var f in new[] { OutputFormat.Png, OutputFormat.Jpg, OutputFormat.WebP })
            OutputFormats.Add(new OutputFormatItem(f));
        _selectedFormat = OutputFormats[0];

        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsBusy);

        // 설정 복원
        LoadSettings();
    }

    // ── 설정 영속성 ───────────────────────────────────────────────────────
    void LoadSettings()
    {
        _settingsDirty = false;
        var s = SettingsService.Load();

        // 모델
        var model = Models.FirstOrDefault(m => m.ModelType.ToString() == s.SelectedModel);
        if (model is not null) _selectedModel = model;

        // 배율
        if (ScaleFactors.Contains(s.ScaleFactor))
            _scaleFactor = s.ScaleFactor;

        // 포맷
        if (Enum.TryParse<OutputFormat>(s.OutputFormat, out var fmt))
        {
            var fi = OutputFormats.FirstOrDefault(f => f.Format == fmt);
            if (fi is not null) _selectedFormat = fi;
        }

        _jpegQuality    = Math.Clamp(s.JpegQuality, 60, 100);
        _batchInputDir  = s.BatchInputDir;
        _batchOutputDir = s.BatchOutputDir;
        _outputPattern  = string.IsNullOrEmpty(s.OutputPattern) ? "{name}_{scale}x" : s.OutputPattern;
        _batchRecursive = s.BatchRecursive;
    }

    void SaveSettings()
    {
        if (_settingsDirty) return;
        _settingsDirty = true;
        // 다음 틱에 저장 (빠른 슬라이더 조작 등 연속 변경 시 한 번만 저장)
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            _settingsDirty = false;
            SettingsService.Save(new AppSettings
            {
                SelectedModel   = _selectedModel?.ModelType.ToString() ?? "Bicubic",
                ScaleFactor     = _scaleFactor,
                OutputFormat    = _selectedFormat?.Format.ToString() ?? "Png",
                JpegQuality     = _jpegQuality,
                BatchInputDir   = _batchInputDir,
                BatchOutputDir  = _batchOutputDir,
                OutputPattern   = _outputPattern,
                BatchRecursive  = _batchRecursive,
            });
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    // ── 이미지 로드 ───────────────────────────────────────────────────────
    public void LoadImage(string path)
    {
        var bmp = LoadBitmapSource(path);
        if (bmp is null)
        {
            StatusText = $"이미지 로드 실패: {Path.GetFileName(path)}";
            return;
        }
        InputPath      = path;
        ResultBitmap   = null;
        OriginalBitmap = bmp;
        StatusText = $"로드됨: {Path.GetFileName(path)}  ({bmp.PixelWidth} × {bmp.PixelHeight})";
    }

    public void LoadFromClipboard()
    {
        if (!System.Windows.Clipboard.ContainsImage()) return;
        var src = System.Windows.Clipboard.GetImage();
        if (src is null) return;

        var tmp = Path.Combine(Path.GetTempPath(), $"brushscale_clip_{DateTime.Now:yyyyMMddHHmmssfff}.png");
        using var fs = File.OpenWrite(tmp);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        enc.Save(fs);

        InputPath      = tmp;
        ResultBitmap   = null;
        OriginalBitmap = src;
        StatusText = $"클립보드에서 로드됨  ({src.PixelWidth} × {src.PixelHeight})";
    }

    // ── 클립보드 복사 ─────────────────────────────────────────────────────
    public void CopyResultToClipboard()
    {
        if (_resultBitmap is null) return;
        try
        {
            System.Windows.Clipboard.SetImage(_resultBitmap);
            StatusText = "결과 이미지가 클립보드에 복사되었습니다";
        }
        catch (Exception ex)
        {
            StatusText = $"클립보드 복사 실패: {ex.Message}";
        }
    }

    // ── 업스케일 실행 ─────────────────────────────────────────────────────
    public async Task RunUpscaleAsync()
    {
        if (_inputPath is null || _selectedModel is null) return;
        if (!ModelManager.IsAvailable(_selectedModel.ModelType))
        {
            StatusText = $"모델 파일 없음 — 폴더를 열어 ONNX 파일을 배치하세요: {ModelManager.ModelDir}";
            return;
        }

        IsBusy   = true;
        Progress = 0;
        ResultBitmap = null;

        _cts = new CancellationTokenSource();
        try
        {
            StatusText = $"업스케일 중... ({_selectedModel.DisplayName})";
            using var src = UpscaleService.LoadBitmap(_inputPath);
            var prog  = new Progress<double>(v => { Progress = v * 100; });
            using var result = await _svc.UpscaleAsync(src, _selectedModel.ModelType, _scaleFactor, prog, _cts.Token);

            ResultBitmap = SkiaBitmapToWpf(result);
            StatusText = $"완료  {result.Width} × {result.Height}";
            Progress = 100;
        }
        catch (OperationCanceledException) { StatusText = "취소됨"; Progress = 0; }
        catch (Exception ex)              { StatusText = $"오류: {ex.Message}"; Progress = 0; }
        finally { IsBusy = false; }
    }

    // ── 저장 ──────────────────────────────────────────────────────────────
    public void SaveResultDirect(string outputPath)
    {
        if (_resultBitmap is null || _selectedFormat is null) return;
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(_resultBitmap));
        using var ms = new System.IO.MemoryStream();
        enc.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);
        using var skBmp = SKBitmap.Decode(ms);
        UpscaleService.SaveImage(skBmp, outputPath, _selectedFormat.Format, _jpegQuality);
        StatusText = $"저장됨: {Path.GetFileName(outputPath)}";
    }

    // ── 배치 ──────────────────────────────────────────────────────────────
    public async Task RunBatchAsync()
    {
        if (string.IsNullOrWhiteSpace(_batchInputDir) || string.IsNullOrWhiteSpace(_batchOutputDir)) return;
        if (_selectedModel is null || !ModelManager.IsAvailable(_selectedModel.ModelType))
        {
            StatusText = "모델 파일 없음";
            return;
        }

        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff", ".tif" };
        var searchOpt = _batchRecursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(_batchInputDir, "*.*", searchOpt)
                             .Where(f => exts.Contains(Path.GetExtension(f)))
                             .ToList();
        if (files.Count == 0) { StatusText = "처리할 이미지가 없습니다"; return; }

        var fmt = _selectedFormat?.Format ?? OutputFormat.Png;
        var jobs = files.Select(f =>
        {
            var name    = Path.GetFileNameWithoutExtension(f);
            var outName = _outputPattern
                .Replace("{name}",  name)
                .Replace("{scale}", _scaleFactor.ToString())
                + fmt.Extension();
            // 하위 폴더 구조 보존
            string outDir = _batchRecursive
                ? Path.Combine(_batchOutputDir, Path.GetRelativePath(_batchInputDir, Path.GetDirectoryName(f)!))
                : _batchOutputDir;
            return new UpscaleJob(f, Path.Combine(outDir, outName),
                                  _selectedModel.ModelType, _scaleFactor, fmt, _jpegQuality);
        }).ToList();

        IsBusy = true;
        Progress = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var prog = new Progress<(int done, int total, string file)>(t =>
            {
                Progress   = t.total > 0 ? (double)t.done / t.total * 100 : 0;
                StatusText = $"배치 처리 중 ({t.done}/{t.total}): {t.file}";
            });
            await _svc.BatchAsync(jobs, prog, _cts.Token);
            StatusText = $"배치 완료 — {jobs.Count}개 파일 처리";
            Progress = 100;
            BatchCompleted?.Invoke(jobs.Count);
        }
        catch (OperationCanceledException) { StatusText = "배치 취소됨"; Progress = 0; }
        catch (Exception ex)              { StatusText = $"배치 오류: {ex.Message}"; Progress = 0; }
        finally { IsBusy = false; }
    }

    // ── 모델 새로고침 ─────────────────────────────────────────────────────
    public void RefreshModelStatus()
    {
        foreach (var m in Models) m.Refresh();
        OnPropertyChanged(nameof(ModelNotAvailable));
    }

    // ── IDisposable ───────────────────────────────────────────────────────
    public void Dispose() => _svc.Dispose();

    // ── 헬퍼 ──────────────────────────────────────────────────────────────
    static BitmapSource? LoadBitmapSource(string path)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption  = BitmapCacheOption.OnLoad;
            bi.UriSource    = new Uri(path);
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }

    static BitmapSource SkiaBitmapToWpf(SKBitmap bmp)
    {
        using var image = SKImage.FromBitmap(bmp);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms    = new System.IO.MemoryStream(data.ToArray());
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption  = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── DTO ──────────────────────────────────────────────────────────────────────
public class ModelItem : INotifyPropertyChanged
{
    readonly UpscaleModelType _modelType;
    CancellationTokenSource?  _downloadCts;

    public ModelItem(UpscaleModelType modelType, bool available)
    {
        _modelType = modelType;
        _available = available;
        Info       = ModelRegistry.Get(modelType);
        DownloadCommand = new AsyncRelayCommand(
            async _ => await DownloadAsync(),
            _        => !IsDownloading && !Available && Info is not null);
        CancelDownloadCommand = new RelayCommand(
            _ => _downloadCts?.Cancel(),
            _ => IsDownloading);
    }

    public UpscaleModelType ModelType => _modelType;
    public ModelInfo?        Info     { get; }
    public bool              CanDownload        => Info is not null && !string.IsNullOrEmpty(Info.DownloadUrl) && !Available && !IsDownloading;
    public bool              ManualInstallNeeded => (Info is null || string.IsNullOrEmpty(Info.DownloadUrl)) && !Available;

    bool _available;
    public bool Available
    {
        get => _available;
        private set { _available = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(CanDownload)); }
    }

    public string DisplayName => Available
        ? _modelType.DisplayName()
        : _modelType.DisplayName() + "  [모델 없음]";

    public override string ToString() => DisplayName;

    public void Refresh() => Available = ModelManager.IsAvailable(_modelType);

    bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set { _isDownloading = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanDownload)); }
    }

    double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }

    string _downloadStatus = "";
    public string DownloadStatus
    {
        get => _downloadStatus;
        set { _downloadStatus = value; OnPropertyChanged(); }
    }

    public ICommand DownloadCommand       { get; }
    public ICommand CancelDownloadCommand { get; }

    async Task DownloadAsync()
    {
        if (Info is null) return;
        IsDownloading    = true;
        DownloadProgress = 0;
        DownloadStatus   = "연결 중...";
        _downloadCts     = new CancellationTokenSource();

        try
        {
            var prog = new Progress<(double ratio, long downloaded, long total)>(t =>
            {
                DownloadProgress = t.ratio >= 0 ? t.ratio * 100 : -1;
                DownloadStatus   = t.ratio >= 0
                    ? $"{ModelDownloadService.FormatBytes(t.downloaded)} / {ModelDownloadService.FormatBytes(t.total)}"
                    : ModelDownloadService.FormatBytes(t.downloaded);
            });
            await ModelDownloadService.DownloadAsync(Info, prog, _downloadCts.Token);
            Refresh();
            DownloadStatus   = "다운로드 완료";
            DownloadProgress = 100;
        }
        catch (OperationCanceledException)
        {
            DownloadStatus   = "취소됨";
            DownloadProgress = 0;
        }
        catch (Exception ex)
        {
            DownloadStatus   = $"실패: {ex.Message}";
            DownloadProgress = 0;
        }
        finally
        {
            IsDownloading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public record OutputFormatItem(OutputFormat Format)
{
    public string DisplayName => Format switch
    {
        OutputFormat.Jpg  => "JPEG",
        OutputFormat.WebP => "WebP",
        _                 => "PNG",
    };

    public override string ToString() => DisplayName;
}
