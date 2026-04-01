using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// Slack 자동 상태 변경 시간 설정 폼
    /// </summary>
    public class SlackSettingsForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly NumericUpDown _startHour;
        private readonly NumericUpDown _startMinute;
        private readonly NumericUpDown _endHour;
        private readonly NumericUpDown _endMinute;

        public int WorkStartHour => (int)_startHour.Value;
        public int WorkStartMinute => (int)_startMinute.Value;
        public int WorkEndHour => (int)_endHour.Value;
        public int WorkEndMinute => (int)_endMinute.Value;

        private static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        private static readonly Color PanelColor = Color.FromArgb(40, 40, 40);
        private static readonly Color TextColor = Color.FromArgb(224, 224, 224);
        private static readonly Color SubTextColor = Color.FromArgb(160, 160, 160);
        private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
        private static readonly Color InputBg = Color.FromArgb(50, 50, 50);
        private static readonly Color AccentColor = Color.FromArgb(67, 217, 123); // #43D97B, 아이콘 배지 색상
        private static readonly Color BtnCancelColor = Color.FromArgb(55, 55, 55);

        public SlackSettingsForm(int startHour, int startMinute, int endHour, int endMinute)
        {
            Text = "Slack 자동 상태 시간 설정";
            Size = new Size(320, 255);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgColor;
            ForeColor = TextColor;
            Font = new Font("Segoe UI", 9.5f);

            // 다크 타이틀바
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));

            // 출근 시간 레이블
            var lblStart = new Label
            {
                Text = "출근 (활성으로 변경)",
                Location = new Point(20, 20),
                AutoSize = true,
                ForeColor = SubTextColor,
                Font = new Font("Segoe UI", 8.5f)
            };

            _startHour = CreateTimeSpinner(startHour, 0, 23, new Point(20, 42));
            var colon1 = CreateColonLabel(new Point(76, 48));
            _startMinute = CreateTimeSpinner(startMinute, 0, 59, new Point(90, 42));

            // 퇴근 시간 레이블
            var lblEnd = new Label
            {
                Text = "퇴근 (자리비움으로 변경)",
                Location = new Point(20, 90),
                AutoSize = true,
                ForeColor = SubTextColor,
                Font = new Font("Segoe UI", 8.5f)
            };

            _endHour = CreateTimeSpinner(endHour, 0, 23, new Point(20, 112));
            var colon2 = CreateColonLabel(new Point(76, 118));
            _endMinute = CreateTimeSpinner(endMinute, 0, 59, new Point(90, 112));

            // 안내 레이블
            var lblHint = new Label
            {
                Text = "※ Slack 앱이 실행 중이어야 합니다",
                Location = new Point(20, 152),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8f)
            };

            // 버튼
            var btnOk = new Button
            {
                Text = "확인",
                Size = new Size(80, 30),
                Location = new Point(130, 178),
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK,
                Font = new Font("Segoe UI", 9.5f)
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "취소",
                Size = new Size(80, 30),
                Location = new Point(218, 178),
                BackColor = BtnCancelColor,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9.5f)
            };
            btnCancel.FlatAppearance.BorderColor = BorderColor;
            btnCancel.FlatAppearance.BorderSize = 1;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblStart, _startHour, colon1, _startMinute,
                lblEnd, _endHour, colon2, _endMinute,
                lblHint, btnOk, btnCancel
            });
        }

        private static NumericUpDown CreateTimeSpinner(int value, int min, int max, Point location)
        {
            return new NumericUpDown
            {
                Value = value,
                Minimum = min,
                Maximum = max,
                Location = location,
                Size = new Size(52, 26),
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(224, 224, 224),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f),
                TextAlign = HorizontalAlignment.Center
            };
        }

        private static Label CreateColonLabel(Point location)
        {
            return new Label
            {
                Text = ":",
                Location = location,
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold)
            };
        }
    }
}
