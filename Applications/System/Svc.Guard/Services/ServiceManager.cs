using System.Management;
using System.ServiceProcess;
using SvcGuard.Models;

namespace SvcGuard.Services;

public class ServiceManager
{
    // 시스템 필수 서비스 (비활성화 시 Windows 부팅 불가)
    private static readonly HashSet<string> CriticalServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "lsass", "services",
        "WinDefend", "EventLog", "PlugPlay", "CryptSvc", "Winmgmt",
        "TrkWks", "LanmanWorkstation", "NlaSvc", "Netlogon", "W32Time"
    };

    // 권장 서비스 (비활성화 시 일부 기능 저하)
    private static readonly HashSet<string> RecommendedServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "wuauserv", "BITS", "Spooler", "AudioSrv", "Audiosrv", "AudioEndpointBuilder",
        "WlanSvc", "Dhcp", "Dnscache", "BFE", "mpssvc", "SharedAccess",
        "UsoSvc", "WpnService", "WSearch", "SysMain", "Schedule", "VSS"
    };

    // 안전하게 비활성화 가능
    private static readonly HashSet<string> SafeToDisable = new(StringComparer.OrdinalIgnoreCase)
    {
        "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc",
        "DiagTrack", "dmwappushservice", "WerSvc", "RemoteRegistry",
        "Fax", "TapiSrv", "MapsBroker", "RetailDemo", "WbioSrvc",
        "TabletInputService", "lfsvc", "PhoneSvc", "OneSyncSvc",
        "WMPNetworkSvc", "icssvc", "WalletService", "MessagingService",
        "PimIndexMaintenanceSvc", "UnistoreSvc", "UserDataSvc"
    };

    public List<ServiceItem> GetAllServices()
    {
        var items = new List<ServiceItem>();
        var controllers = ServiceController.GetServices();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DisplayName, Description, StartName, StartMode FROM Win32_Service");
        var wmiMap = new Dictionary<string, ManagementObject>(StringComparer.OrdinalIgnoreCase);
        foreach (ManagementObject obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "";
            if (!string.IsNullOrEmpty(name))
                wmiMap[name] = obj;
        }

        foreach (var sc in controllers)
        {
            try
            {
                wmiMap.TryGetValue(sc.ServiceName, out var wmi);
                var item = new ServiceItem
                {
                    Name        = sc.ServiceName,
                    DisplayName = sc.DisplayName,
                    Description = wmi?["Description"]?.ToString() ?? "",
                    Account     = wmi?["StartName"]?.ToString() ?? "",
                    Status      = sc.Status.ToString(),
                    StartType   = wmi?["StartMode"]?.ToString() ?? sc.StartType.ToString(),
                    Category    = GetCategory(sc.ServiceName)
                };
                items.Add(item);
            }
            catch { /* 접근 불가 서비스 스킵 */ }
        }

        return items.OrderBy(s => s.DisplayName).ToList();
    }

    public void RefreshStatus(ServiceItem item)
    {
        try
        {
            using var sc = new ServiceController(item.Name);
            sc.Refresh();
            item.Status = sc.Status.ToString();
        }
        catch { }
    }

    public (bool success, string error) StartService(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool success, string error) StopService(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public (bool success, string error) RestartService(string serviceName)
    {
        var (stopped, err1) = StopService(serviceName);
        if (!stopped) return (false, err1);
        return StartService(serviceName);
    }

    public (bool success, string error) SetStartType(string serviceName, string mode)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT * FROM Win32_Service WHERE Name='{serviceName}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("ChangeStartMode", new object[] { mode });
            }
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public List<string> GetDependencies(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.DependentServices.Select(s => s.DisplayName).ToList();
        }
        catch { return new List<string>(); }
    }

    public List<string> GetDependsOn(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.ServicesDependedOn.Select(s => s.DisplayName).ToList();
        }
        catch { return new List<string>(); }
    }

    private ServiceCategory GetCategory(string name)
    {
        if (CriticalServices.Contains(name)) return ServiceCategory.SystemCritical;
        if (RecommendedServices.Contains(name)) return ServiceCategory.Recommended;
        if (SafeToDisable.Contains(name)) return ServiceCategory.SafeToDisable;
        return ServiceCategory.ThirdParty;
    }
}
