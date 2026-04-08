using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// 다크 테마 정보 다이얼로그 (OS 기본 MessageBox 대체)
    /// </summary>
    public class DarkInfoDialog : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        private static readonly Color TextColor = Color.FromArgb(224, 224, 224);
        private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
        private static readonly Color BtnColor = Color.FromArgb(55, 55, 55);

        /// <summary>
        /// 다크 테마 정보 다이얼로그를 표시합니다.
        /// </summary>
        public static void Show(string title, string message, int width = 480, int height = 400)
        {
            using var form = new DarkInfoDialog(title, message, width, height);
            form.ShowDialog();
        }

        private DarkInfoDialog(string title, string message, int width, int height)
        {
            Text = title;
            Size = new Size(width, height);
            MinimumSize = new Size(300, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9.5f);

            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));

            var textBox = new RichTextBox
            {
                Text = message,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = BgColor,
                ForeColor = TextColor,
                Font = new Font("Consolas", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Dock = DockStyle.None,
                Location = new Point(16, 16),
                Size = new Size(width - 48, height - 80),
                TabStop = false,
                DetectUrls = true
            };
            textBox.LinkClicked += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.LinkText) { UseShellExecute = true }); }
                catch { }
            };

            var btnOk = new Button
            {
                Text = "확인",
                Size = new Size(90, 30),
                BackColor = BtnColor,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9.5f)
            };
            btnOk.FlatAppearance.BorderColor = BorderColor;
            btnOk.FlatAppearance.BorderSize = 1;
            btnOk.Location = new Point(width - btnOk.Width - 45, ClientSize.Height - btnOk.Height - 25);

            AcceptButton = btnOk;
            Controls.Add(btnOk);
            Controls.Add(textBox);
        }
    }
}
