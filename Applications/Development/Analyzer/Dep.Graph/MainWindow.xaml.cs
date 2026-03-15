using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace DepGraph;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private List<PackageItem> _packages = [];
    private string _currentEcosystem = "nuget";
    private string _htmlContent = "";

    public MainWindow()
    {
        InitializeComponent();
        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _htmlContent = LoadGraphHtml();
        _ = InitWebViewAsync();
    }

    private string LoadGraphHtml()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("DepGraph.Resources.graph.html")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task InitWebViewAsync()
    {
        await WebGraph.EnsureCoreWebView2Async();
        WebGraph.CoreWebView2.WebMessageReceived += WebView_MessageReceived;
        WebGraph.NavigateToString(_htmlContent);
    }

    private void WebView_MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(msg)) return;
        try
        {
            var json = JsonNode.Parse(msg);
            var action = json?["action"]?.GetValue<string>();
            if (action == "select")
            {
                var id = json?["id"]?.GetValue<string>() ?? "";
                var ver = json?["version"]?.GetValue<string>() ?? "";
                Dispatcher.Invoke(() => StatusBar.Text = $"선택: {id} {ver}");
            }
        }
        catch { }
    }

    private void CmbEcosystem_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _currentEcosystem = CmbEcosystem.SelectedIndex switch
        {
            0 => "nuget",
            1 => "npm",
            2 => "pip",
            _ => "nuget"
        };
    }

    private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = _currentEcosystem switch
            {
                "npm" => "package.json|package.json|모든 파일|*.*",
                "pip" => "requirements.txt|requirements.txt|*.txt|*.txt|모든 파일|*.*",
                _ => ".csproj 파일|*.csproj|packages.config|packages.config|*.sln|*.sln|모든 파일|*.*"
            }
        };
        if (dlg.ShowDialog() == true)
        {
            TxtFilePath.Text = dlg.FileName;
            LoadPackagesFromFile(dlg.FileName);
        }
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "의존성 파일이 있는 폴더를 선택하세요",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtFilePath.Text = dlg.SelectedPath;
            ScanFolder(dlg.SelectedPath);
        }
    }

    private void LoadPackagesFromFile(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLower();
            var fileName = Path.GetFileName(filePath).ToLower();

            _packages = _currentEcosystem switch
            {
                "nuget" => ParseNuGet(filePath),
                "npm" => ParseNpm(filePath),
                "pip" => ParsePip(filePath),
                _ => []
            };
            RefreshPackageList();
            SetStatus($"{_packages.Count}개 패키지 로드 완료 — {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            SetStatus($"오류: {ex.Message}");
        }
    }

    private void ScanFolder(string folder)
    {
        var files = _currentEcosystem switch
        {
            "nuget" => Directory.GetFiles(folder, "*.csproj", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(folder, "packages.config", SearchOption.AllDirectories)),
            "npm" => Directory.GetFiles(folder, "package.json", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("node_modules")),
            "pip" => Directory.GetFiles(folder, "requirements*.txt", SearchOption.AllDirectories),
            _ => []
        };

        _packages.Clear();
        foreach (var f in files)
        {
            var parsed = _currentEcosystem switch
            {
                "nuget" => ParseNuGet(f),
                "npm" => ParseNpm(f),
                "pip" => ParsePip(f),
                _ => []
            };
            foreach (var p in parsed)
            {
                if (!_packages.Any(x => x.Id == p.Id && x.Version == p.Version))
                    _packages.Add(p);
            }
        }
        RefreshPackageList();
        SetStatus($"{_packages.Count}개 패키지 발견 (폴더 스캔)");
    }

    // ── NuGet 파서 ──────────────────────────────────────────────
    private static List<PackageItem> ParseNuGet(string filePath)
    {
        var result = new List<PackageItem>();
        var content = File.ReadAllText(filePath);

        if (filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            // PackageReference
            var matches = Regex.Matches(content,
                @"<PackageReference\s+Include=""([^""]+)""\s+Version=""([^""]+)""",
                RegexOptions.IgnoreCase);
            foreach (Match m in matches)
                result.Add(new PackageItem(m.Groups[1].Value, m.Groups[2].Value, "nuget", "direct"));

            // Version 속성 따로 있는 경우
            var matches2 = Regex.Matches(content,
                @"<PackageReference\s+Include=""([^""]+)""[^>]*>\s*<Version>([^<]+)</Version>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in matches2)
                if (!result.Any(r => r.Id == m.Groups[1].Value))
                    result.Add(new PackageItem(m.Groups[1].Value, m.Groups[2].Value.Trim(), "nuget", "direct"));
        }
        else // packages.config
        {
            var matches = Regex.Matches(content,
                @"<package\s+id=""([^""]+)""\s+version=""([^""]+)""",
                RegexOptions.IgnoreCase);
            foreach (Match m in matches)
                result.Add(new PackageItem(m.Groups[1].Value, m.Groups[2].Value, "nuget", "direct"));
        }
        return result;
    }

    // ── npm 파서 ──────────────────────────────────────────────
    private static List<PackageItem> ParseNpm(string filePath)
    {
        var result = new List<PackageItem>();
        try
        {
            var json = JsonNode.Parse(File.ReadAllText(filePath));
            ParseNpmDeps(json?["dependencies"], result, "direct");
            ParseNpmDeps(json?["devDependencies"], result, "dev");
            ParseNpmDeps(json?["peerDependencies"], result, "peer");
        }
        catch { }
        return result;
    }

    private static void ParseNpmDeps(JsonNode? deps, List<PackageItem> result, string depType)
    {
        if (deps is not JsonObject obj) return;
        foreach (var kv in obj)
        {
            var ver = kv.Value?.GetValue<string>() ?? "";
            ver = Regex.Replace(ver, @"^[\^~>=<]", "").Trim();
            result.Add(new PackageItem(kv.Key, ver, "npm", depType));
        }
    }

    // ── pip 파서 ──────────────────────────────────────────────
    private static List<PackageItem> ParsePip(string filePath)
    {
        var result = new List<PackageItem>();
        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('-'))
                continue;
            var match = Regex.Match(trimmed, @"^([A-Za-z0-9_\-\.]+)\s*(?:[=><!\^~]+\s*([^\s;#]+))?");
            if (match.Success)
                result.Add(new PackageItem(match.Groups[1].Value, match.Groups[2].Value, "pip", "direct"));
        }
        return result;
    }

    private void RefreshPackageList()
    {
        var conflicts = FindConflicts();
        foreach (var p in _packages)
            p.HasConflict = conflicts.Contains(p.Id);

        LstPackages.ItemsSource = null;
        LstPackages.ItemsSource = _packages;
        LblPkgCount.Text = $"패키지 {_packages.Count}개";
    }

    private HashSet<string> FindConflicts()
    {
        // 같은 ID에 서로 다른 버전이 있으면 충돌
        return _packages.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Version).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void LstPackages_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LstPackages.SelectedItem is PackageItem pkg)
            SetStatus($"{pkg.Id} {pkg.Version} — {pkg.Ecosystem} ({pkg.DepType})");
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_packages.Count == 0)
        {
            SetStatus("먼저 파일을 선택하세요.");
            return;
        }
        PanelEmpty.Visibility = Visibility.Collapsed;
        var graphJson = BuildGraphJson();
        var script = $"window.setGraphData({graphJson});";
        WebGraph.CoreWebView2?.ExecuteScriptAsync(script);
        SetStatus($"그래프 렌더링 완료 — {_packages.Count}개 노드");
    }

    private string BuildGraphJson()
    {
        var conflicts = FindConflicts();
        var nodes = new List<object>();
        var links = new List<object>();

        // 루트 노드 (프로젝트)
        var projectName = Path.GetFileNameWithoutExtension(TxtFilePath.Text);
        if (string.IsNullOrEmpty(projectName)) projectName = "Project";
        nodes.Add(new { id = projectName, label = projectName, type = "root", version = "", conflict = false });

        foreach (var p in _packages)
        {
            bool isConflict = conflicts.Contains(p.Id);
            nodes.Add(new
            {
                id = p.Id,
                label = p.Id.Length > 18 ? p.Id[..18] + "…" : p.Id,
                type = p.DepType == "direct" ? "direct" : "indirect",
                version = p.Version,
                latestVersion = p.LatestVersion,
                conflict = isConflict,
                deprecated = p.IsOutdated
            });
            links.Add(new
            {
                source = projectName,
                target = p.Id,
                conflict = isConflict,
                outdated = p.IsOutdated
            });
        }

        return JsonSerializer.Serialize(new { nodes, links });
    }

    private async void BtnCheckLatest_Click(object sender, RoutedEventArgs e)
    {
        if (_packages.Count == 0) return;
        SetStatus("NuGet 최신 버전 확인 중...");
        BtnCheckLatest.IsEnabled = false;
        try
        {
            if (_currentEcosystem == "nuget")
                await CheckNuGetLatestAsync();
            RefreshPackageList();
            SetStatus("최신 버전 확인 완료");
        }
        catch (Exception ex)
        {
            SetStatus($"최신 버전 확인 실패: {ex.Message}");
        }
        finally
        {
            BtnCheckLatest.IsEnabled = true;
        }
    }

    private async Task CheckNuGetLatestAsync()
    {
        var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
        var cache = new SourceCacheContext();
        var semaphore = new SemaphoreSlim(5, 5);

        var tasks = _packages.Select(async p =>
        {
            await semaphore.WaitAsync();
            try
            {
                var versions = await resource.GetAllVersionsAsync(p.Id, cache, NuGet.Common.NullLogger.Instance, CancellationToken.None);
                var latest = versions?.Where(v => !v.IsPrerelease).OrderByDescending(v => v).FirstOrDefault();
                if (latest != null)
                {
                    p.LatestVersion = latest.ToString();
                    p.IsOutdated = !string.Equals(p.Version.TrimStart('v'), latest.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_packages.Count == 0) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON 파일|*.json|CSV 파일|*.csv",
            FileName = "dep-graph-export"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileName.EndsWith(".csv"))
            {
                var sb = new StringBuilder("ID,Version,LatestVersion,Ecosystem,DepType,HasConflict,IsOutdated\n");
                foreach (var p in _packages)
                    sb.AppendLine($"{p.Id},{p.Version},{p.LatestVersion},{p.Ecosystem},{p.DepType},{p.HasConflict},{p.IsOutdated}");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            }
            else
            {
                var json = JsonSerializer.Serialize(_packages, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json, Encoding.UTF8);
            }
            SetStatus($"내보내기 완료: {dlg.FileName}");
        }
        catch (Exception ex)
        {
            SetStatus($"내보내기 실패: {ex.Message}");
        }
    }

    private void SetStatus(string msg) => StatusBar.Text = msg;
}

// ── 데이터 모델 ──────────────────────────────────────────────
public class PackageItem(string id, string version, string ecosystem, string depType)
    : System.ComponentModel.INotifyPropertyChanged
{
    public string Id { get; } = id;
    public string Version { get; } = version;
    public string Ecosystem { get; } = ecosystem;
    public string DepType { get; } = depType;

    private string _latestVersion = "";
    public string LatestVersion
    {
        get => _latestVersion;
        set { _latestVersion = value; OnPropertyChanged(nameof(LatestVersion)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
    }

    private bool _hasConflict;
    public bool HasConflict
    {
        get => _hasConflict;
        set { _hasConflict = value; OnPropertyChanged(nameof(HasConflict)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
    }

    private bool _isOutdated;
    public bool IsOutdated
    {
        get => _isOutdated;
        set { _isOutdated = value; OnPropertyChanged(nameof(IsOutdated)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
    }

    public string DisplayName => $"{Id} {Version}";
    public string StatusText => HasConflict ? "⚠ 버전 충돌" : IsOutdated ? $"→ {LatestVersion}" : LatestVersion != "" ? "✓ 최신" : "";
    public string StatusColor => HasConflict ? "#FF6666" : IsOutdated ? "#FFAA00" : "#66CC88";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new(name));
}
