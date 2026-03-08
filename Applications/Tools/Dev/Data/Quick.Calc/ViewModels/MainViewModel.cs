namespace QuickCalc.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // ── 내부 상태 ──────────────────────────────────────────────
    private ulong _value;
    private bool _updating;      // 재진입 방지
    private int _wordSize = 64;  // 8 / 16 / 32 / 64

    // ── 비트 레이아웃 ──────────────────────────────────────────
    public ObservableCollection<BitItem> Bits { get; } = [];
    // 상위 32비트 (bit63~32), 하위 32비트 (bit31~0) — 두 줄 표시용
    public IEnumerable<BitItem> BitsHigh => Bits.Take(32);
    public IEnumerable<BitItem> BitsLow  => Bits.Skip(32);

    // ── IEEE 754 ──────────────────────────────────────────────
    // Float32 패널용 BitItem 32개 (bit31=MSB)
    public ObservableCollection<BitItem> IeeeBits32 { get; } = [];
    // Float64 패널용 BitItem 64개 (bit63=MSB)
    public ObservableCollection<BitItem> IeeeBits64 { get; } = [];

    // ── 입력 필드 ──────────────────────────────────────────────
    private string _decText = "0";
    private string _hexText = "0";
    private string _octText = "0";
    private string _binText = "0";

    public string DecText
    {
        get => _decText;
        set { _decText = value; OnPropertyChanged(); if (!_updating) ParseDec(value); }
    }
    public string HexText
    {
        get => _hexText;
        set { _hexText = value; OnPropertyChanged(); if (!_updating) ParseHex(value); }
    }
    public string OctText
    {
        get => _octText;
        set { _octText = value; OnPropertyChanged(); if (!_updating) ParseOct(value); }
    }
    public string BinText
    {
        get => _binText;
        set { _binText = value; OnPropertyChanged(); if (!_updating) ParseBin(value); }
    }

    // ── Word Size ──────────────────────────────────────────────
    public bool WS8  { get => _wordSize == 8;  set { if (value) SetWordSize(8);  } }
    public bool WS16 { get => _wordSize == 16; set { if (value) SetWordSize(16); } }
    public bool WS32 { get => _wordSize == 32; set { if (value) SetWordSize(32); } }
    public bool WS64 { get => _wordSize == 64; set { if (value) SetWordSize(64); } }

    // ── IEEE 표시 ──────────────────────────────────────────────
    private string _ieee32Text = "0.0";
    private string _ieee64Text = "0.0";
    public string Ieee32Text { get => _ieee32Text; private set { _ieee32Text = value; OnPropertyChanged(); } }
    public string Ieee64Text { get => _ieee64Text; private set { _ieee64Text = value; OnPropertyChanged(); } }

    // ── 히스토리 ───────────────────────────────────────────────
    public ObservableCollection<HistoryItem> History { get; } = [];

    // ── Pending 연산 ───────────────────────────────────────────
    private string _pendingOp = "";
    private ulong _operand1;
    private string _pendingOpDisplay = "";
    public string PendingOpDisplay { get => _pendingOpDisplay; private set { _pendingOpDisplay = value; OnPropertyChanged(); } }

    // ── Commands ───────────────────────────────────────────────
    public ICommand AndCmd   { get; }
    public ICommand OrCmd    { get; }
    public ICommand XorCmd   { get; }
    public ICommand NotCmd   { get; }
    public ICommand ShlCmd   { get; }
    public ICommand ShrCmd   { get; }
    public ICommand TwosCmd  { get; }
    public ICommand SignExtCmd { get; }
    public ICommand EqualCmd { get; }
    public ICommand ClearCmd { get; }
    public ICommand HistorySelectCmd { get; }

    public MainViewModel()
    {
        // 비트 아이템 초기화 (64개, bit63=index0)
        for (int i = 63; i >= 0; i--)
            Bits.Add(new BitItem(i, this));

        // IEEE32 비트 (32개, bit31=index0)
        for (int i = 31; i >= 0; i--)
            IeeeBits32.Add(new BitItem(i, this) { IeeePart = GetIeeePart32(i) });

        // IEEE64 비트 (64개, bit63=index0)
        for (int i = 63; i >= 0; i--)
            IeeeBits64.Add(new BitItem(i, this) { IeeePart = GetIeeePart64(i) });

        AndCmd    = new RelayCommand(_ => SetPendingOp("AND"));
        OrCmd     = new RelayCommand(_ => SetPendingOp("OR"));
        XorCmd    = new RelayCommand(_ => SetPendingOp("XOR"));
        NotCmd    = new RelayCommand(_ => ApplyNot());
        ShlCmd    = new RelayCommand(_ => ApplyShl());
        ShrCmd    = new RelayCommand(_ => ApplyShr());
        TwosCmd   = new RelayCommand(_ => ApplyTwosComplement());
        SignExtCmd = new RelayCommand(_ => ApplySignExtend());
        EqualCmd  = new RelayCommand(_ => ApplyEqual());
        ClearCmd  = new RelayCommand(_ => SetValue(0));
        HistorySelectCmd = new RelayCommand(item =>
        {
            if (item is HistoryItem h) SetValue(h.Value);
        });

        UpdateAll();
    }

    // ── 값 설정 ────────────────────────────────────────────────
    public void SetValue(ulong v)
    {
        _value = ApplyWordMask(v);
        UpdateAll();
    }

    private ulong ApplyWordMask(ulong v) => _wordSize switch
    {
        8  => v & 0xFF,
        16 => v & 0xFFFF,
        32 => v & 0xFFFFFFFF,
        _  => v
    };

    // ── 파싱 ───────────────────────────────────────────────────
    private void ParseDec(string s)
    {
        s = s.Trim().TrimStart('+');
        // 부호 있는 10진수 지원 (음수 → 2의 보수 ulong)
        if (s.StartsWith('-'))
        {
            if (long.TryParse(s, out long sv))
            { SetValue((ulong)sv); }
        }
        else if (ulong.TryParse(s, out ulong uv))
        { SetValue(uv); }
    }

    private void ParseHex(string s)
    {
        s = s.Trim().TrimStart('0', 'x', 'X').Replace("_", "");
        if (s.Length == 0) { SetValue(0); return; }
        if (ulong.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out ulong v))
            SetValue(v);
    }

    private void ParseOct(string s)
    {
        s = s.Trim().TrimStart('0').Replace("_", "");
        if (s.Length == 0) { SetValue(0); return; }
        try { SetValue(Convert.ToUInt64(s, 8)); } catch { }
    }

    private void ParseBin(string s)
    {
        s = s.Trim().TrimStart('0', 'b', 'B').Replace("_", "").Replace(" ", "");
        if (s.Length == 0) { SetValue(0); return; }
        try { SetValue(Convert.ToUInt64(s, 2)); } catch { }
    }

    // ── 표시 갱신 ──────────────────────────────────────────────
    private void UpdateAll()
    {
        _updating = true;
        try
        {
            var mask = ApplyWordMask(_value);

            // Dec: 부호 있는 표시 (word size 기준)
            _decText = ToSignedDecString(mask);
            _hexText = ToHexString(mask);
            _octText = Convert.ToString((long)mask, 8);
            _binText = ToBinString(mask);

            OnPropertyChanged(nameof(DecText));
            OnPropertyChanged(nameof(HexText));
            OnPropertyChanged(nameof(OctText));
            OnPropertyChanged(nameof(BinText));

            UpdateBits(mask);
            UpdateIeee(mask);
        }
        finally { _updating = false; }
    }

    private string ToSignedDecString(ulong v) => _wordSize switch
    {
        8  => ((sbyte)(byte)v).ToString(),
        16 => ((short)(ushort)v).ToString(),
        32 => ((int)(uint)v).ToString(),
        _  => ((long)v).ToString()
    };

    private string ToHexString(ulong v) => _wordSize switch
    {
        8  => v.ToString("X2"),
        16 => v.ToString("X4"),
        32 => v.ToString("X8"),
        _  => v.ToString("X16")
    };

    private string ToBinString(ulong v)
    {
        if (v == 0) return "0";
        var bits = Convert.ToString((long)v, 2);
        // 워드 사이즈에 맞게 패딩
        int pad = _wordSize;
        if (bits.Length < pad) bits = bits.PadLeft(pad, '0');
        // 4자리마다 _ 구분
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < bits.Length; i++)
        {
            if (i > 0 && (bits.Length - i) % 4 == 0) sb.Append('_');
            sb.Append(bits[i]);
        }
        return sb.ToString();
    }

    private void UpdateBits(ulong v)
    {
        foreach (var bit in Bits)
        {
            bool visible = bit.Index < _wordSize;
            bit.IsOn = visible && (v & (1UL << bit.Index)) != 0;
        }
        // IEEE32: 현재 값의 하위 32비트 → float 비트
        uint f32bits = (uint)(v & 0xFFFFFFFF);
        foreach (var bit in IeeeBits32)
            bit.IsOn = (f32bits & (1u << bit.Index)) != 0;

        // IEEE64: 현재 값 전체 → double 비트
        foreach (var bit in IeeeBits64)
            bit.IsOn = (v & (1UL << bit.Index)) != 0;
    }

    private void UpdateIeee(ulong v)
    {
        float f32 = BitConverter.UInt32BitsToSingle((uint)(v & 0xFFFFFFFF));
        double f64 = BitConverter.Int64BitsToDouble((long)v);
        Ieee32Text = float.IsNaN(f32) ? "NaN" : float.IsInfinity(f32) ? (float.IsPositiveInfinity(f32) ? "+∞" : "-∞") : f32.ToString("G9");
        Ieee64Text = double.IsNaN(f64) ? "NaN" : double.IsInfinity(f64) ? (double.IsPositiveInfinity(f64) ? "+∞" : "-∞") : f64.ToString("G17");
    }

    // ── 비트 직접 토글 ─────────────────────────────────────────
    public void ToggleBit(int bitIndex)
    {
        _value ^= (1UL << bitIndex);
        SetValue(_value);
    }

    // ── IEEE 비트 토글 (32) ────────────────────────────────────
    public void ToggleIeeeBit32(int bitIndex)
    {
        uint f32bits = (uint)(_value & 0xFFFFFFFF);
        f32bits ^= (1u << bitIndex);
        SetValue((_value & 0xFFFFFFFF00000000UL) | f32bits);
    }

    // ── IEEE 비트 토글 (64) ────────────────────────────────────
    public void ToggleIeeeBit64(int bitIndex)
    {
        _value ^= (1UL << bitIndex);
        SetValue(_value);
    }

    // ── 워드 사이즈 ────────────────────────────────────────────
    private void SetWordSize(int ws)
    {
        _wordSize = ws;
        OnPropertyChanged(nameof(WS8));
        OnPropertyChanged(nameof(WS16));
        OnPropertyChanged(nameof(WS32));
        OnPropertyChanged(nameof(WS64));
        SetValue(_value);
    }

    // ── 연산 ───────────────────────────────────────────────────
    private void SetPendingOp(string op)
    {
        _operand1 = _value;
        _pendingOp = op;
        PendingOpDisplay = $"{ToHexString(_operand1)} {op} ...";
    }

    private void ApplyEqual()
    {
        if (_pendingOp == "") return;
        ulong result = _pendingOp switch
        {
            "AND" => _operand1 & _value,
            "OR"  => _operand1 | _value,
            "XOR" => _operand1 ^ _value,
            _     => _value
        };
        AddHistory(_value);
        PendingOpDisplay = "";
        _pendingOp = "";
        SetValue(result);
    }

    private void ApplyNot()
    {
        AddHistory(_value);
        SetValue(~_value);
    }

    private void ApplyShl()
    {
        AddHistory(_value);
        SetValue(_value << 1);
    }

    private void ApplyShr()
    {
        AddHistory(_value);
        SetValue(_value >> 1);
    }

    private void ApplyTwosComplement()
    {
        AddHistory(_value);
        SetValue((~_value) + 1);
    }

    private void ApplySignExtend()
    {
        // 현재 워드 사이즈의 MSB 기준으로 부호 확장 → 64비트
        ulong v = _value;
        ulong result = _wordSize switch
        {
            8  => (ulong)(long)(sbyte)(byte)v,
            16 => (ulong)(long)(short)(ushort)v,
            32 => (ulong)(long)(int)(uint)v,
            _  => v
        };
        AddHistory(_value);
        _wordSize = 64;
        OnPropertyChanged(nameof(WS8));  OnPropertyChanged(nameof(WS16));
        OnPropertyChanged(nameof(WS32)); OnPropertyChanged(nameof(WS64));
        SetValue(result);
    }

    // ── 히스토리 ───────────────────────────────────────────────
    private void AddHistory(ulong v)
    {
        if (History.Count > 0 && History[0].Value == v) return;
        History.Insert(0, new HistoryItem { Value = v });
        if (History.Count > 50) History.RemoveAt(History.Count - 1);
    }

    // ── IEEE 파트 분류 ─────────────────────────────────────────
    private static string GetIeeePart32(int bit) => bit switch
    {
        31      => "Sign",
        >= 23   => "Exp",
        _       => "Man"
    };

    private static string GetIeeePart64(int bit) => bit switch
    {
        63      => "Sign",
        >= 52   => "Exp",
        _       => "Man"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
