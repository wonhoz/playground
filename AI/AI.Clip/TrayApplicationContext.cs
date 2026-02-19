using AiClip.Forms;
using AiClip.Models;
using AiClip.Rendering;

namespace AiClip
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly AppSettings _settings;

        private ToolStripMenuItem _clipPreviewItem = null!;
        private ToolStripMenuItem _summarizeItem   = null!;
        private ToolStripMenuItem _translateItem   = null!;
        private ToolStripMenuItem _proofreadItem   = null!;
        private ToolStripMenuItem _convertItem     = null!;

        public TrayApplicationContext()
        {
            _settings     = AppSettings.Load();
            _contextMenu  = BuildMenu();

            _trayIcon = new NotifyIcon
            {
                Icon             = IconGenerator.CreateTrayIcon(),
                Text             = "AI.Clip",
                ContextMenuStrip = _contextMenu,
                Visible          = true
            };

            _contextMenu.Opening += (s, e) => RefreshClipboardPreview();
        }

        // ── Menu builder ──────────────────────────────────────────

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip
            {
                Renderer  = new DarkMenuRenderer(),
                BackColor = Color.FromArgb(32, 32, 32),
                ForeColor = Color.FromArgb(240, 240, 240),
                Font      = new Font("Segoe UI", 9.5f)
            };

            // Clipboard preview (disabled display item)
            _clipPreviewItem = new ToolStripMenuItem("(클립보드가 비어있음)")
            {
                Enabled   = false,
                ForeColor = Color.FromArgb(90, 90, 90),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };
            menu.Items.Add(_clipPreviewItem);
            menu.Items.Add(new ToolStripSeparator());

            // AI operations
            _summarizeItem = new ToolStripMenuItem("Summarize  (요약)");
            _summarizeItem.Click += OnSummarize;

            _translateItem = new ToolStripMenuItem("Translate  (번역)");
            foreach (var lang in new[] { "Korean", "English", "Japanese", "Chinese", "Spanish", "French" })
            {
                var l = lang;
                _translateItem.DropDownItems.Add(
                    new ToolStripMenuItem($"→  {l}", null, (s, e) => OnTranslate(l)));
            }

            _proofreadItem = new ToolStripMenuItem("Proofread  (교정)");
            _proofreadItem.Click += OnProofread;

            _convertItem = new ToolStripMenuItem("Convert Code  (코드 변환)");
            foreach (var lang in new[] { "Python", "JavaScript", "TypeScript", "C#", "Java", "Go" })
            {
                var l = lang;
                _convertItem.DropDownItems.Add(
                    new ToolStripMenuItem($"→  {l}", null, (s, e) => OnConvertCode(l)));
            }

            menu.Items.Add(_summarizeItem);
            menu.Items.Add(_translateItem);
            menu.Items.Add(_proofreadItem);
            menu.Items.Add(_convertItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Settings", null, OnSettings));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExit));

            return menu;
        }

        // ── Clipboard preview refresh ─────────────────────────────

        private void RefreshClipboardPreview()
        {
            var text    = GetClipboardText();
            bool hasText = !string.IsNullOrWhiteSpace(text);

            if (hasText)
            {
                var preview = text!.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (preview.Length > 70) preview = preview[..70] + "…";
                _clipPreviewItem.Text      = preview;
                _clipPreviewItem.ForeColor = Color.FromArgb(150, 150, 150);
            }
            else
            {
                _clipPreviewItem.Text      = "(클립보드가 비어있음)";
                _clipPreviewItem.ForeColor = Color.FromArgb(90, 90, 90);
            }

            _summarizeItem.Enabled = hasText;
            _translateItem.Enabled = hasText;
            _proofreadItem.Enabled = hasText;
            _convertItem.Enabled   = hasText;
        }

        // ── Helpers ───────────────────────────────────────────────

        private static string? GetClipboardText()
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch { return null; }
        }

        private bool EnsureApiKey()
        {
            if (_settings.HasApiKey) return true;
            _trayIcon.ShowBalloonTip(3000, "AI.Clip",
                "Anthropic API 키가 설정되지 않았습니다.\n우클릭 → Settings에서 API 키를 입력해주세요.",
                ToolTipIcon.Warning);
            return false;
        }

        // ── Operation handlers (Commit 2에서 실제 API 호출로 교체) ──

        private void OnSummarize(object? s, EventArgs e)
        {
            if (!EnsureApiKey()) return;
            var text = GetClipboardText();
            if (string.IsNullOrWhiteSpace(text)) return;
            _trayIcon.ShowBalloonTip(2000, "AI.Clip", "Summarize 기능 준비 중...", ToolTipIcon.Info);
        }

        private void OnTranslate(string lang)
        {
            if (!EnsureApiKey()) return;
            var text = GetClipboardText();
            if (string.IsNullOrWhiteSpace(text)) return;
            _trayIcon.ShowBalloonTip(2000, "AI.Clip", $"Translate → {lang} 기능 준비 중...", ToolTipIcon.Info);
        }

        private void OnProofread(object? s, EventArgs e)
        {
            if (!EnsureApiKey()) return;
            var text = GetClipboardText();
            if (string.IsNullOrWhiteSpace(text)) return;
            _trayIcon.ShowBalloonTip(2000, "AI.Clip", "Proofread 기능 준비 중...", ToolTipIcon.Info);
        }

        private void OnConvertCode(string lang)
        {
            if (!EnsureApiKey()) return;
            var text = GetClipboardText();
            if (string.IsNullOrWhiteSpace(text)) return;
            _trayIcon.ShowBalloonTip(2000, "AI.Clip", $"Convert Code → {lang} 기능 준비 중...", ToolTipIcon.Info);
        }

        private void OnSettings(object? s, EventArgs e)
        {
            using var form = new SettingsForm(_settings);
            form.ShowDialog();
        }

        private void OnExit(object? s, EventArgs e)
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _trayIcon.Dispose();
            base.Dispose(disposing);
        }
    }
}
