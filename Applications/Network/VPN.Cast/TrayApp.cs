using System.Drawing;
using System.Drawing.Drawing2D;
using VpnCast.Models;
using VpnCast.Services;

namespace VpnCast;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon        _tray;
    private readonly ContextMenuStrip  _menu;
    private readonly List<TunnelProfile> _profiles;
    private bool _killSwitchEnabled;
    private readonly System.Windows.Forms.Timer _statusTimer;

    public TrayApp()
    {
        _profiles = ProfileStore.Load();

        _menu = new ContextMenuStrip
        {
            Renderer     = new DarkMenuRenderer(),
            AutoSize     = true,
            ShowImageMargin = false,
            Font         = new Font("Segoe UI", 9.5f)
        };

        _tray = new NotifyIcon
        {
            Icon    = CreateIcon(false),
            Text    = "VPN.Cast",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _tray.ShowBalloonTip(2000, "VPN.Cast",
            "VPN 프로파일 관리자 실행 완료\n.conf / .ovpn 파일을 가져오세요",
            ToolTipIcon.Info);

        _statusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();

        BuildMenu();
    }

    // ── 상태 갱신 ────────────────────────────────────────────────────
    private void RefreshStatus()
    {
        bool changed = false;
        foreach (var p in _profiles)
        {
            var old = p.Status;
            p.Status = TunnelService.GetStatus(p);
            if (p.Status != old) changed = true;
        }
        if (!changed) return;

        BuildMenu();
        bool anyConnected = _profiles.Any(p => p.Status == TunnelStatus.Connected);
        _tray.Icon = CreateIcon(anyConnected);
        if (anyConnected)
        {
            var name = _profiles.First(p => p.Status == TunnelStatus.Connected).Name;
            _tray.Text = $"VPN.Cast — {name} 연결됨";
        }
        else
        {
            _tray.Text = "VPN.Cast — 연결 없음";
        }
    }

    // ── 메뉴 빌드 ────────────────────────────────────────────────────
    private void BuildMenu()
    {
        _menu.Items.Clear();

        bool anyConnected = _profiles.Any(p => p.Status == TunnelStatus.Connected);
        var statusText = anyConnected ? "🟢 VPN 연결됨" : "⚫ VPN 끊김";
        _menu.Items.Add(new ToolStripMenuItem(statusText) { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());

        if (_profiles.Count == 0)
        {
            _menu.Items.Add(new ToolStripMenuItem("프로파일 없음 — 아래에서 가져오세요") { Enabled = false });
        }
        else
        {
            foreach (var profile in _profiles)
            {
                var statusIcon = profile.Status == TunnelStatus.Connected ? "🟢 " : "⚫ ";
                var item = new ToolStripMenuItem($"{statusIcon}{profile.TypeIcon} {profile.Name}");

                if (profile.ServerAddress != null)
                {
                    var serverInfo = new ToolStripMenuItem($"   서버: {profile.ServerAddress}")
                        { Enabled = false };
                    item.DropDownItems.Add(serverInfo);
                    item.DropDownItems.Add(new ToolStripSeparator());
                }

                var connectItem = new ToolStripMenuItem("▶ 연결");
                connectItem.Enabled = profile.Status != TunnelStatus.Connected;
                connectItem.Click += async (_, _) => await ConnectAsync(profile);

                var disconnectItem = new ToolStripMenuItem("■ 해제");
                disconnectItem.Enabled = profile.Status == TunnelStatus.Connected;
                disconnectItem.Click += async (_, _) => await DisconnectAsync(profile);

                item.DropDownItems.Add(connectItem);
                item.DropDownItems.Add(disconnectItem);
                item.DropDownItems.Add(new ToolStripSeparator());

                if (profile.ConnectedAt.HasValue)
                {
                    item.DropDownItems.Add(new ToolStripMenuItem(
                        $"   연결됨: {profile.ConnectedAt:MM-dd HH:mm}") { Enabled = false });
                }

                var deleteItem = new ToolStripMenuItem("🗑 삭제");
                deleteItem.Click += (_, _) => DeleteProfile(profile);
                item.DropDownItems.Add(deleteItem);

                _menu.Items.Add(item);
            }
        }

        _menu.Items.Add(new ToolStripSeparator());

        // 가져오기
        var importItem = new ToolStripMenuItem("📂 프로파일 가져오기");
        var wgItem = new ToolStripMenuItem("🔒 WireGuard (.conf)");
        wgItem.Click += (_, _) => ImportProfile(TunnelType.WireGuard);
        var ovpnItem = new ToolStripMenuItem("🛡 OpenVPN (.ovpn)");
        ovpnItem.Click += (_, _) => ImportProfile(TunnelType.OpenVPN);
        importItem.DropDownItems.Add(wgItem);
        importItem.DropDownItems.Add(ovpnItem);
        _menu.Items.Add(importItem);

        // 킬 스위치
        var killItem = new ToolStripMenuItem("🔐 킬 스위치") { Checked = _killSwitchEnabled };
        killItem.Click += async (_, _) => await ToggleKillSwitchAsync();
        _menu.Items.Add(killItem);

        // 연결 로그
        var logItem = new ToolStripMenuItem("📋 연결 로그");
        logItem.Click += (_, _) => ShowLog();
        _menu.Items.Add(logItem);

        _menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("❌ 종료");
        exitItem.Click += (_, _) => { _tray.Visible = false; Application.Exit(); };
        _menu.Items.Add(exitItem);
    }

    // ── 연결 ────────────────────────────────────────────────────────
    private async Task ConnectAsync(TunnelProfile profile)
    {
        _tray.ShowBalloonTip(1500, "VPN.Cast", $"⏳ {profile.Name} 연결 중...", ToolTipIcon.Info);
        var (success, error) = await TunnelService.ConnectAsync(profile);

        ConnectionLogStore.Add(new ConnectionLog
        {
            ProfileName = profile.Name,
            Type        = profile.Type,
            Action      = "연결",
            Success     = success,
            Error       = error
        });

        if (success)
        {
            profile.ConnectedAt = DateTime.Now;
            ProfileStore.Save(_profiles);
            _tray.ShowBalloonTip(2000, "VPN.Cast", $"✅ {profile.Name} 연결됨", ToolTipIcon.Info);
            _tray.Icon = CreateIcon(true);
            _tray.Text = $"VPN.Cast — {profile.Name} 연결됨";
        }
        else
        {
            _tray.ShowBalloonTip(4000, "VPN.Cast", $"❌ 연결 실패:\n{error}", ToolTipIcon.Error);
        }

        profile.Status = TunnelService.GetStatus(profile);
        BuildMenu();
    }

    // ── 연결 해제 ────────────────────────────────────────────────────
    private async Task DisconnectAsync(TunnelProfile profile)
    {
        var (success, error) = await TunnelService.DisconnectAsync(profile);

        ConnectionLogStore.Add(new ConnectionLog
        {
            ProfileName = profile.Name,
            Type        = profile.Type,
            Action      = "해제",
            Success     = success,
            Error       = error
        });

        if (success)
        {
            profile.ConnectedAt = null;
            ProfileStore.Save(_profiles);
            _tray.ShowBalloonTip(2000, "VPN.Cast", $"⚫ {profile.Name} 연결 해제됨", ToolTipIcon.Info);
        }
        else
        {
            _tray.ShowBalloonTip(3000, "VPN.Cast", $"❌ 해제 실패: {error}", ToolTipIcon.Error);
        }

        profile.Status = TunnelService.GetStatus(profile);
        bool anyConnected = _profiles.Any(p => p.Status == TunnelStatus.Connected);
        _tray.Icon = CreateIcon(anyConnected);
        _tray.Text = anyConnected ? $"VPN.Cast — {_profiles.First(p => p.Status == TunnelStatus.Connected).Name} 연결됨" : "VPN.Cast — 연결 없음";
        BuildMenu();
    }

    // ── 프로파일 가져오기 ────────────────────────────────────────────
    private void ImportProfile(TunnelType type)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = type == TunnelType.WireGuard ? "WireGuard 설정 파일 선택" : "OpenVPN 설정 파일 선택",
            Filter = type == TunnelType.WireGuard
                ? "WireGuard Config (*.conf)|*.conf|모든 파일 (*.*)|*.*"
                : "OpenVPN Config (*.ovpn)|*.ovpn|모든 파일 (*.*)|*.*"
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        // 설정 파일을 로컬에 복사
        var destDir = ProfileStore.ProfilesDir;
        Directory.CreateDirectory(destDir);
        var destPath = Path.Combine(destDir, Path.GetFileName(dlg.FileName));
        File.Copy(dlg.FileName, destPath, overwrite: true);

        var name          = Path.GetFileNameWithoutExtension(dlg.FileName);
        var serverAddress = TunnelService.ExtractServerAddress(dlg.FileName, type);

        // 중복 이름 처리
        if (_profiles.Any(p => p.Name == name))
        {
            name = $"{name}_{DateTime.Now:HHmmss}";
        }

        var profile = new TunnelProfile
        {
            Name          = name,
            Type          = type,
            ConfigPath    = destPath,
            ServerAddress = serverAddress
        };

        _profiles.Add(profile);
        ProfileStore.Save(_profiles);
        BuildMenu();

        _tray.ShowBalloonTip(2000, "VPN.Cast",
            $"✅ 프로파일 추가됨: {name}\n{(serverAddress != null ? $"서버: {serverAddress}" : "")}",
            ToolTipIcon.Info);
    }

    // ── 프로파일 삭제 ────────────────────────────────────────────────
    private void DeleteProfile(TunnelProfile profile)
    {
        if (profile.Status == TunnelStatus.Connected)
        {
            MessageBox.Show("연결 중인 프로파일은 삭제할 수 없습니다.\n먼저 연결을 해제하세요.",
                "VPN.Cast", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _profiles.Remove(profile);
        ProfileStore.Save(_profiles);
        BuildMenu();
    }

    // ── 킬 스위치 ────────────────────────────────────────────────────
    private async Task ToggleKillSwitchAsync()
    {
        _killSwitchEnabled = !_killSwitchEnabled;

        if (_killSwitchEnabled)
        {
            await KillSwitchService.EnableAsync();
            _tray.ShowBalloonTip(3000, "VPN.Cast",
                "🔐 킬 스위치 활성화됨\nVPN 연결 없이는 인터넷 사용 불가",
                ToolTipIcon.Warning);
        }
        else
        {
            await KillSwitchService.DisableAsync();
            _tray.ShowBalloonTip(2000, "VPN.Cast", "🔓 킬 스위치 비활성화됨", ToolTipIcon.Info);
        }

        BuildMenu();
    }

    // ── 연결 로그 ────────────────────────────────────────────────────
    private void ShowLog()
    {
        var logs = ConnectionLogStore.GetRecent(30);

        if (!logs.Any())
        {
            MessageBox.Show("연결 이력이 없습니다.", "VPN.Cast 연결 로그",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var lines = logs.Select(l =>
        {
            var icon = l.Success ? "✅" : "❌";
            return $"[{l.Timestamp:MM-dd HH:mm:ss}] {icon} {l.Action} — {l.ProfileName} ({l.Type})" +
                   (l.Error != null ? $"\n   오류: {l.Error}" : "");
        });

        MessageBox.Show(string.Join("\n", lines), "VPN.Cast 연결 로그",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── 아이콘 ────────────────────────────────────────────────────────
    private static Icon CreateIcon(bool connected)
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(18, 18, 30));

        // 방패 외형
        var shieldColor = connected ? Color.FromArgb(60, 200, 100) : Color.FromArgb(100, 100, 140);
        using var shieldPen = new Pen(shieldColor, 2f);
        var pts = new PointF[]
        {
            new(16, 4), new(27, 9), new(27, 18), new(16, 28), new(5, 18), new(5, 9)
        };
        g.DrawPolygon(shieldPen, pts);

        // 자물쇠
        using var lockBrush = new SolidBrush(shieldColor);
        g.FillRectangle(lockBrush, 11, 16, 10, 7);
        using var arcPen = new Pen(shieldColor, 2f);
        g.DrawArc(arcPen, 11, 10, 10, 10, 180, 180);

        // 열쇠 구멍
        using var holeBrush = new SolidBrush(Color.FromArgb(18, 18, 30));
        g.FillEllipse(holeBrush, 14, 18, 4, 4);
        g.FillRectangle(holeBrush, 15, 21, 2, 3);

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusTimer.Dispose();
            _tray.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
