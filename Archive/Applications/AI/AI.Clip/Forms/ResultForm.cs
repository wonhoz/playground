using System.Drawing;
using System.Windows.Forms;

namespace AiClip.Forms
{
    public class ResultForm : Form
    {
        private readonly RichTextBox _resultBox;
        private readonly Label       _statusLabel;
        private readonly Button      _copyBtn;
        private string? _resultText;
        private Point   _dragStart;

        public ResultForm(string operationName)
        {
            BackColor       = Color.FromArgb(30, 30, 30);
            ForeColor       = Color.FromArgb(238, 238, 238);
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(580, 460);
            StartPosition   = FormStartPosition.CenterScreen;
            Text            = $"AI.Clip — {operationName}";
            Font            = new Font("Segoe UI", 9.5f);
            ShowInTaskbar   = true;

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
                Text      = $"AI.Clip  —  {operationName}",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font      = new Font("Segoe UI", 10f),
                Location  = new Point(12, 0),
                Size      = new Size(480, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var closeBtn = new Button
            {
                Text      = "✕",
                Size      = new Size(44, 36),
                Location  = new Point(536, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(160, 160, 160),
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 10f)
            };
            closeBtn.FlatAppearance.BorderSize         = 0;
            closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            closeBtn.Click += (s, e) => Close();

            titleBar.Controls.AddRange([titleLabel, closeBtn]);

            // ── Result text area ───────────────────────────────────
            _resultBox = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.FromArgb(28, 28, 28),
                ForeColor   = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.None,
                Font        = new Font("Segoe UI", 10.5f),
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                WordWrap    = true,
                Padding     = new Padding(16),
                Text        = "AI가 처리 중입니다…"
            };

            // ── Bottom bar ─────────────────────────────────────────
            var bottomBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 48,
                BackColor = Color.FromArgb(37, 37, 37),
                Padding   = new Padding(12, 8, 12, 8)
            };

            _statusLabel = new Label
            {
                Text      = "처리 중…",
                ForeColor = Color.FromArgb(110, 110, 110),
                Location  = new Point(14, 14),
                Size      = new Size(320, 20),
                Font      = new Font("Segoe UI", 8.5f)
            };

            _copyBtn = new Button
            {
                Text      = "Copy to Clipboard",
                Location  = new Point(424, 10),
                Size      = new Size(142, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 107, 53),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Enabled   = false,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _copyBtn.FlatAppearance.BorderSize         = 0;
            _copyBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 133, 85);
            _copyBtn.Click += OnCopy;

            bottomBar.Controls.AddRange([_statusLabel, _copyBtn]);

            Controls.AddRange([titleBar, _resultBox, bottomBar]);
        }

        // ── Public state setters ───────────────────────────────────

        public void ShowResult(string result)
        {
            _resultText         = result;
            _resultBox.ForeColor = Color.FromArgb(220, 220, 220);
            _resultBox.Text      = result;
            _statusLabel.Text    = $"완료  —  {result.Length:N0}자";
            _copyBtn.Enabled     = true;
        }

        public void ShowError(string error)
        {
            _resultBox.ForeColor = Color.FromArgb(255, 100, 100);
            _resultBox.Text      = $"오류가 발생했습니다:\n\n{error}";
            _statusLabel.Text    = "오류";
            _copyBtn.Enabled     = false;
        }

        // ── Copy to clipboard ──────────────────────────────────────

        private void OnCopy(object? sender, EventArgs e)
        {
            if (_resultText == null) return;
            try
            {
                Clipboard.SetText(_resultText);
                _copyBtn.Text = "Copied!";

                var timer = new System.Windows.Forms.Timer { Interval = 1800 };
                timer.Tick += (_, __) =>
                {
                    if (!IsDisposed) _copyBtn.Text = "Copy to Clipboard";
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
            catch { /* clipboard access failure */ }
        }
    }
}
