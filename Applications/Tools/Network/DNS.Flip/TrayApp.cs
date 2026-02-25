using System.Drawing;
using DnsFlip.Models;
using DnsFlip.Services;

namespace DnsFlip;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly AppConfig _config;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public TrayApp()
    {
        _config = AppConfig.Load();

        _menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer() };

        _tray = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "DNS.Flip",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _tray.ShowBalloonTip(2000, "DNS.Flip",
            DnsService.IsAdmin() ? "DNS 프리셋 전환 준비 완료 (관리자)" : "DNS 프리셋 전환 준비 완료\n⚠ DNS 변경은 관리자 권한 필요",
            ToolTipIcon.Info);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _refreshTimer.Tick += (_, _) => BuildMenu();
        _refreshTimer.Start();

        BuildMenu();
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        // 현재 상태
        var adapters = DnsService.GetActiveAdapters();
        if (adapters.Count == 0)
        {
            _menu.Items.Add(new ToolStripMenuItem("네트워크 어댑터 없음") { Enabled = false });
        }
        else
        {
            foreach (var adapter in adapters)
            {
                var currentDns = DnsService.GetCurrentDns(adapter);
                var adapterItem = new ToolStripMenuItem($"🖧 {adapter}  —  [{currentDns}]")
                {
                    Enabled = false
                };
                _menu.Items.Add(adapterItem);

                // 프리셋 하위 메뉴
                foreach (var preset in _config.Presets)
                {
                    var isActive = IsPresetActive(currentDns, preset);
                    var item = new ToolStripMenuItem($"    {preset.Icon} {preset.Name}")
                    {
                        Checked = isActive,
                        Tag = (adapter, preset)
                    };
                    item.Click += OnPresetClick;
                    _menu.Items.Add(item);
                }

                _menu.Items.Add(new ToolStripSeparator());
            }
        }

        // Ping 테스트
        var pingItem = new ToolStripMenuItem("⚡ DNS Ping 테스트");
        pingItem.Click += OnPingClick;
        _menu.Items.Add(pingItem);

        // 변경 로그
        var logItem = new ToolStripMenuItem("📋 변경 로그");
        logItem.Click += OnLogClick;
        _menu.Items.Add(logItem);

        _menu.Items.Add(new ToolStripSeparator());

        // 종료
        var exitItem = new ToolStripMenuItem("❌ 종료");
        exitItem.Click += (_, _) => { _tray.Visible = false; Application.Exit(); };
        _menu.Items.Add(exitItem);
    }

    private async void OnPresetClick(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item) return;
        if (item.Tag is not (string adapter, DnsPreset preset)) return;

        if (!DnsService.IsAdmin())
        {
            _tray.ShowBalloonTip(3000, "DNS.Flip", "DNS 변경은 관리자 권한으로 실행해야 합니다.", ToolTipIcon.Warning);
            return;
        }

        var (success, error) = DnsService.ApplyPreset(adapter, preset);

        LogService.AddEntry(new DnsLogEntry
        {
            Adapter = adapter,
            PresetName = preset.Name,
            Dns = string.IsNullOrEmpty(preset.Primary) ? "DHCP" : $"{preset.Primary}, {preset.Secondary}",
            Success = success,
            Error = error
        });

        if (success)
        {
            _tray.ShowBalloonTip(2000, "DNS.Flip",
                $"{adapter}: {preset.Name} 적용 완료", ToolTipIcon.Info);
        }
        else
        {
            _tray.ShowBalloonTip(3000, "DNS.Flip",
                $"DNS 변경 실패: {error}", ToolTipIcon.Error);
        }

        // Wait a moment for the change to take effect, then rebuild menu
        await Task.Delay(1000);
        BuildMenu();
    }

    private async void OnPingClick(object? sender, EventArgs e)
    {
        var results = new List<string>();

        foreach (var preset in _config.Presets.Where(p => !string.IsNullOrEmpty(p.Primary)))
        {
            var (ms, ok) = await DnsService.PingDnsAsync(preset.Primary);
            var status = ok ? $"{ms}ms" : "실패";
            results.Add($"{preset.Icon} {preset.Name} ({preset.Primary}): {status}");
        }

        var msg = string.Join("\n", results);
        MessageBox.Show(msg, "DNS Ping 테스트 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnLogClick(object? sender, EventArgs e)
    {
        var entries = LogService.GetEntries().Take(30);
        var lines = entries.Select(l =>
        {
            var status = l.Success ? "✅" : "❌";
            return $"[{l.Timestamp:MM-dd HH:mm:ss}] {status} {l.Adapter} → {l.PresetName} ({l.Dns})";
        });

        var msg = lines.Any() ? string.Join("\n", lines) : "변경 기록이 없습니다.";
        MessageBox.Show(msg, "DNS 변경 로그", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static bool IsPresetActive(string currentDns, DnsPreset preset)
    {
        if (string.IsNullOrEmpty(preset.Primary))
            return currentDns == "DHCP" || !currentDns.Contains('.');

        return currentDns.Contains(preset.Primary);
    }

    private static Icon CreateIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(20, 20, 42));

        // DNS globe
        using var pen = new Pen(Color.FromArgb(64, 160, 255), 2f);
        g.DrawEllipse(pen, 4, 4, 24, 24);
        g.DrawLine(pen, 16, 4, 16, 28);
        g.DrawLine(pen, 4, 16, 28, 16);
        g.DrawArc(pen, 8, 4, 16, 24, 0, 180);

        // Flip arrow
        using var arrowPen = new Pen(Color.FromArgb(80, 255, 120), 2.5f);
        g.DrawLine(arrowPen, 22, 8, 28, 8);
        g.DrawLine(arrowPen, 25, 5, 28, 8);
        g.DrawLine(arrowPen, 25, 11, 28, 8);

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
            _tray.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
