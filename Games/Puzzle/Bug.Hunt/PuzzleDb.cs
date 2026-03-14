namespace BugHunt;

public static class PuzzleDb
{
    public static readonly Puzzle[] All =
    [
        // ── C# 주니어 ──────────────────────────────────────────────────────────
        new Puzzle("C#", "junior",
            "배열의 모든 요소를 더하는 함수 — 버그를 찾으세요.",
            [
                "public int Sum(int[] arr)",
                "{",
                "    int total = 0;",
                "    for (int i = 0; i <= arr.Length; i++)",
                "    {",
                "        total += arr[i];",
                "    }",
                "    return total;",
                "}",
            ],
            [4],
            "줄 4: i <= arr.Length 는 IndexOutOfRangeException!\n올바른 조건: i < arr.Length (0-indexed, Length 초과 접근 방지).\n이것이 classic off-by-one 오류입니다."
        ),
        new Puzzle("C#", "junior",
            "null 체크가 필요한 함수 — 버그를 찾으세요.",
            [
                "public int GetLength(string s)",
                "{",
                "    return s.Length;",
                "}",
                "",
                "// 호출 코드:",
                "string name = null;",
                "int len = GetLength(name);",
            ],
            [3, 7],
            "줄 3: s가 null이면 NullReferenceException 발생.\n줄 7: null을 인자로 전달.\n수정: s?.Length ?? 0 또는 null 가드 추가."
        ),
        new Puzzle("C#", "junior",
            "문자열 비교 함수 — 버그를 찾으세요.",
            [
                "public bool IsAdmin(string role)",
                "{",
                "    return role == \"admin\";",
                "}",
                "",
                "// 테스트:",
                "string userRole = \"Admin\";",
                "bool ok = IsAdmin(userRole); // false가 반환됨!",
            ],
            [3],
            "줄 3: 대소문자 구분 비교. \"Admin\" != \"admin\".\n수정: string.Equals(role, \"admin\", StringComparison.OrdinalIgnoreCase)"
        ),
        new Puzzle("C#", "junior",
            "정수 나눗셈 버그 — 버그를 찾으세요.",
            [
                "public double Average(int sum, int count)",
                "{",
                "    return sum / count;",
                "}",
                "",
                "// 테스트:",
                "double avg = Average(7, 2); // 3.5 예상, 3.0 반환!",
            ],
            [3],
            "줄 3: 정수 / 정수 = 정수 (소수점 버림).\n7 / 2 = 3 (int), 3.5가 아님!\n수정: (double)sum / count 또는 sum / (double)count"
        ),
        new Puzzle("C#", "junior",
            "리스트 수정 중 순회 — 버그를 찾으세요.",
            [
                "var numbers = new List<int> { 1, 2, 3, 4, 5 };",
                "foreach (var n in numbers)",
                "{",
                "    if (n % 2 == 0)",
                "        numbers.Remove(n);",
                "}",
            ],
            [5],
            "줄 5: foreach 도중 컬렉션 수정 → InvalidOperationException!\n수정: numbers.RemoveAll(n => n % 2 == 0) 또는 역방향 for 루프 사용."
        ),

        // ── C# 시니어 ──────────────────────────────────────────────────────────
        new Puzzle("C#", "senior",
            "스레드 안전성 문제 — 버그를 찾으세요.",
            [
                "private int _count = 0;",
                "",
                "public void Increment()",
                "{",
                "    _count++;",
                "}",
                "",
                "// 여러 스레드에서 동시 호출 시 race condition 발생!",
            ],
            [5],
            "줄 5: _count++ 는 원자적 연산이 아닙니다 (읽기-수정-쓰기 3단계).\n멀티스레드 환경에서 race condition → 값 손실.\n수정: Interlocked.Increment(ref _count) 사용."
        ),
        new Puzzle("C#", "senior",
            "IDisposable 누수 — 버그를 찾으세요.",
            [
                "public string ReadFile(string path)",
                "{",
                "    var reader = new StreamReader(path);",
                "    string content = reader.ReadToEnd();",
                "    return content;",
                "}",
                "",
                "// reader가 닫히지 않음 → 파일 핸들 누수!",
            ],
            [3],
            "줄 3: StreamReader를 using 없이 생성 → 파일 핸들이 GC 전까지 해제되지 않음.\n수정: using var reader = new StreamReader(path);"
        ),

        // ── Python 주니어 ──────────────────────────────────────────────────────
        new Puzzle("Python", "junior",
            "리스트 기본값 공유 버그 — 버그를 찾으세요.",
            [
                "def add_item(item, lst=[]):",
                "    lst.append(item)",
                "    return lst",
                "",
                "r1 = add_item('a')",
                "r2 = add_item('b')",
                "# r2 == ['a', 'b'] — 예상: ['b']!",
            ],
            [1],
            "줄 1: 가변 기본값(mutable default argument) 버그!\n기본값 []은 함수 정의 시 한 번만 생성되어 모든 호출이 공유합니다.\n수정: def add_item(item, lst=None): lst = lst if lst is not None else []"
        ),
        new Puzzle("Python", "junior",
            "인덱스 오류 — 버그를 찾으세요.",
            [
                "def last_element(lst):",
                "    return lst[len(lst)]",
                "",
                "# last_element([1, 2, 3]) → IndexError!",
            ],
            [2],
            "줄 2: len(lst)는 범위를 벗어남 (0-indexed, 마지막 인덱스는 len-1).\n수정: return lst[len(lst) - 1] 또는 return lst[-1]"
        ),
        new Puzzle("Python", "junior",
            "문자열 * 연산 오해 — 버그를 찾으세요.",
            [
                "def repeat_chars(s, n):",
                "    return s * n",
                "",
                "result = repeat_chars('ab', 3)",
                "# 예상: 'aabbbb', 실제: 'ababab'",
            ],
            [2],
            "줄 2: 'ab' * 3 = 'ababab' (문자열 반복).\n'a'*3 + 'b'*3 = 'aaabbb'를 원했다면:\nreturn ''.join(c * n for c in s)"
        ),

        // ── JavaScript 주니어 ──────────────────────────────────────────────────
        new Puzzle("JavaScript", "junior",
            "동등 비교 연산자 버그 — 버그를 찾으세요.",
            [
                "function isZero(n) {",
                "    return n == 0;",
                "}",
                "",
                "isZero('');   // true!",
                "isZero(null); // true!",
                "isZero(false); // true!",
            ],
            [2],
            "줄 2: == (느슨한 동등) 사용 → 타입 강제 변환 발생!\n'' == 0, null == 0, false == 0 모두 true.\n수정: return n === 0; (엄격한 동등)"
        ),
        new Puzzle("JavaScript", "junior",
            "var 호이스팅 버그 — 버그를 찾으세요.",
            [
                "for (var i = 0; i < 3; i++) {",
                "    setTimeout(function() {",
                "        console.log(i);",
                "    }, 1000);",
                "}",
                "// 예상: 0, 1, 2 → 실제: 3, 3, 3",
            ],
            [1],
            "줄 1: var는 함수 스코프 → 루프 종료 후 i=3을 모든 클로저가 공유.\n수정: var → let (블록 스코프, 각 반복에서 독립적인 i 캡처)"
        ),
        new Puzzle("JavaScript", "senior",
            "비동기 오류 처리 누락 — 버그를 찾으세요.",
            [
                "async function fetchData(url) {",
                "    const res = await fetch(url);",
                "    const data = await res.json();",
                "    return data;",
                "}",
                "",
                "fetchData('https://api.example.com/data');",
                "// 네트워크 오류 시 UnhandledPromiseRejection!",
            ],
            [7],
            "줄 7: await 없이 호출 + .catch() 없음 → Promise rejection이 처리되지 않음.\n수정: try/catch 블록 추가 또는 fetchData(...).catch(err => console.error(err))"
        ),

        // ── Java 주니어 ──────────────────────────────────────────────────────
        new Puzzle("Java", "junior",
            "정수 오버플로 — 버그를 찾으세요.",
            [
                "public long multiply(int a, int b) {",
                "    return a * b;",
                "}",
                "",
                "// multiply(100000, 100000)",
                "// 예상: 10000000000L, 실제: 1410065408 (오버플로!)",
            ],
            [2],
            "줄 2: a * b가 int 범위에서 먼저 계산 (Integer.MAX_VALUE = ~2.1B).\nint × int = int → 오버플로 후 long으로 변환됨.\n수정: return (long)a * b;"
        ),
        new Puzzle("Java", "junior",
            "String == 비교 버그 — 버그를 찾으세요.",
            [
                "public boolean isHello(String s) {",
                "    return s == \"Hello\";",
                "}",
                "",
                "String test = new String(\"Hello\");",
                "isHello(test); // false! (동일 내용, 다른 객체)",
            ],
            [2],
            "줄 2: == 는 참조 비교 (같은 객체인지). 문자열 내용 비교는 .equals()!\n수정: return \"Hello\".equals(s);  // null-safe 버전"
        ),
    ];

    public static Puzzle[] Filter(string lang, string diff) =>
        All.Where(p =>
            p.Language.Equals(lang, StringComparison.OrdinalIgnoreCase) &&
            (diff == "all" || p.Difficulty.Equals(
                diff == "주니어" ? "junior" : "senior", StringComparison.OrdinalIgnoreCase)))
        .ToArray();
}
