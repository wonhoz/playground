using CipherQuest.Models;

namespace CipherQuest.Services;

public static class PuzzleData
{
    public static readonly List<Chapter> All;

    static PuzzleData()
    {
        All = [Caesar(), Vigenere(), Substitution(), RailFence(), Enigma()];
    }

    // ── Chapter 1: Caesar ─────────────────────────────────────────────

    private static Chapter Caesar() => new()
    {
        Number = 1,
        Name   = "카이사르 암호",
        Era    = "로마 공화정  B.C. 58",
        Type   = CipherType.Caesar,
        Desc   = "알파벳을 일정 수만큼 밀어 암호화합니다. 율리우스 카이사르가 군사 명령에 사용했습니다.",
        Puzzles =
        [
            MakeCaesar(1, "예언의 경고",   "BEWARE THE IDES OF MARCH",          3,
                "힌트: 황제의 즐겨쓰는 이동 거리는 3입니다",
                "B.C. 44년, 로마의 예언자 스퓨리나는 카이사르에게 '3월의 이데스를 조심하라'고 경고했습니다."),

            MakeCaesar(2, "갈리아 원정",   "ATTACK AT DAWN CROSS THE RIVER",    7,
                "힌트: 이동 거리가 한 주의 날 수와 같습니다",
                "B.C. 52년, 갈리아 전쟁 중 카이사르는 야간 기습 명령을 암호화하여 전달했습니다."),

            MakeCaesar(3, "로마의 유언",   "ET TU BRUTE THEN FALL CAESAR",     13,
                "힌트: ROT13 — 알파벳 절반만큼 이동합니다",
                "셰익스피어 희곡에서 카이사르의 마지막 말. ROT13은 두 번 적용하면 원문이 복원됩니다."),
        ],
    };

    // ── Chapter 2: Vigenere ───────────────────────────────────────────

    private static Chapter Vigenere() => new()
    {
        Number = 2,
        Name   = "비제네르 암호",
        Era    = "16세기 유럽",
        Type   = CipherType.Vigenere,
        Desc   = "키워드를 반복 사용해 카이사르 암호를 강화합니다. 300년간 해독 불가로 여겨졌습니다.",
        Puzzles =
        [
            MakeVigenere(1, "외교 전문",    "MEET ME TONIGHT AT THE OLD BRIDGE", "KEY",
                "힌트: 열쇠 단어는 암호학의 영어 단어",
                "비제네르 암호는 카시스키 테스트로 열쇠 길이를 파악하면 빈도 분석으로 해독 가능합니다."),

            MakeVigenere(2, "블레칠리의 밤", "THE GERMAN PLANS ARE CONFIRMED",   "LONDON",
                "힌트: 영국의 수도 이름",
                "제2차 세계대전 중 영국 정보부는 비제네르 변형 암호 해독으로 많은 작전을 무산시켰습니다."),

            MakeVigenere(3, "기밀 해제",    "WE HAVE CRACKED THE CODE TONIGHT", "CIPHER",
                "힌트: 암호 해독을 뜻하는 영어 단어",
                "1863년 찰스 배비지는 비제네르 암호를 최초로 해독했으나 군사 기밀로 분류되었습니다."),
        ],
    };

    // ── Chapter 3: Substitution ───────────────────────────────────────

    private static Chapter Substitution() => new()
    {
        Number = 3,
        Name   = "단순 치환 암호",
        Era    = "고대 ~ 중세",
        Type   = CipherType.Substitution,
        Desc   = "각 알파벳을 다른 글자로 교체합니다. 빈도 분석(E·T·A가 가장 많음)으로 해독하세요.",
        Puzzles =
        [
            MakeSub(1, "QWERTY 암호기",    "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG",
                "QWERTYUIOPASDFGHJKLZXCVBNM",
                "힌트: 키보드 첫 줄부터 순서대로 A→Q, B→W, C→E...",
                "QWERTY 자판 배열을 치환 키로 사용한 암호입니다. 타자기 시대에 즐겨 사용되었습니다."),

            MakeSub(2, "역방향 알파벳",   "ENIGMA WAS USED BY GERMANY IN WORLD WAR TWO",
                "ZYXWVUTSRQPONMLKJIHGFEDCBA",
                "힌트: 알파벳을 거꾸로 뒤집으면 됩니다 (A↔Z, B↔Y...)",
                "아트바쉬(Atbash) 암호라고도 불리는 이 방식은 히브리 성경에서도 발견됩니다."),

            MakeSub(3, "스파이 메시지",   "THE MEETING IS AT MIDNIGHT IN THE PARK",
                "BCDEFTUVWXYZGHIJKLMNOPQRSA",
                "힌트: A→B, B→C... 패턴이 숨어있습니다",
                "연속 치환 암호는 ROT-1과 ROT-24를 혼합한 비대칭 방식입니다."),
        ],
    };

