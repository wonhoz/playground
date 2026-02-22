using AiClip.Models;
using System.Drawing;
using System.Windows.Forms;

namespace AiClip.Forms
{
    public class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private TextBox _apiKeyBox = null!;
        private ComboBox _translateLangBox = null!;
        private ComboBox _codeLangBox = null!;
        private Point _dragStart;

        private static readonly string[] TranslateLanguages =
            ["Korean", "English", "Japanese", "Chinese", "Spanish", "French", "German"];

        private static readonly string[] CodeLanguages =
            ["Python", "JavaScript", "TypeScript", "C#", "Java", "Go", "Rust", "Swift"];

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            InitializeUI();
        }

        private void InitializeUI()
        {
            BackColor  = Color.FromArgb(30, 30, 30);
            ForeColor  = Color.FromArgb(238, 238, 238);
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(480, 310);
            StartPosition   = FormStartPosition.CenterScreen;
            Text            = "AI.Clip Settings";
            Font            = new Font("Segoe UI", 9.5f);

            // ── Title bar ──────────────────────────────────────────
            var titleBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 36,
                BackColor = Color.FromArgb(37, 37, 37)
            };
            titleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _dragStart = e.Location; };
            titleBar.MouseMove += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
            };

            var titleLabel = new Label
            {
                Text      = "AI.Clip — Settings",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font      = new Font("Segoe UI", 10f),
                Location  = new Point(12, 0),
                Size      = new Size(400, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var closeBtn = MakeTitleBarButton("✕", 436);
            closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            closeBtn.Click += (s, e) => Close();
            titleBar.Controls.AddRange([titleLabel, closeBtn]);

            // ── Content ────────────────────────────────────────────
            int y = 20;

            // API Key row
            var apiKeyLabel = MakeLabel("Anthropic API Key", 20, y);
            y += 24;
            _apiKeyBox = new TextBox
            {
                Location     = new Point(20, y),
                Size         = new Size(396, 28),
                BackColor    = Color.FromArgb(45, 45, 45),
                ForeColor    = Color.FromArgb(238, 238, 238),
                BorderStyle  = BorderStyle.FixedSingle,
                PasswordChar = '●',
                Text         = _settings.ApiKey,
                Font         = new Font("Segoe UI", 9.5f)
            };
            var showBtn = new Button
            {
                Text      = "👁",
                Location  = new Point(420, y),
                Size      = new Size(40, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.FromArgb(200, 200, 200),
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 11f)
            };
            showBtn.FlatAppearance.BorderSize = 0;
            showBtn.Click += (s, e) => _apiKeyBox.PasswordChar = (_apiKeyBox.PasswordChar == '\0') ? '●' : '\0';
            y += 40;

            // Translation language
            var transLabel = MakeLabel("Default Translation Language", 20, y);
            y += 24;
            _translateLangBox = MakeComboBox(20, y, TranslateLanguages, _settings.TranslateTargetLanguage);
            y += 44;

            // Code language
            var codeLabel = MakeLabel("Default Code Conversion Language", 20, y);
            y += 24;
            _codeLangBox = MakeComboBox(20, y, CodeLanguages, _settings.CodeTargetLanguage);
            y += 48;

            // Buttons
            var saveBtn = new Button
            {
                Text      = "Save",
                Location  = new Point(20, y),
                Size      = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 107, 53),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += OnSave;

            var cancelBtn = new Button
            {
                Text      = "Cancel",
                Location  = new Point(128, y),
                Size      = new Size(80, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.FromArgb(200, 200, 200),
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9.5f)
            };
            cancelBtn.FlatAppearance.BorderSize = 0;
            cancelBtn.Click += (s, e) => Close();

            Controls.AddRange([titleBar]);
            Controls.AddRange([apiKeyLabel, _apiKeyBox, showBtn,
                               transLabel, _translateLangBox,
                               codeLabel, _codeLangBox,
                               saveBtn, cancelBtn]);

            // Adjust content controls' location to account for title bar
            foreach (Control c in Controls)
                if (c != titleBar) c.Location = new Point(c.Location.X, c.Location.Y + 36);
        }

        private void OnSave(object? sender, EventArgs e)
        {
            _settings.ApiKey                  = _apiKeyBox.Text.Trim();
            _settings.TranslateTargetLanguage = _translateLangBox.SelectedItem?.ToString() ?? "Korean";
            _settings.CodeTargetLanguage      = _codeLangBox.SelectedItem?.ToString() ?? "Python";
            _settings.Save();
            Close();
        }

        // ── Helpers ────────────────────────────────────────────────

        private static Label MakeLabel(string text, int x, int y) => new()
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(440, 20),
            ForeColor = Color.FromArgb(150, 150, 150),
            Font      = new Font("Segoe UI", 8.5f)
        };

        private static ComboBox MakeComboBox(int x, int y, string[] items, string selected)
        {
            var box = new ComboBox
            {
                Location      = new Point(x, y),
                Size          = new Size(200, 28),
                BackColor     = Color.FromArgb(45, 45, 45),
                ForeColor     = Color.FromArgb(238, 238, 238),
                FlatStyle     = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5f)
            };
            box.Items.AddRange(items);
            box.SelectedItem = selected;
            if (box.SelectedIndex < 0) box.SelectedIndex = 0;
            return box;
        }

        private static Button MakeTitleBarButton(string text, int x) => new()
        {
            Text      = text,
            Size      = new Size(44, 36),
            Location  = new Point(x, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(160, 160, 160),
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 10f)
        };
    }
}
