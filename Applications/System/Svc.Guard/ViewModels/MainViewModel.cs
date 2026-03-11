using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using SvcGuard.Models;
using SvcGuard.Services;

namespace SvcGuard.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ServiceManager _svc = new();
    private string _searchText = "";
    private string _filterCategory = "전체";
    private string _filterStatus = "전체";
    private ServiceItem? _selectedService;
    private bool _isLoaded;
    private bool _isBusy;
    private string _statusText = "서비스 목록을 불러오는 중...";

    public ObservableCollection<ServiceItem> AllServices { get; } = new();
    public ObservableCollection<ServiceItem> FilteredServices { get; } = new();
    public ObservableCollection<string> DependsOnList { get; } = new();
    public ObservableCollection<string> DependentList { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string FilterCategory
    {
        get => _filterCategory;
        set { _filterCategory = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public string FilterStatus
    {
        get => _filterStatus;
        set { _filterStatus = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public ServiceItem? SelectedService
    {
        get => _selectedService;
        set
        {
            _selectedService = value;
            OnPropertyChanged();
            LoadDependencies();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public List<string> CategoryFilters { get; } = new()
    {
        "전체", "시스템 필수", "권장", "비활성화 가능", "서드파티"
    };

    public List<string> StatusFilters { get; } = new()
    {
        "전체", "실행 중", "중지됨"
    };

    public async Task LoadServicesAsync()
    {
        IsBusy = true;
        StatusText = "서비스 목록 로드 중...";
        try
        {
            var services = await Task.Run(() => _svc.GetAllServices());
            AllServices.Clear();
            foreach (var s in services) AllServices.Add(s);
            ApplyFilter();
            _isLoaded = true;
            StatusText = $"총 {AllServices.Count}개 서비스";
        }
        catch (Exception ex)
        {
            StatusText = $"오류: {ex.Message}";
            MessageBox.Show($"서비스 목록 로드 중 오류가 발생했습니다.\n{ex.Message}",
                "Svc.Guard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    public async Task StartAsync(ServiceItem item)
    {
        IsBusy = true;
        StatusText = $"{item.DisplayName} 시작 중...";
        var (ok, err) = await Task.Run(() => _svc.StartService(item.Name));
        if (!ok) MessageBox.Show($"시작 실패: {err}", "Svc.Guard", MessageBoxButton.OK, MessageBoxImage.Warning);
        await Task.Run(() => _svc.RefreshStatus(item));
        StatusText = ok ? $"{item.DisplayName} 시작됨" : $"시작 실패: {err}";
        IsBusy = false;
    }

    public async Task StopAsync(ServiceItem item)
    {
        if (item.Category == ServiceCategory.SystemCritical)
        {
            if (MessageBox.Show($"'{item.DisplayName}'은 시스템 필수 서비스입니다.\n정말 중지하시겠습니까?",
                "경고", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }
        IsBusy = true;
        StatusText = $"{item.DisplayName} 중지 중...";
        var (ok, err) = await Task.Run(() => _svc.StopService(item.Name));
        if (!ok) MessageBox.Show($"중지 실패: {err}", "Svc.Guard", MessageBoxButton.OK, MessageBoxImage.Warning);
        await Task.Run(() => _svc.RefreshStatus(item));
        StatusText = ok ? $"{item.DisplayName} 중지됨" : $"중지 실패: {err}";
        IsBusy = false;
    }

    public async Task RestartAsync(ServiceItem item)
    {
        IsBusy = true;
        StatusText = $"{item.DisplayName} 재시작 중...";
        var (ok, err) = await Task.Run(() => _svc.RestartService(item.Name));
        if (!ok) MessageBox.Show($"재시작 실패: {err}", "Svc.Guard", MessageBoxButton.OK, MessageBoxImage.Warning);
        await Task.Run(() => _svc.RefreshStatus(item));
        StatusText = ok ? $"{item.DisplayName} 재시작됨" : $"재시작 실패: {err}";
        IsBusy = false;
    }

    public async Task SetStartTypeAsync(ServiceItem item, string mode)
    {
        IsBusy = true;
        var (ok, err) = await Task.Run(() => _svc.SetStartType(item.Name, mode));
        if (!ok) MessageBox.Show($"시작 유형 변경 실패: {err}", "Svc.Guard", MessageBoxButton.OK, MessageBoxImage.Warning);
        else item.StartType = mode;
        StatusText = ok ? $"{item.DisplayName} 시작 유형: {mode}" : $"변경 실패: {err}";
        IsBusy = false;
    }

    public async Task ApplyGamingPresetAsync()
    {
        var gamingDisable = new[] { "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc",
            "DiagTrack", "WSearch", "SysMain", "MapsBroker", "RetailDemo", "WerSvc",
            "WMPNetworkSvc", "TabletInputService", "PhoneSvc" };

        IsBusy = true;
        StatusText = "게이밍 모드 적용 중...";
        int count = 0;
        foreach (var name in gamingDisable)
        {
            var item = AllServices.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (item?.IsRunning == true)
            {
                _svc.StopService(name);
                _svc.RefreshStatus(item);
                count++;
            }
        }
        await Task.Delay(500);
        StatusText = $"게이밍 모드 적용 완료 ({count}개 서비스 중지)";
        IsBusy = false;
        ApplyFilter();
    }

    public async Task ApplyDevPresetAsync()
    {
        var devEnable = new[] { "WSearch", "W3SVC", "MSSQLSERVER", "SQLWriter", "WAS" };

        IsBusy = true;
        StatusText = "개발 모드 적용 중...";
        int count = 0;
        foreach (var name in devEnable)
        {
            var item = AllServices.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (item != null && !item.IsRunning)
            {
                _svc.StartService(name);
                _svc.RefreshStatus(item);
                count++;
            }
        }
        await Task.Delay(500);
        StatusText = $"개발 모드 적용 완료 ({count}개 서비스 시작)";
        IsBusy = false;
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        if (!_isLoaded) return;

        var query = AllServices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var kw = _searchText.Trim();
            query = query.Where(s =>
                s.DisplayName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        if (_filterCategory != "전체")
        {
            query = _filterCategory switch
            {
                "시스템 필수"     => query.Where(s => s.Category == ServiceCategory.SystemCritical),
                "권장"           => query.Where(s => s.Category == ServiceCategory.Recommended),
                "비활성화 가능"   => query.Where(s => s.Category == ServiceCategory.SafeToDisable),
                "서드파티"        => query.Where(s => s.Category == ServiceCategory.ThirdParty),
                _                => query
            };
        }

        if (_filterStatus != "전체")
        {
            query = _filterStatus switch
            {
                "실행 중" => query.Where(s => s.IsRunning),
                "중지됨"  => query.Where(s => !s.IsRunning),
                _         => query
            };
        }

        FilteredServices.Clear();
        foreach (var s in query) FilteredServices.Add(s);
        StatusText = $"{FilteredServices.Count} / {AllServices.Count}개 서비스";
    }

    private void LoadDependencies()
    {
        DependsOnList.Clear();
        DependentList.Clear();
        if (_selectedService == null) return;
        foreach (var d in _svc.GetDependsOn(_selectedService.Name)) DependsOnList.Add(d);
        foreach (var d in _svc.GetDependencies(_selectedService.Name)) DependentList.Add(d);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
