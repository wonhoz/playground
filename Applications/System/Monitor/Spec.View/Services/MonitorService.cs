using LibreHardwareMonitor.Hardware;

namespace SpecView.Services;

public class MonitorService : IDisposable
{
    private Computer? _computer;
    private bool      _disposed;
    public bool       IsAvailable { get; private set; }

    public void Initialize()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled         = true,
                IsGpuEnabled         = true,
                IsMemoryEnabled      = true,
                IsStorageEnabled     = true,
                IsMotherboardEnabled = true
            };
            _computer.Open();
            IsAvailable = true;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public List<SensorReading> GetReadings()
    {
        if (_computer is null || !IsAvailable) return [];

        var readings = new List<SensorReading>();
        try
        {
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                AddReadings(readings, hw);
                foreach (var sub in hw.SubHardware)
                {
                    sub.Update();
                    AddReadings(readings, sub);
                }
            }
        }
        catch { }
        return readings;
    }

    private static void AddReadings(List<SensorReading> list, IHardware hw)
    {
        // 표시할 센서 타입만 필터링
        var showTypes = new HashSet<string>
        {
            "Temperature", "Load", "Clock", "Fan", "Power", "Voltage"
        };

        foreach (var sensor in hw.Sensors)
        {
            if (!showTypes.Contains(sensor.SensorType.ToString())) continue;
            if (sensor.Value is null) continue;

            list.Add(new SensorReading
            {
                HardwareName = hw.Name,
                HardwareType = hw.HardwareType.ToString(),
                SensorName   = sensor.Name,
                SensorType   = sensor.SensorType.ToString(),
                Value        = sensor.Value.Value
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _computer?.Close(); } catch { }
    }
}
