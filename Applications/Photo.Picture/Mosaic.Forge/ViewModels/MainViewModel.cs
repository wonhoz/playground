using System.ComponentModel;

namespace Mosaic.Forge.ViewModels;

sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── 설정 ─────────────────────────────────────────────────────────────────

    int _tileSize = 32;
    public int TileSize
    {
        get => _tileSize;
        set { SetField(ref _tileSize, Math.Clamp(value, 8, 128)); }
    }

    int _maxReuse = 5;
    public int MaxReuse
    {
        get => _maxReuse;
        set { SetField(ref _maxReuse, Math.Clamp(value, 0, 50)); }
    }

    // ── 경로 / 상태 ──────────────────────────────────────────────────────────

    string? _targetPath;
    public string? TargetPath
    {
        get => _targetPath;
        set { SetField(ref _targetPath, value); OnPropertyChanged(nameof(CanGenerate)); }
    }

    string? _sourceFolderPath;
    public string? SourceFolderPath
    {
        get => _sourceFolderPath;
        set { SetField(ref _sourceFolderPath, value); }
    }

    int _sourceTileCount;
    public int SourceTileCount
    {
        get => _sourceTileCount;
        set
        {
            SetField(ref _sourceTileCount, value);
            OnPropertyChanged(nameof(CanGenerate));
            OnPropertyChanged(nameof(SourceCountText));
        }
    }

    public string SourceCountText =>
        _sourceTileCount == 0 ? "소스 이미지 없음" : $"{_sourceTileCount:N0}개 이미지 발견";

    // ── 진행 상태 ─────────────────────────────────────────────────────────────

    bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { SetField(ref _isScanning, value); OnPropertyChanged(nameof(CanGenerate)); OnPropertyChanged(nameof(IsBusy)); }
    }

    bool _isGenerating;
    public bool IsGenerating
    {
        get => _isGenerating;
        set { SetField(ref _isGenerating, value); OnPropertyChanged(nameof(CanGenerate)); OnPropertyChanged(nameof(IsBusy)); }
    }

    public bool IsBusy => _isScanning || _isGenerating;

    bool _hasResult;
    public bool HasResult
    {
        get => _hasResult;
        set { SetField(ref _hasResult, value); }
    }

    string _statusText = "대상 이미지와 소스 폴더를 선택하세요.";
    public string StatusText
    {
        get => _statusText;
        set { SetField(ref _statusText, value); }
    }

    double _progress;
    public double Progress
    {
        get => _progress;
        set { SetField(ref _progress, value); }
    }

    double _progressMax = 100;
    public double ProgressMax
    {
        get => _progressMax;
        set { SetField(ref _progressMax, value); }
    }

    string _targetSizeText = "";
    public string TargetSizeText
    {
        get => _targetSizeText;
        set { SetField(ref _targetSizeText, value); }
    }

    string _outputSizeText = "";
    public string OutputSizeText
    {
        get => _outputSizeText;
        set { SetField(ref _outputSizeText, value); }
    }

    public bool CanGenerate =>
        !IsBusy && TargetPath != null && SourceTileCount > 0;

    public void UpdateOutputSize(int targetW, int targetH)
    {
        int gW = Math.Max(1, targetW  / TileSize);
        int gH = Math.Max(1, targetH / TileSize);
        OutputSizeText = $"출력: {gW * TileSize} × {gH * TileSize}px ({gW * gH:N0} 타일)";
    }
}
