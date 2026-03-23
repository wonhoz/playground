using Microsoft.Win32;

namespace Net.Trace.ViewModels;

public class MainViewModel : BaseViewModel, IDisposable
{
    string _target = "8.8.8.8";
    bool _isRunning;
    string _status = "목적지 IP 또는 도메인을 입력하고 추적을 시작하세요.";

    public string Target    { get => _target; set { Set(ref _target, value); StartCmd.Raise(); } }
    public bool   IsRunning { get => _isRunning; private set { Set(ref _isRunning, value); StartCmd.Raise(); StopCmd.Raise(); } }
    public string Status    { get => _status; set => Set(ref _status, value); }

    public ObservableCollection<HopInfo> Hops { get; } = [];

    public RelayCommand StartCmd     { get; }
    public RelayCommand StopCmd      { get; }
    public RelayCommand ClearCmd     { get; }
    public RelayCommand ExportCsvCmd { get; }

    public event Action? MapRefreshRequested;

    readonly TracerouteService _tracer = new();
    readonly GeoIpService      _geo    = new();
    CancellationTokenSource?   _cts;

    public MainViewModel()
    {
        StartCmd     = new(DoStart, () => !IsRunning && !string.IsNullOrWhiteSpace(Target));
        StopCmd      = new(DoStop,  () => IsRunning);
        ClearCmd     = new(DoClear, () => !IsRunning);
        ExportCsvCmd = new(DoExportCsv, () => Hops.Count > 0);
    }

    async void DoStart()
    {
        Hops.Clear();
        IsRunning = true;
        _cts = new CancellationTokenSource();
        Status = $"추적 중: {Target.Trim()} ...";
        MapRefreshRequested?.Invoke();

        try
        {
            await foreach (var hop in _tracer.TraceAsync(Target.Trim(), ct: _cts.Token))
            {
                // GeoIP lookup for non-timeout, non-private hops
                if (!hop.IsTimeout && hop.Ip != null)
                {
                    var (country, city, lat, lon) = await _geo.LookupAsync(hop.Ip);
                    hop.Country   = country;
                    hop.City      = city;
                    hop.Latitude  = lat;
                    hop.Longitude = lon;
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    Hops.Add(hop);
                    Status = $"홉 {hop.HopNumber}: {hop.HostDisplay}";
                    MapRefreshRequested?.Invoke();
                    ExportCsvCmd.Raise();
                });
            }

            var reached = Hops.LastOrDefault(h => !h.IsTimeout);
            Status = $"추적 완료 — {Hops.Count}개 홉 | 목적지: {reached?.HostDisplay ?? Target}";
        }
        catch (OperationCanceledException)
        {
            Status = "추적 취소됨";
        }
        catch (Exception ex)
        {
            Status = $"오류: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    void DoStop()  => _cts?.Cancel();
    void DoClear() { Hops.Clear(); Status = "초기화됨"; MapRefreshRequested?.Invoke(); ExportCsvCmd.Raise(); }

    void DoExportCsv()
    {
        var dlg = new SaveFileDialog { Title = "CSV 내보내기", Filter = "CSV 파일|*.csv", FileName = $"trace_{Target.Trim()}" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var lines = new List<string> { "홉#,IP,호스트명,국가,도시,위도,경도,RTT평균(ms),RTT최소,RTT최대,손실%" };
            foreach (var h in Hops)
                lines.Add($"{h.HopNumber},{h.Ip ?? "*"},{h.Hostname ?? ""},{h.Country ?? ""},{h.City ?? ""}," +
                          $"{h.Latitude?.ToString("F4") ?? ""},{h.Longitude?.ToString("F4") ?? ""}," +
                          $"{h.RttAvg?.ToString("F1") ?? ""},{h.RttMin?.ToString("F1") ?? ""},{h.RttMax?.ToString("F1") ?? ""},{h.LossText}");
            File.WriteAllLines(dlg.FileName, lines, System.Text.Encoding.UTF8);
            Status = $"CSV 저장 완료: {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    public void Dispose() { _cts?.Dispose(); _geo.Dispose(); }
}
