using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Brush.Scale.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    readonly UpscaleService _svc = new();

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
    public ObservableCollection<ModelItem> Models { get; } = [];

    ModelItem? _selectedModel;
    public ModelItem? SelectedModel
    {
        get => _selectedModel;
        set { _selectedModel = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModelNotAvailable)); }
    }

    public bool ModelNotAvailable =>
        _selectedModel is not null &&
        !ModelManager.IsAvailable(_selectedModel.ModelType);

    public ObservableCollection<int> ScaleFactors { get; } = [2, 4, 8];

    int _scaleFactor = 4;
    public int ScaleFactor
    {
        get => _scaleFactor;
        set { _scaleFactor = value; OnPropertyChanged(); }
    }

    public ObservableCollection<OutputFormatItem> OutputFormats { get; } = [];

    OutputFormatItem? _selectedFormat;
    public OutputFormatItem? SelectedFormat
    {
        get => _selectedFormat;
        set { _selectedFormat = value; OnPropertyChanged(); }
    }

    int _jpegQuality = 95;
    public int JpegQuality
    {
        get => _jpegQuality;
        set { _jpegQuality = value; OnPropertyChanged(); }
    }

    // ── 배치 ──────────────────────────────────────────────────────────────
    string _batchInputDir  = "";
    public string BatchInputDir
    {
        get => _batchInputDir;
        set { _batchInputDir = value; OnPropertyChanged(); }
    }

    string _batchOutputDir = "";
    public string BatchOutputDir
    {
        get => _batchOutputDir;
        set { _batchOutputDir = value; OnPropertyChanged(); }
    }

    string _outputPattern = "{name}_{scale}x";
    public string OutputPattern
    {
        get => _outputPattern;
        set { _outputPattern = value; OnPropertyChanged(); }
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

    // ── 커맨드 ────────────────────────────────────────────────────────────
    public ICommand CancelCommand { get; }

    public MainViewModel()
    {
        // 모델 목록
        foreach (var (t, avail) in ModelManager.GetModelStatus())
            Models.Add(new ModelItem(t, avail));
        _selectedModel = Models.FirstOrDefault();

        // 출력 형식
        foreach (var f in new[] { OutputFormat.Png, OutputFormat.Jpg, OutputFormat.WebP })
            OutputFormats.Add(new OutputFormatItem(f));
        _selectedFormat = OutputFormats[0];

        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsBusy);
    }

    // ── 이미지 로드 ───────────────────────────────────────────────────────
    public void LoadImage(string path)
    {
        InputPath = path;
        ResultBitmap = null;
        OriginalBitmap = LoadBitmapSource(path);
        StatusText = $"로드됨: {Path.GetFileName(path)}  ({OriginalBitmap!.PixelWidth} × {OriginalBitmap.PixelHeight})";
    }

    public void LoadFromClipboard()
    {
        if (!System.Windows.Clipboard.ContainsImage()) return;
        var src = System.Windows.Clipboard.GetImage();
        if (src is null) return;

        // 클립보드 이미지 → 임시파일 저장
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
    public void SaveResult(string outputPath)
    {
        if (_resultBitmap is null || _inputPath is null || _selectedFormat is null) return;
        using var src    = UpscaleService.LoadBitmap(_inputPath);
        using var result = UpscaleService.LoadBitmap(outputPath.Replace(
            _selectedFormat.Format.Extension(), ".tmp_placeholder"));

        // ResultBitmap → SKBitmap → save
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(_resultBitmap));
        using var ms = new System.IO.MemoryStream();
        enc.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);
        using var skBmp = SKBitmap.Decode(ms);
        UpscaleService.SaveImage(skBmp, outputPath, _selectedFormat.Format, _jpegQuality);
        StatusText = $"저장됨: {Path.GetFileName(outputPath)}";
    }

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

        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tiff", ".tif" };
        var files = Directory.GetFiles(_batchInputDir)
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
            return new UpscaleJob(f, Path.Combine(_batchOutputDir, outName),
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
        }
        catch (OperationCanceledException) { StatusText = "배치 취소됨"; Progress = 0; }
        catch (Exception ex)              { StatusText = $"배치 오류: {ex.Message}"; Progress = 0; }
        finally { IsBusy = false; }
    }

    // ── 모델 새로고침 ─────────────────────────────────────────────────────
    public void RefreshModelStatus()
    {
        foreach (var m in Models)
            m.Refresh();
        OnPropertyChanged(nameof(ModelNotAvailable));
    }

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

    public ModelItem(UpscaleModelType modelType, bool available)
    {
        _modelType = modelType;
        _available = available;
    }

    public UpscaleModelType ModelType => _modelType;

    bool _available;
    public bool Available
    {
        get => _available;
        private set { _available = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string DisplayName => Available
        ? _modelType.DisplayName()
        : _modelType.DisplayName() + "  [모델 없음]";

    public void Refresh()
    {
        Available = ModelManager.IsAvailable(_modelType);
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
}
