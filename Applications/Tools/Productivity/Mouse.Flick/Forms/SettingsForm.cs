using System.Runtime.InteropServices;
using MouseFlick.Models;
using MouseFlick.Rendering;
using MouseFlick.Services;

namespace MouseFlick.Forms;

internal sealed class SettingsForm : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    private static readonly Color _bg   = Color.FromArgb(28, 28, 42);
    private static readonly Color _bg2  = Color.FromArgb(38, 38, 58);
    private static readonly Color _fg   = Color.FromArgb(220, 220, 230);
    private static readonly Color _fg2  = Color.FromArgb(130, 130, 158);
    private static readonly Color _sel  = Color.FromArgb(55, 95, 195);

    private readonly AppSettings _settings;
    private bool _initialized = false;

    // 프로필 목록
    private readonly ListBox _lstProfiles;
    // 제스처 목록
    private readonly ListView  _lstGestures;
    // 설정 컨트롤
    private readonly TrackBar _trackThreshold;
    private readonly Label    _lblThresholdVal;
    private readonly CheckBox _chkOverlay;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text            = "Mouse.Flick — 설정";
        Size            = new Size(760, 520);
        MinimumSize     = new Size(680, 460);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor       = _bg;
        ForeColor       = _fg;
        Font            = new Font("Segoe UI", 9.5f);
        ShowInTaskbar   = true;

        // ── 하단 설정 바 ──────────────────────────────────────────────────────
        var bottomBar = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 52,
            BackColor = Color.FromArgb(22, 22, 36),
        };

        var lblThreshold = new Label
        {
            Text = "임계값:", ForeColor = _fg,
            AutoSize = true, Location = new Point(14, 16)
        };

        _trackThreshold = new TrackBar
        {
            Minimum       = 10, Maximum = 100,
            Value         = Math.Clamp(_settings.GestureThreshold, 10, 100),
            TickFrequency = 10, SmallChange = 5, LargeChange = 10,
            Location      = new Point(70, 7),
            Size          = new Size(160, 36),
        };
        _trackThreshold.Scroll += (_, _) =>
        {
            if (!_initialized) return;
            if (_lblThresholdVal != null) _lblThresholdVal.Text = $"{_trackThreshold.Value}px";
        };

        _lblThresholdVal = new Label
        {
            Text = $"{_settings.GestureThreshold}px",
            ForeColor = _fg, AutoSize = true, Location = new Point(234, 16)
        };

        _chkOverlay = new CheckBox
        {
            Text = "오버레이 표시", Checked = _settings.ShowOverlay,
            ForeColor = _fg, Size = new Size(120, 30), Location = new Point(290, 10),
            Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 56),
            TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
        };
        _chkOverlay.FlatAppearance.CheckedBackColor  = Color.FromArgb(24, 74, 50);
        _chkOverlay.FlatAppearance.BorderColor        = Color.FromArgb(54, 54, 76);
        _chkOverlay.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 74);

        var btnSave = MakeButton("저장", new Point(bottomBar.Width - 180, 10), 84);
        btnSave.Anchor    = AnchorStyles.Right | AnchorStyles.Top;
        btnSave.BackColor = Color.FromArgb(24, 74, 140);
        btnSave.ForeColor = Color.White;
        btnSave.FlatAppearance.BorderColor = Color.FromArgb(30, 90, 180);
        btnSave.Click += OnSave;

        var btnClose = MakeButton("닫기", new Point(bottomBar.Width - 90, 10), 84);
        btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnClose.Click += (_, _) => Close();

        bottomBar.Controls.AddRange([lblThreshold, _trackThreshold, _lblThresholdVal,
            _chkOverlay, btnSave, btnClose]);

        // ── 왼쪽 패널 (고정폭 210px) ──────────────────────────────────────────
        // SplitContainer 대신 DockStyle.Left + DockStyle.Fill 사용
        // — SplitContainer.SplitterDistance는 생성자에서 Width=0일 때 설정 불가 (InvalidOperationException)
        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = _bg };

        var leftBtnPanel = new Panel
        {
            Dock = DockStyle.Bottom, Height = 38,
            BackColor = _bg, Padding = new Padding(6, 4, 6, 4),
        };

        var btnAddProfile = MakeSmallButton("+ 추가", new Point(6, 4));
        btnAddProfile.Click += OnAddProfile;
        var btnDelProfile = MakeSmallButton("− 삭제", new Point(70, 4));
        btnDelProfile.ForeColor = Color.FromArgb(220, 80, 80);
        btnDelProfile.Click += OnDelProfile;
        leftBtnPanel.Controls.AddRange([btnAddProfile, btnDelProfile]);

        var leftHdr = new Label
        {
            Text = "프로필", ForeColor = _fg2, Dock = DockStyle.Top,
            Height = 26, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.FromArgb(24, 24, 36),
            Font = new Font("Segoe UI", 8.5f),
        };

        _lstProfiles = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = _bg2, ForeColor = _fg,
            BorderStyle = BorderStyle.None,
            DrawMode    = DrawMode.OwnerDrawFixed,
            ItemHeight  = 28,
        };
        _lstProfiles.DrawItem             += LstProfiles_DrawItem;
        _lstProfiles.SelectedIndexChanged += (_, _) => RefreshGestureList();

        // 역순: Fill 먼저, Bottom, Top 마지막
        leftPanel.Controls.AddRange([_lstProfiles, leftBtnPanel, leftHdr]);

        // 구분선
        var sepPanel = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.FromArgb(48, 48, 68) };

        // ── 오른쪽 패널 (나머지 영역) ─────────────────────────────────────────
        var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = _bg };

        var rightBtnPanel = new Panel
        {
            Dock = DockStyle.Bottom, Height = 38,
            BackColor = _bg, Padding = new Padding(6, 4, 6, 4),
        };

        var btnAddGesture  = MakeSmallButton("+ 추가",    new Point(6,   4));
        var btnEditGesture = MakeSmallButton("✎ 편집",    new Point(70,  4));
        var btnDelGesture  = MakeSmallButton("− 삭제",    new Point(134, 4));
        var btnPreset      = MakeSmallButton("프리셋 ▾", new Point(198, 4), 96);

        btnAddGesture.Click  += OnAddGesture;
        btnEditGesture.Click += OnEditGesture;
        btnDelGesture.Click  += OnDelGesture;
        btnDelGesture.ForeColor = Color.FromArgb(220, 80, 80);

        var presetMenu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ShowImageMargin = false, AutoSize = true,
            Font = new Font("Segoe UI", 9.5f),
        };
        foreach (var preset in ProfileManager.BuiltinPresets)
        {
            var p = preset;
            presetMenu.Items.Add(new ToolStripMenuItem(p.Name, null, (_, _) => LoadPreset(p)));
        }
        btnPreset.Click += (_, _) => presetMenu.Show(btnPreset, new Point(0, btnPreset.Height));

        rightBtnPanel.Controls.AddRange([btnAddGesture, btnEditGesture, btnDelGesture, btnPreset]);

        var rightHdr = new Label
        {
            Text = "제스처 → 단축키", ForeColor = _fg2, Dock = DockStyle.Top,
            Height = 26, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.FromArgb(24, 24, 36),
            Font = new Font("Segoe UI", 8.5f),
        };

        _lstGestures = new ListView
        {
            Dock = DockStyle.Fill,
            BackColor = _bg2, ForeColor = _fg,
            BorderStyle = BorderStyle.None,
            View = View.Details, FullRowSelect = true,
            GridLines = false, OwnerDraw = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _lstGestures.Columns.Add("제스처",  80);
        _lstGestures.Columns.Add("설명",   168);
        _lstGestures.Columns.Add("단축키", 160);
        _lstGestures.DrawColumnHeader += LstGestures_DrawColumnHeader;
        _lstGestures.DrawItem         += (_, e) => e.DrawDefault = false;
        _lstGestures.DrawSubItem      += LstGestures_DrawSubItem;
        _lstGestures.DoubleClick      += OnEditGesture;

        // 역순: Fill 먼저, Bottom, Top 마지막
        rightPanel.Controls.AddRange([_lstGestures, rightBtnPanel, rightHdr]);

        // WinForms 도킹 역순 처리: Fill(rightPanel)=index0, Left(sep)=1, Left(leftPanel)=2, Bottom(bottomBar)=3
        Controls.AddRange([rightPanel, sepPanel, leftPanel, bottomBar]);

        // 프로필 로드
        RefreshProfileList();
        if (_lstProfiles.Items.Count > 0) _lstProfiles.SelectedIndex = 0;

        _initialized = true;
    }

    // ── 프로필 목록 그리기 ────────────────────────────────────────────────────
    private void LstProfiles_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _settings.Profiles.Count) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        e.Graphics.FillRectangle(new SolidBrush(sel ? _sel : _bg2), e.Bounds);
        var p    = _settings.Profiles[e.Index];
        var text = p.IsDefault ? $"★ {p.Name}" : $"  {p.Name}";
        TextRenderer.DrawText(e.Graphics, text, e.Font ?? Font, e.Bounds,
            sel ? Color.White : _fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.LeftAndRightPadding);
    }

    // ── 제스처 ListView 그리기 ────────────────────────────────────────────────
    private void LstGestures_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(32, 32, 50)), e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Header!.Text, e.Font ?? Font,
            new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
            _fg2, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        using var pen = new Pen(Color.FromArgb(48, 48, 68));
        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    private void LstGestures_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        bool sel     = e.Item!.Selected;
        var  evenRow = e.ItemIndex % 2 == 0;
        var  bg      = sel ? _sel : (evenRow ? _bg2 : Color.FromArgb(33, 33, 51));
        e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.SubItem!.Text, e.SubItem.Font ?? Font,
            new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
            sel ? Color.White : _fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
    }

    // ── 프로필 액션 ─────────────────────────────────────────────────────────
    private void RefreshProfileList()
    {
        _lstProfiles.Items.Clear();
        foreach (var p in _settings.Profiles)
            _lstProfiles.Items.Add(p);
    }

    private void OnAddProfile(object? sender, EventArgs e)
    {
        using var dlg = new InputDialog("새 프로필 이름을 입력하세요:", "프로필 추가");
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var name = dlg.InputText.Trim();
        if (string.IsNullOrEmpty(name)) return;

        _settings.Profiles.Add(new GestureProfile { Name = name });
        RefreshProfileList();
        _lstProfiles.SelectedIndex = _lstProfiles.Items.Count - 1;
    }

    private void OnDelProfile(object? sender, EventArgs e)
    {
        int idx = _lstProfiles.SelectedIndex;
        if (idx < 0 || idx >= _settings.Profiles.Count) return;
        if (_settings.Profiles[idx].IsDefault)
        {
            MessageBox.Show("기본 프로필은 삭제할 수 없습니다.", "Mouse.Flick",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _settings.Profiles.RemoveAt(idx);
        RefreshProfileList();
        if (_lstProfiles.Items.Count > 0)
            _lstProfiles.SelectedIndex = Math.Min(idx, _lstProfiles.Items.Count - 1);
    }

    // ── 제스처 액션 ─────────────────────────────────────────────────────────
    private GestureProfile? CurrentProfile =>
        _lstProfiles.SelectedIndex >= 0 && _lstProfiles.SelectedIndex < _settings.Profiles.Count
            ? _settings.Profiles[_lstProfiles.SelectedIndex]
            : null;

    private void RefreshGestureList()
    {
        _lstGestures.Items.Clear();
        var prof = CurrentProfile;
        if (prof == null) return;
        foreach (var a in prof.Actions)
        {
            var item = new ListViewItem(ToArrow(a.Gesture));
            item.SubItems.Add(a.Description);
            item.SubItems.Add(a.KeyCombo);
            item.Tag = a;
            _lstGestures.Items.Add(item);
        }
    }

    private static string ToArrow(string gesture) =>
        gesture.Replace("L", "←").Replace("R", "→")
               .Replace("U", "↑").Replace("D", "↓");

    private void OnAddGesture(object? sender, EventArgs e)
    {
        var prof = CurrentProfile;
        if (prof == null) return;
        using var dlg = new GestureEditorForm();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrEmpty(dlg.Result.Gesture) || string.IsNullOrEmpty(dlg.Result.KeyCombo)) return;
        prof.Actions.Add(dlg.Result);
        RefreshGestureList();
    }

    private void OnEditGesture(object? sender, EventArgs e)
    {
        var prof = CurrentProfile;
        if (prof == null || _lstGestures.SelectedItems.Count == 0) return;
        var action = (GestureAction)_lstGestures.SelectedItems[0].Tag!;
        int idx    = prof.Actions.IndexOf(action);
        if (idx < 0) return;
        using var dlg = new GestureEditorForm(action);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        prof.Actions[idx] = dlg.Result;
        RefreshGestureList();
    }

    private void OnDelGesture(object? sender, EventArgs e)
    {
        var prof = CurrentProfile;
        if (prof == null || _lstGestures.SelectedItems.Count == 0) return;
        prof.Actions.Remove((GestureAction)_lstGestures.SelectedItems[0].Tag!);
        RefreshGestureList();
    }

    private void LoadPreset(GestureProfile preset)
    {
        var prof = CurrentProfile;
        if (prof == null) return;
        if (MessageBox.Show(
            $"'{preset.Name}' 프리셋을 현재 프로필에 불러오겠습니까?\n기존 중복 제스처는 건너뜁니다.",
            "프리셋 불러오기", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        foreach (var a in preset.Actions)
        {
            if (!prof.Actions.Any(x => x.Gesture == a.Gesture))
                prof.Actions.Add(new GestureAction
                    { Gesture = a.Gesture, Description = a.Description, KeyCombo = a.KeyCombo });
        }
        RefreshGestureList();
    }

    // ── 저장 ─────────────────────────────────────────────────────────────────
    private void OnSave(object? sender, EventArgs e)
    {
        if (!_initialized) return;
        _settings.GestureThreshold = _trackThreshold.Value;
        _settings.ShowOverlay      = _chkOverlay.Checked;
        _settings.Save();
        MessageBox.Show("설정이 저장되었습니다.", "Mouse.Flick",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ── DWM 다크 타이틀바 ────────────────────────────────────────────────────
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        SetWindowTheme(_trackThreshold.Handle, "DarkMode_Explorer", null);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────
    private static Button MakeButton(string text, Point loc, int width)
    {
        var btn = new Button
        {
            Text = text, Location = loc, Size = new Size(width, 30),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 36, 56),
            ForeColor = Color.FromArgb(200, 200, 222),
            Font = new Font("Segoe UI", 9.5f), Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor        = Color.FromArgb(54, 54, 76);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 74);
        return btn;
    }

    private static Button MakeSmallButton(string text, Point loc, int width = 60)
    {
        var btn = new Button
        {
            Text = text, Location = loc, Size = new Size(width, 28),
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 36, 56),
            ForeColor = Color.FromArgb(200, 200, 222),
            Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor        = Color.FromArgb(54, 54, 76);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 52, 74);
        return btn;
    }
}

// ── 간단 텍스트 입력 다이얼로그 ─────────────────────────────────────────────
internal sealed class InputDialog : Form
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static readonly Color _bg  = Color.FromArgb(28, 28, 42);
    private static readonly Color _bg2 = Color.FromArgb(38, 38, 58);
    private static readonly Color _fg  = Color.FromArgb(220, 220, 230);

    private readonly TextBox _txt;
    public string InputText => _txt.Text;

    public InputDialog(string prompt, string title)
    {
        Text            = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = _bg;
        ForeColor       = _fg;
        Font            = new Font("Segoe UI", 9.5f);
        Size            = new Size(360, 160);
        ShowInTaskbar   = false;

        Controls.Add(new Label
        {
            Text = prompt, Location = new Point(14, 16), AutoSize = true,
            ForeColor = Color.FromArgb(150, 150, 170),
        });

        _txt = new TextBox
        {
            Location = new Point(14, 40), Width = 320,
            BackColor = _bg2, ForeColor = _fg, BorderStyle = BorderStyle.FixedSingle,
        };
        Controls.Add(_txt);

        var btnOk = new Button
        {
            Text = "확인", Location = new Point(ClientSize.Width - 174, 78),
            Size = new Size(80, 28), FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 56), ForeColor = _fg,
            DialogResult = DialogResult.OK, Cursor = Cursors.Hand,
        };
        btnOk.FlatAppearance.BorderColor = Color.FromArgb(54, 54, 76);

        var btnCancel = new Button
        {
            Text = "취소", Location = new Point(ClientSize.Width - 88, 78),
            Size = new Size(80, 28), FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(36, 36, 56), ForeColor = _fg,
            DialogResult = DialogResult.Cancel, Cursor = Cursors.Hand,
        };
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(54, 54, 76);

        Controls.AddRange([btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        int dark = 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        _txt.Focus();
    }
}
