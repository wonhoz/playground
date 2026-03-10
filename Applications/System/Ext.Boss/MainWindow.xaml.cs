using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ExtBoss;

// ── 모델 ──────────────────────────────────────────────────────────────
public enum AssocStatus { OK, Broken, NotSet }

public class ExtEntry
{
    public string Extension { get; set; } = "";
    public string ProgId    { get; set; } = "";
    public string AppName   { get; set; } = "";
    public string AppPath   { get; set; } = "";
    public string Source    { get; set; } = "None";  // UserChoice | HKCR | None
    public AssocStatus Status { get; set; } = AssocStatus.NotSet;

    public string StatusText => Status switch
    {
        AssocStatus.OK     => "OK",
        AssocStatus.Broken => "Broken",
        _                  => "None"
    };

    public Brush StatusColor => Status switch
    {
        AssocStatus.OK     => new SolidColorBrush(Color.FromRgb(0x50, 0xE0, 0x80)),
        AssocStatus.Broken => new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55)),
        _                  => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
    };

    public Brush SourceBg => Source switch
    {
        "UserChoice" => new SolidColorBrush(Color.FromRgb(0x2A, 0x22, 0x00)),
        "HKCR"       => new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x3A)),
        _            => new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))
    };

    public Brush SourceFg => Source switch
    {
        "UserChoice" => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x40)),
        "HKCR"       => new SolidColorBrush(Color.FromRgb(0x70, 0xA0, 0xFF)),
        _            => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
    };

}

// JSON 직렬화용 DTO
public record AssocDto(string Extension, string ProgId, string AppName, string AppPath, string Source);

// ── MainWindow ────────────────────────────────────────────────────────
public partial class MainWindow : Window
{
    private List<ExtEntry> _all  = [];
    private ObservableCollection<ExtEntry> _view = [];
    private bool _loaded = false;

    public MainWindow()
    {
        InitializeComponent();
        Grid.ItemsSource = _view;
        Loaded += async (_, _) =>
        {
            _loaded = true;
            ShowAdminBadge();
            await LoadAsync();
        };
    }

    // ── 관리자 권한 표시 ──────────────────────────────
    private void ShowAdminBadge()
    {
        bool isAdmin = IsAdmin();
        AdminBadge.Visibility   = isAdmin ? Visibility.Collapsed : Visibility.Visible;
        AdminOkBadge.Visibility = isAdmin ? Visibility.Visible   : Visibility.Collapsed;
    }

    private static bool IsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    // ── 비동기 로딩 ───────────────────────────────────
    private async Task LoadAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        BtnRefresh.IsEnabled = false;

        var progress = new Progress<string>(msg => TxtLoadCount.Text = msg);
        _all = await Task.Run(() => ReadRegistry(progress));

        ApplyFilter();
        UpdateStatusBar();

