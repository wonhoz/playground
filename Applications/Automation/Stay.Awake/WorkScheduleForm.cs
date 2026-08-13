using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// 업무시간 스케줄 설정 폼 — 적용 요일 + 시작/종료 시각
    /// </summary>
    public class WorkScheduleForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly ModernSpinner _startHour;
        private readonly ModernSpinner _startMinute;
        private readonly ModernSpinner _endHour;
        private readonly ModernSpinner _endMinute;
        private readonly Button[] _dayButtons = new Button[7];
        private readonly bool[] _dayChecked = new bool[7];
        private readonly Label _lblSummary;

        public int StartHour => _startHour.Value;
        public int StartMinute => _startMinute.Value;
        public int EndHour => _endHour.Value;
        public int EndMinute => _endMinute.Value;

        public int DaysMask
        {
            get
            {
                var mask = 0;
                for (var d = 0; d < 7; d++)
                    if (_dayChecked[d]) mask |= 1 << d;
                return mask;
            }
        }

        private static readonly string[] DayNames = { "일", "월", "화", "수", "목", "금", "토" };

        private static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        private static readonly Color TextColor = Color.FromArgb(224, 224, 224);
        private static readonly Color SubTextColor = Color.FromArgb(160, 160, 160);
        private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
        private static readonly Color AccentColor = Color.FromArgb(67, 217, 123);
        private static readonly Color BtnCancelColor = Color.FromArgb(55, 55, 55);
        private static readonly Color DayOffBg = Color.FromArgb(45, 45, 45);
        private static readonly Color DayOffText = Color.FromArgb(140, 140, 140);
        private static readonly Color SundayText = Color.FromArgb(235, 110, 110);
        private static readonly Color SaturdayText = Color.FromArgb(110, 165, 235);

        // 요일 버튼 폰트 — 상태 전환마다 새로 만들지 않도록 재사용 (GDI 핸들 누수 방지)
        private static readonly Font DayFontOn = new("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font DayFontOff = new("Segoe UI", 10f);

        public WorkScheduleForm(WorkSchedule schedule)
        {
            Text = "업무시간 스케줄 설정";
            Size = new Size(400, 470);
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

            // 업무 시작 시각
            var lblStart = CreateCaption("업무 시작 (시뮬레이션 시작)", new Point(24, 20));
            _startHour = new ModernSpinner(schedule.StartHour, 0, 23) { Location = new Point(27, 46) };
            var colon1 = CreateColonLabel(new Point(99, 46));
            _startMinute = new ModernSpinner(schedule.StartMinute, 0, 59) { Location = new Point(120, 46) };

            // 업무 종료 시각
            var lblEnd = CreateCaption("업무 종료 (시뮬레이션 정지)", new Point(212, 20));
            _endHour = new ModernSpinner(schedule.EndHour, 0, 23) { Location = new Point(215, 46) };
            var colon2 = CreateColonLabel(new Point(287, 46));
            _endMinute = new ModernSpinner(schedule.EndMinute, 0, 59) { Location = new Point(308, 46) };

            // 적용 요일
            var lblDays = CreateCaption("적용 요일", new Point(24, 104));
            for (var d = 0; d < 7; d++)
            {
                _dayChecked[d] = schedule.IsDayEnabled((DayOfWeek)d);
                var index = d;
                var btn = new Button
                {
                    Text = DayNames[d],
                    Size = new Size(44, 34),
                    Location = new Point(27 + d * 48, 130),
                    FlatStyle = FlatStyle.Flat,
                    Font = DayFontOff,
                    Cursor = Cursors.Hand,
                    TabStop = false
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += (_, _) => { _dayChecked[index] = !_dayChecked[index]; ApplyDayStyle(index); UpdateSummary(); };
                _dayButtons[d] = btn;
                ApplyDayStyle(d);
            }

            // 프리셋 버튼
            var btnWeekdays = CreatePresetButton("평일", new Point(27, 176), WorkSchedule.MaskWeekdays);
            var btnEveryDay = CreatePresetButton("매일", new Point(103, 176), WorkSchedule.MaskEveryDay);
            var btnWeekend = CreatePresetButton("주말", new Point(179, 176), WorkSchedule.MaskWeekend);

            // 요약
            _lblSummary = new Label
            {
                Location = new Point(24, 226),
                Size = new Size(340, 24),
                ForeColor = AccentColor,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            // 안내
            var lblHint = new Label
            {
                Text = "※ 스케줄이 켜지면 업무시간에만 자동으로 시뮬레이션이 시작되고,\n"
                     + "   업무 종료 시각에 자동으로 정지됩니다.\n"
                     + "※ 구간 안에서는 수동 시작·정지가 그대로 유지됩니다.\n"
                     + "※ 종료 시각이 시작보다 이르면 자정을 넘는 구간(야간)으로\n"
                     + "   동작합니다. 요일은 구간이 시작된 날 기준입니다.",
                Location = new Point(24, 258),
                Size = new Size(345, 96),
                ForeColor = Color.FromArgb(130, 130, 130),
                Font = new Font("Segoe UI", 8.5f)
            };

            // 버튼
            var btnOk = new Button
            {
                Text = "확인",
                Size = new Size(82, 32),
                Location = new Point(190, 366),
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
                Size = new Size(82, 32),
                Location = new Point(282, 366),
                BackColor = BtnCancelColor,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel,
                Font = new Font("Segoe UI", 9.5f)
            };
            btnCancel.FlatAppearance.BorderColor = BorderColor;
            btnCancel.FlatAppearance.BorderSize = 1;

            // 요일 미선택 상태로는 저장 불가 — 스케줄이 영영 동작하지 않는 설정 방지
            btnOk.Click += (_, _) =>
            {
                if (DaysMask != 0) return;
                DialogResult = DialogResult.None;
                UpdateSummary();
            };

            // 시각 변경 시 요약 즉시 갱신
            foreach (var spinner in new[] { _startHour, _startMinute, _endHour, _endMinute })
                spinner.ValueChanged += (_, _) => UpdateSummary();

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[]
            {
                lblStart, _startHour, colon1, _startMinute,
                lblEnd, _endHour, colon2, _endMinute,
                lblDays, btnWeekdays, btnEveryDay, btnWeekend,
                _lblSummary, lblHint, btnOk, btnCancel
            });
            Controls.AddRange(_dayButtons);

            UpdateSummary();
        }

        private Button CreatePresetButton(string text, Point location, int mask)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(68, 30),
                Location = location,
                BackColor = Color.FromArgb(48, 48, 48),
                ForeColor = SubTextColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderColor = BorderColor;
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (_, _) =>
            {
                for (var d = 0; d < 7; d++)
                {
                    _dayChecked[d] = (mask & (1 << d)) != 0;
                    ApplyDayStyle(d);
                }
                UpdateSummary();
            };
            return btn;
        }

        private void ApplyDayStyle(int day)
        {
            var btn = _dayButtons[day];
            if (_dayChecked[day])
            {
                btn.BackColor = AccentColor;
                btn.ForeColor = Color.FromArgb(20, 20, 20);
                btn.FlatAppearance.BorderColor = AccentColor;
                btn.Font = DayFontOn;
            }
            else
            {
                btn.BackColor = DayOffBg;
                btn.ForeColor = day == 0 ? SundayText : day == 6 ? SaturdayText : DayOffText;
                btn.FlatAppearance.BorderColor = BorderColor;
                btn.Font = DayFontOff;
            }
        }

        private void UpdateSummary()
        {
            var preview = new WorkSchedule
            {
                StartHour = _startHour.Value,
                StartMinute = _startMinute.Value,
                EndHour = _endHour.Value,
                EndMinute = _endMinute.Value,
                DaysMask = DaysMask
            };
            _lblSummary.Text = DaysMask == 0
                ? "요일을 하나 이상 선택하세요"
                : $"→ {preview.Summary()}";
            _lblSummary.ForeColor = DaysMask == 0 ? Color.FromArgb(235, 110, 110) : AccentColor;
        }

        private static Label CreateCaption(string text, Point location) => new()
        {
            Text = text,
            Location = location,
            AutoSize = true,
            ForeColor = SubTextColor,
            Font = new Font("Segoe UI", 8.5f)
        };

        private static Label CreateColonLabel(Point location) => new()
        {
            Text = ":",
            Location = location,
            AutoSize = true,
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold)
        };
    }
}
