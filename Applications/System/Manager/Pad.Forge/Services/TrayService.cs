using System.Drawing;
using System.Windows.Forms;
using PadForge.Models;
using PadForge.Views;

namespace PadForge.Services;

public class TrayService : IDisposable
{
    private readonly MainWindow      _mainWindow;
    private readonly ControllerService _controllerService;
    private NotifyIcon?  _tray;
    private ContextMenuStrip? _menu;

    public TrayService(MainWindow mainWindow, ControllerService controllerService)
    {
        _mainWindow        = mainWindow;
        _controllerService = controllerService;
    }

    public void Initialize()
    {
        _menu = new ContextMenuStrip
        {
            ShowImageMargin = true,
            ShowCheckMargin = true,
            AutoSize        = true,
            Font            = new Font("Segoe UI", 9.5f),
            Renderer        = new DarkMenuRenderer()
        };

        _menu.Items.Add("열기",    null, (_, _) => ShowWindow());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("종료",    null, (_, _) => Quit());

        _tray = new NotifyIcon
        {
            Text    = "Pad.Forge — 게임패드 매핑",
            Visible = true,
        };

        // 아이콘 로드 (없으면 기본 아이콘)
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(iconPath))
                _tray.Icon = new Icon(iconPath);
            else
                _tray.Icon = SystemIcons.Application;
        }
        catch
        {
            _tray.Icon = SystemIcons.Application;
        }

        _tray.ContextMenuStrip = _menu;
        _tray.DoubleClick += (_, _) => ShowWindow();

        // 컨트롤러 연결/해제 알림
        _controllerService.ControllerConnected    += OnConnected;
        _controllerService.ControllerDisconnected += OnDisconnected;

        // 실행 시 풍선 알림
        _tray.ShowBalloonTip(2000, "Pad.Forge", "게임패드 매핑이 시작되었습니다.", ToolTipIcon.Info);
    }

    private void OnConnected(ControllerState state)
    {
        var label = $"컨트롤러 {state.Index + 1} ({state.Type}) 연결됨";
        if (state.BatteryLevel > 0) label += $" — 배터리 {state.BatteryLevel}%";
        ShowBalloon("컨트롤러 연결", label, ToolTipIcon.Info);
        UpdateTrayTooltip();
    }

    private void OnDisconnected(ControllerState state)
    {
        ShowBalloon("컨트롤러 연결 해제", $"컨트롤러 {state.Index + 1} 연결이 해제되었습니다.", ToolTipIcon.Warning);
        UpdateTrayTooltip();
    }

    private void ShowBalloon(string title, string msg, ToolTipIcon icon)
    {
        _tray?.ShowBalloonTip(3000, title, msg, icon);
    }

    private void UpdateTrayTooltip()
    {
        var connected = _controllerService.GetConnectedControllers().Count();
        if (_tray is not null)
            _tray.Text = $"Pad.Forge — 컨트롤러 {connected}개 연결됨";
    }

    private void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
        _mainWindow.Activate();
    }

    private void Quit()
    {
        WpfApplication.Current.Shutdown();
    }

    public void Dispose()
    {
        _controllerService.ControllerConnected    -= OnConnected;
        _controllerService.ControllerDisconnected -= OnDisconnected;
        _tray?.Dispose();
        _menu?.Dispose();
    }
}

/// <summary>다크 트레이 메뉴 렌더러</summary>
public class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly System.Drawing.Color BgColor    = System.Drawing.Color.FromArgb(30, 30, 46);
    private static readonly System.Drawing.Color HoverColor = System.Drawing.Color.FromArgb(42, 42, 58);
    private static readonly System.Drawing.Color TextColor  = System.Drawing.Color.FromArgb(224, 224, 224);
    private static readonly System.Drawing.Color BorderColor = System.Drawing.Color.FromArgb(51, 51, 68);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(BgColor);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        if (e.Item.Selected)
        {
            using var b = new SolidBrush(HoverColor);
            using var pen = new System.Drawing.Pen(BorderColor);
            e.Graphics.FillRectangle(b, rc);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = TextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new System.Drawing.Pen(BorderColor);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }
}