        LoadingOverlay.Visibility = Visibility.Collapsed;
        BtnRefresh.IsEnabled = true;
    }

    // ── 레지스트리 읽기 ───────────────────────────────
    private static List<ExtEntry> ReadRegistry(IProgress<string> progress)
    {
        var result = new List<ExtEntry>();

        string[] names;
        try { names = Registry.ClassesRoot.GetSubKeyNames(); }
        catch { return result; }

        int idx = 0;
        foreach (var name in names)
        {
            if (!name.StartsWith('.')) continue;
            idx++;
            if (idx % 50 == 0)
                progress.Report($"{idx}개 처리 중...");

            var entry = BuildEntry(name);
            result.Add(entry);
        }

        return result;
    }

    private static ExtEntry BuildEntry(string ext)
    {
        // 1. UserChoice 우선 확인 (HKCU)
        string? progId = null;
        string source = "None";

        try
        {
            using var uc = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
            progId = uc?.GetValue("Progid") as string;
            if (progId != null) source = "UserChoice";
        }
        catch { /* 접근 불가 무시 */ }

        // 2. HKCR fallback
        if (progId == null)
        {
            try
            {
                using var extKey = Registry.ClassesRoot.OpenSubKey(ext);
                progId = extKey?.GetValue("") as string;
                if (progId != null) source = "HKCR";
            }
            catch { /* 접근 불가 무시 */ }
        }

        // 3. ProgID → 앱 정보 해석
        string appName = "", appPath = "";
        if (progId != null)
        {
            try
            {
                using var progKey = Registry.ClassesRoot.OpenSubKey(progId);
                appName = (progKey?.GetValue("") as string) ?? "";

                // shell\open\command
                string? cmdLine = null;
                foreach (var shell in new[] { "shell\\open\\command", "shell\\Open\\command" })
                {
                    using var cmdKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\{shell}");
                    cmdLine = cmdKey?.GetValue("") as string;
                    if (cmdLine != null) break;
                }

                if (cmdLine != null)
                {
                    var parsed = ParseExePath(cmdLine);
                    if (parsed != null)
                        appPath = Environment.ExpandEnvironmentVariables(parsed);
                }

                // 앱 이름이 비어있으면 실행파일명으로 대체
                if (string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(appPath))
                    appName = Path.GetFileNameWithoutExtension(appPath);
            }
            catch { /* 접근 불가 무시 */ }
        }

        // 4. 상태 판단
        var status = progId == null ? AssocStatus.NotSet
            : (!string.IsNullOrEmpty(appPath) && File.Exists(appPath)) ? AssocStatus.OK
            : AssocStatus.Broken;

        return new ExtEntry
        {
            Extension = ext,
            ProgId    = progId ?? "",
            AppName   = appName,
            AppPath   = appPath,
            Source    = source,
            Status    = status
        };
    }

    private static string? ParseExePath(string cmdLine)
    {
        cmdLine = cmdLine.Trim();
        if (cmdLine.StartsWith('"'))
        {
            int end = cmdLine.IndexOf('"', 1);
            return end > 0 ? cmdLine[1..end] : null;
        }
        // 따옴표 없음 → 첫 공백까지
        int space = cmdLine.IndexOf(' ');
        return space > 0 ? cmdLine[..space] : cmdLine;
    }

    // ── 필터링 ────────────────────────────────────────
    private void ApplyFilter()
    {
        if (!_loaded) return;

        var search      = TxtSearch.Text.Trim().ToLower();
        var statusTag   = (CmbStatus.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "All";
        bool brokenOnly = ChkBroken.IsChecked == true;

        var filtered = _all.Where(e =>
        {
            if (brokenOnly && e.Status != AssocStatus.Broken) return false;
            if (statusTag == "OK"     && e.Status != AssocStatus.OK)     return false;
            if (statusTag == "Broken" && e.Status != AssocStatus.Broken) return false;
            if (statusTag == "NotSet" && e.Status != AssocStatus.NotSet) return false;

            if (!string.IsNullOrEmpty(search))
                return e.Extension.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || e.AppName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || e.ProgId.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || e.AppPath.Contains(search, StringComparison.OrdinalIgnoreCase);

            return true;
        }).ToList();

        _view.Clear();
        foreach (var e in filtered) _view.Add(e);

        TxtFiltered.Text = filtered.Count < _all.Count
            ? $"필터: {filtered.Count}개 표시"
            : "";
    }

    private void UpdateStatusBar()
    {
        int total   = _all.Count;
        int ok      = _all.Count(e => e.Status == AssocStatus.OK);
        int broken  = _all.Count(e => e.Status == AssocStatus.Broken);
        int notSet  = _all.Count(e => e.Status == AssocStatus.NotSet);

        TxtTotal.Text  = $"전체 {total}개";
        TxtOkCount.Text= $"✅ OK {ok}개";
        TxtBroken.Text = $"❌ Broken {broken}개";
        TxtNotSet.Text = $"➖ 미설정 {notSet}개";
    }

    // ── 선택된 항목 ───────────────────────────────────
    private ExtEntry? Selected => Grid.SelectedItem as ExtEntry;

    private List<ExtEntry> SelectedItems =>
        Grid.SelectedItems.Cast<ExtEntry>().ToList();

    // ── 이벤트 핸들러 ─────────────────────────────────
    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter();

    private void CmbStatus_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => ApplyFilter();

    private void ChkBroken_Changed(object sender, RoutedEventArgs e)
        => ApplyFilter();

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        => await LoadAsync();

    private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => BtnChangeApp_Click(sender, e);

    // ── 앱 변경 ───────────────────────────────────────
    private void BtnChangeApp_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel == null)
        {
            ShowInfo("확장자를 선택하세요.");
            return;
        }

        // 임시 파일 생성
        string tmp = Path.Combine(Path.GetTempPath(), $"extboss_preview{sel.Extension}");
        try { File.WriteAllText(tmp, ""); } catch { }

        // openwith.exe 호출
        string openwith = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "openwith.exe");

        if (File.Exists(openwith))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName  = openwith,
                Arguments = $"\"{tmp}\"",
                UseShellExecute = true
            });
        }
        else
        {
            // 폴백: 직접 파일 실행 (Open With 메뉴 선택 가능)
            Process.Start(new ProcessStartInfo
            {
                FileName  = tmp,
                Verb      = "openas",
                UseShellExecute = true
            });
        }
    }

    // ── Windows 기본 앱 설정 ─────────────────────────
    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "ms-settings:defaultapps",
            UseShellExecute = true
        });
    }

    // ── 경로 복사 ─────────────────────────────────────
    private void BtnCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel == null || string.IsNullOrEmpty(sel.AppPath))
        {
            ShowInfo("앱 경로가 없습니다."); return;
        }
        Clipboard.SetText(sel.AppPath);
        ShowInfo($"복사됨: {sel.AppPath}");
    }

    // ── 폴더 열기 ─────────────────────────────────────
    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel == null || string.IsNullOrEmpty(sel.AppPath))
        {
            ShowInfo("앱 경로가 없습니다."); return;
        }

        string? dir = Path.GetDirectoryName(sel.AppPath);
        if (dir != null && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName  = "explorer.exe",
                Arguments = $"/select,\"{sel.AppPath}\"",
                UseShellExecute = true
            });
        }
        else
        {
            ShowInfo("폴더를 찾을 수 없습니다.");
        }
    }

    // ── JSON 내보내기 ─────────────────────────────────
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title      = "프로파일 내보내기",
            Filter     = "JSON 파일|*.json",
            FileName   = $"ext-boss-profile-{DateTime.Now:yyyyMMdd_HHmmss}.json",
            DefaultExt = "json"
        };
        if (dlg.ShowDialog() != true) return;

        var dtos = _all.Select(x => new AssocDto(
            x.Extension, x.ProgId, x.AppName, x.AppPath, x.Source)).ToList();

        var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
        ShowInfo($"저장 완료: {dtos.Count}개 항목 → {Path.GetFileName(dlg.FileName)}");
    }

    // ── JSON 가져오기 ─────────────────────────────────
    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "프로파일 가져오기",
            Filter = "JSON 파일|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        List<AssocDto>? dtos;
        try
        {
            var json = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            dtos = JsonSerializer.Deserialize<List<AssocDto>>(json);
        }
        catch (Exception ex)
        {
            ShowError($"파일 읽기 실패: {ex.Message}");
            return;
        }

        if (dtos == null || dtos.Count == 0)
        {
            ShowInfo("복원할 항목이 없습니다."); return;
        }

        // HKCR 소스 항목만 복원 가능 (UserChoice는 해시 보호)
        var hkcrOnly = dtos.Where(d => d.Source == "HKCR" && !string.IsNullOrEmpty(d.ProgId)).ToList();
        var ucCount  = dtos.Count(d => d.Source == "UserChoice");

        var msg = $"가져올 항목: {dtos.Count}개\n" +
                  $"  • HKCR 복원 가능: {hkcrOnly.Count}개\n" +
                  $"  • UserChoice (제한됨): {ucCount}개\n\n" +
                  $"HKCR 항목을 레지스트리에 적용하시겠습니까?\n" +
                  $"(관리자 권한 필요. UserChoice 항목은 건너뜁니다)";

        if (MessageBox.Show(msg, "프로파일 가져오기",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (!IsAdmin())
        {
            var adminMsg = "레지스트리 쓰기에는 관리자 권한이 필요합니다.\n" +
                           "관리자 권한으로 Ext.Boss를 다시 시작하시겠습니까?";
            if (MessageBox.Show(adminMsg, "권한 필요",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
            return;
        }

        int ok = 0, fail = 0;
        foreach (var dto in hkcrOnly)
        {
            try
            {
                // HKCR\<ext> 기본값 설정
                using var key = Registry.ClassesRoot.CreateSubKey(dto.Extension);
                key.SetValue("", dto.ProgId);
                ok++;
            }
            catch { fail++; }
        }

        ShowInfo($"복원 완료: 성공 {ok}개, 실패 {fail}개\n시스템을 재시작하면 변경사항이 반영됩니다.");
    }

    private static void RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? "Ext.Boss.exe",
                Verb = "runas",
                UseShellExecute = true
            });
            Application.Current.Shutdown();
        }
        catch { /* 사용자가 UAC 취소 */ }
    }

    // ── 알림 헬퍼 ────────────────────────────────────
    private void ShowInfo(string msg)  =>
        MessageBox.Show(msg, "Ext.Boss", MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowError(string msg) =>
        MessageBox.Show(msg, "Ext.Boss", MessageBoxButton.OK, MessageBoxImage.Error);
}
