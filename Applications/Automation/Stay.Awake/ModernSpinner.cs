namespace StayAwake
{
    /// <summary>
    /// 다크 테마 모던 스피너 컨트롤 (시/분 입력 공용)
    /// 패널 배경색으로 1px 테두리·구분선 효과 구현
    /// </summary>
    internal sealed class ModernSpinner : Panel
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

        /// <summary>값이 변경될 때 발생 (미리보기 갱신용)</summary>
        public event EventHandler? ValueChanged;

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
            var newValue = Math.Clamp(_value + delta, _min, _max);
            if (newValue == _value) return;

            _value = newValue;
            _display.Text = _value.ToString("D2");
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
