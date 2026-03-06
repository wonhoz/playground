using System.Runtime.InteropServices;
using MouseFlick.Models;

namespace MouseFlick.Forms;

/// <summary>제스처 추가/편집 다이얼로그</summary>
internal sealed class GestureEditorForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static readonly Color _bg  = Color.FromArgb(28, 28, 42);
    private static readonly Color _bg2 = Color.FromArgb(38, 38, 58);
    private static readonly Color _fg  = Color.FromArgb(220, 220, 230);

    private readonly TextBox _txtGesture;
    private readonly TextBox _txtDescription;
    private readonly TextBox _txtKeyCombo;

    public GestureAction Result { get; private set; }

    public GestureEditorForm(GestureAction? existing = null)
    {
        Result = existing != null
            ? new GestureAction
              { Gesture = existing.Gesture, Description = existing.Description, KeyCombo = existing.KeyCombo }
            : new GestureAction();

        Text            = existing != null ? "제스처 편집" : "제스처 추가";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = _bg;
        ForeColor       = _fg;
        Font            = new Font("Segoe UI", 9.5f);
        Size            = new Size(420, 282);
        ShowInTaskbar   = false;

        int y = 20;

        // 제스처 방향 코드
        AddLabel("제스처 (L/R/U/D 조합, 예: L, UD, LR):", 14, y); y += 22;
        _txtGesture = AddTextBox(14, y, 390); y += 36;
        _txtGesture.Text             = Result.Gesture;
        _txtGesture.CharacterCasing  = CharacterCasing.Upper;
        _txtGesture.MaxLength        = 8;

        // 설명
        AddLabel("설명:", 14, y); y += 22;
        _txtDescription = AddTextBox(14, y, 390); y += 36;
        _txtDescription.Text = Result.Description;

        // 단축키 캡처
        AddLabel("단축키 (아래 입력창 클릭 후 키 입력):", 14, y); y += 22;
        _txtKeyCombo = AddTextBox(14, y, 390); y += 40;
        _txtKeyCombo.Text      = Result.KeyCombo;
        _txtKeyCombo.ReadOnly  = true;
        _txtKeyCombo.KeyDown  += OnKeyComboKeyDown;

        // 버튼
        var btnOk = MakeButton("확인", new Point(ClientSize.Width - 198, y), 90);
        btnOk.DialogResult = DialogResult.OK;
        btnOk.Click += (_, _) =>
        {
            Result.Gesture     = _txtGesture.Text.Trim().ToUpperInvariant();
            Result.Description = _txtDescription.Text.Trim();
            Result.KeyCombo    = _txtKeyCombo.Text.Trim();
        };

        var btnCancel = MakeButton("취소", new Point(ClientSize.Width - 102, y), 90);
        btnCancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange([btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    // ── 단축키 캡처 ───────────────────────────────────────────────────────────
    private void OnKeyComboKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;

        // 단독 수식자는 무시
        if (e.KeyCode is Keys.ControlKey or Keys.Menu or Keys.ShiftKey
                      or Keys.LWin or Keys.RWin) return;

        var parts = new List<string>();
        if (e.Control) parts.Add("Ctrl");
        if (e.Alt)     parts.Add("Alt");
        if (e.Shift)   parts.Add("Shift");

        // D0~D9 → "0"~"9" 로 표시
        var key = e.KeyCode.ToString();
        if (key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]))
            key = key[1].ToString();

        parts.Add(key);
        _txtKeyCombo.Text = string.Join("+", parts);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────
    private void AddLabel(string text, int x, int y)
    {
        Controls.Add(new Label
        {
            Text = text, Location = new Point(x, y),
            ForeColor = Color.FromArgb(150, 150, 170),
            AutoSize = true, Font = new Font("Segoe UI", 8.5f)
        });
    }

    private TextBox AddTextBox(int x, int y, int width)
    {
        var tb = new TextBox
        {
            Location = new Point(x, y), Width = width,
            BackColor = _bg2, ForeColor = _fg,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(tb);
        return tb;
    }

    private Button MakeButton(string text, Point loc, int width)
    {
        var btn = new Button
        {
            Text = text, Location = loc, Size = new Size(width, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 56),
            ForeColor = _fg, Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor        = Color.FromArgb(54, 54, 76);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 74);
        Controls.Add(btn);
        return btn;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
}
