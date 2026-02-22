using AiClip.Forms;
using AiClip.Models;
using AiClip.Rendering;
using AiClip.Services;

namespace AiClip
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon    _trayIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly AppSettings   _settings;
        private readonly ClaudeService _claude;

        private ToolStripMenuItem _clipPreviewItem = null!;
        private ToolStripMenuItem _summarizeItem   = null!;
        private ToolStripMenuItem _translateItem   = null!;
        private ToolStripMenuItem _proofreadItem   = null!;
        private ToolStripMenuItem _convertItem     = null!;

        public TrayApplicationContext()
        {
            _settings     = AppSettings.Load();
            _claude       = new ClaudeService(_settings);
            _contextMenu  = BuildMenu();

            _trayIcon = new NotifyIcon
            {
                Icon             = IconGenerator.CreateTrayIcon(),
                Text             = "AI.Clip",
                ContextMenuStrip = _contextMenu,
                Visible          = true
            };

            _contextMenu.Opening += (s, e) => RefreshClipboardPreview();

            // 시작 풍선 알림 (트레이 아이콘 등록 후 600ms 딜레이)
            var startTimer = new System.Windows.Forms.Timer { Interval = 600 };
            startTimer.Tick += (s, e) =>
            {
                startTimer.Stop();
                startTimer.Dispose();
                _trayIcon.ShowBalloonTip(3000, "AI.Clip",
                    "AI.Clip이 실행 중입니다!\n텍스트를 복사한 후 트레이 아이콘을 우클릭하세요.",
                    ToolTipIcon.Info);
            };
            startTimer.Start();
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

            // Summarize
            _summarizeItem       = new ToolStripMenuItem("Summarize  (요약)");
            _summarizeItem.Click += OnSummarize;

            // Translate with submenu
            _translateItem = new ToolStripMenuItem("Translate  (번역)");
            foreach (var lang in new[] { "Korean", "English", "Japanese", "Chinese", "Spanish", "French" })
            {
                var l = lang;
                _translateItem.DropDownItems.Add(
                    new ToolStripMenuItem($"→  {l}", null, (s, e) => OnTranslate(l)));
            }

            // Proofread
            _proofreadItem       = new ToolStripMenuItem("Proofread  (교정)");
            _proofreadItem.Click += OnProofread;

            // Convert Code with submenu
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

        // ── Clipboard preview ─────────────────────────────────────

        private void RefreshClipboardPreview()
        {
            var text     = GetClipboardText();
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

        /// <summary>
        /// AI 작업 실행 공통 패턴:
        /// ResultForm을 즉시 열어 로딩 상태로 보여주고, 비동기 API 완료 시 결과 표시
        /// </summary>
        private async void RunOperation(string opName, Func<CancellationToken, Task<string>> apiCall)
        {
            if (!EnsureApiKey()) return;

            var text = GetClipboardText();
            if (string.IsNullOrWhiteSpace(text)) return;

            using var cts  = new CancellationTokenSource();
            var resultForm = new ResultForm(opName);
            resultForm.FormClosed += (s, e) => cts.Cancel(); // 창 닫으면 취소
            resultForm.Show();

            try
            {
                var result = await apiCall(cts.Token);
                if (!resultForm.IsDisposed)
                    resultForm.Invoke(() => resultForm.ShowResult(result));
            }
            catch (OperationCanceledException)
            {
                // 사용자가 창을 닫음 — 무시
            }
            catch (Exception ex)
            {
                if (!resultForm.IsDisposed)
                    resultForm.Invoke(() => resultForm.ShowError(ex.Message));
            }
        }

        // ── Operation handlers ────────────────────────────────────

        private void OnSummarize(object? s, EventArgs e) =>
            RunOperation("Summarize", ct => _claude.SummarizeAsync(GetClipboardText()!, ct));

        private void OnTranslate(string lang) =>
            RunOperation($"Translate → {lang}", ct => _claude.TranslateAsync(GetClipboardText()!, lang, ct));

        private void OnProofread(object? s, EventArgs e) =>
            RunOperation("Proofread", ct => _claude.ProofreadAsync(GetClipboardText()!, ct));

        private void OnConvertCode(string lang) =>
            RunOperation($"Convert Code → {lang}", ct => _claude.ConvertCodeAsync(GetClipboardText()!, lang, ct));

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