    // ── Chapter 4: Rail Fence ─────────────────────────────────────────

    private static Chapter RailFence() => new()
    {
        Number = 4,
        Name   = "레일 펜스 암호",
        Era    = "남북전쟁 (1861~1865)",
        Type   = CipherType.RailFence,
        Desc   = "텍스트를 지그재그로 레일에 쓴 후 행별로 읽습니다. 레일 수를 맞춰 해독하세요.",
        Puzzles =
        [
            MakeRail(1, "전보 메시지",     "MEET ME AT THE OLD CLOCK TOWER",    2,
                "힌트: 레일 수가 가장 작은 소수입니다",
                "미국 남북전쟁 당시 전신 전보의 암호화에 레일 펜스가 사용되었습니다."),

            MakeRail(2, "야간 작전",       "THE EAGLE FLIES AT MIDNIGHT SHARP", 3,
                "힌트: 레일 수가 3입니다",
                "3개 레일은 지그재그 패턴이 더 복잡해져 단순 복호화를 어렵게 만듭니다."),

            MakeRail(3, "최후의 지령",     "RENDEZVOUS AT DAWN ON THE OLD BRIDGE", 4,
                "힌트: 레일 수가 4입니다 — 두 번 두 번",
                "레일 수가 늘어날수록 재배열이 복잡해지나, 브루트 포스(1~n 시도)로 쉽게 해독 가능합니다."),
        ],
    };

    // ── Chapter 5: Enigma ─────────────────────────────────────────────

    private static Chapter Enigma() => new()
    {
        Number = 5,
        Name   = "에니그마",
        Era    = "제2차 세계대전 (1939~1945)",
        Type   = CipherType.Enigma,
        Desc   = "3개 로터의 초기 위치를 설정해 해독합니다. 각 로터는 A~Z 중 하나입니다.",
        Puzzles =
        [
            MakeEnigma(1, "기상 예보",      "WEATHER FORECAST CLOUDY NIGHT",    'A', 'A', 'C',
                "힌트: 로터1=A, 로터2=A  나머지는 C 이전입니다",
                "2차대전 당시 독일 기상 관측선은 짧은 기상 보고를 에니그마로 암호화했고, 이것이 해독의 단서가 되었습니다."),

            MakeEnigma(2, "U보트 명령",     "ATTACK THE CONVOY AT MIDNIGHT",    'B', 'L', 'E',
                "힌트: 로터1=B, 로터3=E  로터2는 L입니다",
                "블레칠리 파크의 수학자들은 알란 튜링의 봄베 머신으로 에니그마의 로터 설정을 해독했습니다."),

            MakeEnigma(3, "최후의 전문",    "U BOAT POSITION NORTH SEA SECTOR", 'E', 'N', 'I',
                "힌트: 로터 설정이 ENI입니다  역사적 영감을 얻으세요",
                "1941년 HMS 불독 함정이 독일 U보트에서 에니그마 기계와 코드북을 탈취하는 데 성공했습니다."),
        ],
    };

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    private static CipherPuzzle MakeCaesar(int n, string title, string plain, int shift,
        string hint, string history) => new()
    {
        Number = n, Title = title, PlainText = plain,
        CipherText = CipherEngine.CaesarEncrypt(plain, shift),
        Key = shift.ToString(), Hint = hint, History = history,
    };

    private static CipherPuzzle MakeVigenere(int n, string title, string plain, string key,
        string hint, string history) => new()
    {
        Number = n, Title = title, PlainText = plain,
        CipherText = CipherEngine.VigenereEncrypt(plain, key),
        Key = key, Hint = hint, History = history,
    };

    private static CipherPuzzle MakeSub(int n, string title, string plain, string key,
        string hint, string history) => new()
    {
        Number = n, Title = title, PlainText = plain,
        CipherText = CipherEngine.SubstitutionEncrypt(plain, key),
        Key = key, Hint = hint, History = history,
    };

    private static CipherPuzzle MakeRail(int n, string title, string plain, int rails,
        string hint, string history) => new()
    {
        Number = n, Title = title, PlainText = plain,
        CipherText = CipherEngine.RailFenceEncrypt(plain, rails),
        Key = rails.ToString(), Hint = hint, History = history,
    };

    private static CipherPuzzle MakeEnigma(int n, string title, string plain,
        char r1, char r2, char r3, string hint, string history) => new()
    {
        Number = n, Title = title, PlainText = plain,
        CipherText = CipherEngine.EnigmaEncrypt(plain, r1, r2, r3),
        Key = $"{r1}{r2}{r3}", Hint = hint, History = history,
    };
}
