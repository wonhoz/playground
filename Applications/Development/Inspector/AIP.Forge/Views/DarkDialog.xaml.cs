using AIPForge.Services;

namespace AIPForge.Views;

public enum DialogChoice { None, Primary, Secondary, Cancel }

/// <summary>다크 테마 알림 · 확인 · 3지선다 대화상자</summary>
public partial class DarkDialog : Window
{
    private DialogChoice _choice = DialogChoice.None;

    private DarkDialog(string message, string title)
    {
        InitializeComponent();
        Title = title;
        TxtMessage.Text = message;
        DarkTitle.Apply(this);
    }

    private void AddButton(string text, DialogChoice choice, bool accent = false, bool isDefault = false, bool isCancel = false)
    {
        var btn = new Button
        {
            Content = text,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 76,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        if (accent) btn.Style = (Style)FindResource("AccentButton");
        btn.Click += (_, _) => { _choice = choice; Close(); };
        PnlButtons.Children.Add(btn);
    }

    public static void Info(Window? owner, string message, string title = "AIP.Forge")
    {
        var dlg = new DarkDialog(message, title) { Owner = owner };
        dlg.AddButton("확인", DialogChoice.Primary, accent: true, isDefault: true, isCancel: true);
        dlg.ShowDialog();
    }

    public static bool Confirm(Window? owner, string message, string okText = "확인", string title = "AIP.Forge")
    {
        var dlg = new DarkDialog(message, title) { Owner = owner };
        dlg.AddButton("취소", DialogChoice.Cancel, isCancel: true);
        dlg.AddButton(okText, DialogChoice.Primary, accent: true, isDefault: true);
        dlg.ShowDialog();
        return dlg._choice == DialogChoice.Primary;
    }

    /// <summary>저장하지 않은 변경 확인: 저장(Primary) / 저장 안 함(Secondary) / 취소</summary>
    public static DialogChoice SaveChanges(Window? owner, string fileName)
    {
        var dlg = new DarkDialog($"'{fileName}'에 저장하지 않은 변경 사항이 있습니다.\n저장할까요?", "AIP.Forge") { Owner = owner };
        dlg.AddButton("취소", DialogChoice.Cancel, isCancel: true);
        dlg.AddButton("저장 안 함", DialogChoice.Secondary);
        dlg.AddButton("저장", DialogChoice.Primary, accent: true, isDefault: true);
        dlg.ShowDialog();
        return dlg._choice == DialogChoice.None ? DialogChoice.Cancel : dlg._choice;
    }
}
