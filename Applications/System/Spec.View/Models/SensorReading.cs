namespace SpecView.Models;

public class SensorReading
{
    public string HardwareName { get; init; } = "";
    public string HardwareType { get; init; } = "";
    public string SensorName   { get; init; } = "";
    public string SensorType   { get; init; } = "";
    public float  Value        { get; init; }

    public string ValueDisplay => SensorType switch
    {
        "Temperature" => $"{Value:F1} °C",
        "Load"        => $"{Value:F1} %",
        "Clock"       => Value >= 1000 ? $"{Value / 1000.0:F2} GHz" : $"{Value:F0} MHz",
        "Voltage"     => $"{Value:F3} V",
        "Fan"         => $"{Value:F0} RPM",
        "Power"       => $"{Value:F1} W",
        "Data"        => $"{Value:F1} GB",
        "Throughput"  => $"{Value / 1024 / 1024:F1} MB/s",
        _             => $"{Value:F1}"
    };

    public string ValueColor => SensorType switch
    {
        "Temperature" => Value switch
        {
            >= 90 => "#EF4444",
            >= 70 => "#F59E0B",
            _     => "#10B981"
        },
        "Load" => Value switch
        {
            >= 90 => "#EF4444",
            >= 70 => "#F59E0B",
            _     => "#00C8E0"
        },
        _ => "#E2E8F0"
    };
}
