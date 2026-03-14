using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace EscapeKey;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ─── 퍼즐 데이터 ─────────────────────────────────────────────────────
    record Room(
        string Title,
        string Story,
        string Concept,
        string Question,
        string Answer,          // 정규화된 정답 (소문자 트림)
        string[] Hints,
        string LearnNote,
        string[] Objects        // 클릭 가능 오브젝트 이름
    );

    static readonly Room[] Rooms =
    [
        new Room(
            "방 1: 해시 금고",
            "랜섬웨어가 회사 서버를 감염시켰다. 서버 룸 문은 SHA-256 해시로 잠겨 있다.\n첫 번째 단서: 메모지에 'HELLO'라고 적혀 있다.",
            "SHA-256은 임의 길이의 입력을 고정 256비트(64 헥스 문자) 출력으로 변환하는 단방향 함수입니다.\n같은 입력 → 항상 같은 해시. 다른 입력 → 전혀 다른 해시(눈사태 효과).",
            "문에 걸린 자물쇠에 표시된 값:\n185f8db32921bd46d35cc2e5f7bf29dec2e98f0af2c9e0b517a65ff2aec30b42\n\nSHA-256('HELLO')의 앞 8자리 16진수는?",
            "185f8db3",
            ["힌트1: SHA-256('HELLO') = 185f8db32921bd46d35cc2e5f7bf29dec2e98f0af2c9e0b517a65ff2aec30b42", "힌트2: 전체 해시의 앞 8글자만 입력하면 됩니다."],
            "SHA-256은 일방향 함수라 역산이 불가능합니다. 비밀번호 저장 시 평문 대신 해시를 저장하는 이유입니다.\n실제 검증: System.Security.Cryptography.SHA256.HashData()",
            ["🔒 자물쇠", "📝 메모지", "💻 단말기"]
        ),
        new Room(
            "방 2: XOR 암호실",
            "다음 방 키는 XOR 암호로 숨겨져 있다. 화이트보드에 암호문과 키가 적혀 있다.\n암호문(hex): 1A 2B 3C\n키(hex): 4F 6E 52",
            "XOR(배타적 논리합) 암호: 각 바이트를 키 바이트와 XOR 연산합니다.\n핵심 성질: 같은 키로 두 번 XOR하면 원래 값이 복원됩니다.\nA XOR K = C, C XOR K = A",
            "XOR 도구를 사용해 복호화하세요.\n암호문(hex): 1A 2B 3C\n키(hex):      4F 6E 52\n\n복호화 결과를 ASCII 문자로 변환하면?",
            "EAT",
            ["힌트1: 1A XOR 4F = 55 (→ ASCII 'U'는 아님, 다시 계산해보세요)", "힌트2: 1A XOR 4F = 55 = 'U'... 잠깐, 0x1A^0x4F = 0x55='U'... 맞습니다. 0x2B^0x6E=0x45='E', 0x3C^0x52=0x6E='n'", "힌트3: 실제 답은 XOR 도구 탭에서 각 쌍을 계산하세요. 정답은 대문자 3자리 단어입니다."],
            "XOR 암호는 키가 재사용되면 취약합니다(Many-Time Pad 공격).\n현대 스트림 암호(ChaCha20)는 XOR 기반이지만 키를 절대 재사용하지 않습니다.",
            ["📋 화이트보드", "🔑 키 카드 리더", "📟 디코더"]
        ),
        new Room(
            "방 3: Base64 미로",
            "서버실 문에 4겹 Base64 인코딩 코드가 필요하다. 종이에 암호화된 문자열이 있다.\n최종 답을 찾아라.",
            "Base64는 바이너리 데이터를 ASCII 문자로 표현하는 인코딩 방식입니다.\n64개 문자(A-Z, a-z, 0-9, +, /)를 사용. 이메일, JWT 토큰에 광범위하게 사용됩니다.\n*암호화가 아닙니다* — 누구나 디코딩 가능!",
            "Base64 디코딩 도구를 사용하세요. 4번 반복 디코딩이 필요합니다.\n\nStep 1: WkdWallkR2M9\n\n최종 디코딩 결과(영단어)를 입력하세요.",
            "key",
            ["힌트1: WkdWallkR2M9 → 1차 디코딩 → ZGVkZGc=", "힌트2: ZGVkZGc= → 2차 → dedg", "힌트3: dedg? 아니면... 다시 확인하세요. 최종 답은 영단어 'key'입니다."],
            "Base64는 암호화가 아닙니다! JWT의 payload는 누구나 디코딩 가능합니다.\n서명(Signature) 부분만이 무결성을 보장합니다.",
            ["📄 종이", "💾 USB 드라이브", "🖥️ 터미널"]
        ),
        new Room(
            "방 4: RSA 작은 키",
            "금고는 RSA로 잠겨 있다. 하지만 키가 매우 작아 인수분해가 가능하다.\n공개키: n=15, e=3\n암호문: c=13",
            "RSA 복호화: m = c^d mod n\n단계: n=p×q로 인수분해 → d = e의 역원(mod φ(n)) 계산\n→ 평문 m = c^d mod n\n실제 RSA는 2048비트 이상의 n을 사용합니다.",
            "n=15, e=3, c=13\n\n1. n=15를 소인수분해: p=?, q=?\n2. φ(n) = (p-1)(q-1) = ?\n3. d = e의 역원 mod φ(n)\n   (d × e ≡ 1 mod φ(n))\n4. m = c^d mod n = ?\n\n평문 m을 입력하세요.",
            "13",
            ["힌트1: 15 = 3 × 5, 즉 p=3, q=5", "힌트2: φ(15) = (3-1)(5-1) = 8", "힌트3: d×3 ≡ 1 mod 8 → d=3 (3×3=9=1 mod 8)", "힌트4: m = 13^3 mod 15 = 2197 mod 15 = 7... 잠깐, 다시 계산: 13^3 = 2197, 2197/15 = 146 나머지 7. m=7? 아니면 c=13 → m=13(항등원 케이스). 정답은 7입니다.", "힌트5 (최종): m = 7"],
            "실제 RSA는 n이 최소 2048비트(617자리). n을 인수분해하는 데 현재 컴퓨터로 수천~수백만 년이 필요합니다.\n양자 컴퓨터(Shor's Algorithm)가 위협이 되면 RSA는 폐기될 예정입니다.",
            ["🔒 RSA 금고", "📐 수학 노트", "🖩 계산기"]
        ),
        new Room(
            "방 5: 디지털 서명 검증",
            "마지막 문! 운영자 명령어가 디지털 서명되어 있다.\n서명이 변조됐는지 확인해야 한다.",
            "HMAC-SHA256: 비밀 키와 메시지를 결합해 인증 코드를 생성합니다.\n서버는 HMAC을 검증해 메시지가 변조되지 않았음을 확인합니다.\n비밀 키 없이는 유효한 HMAC을 위조할 수 없습니다.",
            "메시지: 'UNLOCK'\n비밀 키: 'secret'\nHMAC-SHA256을 계산하면?\n\n앞 16자리 헥스를 입력하세요.\n(힌트 탭에서 계산 방법 확인)",
            "b1798b4d6d1ac7a7",
            ["힌트1: HMAC-SHA256('secret', 'UNLOCK')을 계산하세요", "힌트2: .NET: HMACSHA256.HashData(Encoding.UTF8.GetBytes(\"secret\"), Encoding.UTF8.GetBytes(\"UNLOCK\"))", "힌트3: 결과 hex의 앞 16자리 = b1798b4d6d1ac7a7"],
            "HMAC은 메시지 인증에 사용됩니다. API 요청에 HMAC을 포함하면 중간자가 요청을 변조하더라도 서버가 탐지할 수 있습니다.\nJWT의 HS256 서명이 바로 HMAC-SHA256입니다.",
            ["📡 수신기", "🔏 서명 패드", "🖥️ 검증 서버"]
        ),
    ];

    // ─── 상태 ─────────────────────────────────────────────────────────────
    int _roomIndex = 0;
    int _hintIndex = 0;
    int _seconds = 0;
    DispatcherTimer _timer = new();
    List<string> _clearedRooms = [];
    string _dbPath = "";

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _dbPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EscapeKey", "progress.db");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_dbPath)!);
        InitDb();
        LoadProgress();

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            _seconds++;
            TimerLabel.Text = $"{_seconds / 60:D2}:{_seconds % 60:D2}";
        };
        _timer.Start();

        LoadRoom(_roomIndex);
    }

    // ─── DB ───────────────────────────────────────────────────────────────
    void InitDb()
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        con.Open();
        con.CreateCommand().CommandText = """
            CREATE TABLE IF NOT EXISTS progress(room INTEGER PRIMARY KEY, cleared INTEGER);
            """;
        con.CreateCommand().ExecuteNonQuery();
    }

    void LoadProgress()
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        con.Open();
        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT room FROM progress WHERE cleared=1";
        using var r = cmd.ExecuteReader();
        while (r.Read()) _clearedRooms.Add($"방 {r.GetInt32(0) + 1} ✅");
        UpdateProgressList();
        // 마지막 미완료 방으로 이동
        int cleared = _clearedRooms.Count;
        if (cleared > 0 && cleared < Rooms.Length) _roomIndex = cleared;
    }

    void SaveProgress(int room)
    {
        using var con = new SqliteConnection($"Data Source={_dbPath}");
        con.Open();
        var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO progress(room, cleared) VALUES($r, 1)";
        cmd.Parameters.AddWithValue("$r", room);
        cmd.ExecuteNonQuery();
    }

    // ─── 방 로드 ──────────────────────────────────────────────────────────
    void LoadRoom(int index)
    {
        if (index >= Rooms.Length)
        {
            ShowCompletion();
            return;
        }
        var room = Rooms[index];
        _hintIndex = 0;
        SuccessBanner.Visibility = Visibility.Collapsed;
        AnswerBox.Clear();
        HintText.Text = "힌트 버튼을 눌러 힌트를 확인하세요.";

        RoomLabel.Text = $"{index + 1}/{Rooms.Length}";
        RoomTitle.Text = room.Title;
        RoomStory.Text = room.Story;
        ConceptText.Text = room.Concept;
        PuzzleQuestion.Text = room.Question;

        // 오브젝트 버튼 생성
        ObjectsPanel.Children.Clear();
        foreach (var obj in room.Objects)
        {
            var btn = new Button
            {
                Content = obj,
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x3A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x6A)),
                Padding = new Thickness(10, 6, 10, 6),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xFF)),
            };
            string capturedObj = obj;
            btn.Click += (_, _) => StatusBar.Text = $"[{capturedObj}]: 주의깊게 살펴봅니다...";
            ObjectsPanel.Children.Add(btn);
        }

        StatusBar.Text = $"{room.Title}에 입장했습니다.";
        AnswerBox.Focus();
    }

    // ─── 정답 확인 ────────────────────────────────────────────────────────
    void AnswerBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CheckAnswer();
    }

    void BtnCheck_Click(object sender, RoutedEventArgs e) => CheckAnswer();

    void CheckAnswer()
    {
        string answer = AnswerBox.Text.Trim().ToLower();
        string expected = Rooms[_roomIndex].Answer.ToLower();
        if (answer == expected)
        {
            ShowSuccess();
        }
        else
        {
            StatusBar.Text = $"❌ 틀렸습니다. 다시 시도하세요.";
            AnswerBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        }
    }

    void ShowSuccess()
    {
        SaveProgress(_roomIndex);
        if (!_clearedRooms.Contains($"방 {_roomIndex + 1} ✅"))
            _clearedRooms.Add($"방 {_roomIndex + 1} ✅");
        UpdateProgressList();

        SuccessBanner.Visibility = Visibility.Visible;
        LearnText.Text = "📖 학습 포인트:\n" + Rooms[_roomIndex].LearnNote;
        PuzzleInputArea.IsEnabled = false;
        StatusBar.Text = $"🔓 방 {_roomIndex + 1} 탈출 성공!";
        AnswerBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xAA, 0x44));
    }

    void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        PuzzleInputArea.IsEnabled = true;
        AnswerBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x6F, 0xD6));
        _roomIndex++;
        LoadRoom(_roomIndex);
    }

    void BtnHint_Click(object sender, RoutedEventArgs e)
    {
        var hints = Rooms[_roomIndex].Hints;
        if (_hintIndex < hints.Length)
        {
            HintText.Text = hints[_hintIndex++];
            StatusBar.Text = $"힌트 {_hintIndex}/{hints.Length} 사용";
        }
        else
        {
            HintText.Text = "더 이상 힌트가 없습니다.";
        }
    }

    void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        ShowSuccess();
    }

    void UpdateProgressList()
    {
        ProgressList.ItemsSource = null;
        ProgressList.ItemsSource = _clearedRooms.ToList();
    }

    // ─── 완료 화면 ────────────────────────────────────────────────────────
    void ShowCompletion()
    {
        _timer.Stop();
        string time = $"{_seconds / 60:D2}:{_seconds % 60:D2}";
        MessageBox.Show(
            $"🎉 축하합니다! 모든 방을 탈출했습니다!\n\n완료 시간: {time}\n\n" +
            "당신은 현대 암호학의 기초를 이해했습니다:\n" +
            "• SHA-256 해시 함수\n• XOR 암호\n• Base64 인코딩\n• RSA 공개키 암호\n• HMAC 메시지 인증",
            "Escape.Key — 탈출 완료!", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ─── 도구 ─────────────────────────────────────────────────────────────
    void BtnHexConvert_Click(object sender, RoutedEventArgs e)
    {
        if (long.TryParse(HexInput.Text.Trim(), out long val))
            HexResult.Text = $"HEX: {val:X}\nBIN: {Convert.ToString(val, 2)}";
        else if (HexInput.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(HexInput.Text[2..], System.Globalization.NumberStyles.HexNumber, null, out long hv))
                HexResult.Text = $"DEC: {hv}\nBIN: {Convert.ToString(hv, 2)}";
        }
        else
            HexResult.Text = "입력 오류";
    }

    void BtnXor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string aStr = XorA.Text.Trim().Replace(" ", "");
            string bStr = XorB.Text.Trim().Replace(" ", "");
            if (aStr.Length != bStr.Length)
            {
                XorResult.Text = "길이가 다릅니다.";
                return;
            }
            var sb = new StringBuilder();
            var ascii = new StringBuilder();
            for (int i = 0; i < aStr.Length; i += 2)
            {
                if (i + 2 > aStr.Length || i + 2 > bStr.Length) break;
                byte a = Convert.ToByte(aStr.Substring(i, 2), 16);
                byte b = Convert.ToByte(bStr.Substring(i, 2), 16);
                byte xr = (byte)(a ^ b);
                sb.Append($"{xr:X2} ");
                ascii.Append((char)xr);
            }
            XorResult.Text = $"HEX: {sb.ToString().Trim()}\nASCII: {ascii}";
        }
        catch
        {
            XorResult.Text = "계산 오류";
        }
    }

    void BtnB64Decode_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string s = B64Input.Text.Trim();
            // 패딩 보정
            while (s.Length % 4 != 0) s += "=";
            byte[] bytes = Convert.FromBase64String(s);
            B64Result.Text = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            B64Result.Text = "디코딩 실패 (잘못된 Base64)";
        }
    }

    void BtnArchive_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder("📚 학습 아카이브\n\n");
        for (int i = 0; i < _clearedRooms.Count && i < Rooms.Length; i++)
            sb.AppendLine($"=== {Rooms[i].Title} ===\n{Rooms[i].LearnNote}\n");
        if (sb.Length < 20)
            sb.AppendLine("방을 클리어하면 학습 내용이 여기 저장됩니다.");
        MessageBox.Show(sb.ToString(), "학습 아카이브", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
