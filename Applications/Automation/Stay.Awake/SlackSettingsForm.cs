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

        private readonly ModernSpinner _startHour;
        private readonly ModernSpinner _startMinute;
        private readonly ModernSpinner _endHour;
        private readonly ModernSpinner _endMinute;

        public int WorkStartHour => _startHour.Value;
        public int WorkStartMinute => _startMinute.Value;
        public int WorkEndHour => _endHour.Value;
        public int WorkEndMinute => _endMinute.Value;

        private static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        private static readonly Color TextColor = Color.FromArgb(224, 224, 224);
        private static readonly Color SubTextColor = Color.FromArgb(160, 160, 160);
        private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
        private static readonly Color AccentColor = Color.FromArgb(67, 217, 123);
        private static readonly Color BtnCancelColor = Color.FromArgb(55, 55, 55);

        public SlackSettingsForm(int startHour, int startMinute, int endHour, int endMinute)
        {
            Text = "Slack 자동 상태 시간 설정";
            Size = new Size(350, 335);
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
                Location = new Point(24, 26),
                AutoSize = true,
                ForeColor = SubTextColor,
                Font = new Font("Segoe UI", 8.5f)
            };

            _startHour = new ModernSpinner(startHour, 0, 23) { Location = new Point(26, 50) };
            var colon1 = CreateColonLabel(new Point(99, 50));
            _startMinute = new ModernSpinner(startMinute, 0, 59) { Location = new Point(115, 50) };

            // 퇴근 시간 레이블
            var lblEnd = new Label
            {
                Text = "퇴근 (자리비움으로 변경)",
                Location = new Point(24, 112),
                AutoSize = true,
                ForeColor = SubTextColor,
                Font = new Font("Segoe UI", 8.5f)
            };

            _endHour = new ModernSpinner(endHour, 0, 23) { Location = new Point(26, 136) };
            var colon2 = CreateColonLabel(new Point(99, 135));
            _endMinute = new ModernSpinner(endMinute, 0, 59) { Location = new Point(115, 136) };

            // 안내 레이블
            var lblHint = new Label
            {
                Text = "※ Slack 앱이 실행 중이어야 합니다",
                Location = new Point(24, 196),
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8f)
            };

            // 버튼
            var btnOk = new Button
            {
                Text = "확인",
                Size = new Size(82, 30),
                Location = new Point(130, 224),
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
                Size = new Size(82, 30),
                Location = new Point(222, 224),
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

        /// <summary>
        /// 다크 테마 모던 스피너 컨트롤
        /// 패널 배경색으로 1px 테두리·구분선 효과 구현
        /// </summary>
        private sealed class ModernSpinner : Panel
        {
            private int _value;
            private readonly int _min;
            private readonly int _max;
            private readonly Label _display;

            private static readonly Color SpinnerBorder = Color.FromArgb(70, 70, 70);
            private static readonly Color SpinnerBg = Color.FromArgb(45, 45, 45);
            private static readonly Color BtnBg = Color.FromArgb(58, 58, 58);
            private static readonly Color BtnHover = Color.FromArgb(82, 82, 82);
            private static readonly Color DisplayText = Color.FromArgb(224, 224, 224);
            private static readonly Color ArrowColor = Color.FromArgb(150, 150, 150);

            public int Value => _value;

            // 레이아웃 (68 × 36 px)
            //  ┌──────────────────────────────────────┐
            //  │  [   display (44×34)   ]│ ▲ (22×16) │
            //  │                         ├────────────┤
            //  │                         │ ▼ (22×17) │
            //  └──────────────────────────────────────┘
            //  패널 BackColor = SpinnerBorder → 1px 테두리·구분선

            public ModernSpinner(int value, int min, int max)
            {
                _value = value;
                _min = min;
                _max = max;

                Size = new Size(68, 36);
                BackColor = SpinnerBorder;
                SetStyle(ControlStyles.Selectable, true);
                TabStop = false;

                _display = new Label
                {
                    Text = _value.ToString("D2"),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 11f),
                    ForeColor = DisplayText,
                    BackColor = SpinnerBg,
                    Location = new Point(1, 1),
                    Size = new Size(44, 34)
                };

                var btnUp = new Label
                {
                    Text = "▲",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 6f),
                    ForeColor = ArrowColor,
                    BackColor = BtnBg,
                    Location = new Point(46, 1),
                    Size = new Size(21, 16),
                    Cursor = Cursors.Hand
                };

                var btnDown = new Label
                {
                    Text = "▼",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 6f),
                    ForeColor = ArrowColor,
                    BackColor = BtnBg,
                    Location = new Point(46, 18),
                    Size = new Size(21, 17),
                    Cursor = Cursors.Hand
                };

                btnUp.Click += (_, _) => Step(1);
                btnDown.Click += (_, _) => Step(-1);

                btnUp.MouseEnter += (_, _) => { btnUp.BackColor = BtnHover; Focus(); };
                btnUp.MouseLeave += (_, _) => btnUp.BackColor = BtnBg;
                btnDown.MouseEnter += (_, _) => { btnDown.BackColor = BtnHover; Focus(); };
                btnDown.MouseLeave += (_, _) => btnDown.BackColor = BtnBg;
                _display.MouseEnter += (_, _) => Focus();
                MouseEnter += (_, _) => Focus();
                MouseWheel += (_, e) => Step(e.Delta > 0 ? 1 : -1);

                Controls.AddRange(new Control[] { _display, btnUp, btnDown });
            }

            private void Step(int delta)
            {
                _value = Math.Clamp(_value + delta, _min, _max);
                _display.Text = _value.ToString("D2");
            }
        }
    }
}
