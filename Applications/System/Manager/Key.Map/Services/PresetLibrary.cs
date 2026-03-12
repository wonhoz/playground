namespace Key.Map.Services;

static class PresetLibrary
{
    public static List<AppPreset> All { get; } =
    [
        VSCode(),
        IntelliJ(),
        Chrome(),
        Windows(),
        Vim(),
        new AppPreset { Name = "Custom (빈 프리셋)" }
    ];

    static AppPreset VSCode() => new()
    {
        Name = "VS Code",
        Shortcuts =
        [
            new() { Keys="Ctrl+Shift+P",  Description="명령 팔레트",             Category="View"     },
            new() { Keys="Ctrl+P",        Description="파일 빠른 열기",           Category="File"     },
            new() { Keys="Ctrl+Shift+E",  Description="탐색기 열기",              Category="View"     },
            new() { Keys="Ctrl+Shift+F",  Description="전체 검색",               Category="Navigate" },
            new() { Keys="Ctrl+Shift+G",  Description="소스 제어",               Category="View"     },
            new() { Keys="Ctrl+`",        Description="터미널 열기/닫기",         Category="View"     },
            new() { Keys="Ctrl+/",        Description="줄 주석 토글",             Category="Edit"     },
            new() { Keys="Alt+Shift+F",   Description="코드 포맷",               Category="Edit"     },
            new() { Keys="F12",           Description="정의로 이동",              Category="Navigate" },
            new() { Keys="Alt+F12",       Description="정의 미리보기",            Category="Navigate" },
            new() { Keys="Ctrl+D",        Description="다음 일치 선택",           Category="Edit"     },
            new() { Keys="Ctrl+Shift+K",  Description="줄 삭제",                 Category="Edit"     },
            new() { Keys="Alt+Up",        Description="줄 위로 이동",             Category="Edit"     },
            new() { Keys="Alt+Down",      Description="줄 아래로 이동",           Category="Edit"     },
            new() { Keys="Ctrl+B",        Description="사이드바 토글",            Category="View"     },
            new() { Keys="Ctrl+W",        Description="탭 닫기",                 Category="File"     },
            new() { Keys="Ctrl+Z",        Description="실행 취소",               Category="Edit"     },
            new() { Keys="Ctrl+Shift+Z",  Description="다시 실행",               Category="Edit"     },
            new() { Keys="F5",            Description="디버그 시작",              Category="Run"      },
            new() { Keys="Ctrl+F5",       Description="디버그 없이 실행",          Category="Run"      },
            new() { Keys="F9",            Description="중단점 토글",              Category="Run"      },
            new() { Keys="Ctrl+Shift+X",  Description="확장 마켓플레이스",        Category="View"     },
        ]
    };

    static AppPreset IntelliJ() => new()
    {
        Name = "IntelliJ / Rider",
        Shortcuts =
        [
            new() { Keys="Shift+Shift",    Description="모든 항목 검색",          Category="Navigate" },
            new() { Keys="Ctrl+Shift+A",   Description="액션 찾기",              Category="Navigate" },
            new() { Keys="Alt+Enter",      Description="빠른 수정",              Category="Edit"     },
            new() { Keys="Ctrl+Alt+L",     Description="코드 포맷",              Category="Edit"     },
            new() { Keys="Ctrl+E",         Description="최근 파일",              Category="Navigate" },
            new() { Keys="Ctrl+Shift+E",   Description="최근 위치",              Category="Navigate" },
            new() { Keys="Ctrl+B",         Description="정의로 이동",            Category="Navigate" },
            new() { Keys="Ctrl+Alt+B",     Description="구현으로 이동",          Category="Navigate" },
            new() { Keys="Ctrl+F12",       Description="파일 구조 보기",         Category="Navigate" },
            new() { Keys="Shift+F6",       Description="이름 변경 (리팩터)",      Category="Edit"     },
            new() { Keys="Ctrl+R",         Description="프로젝트 실행",          Category="Run"      },
            new() { Keys="Ctrl+D",         Description="디버그 실행",            Category="Run"      },
            new() { Keys="Ctrl+Y",         Description="줄 삭제",               Category="Edit"     },
            new() { Keys="Ctrl+D",         Description="줄 복제",               Category="Edit"     },
            new() { Keys="Ctrl+Shift+F",   Description="전체 텍스트 검색",       Category="Navigate" },
            new() { Keys="Alt+F7",         Description="사용처 찾기",            Category="Navigate" },
            new() { Keys="Ctrl+Shift+U",   Description="대/소문자 전환",         Category="Edit"     },
        ]
    };

    static AppPreset Chrome() => new()
    {
        Name = "Chrome 브라우저",
        Shortcuts =
        [
            new() { Keys="Ctrl+T",       Description="새 탭",                   Category="File"     },
            new() { Keys="Ctrl+W",       Description="탭 닫기",                 Category="File"     },
            new() { Keys="Ctrl+Shift+T", Description="닫힌 탭 다시 열기",        Category="File"     },
            new() { Keys="Ctrl+Tab",     Description="다음 탭",                 Category="Navigate" },
            new() { Keys="Ctrl+L",       Description="주소창으로 포커스",        Category="Navigate" },
            new() { Keys="Ctrl+R",       Description="새로고침",                Category="Navigate" },
            new() { Keys="Ctrl+F",       Description="페이지에서 찾기",          Category="Navigate" },
            new() { Keys="Ctrl+D",       Description="즐겨찾기 추가",           Category="File"     },
            new() { Keys="Ctrl+Shift+B", Description="북마크 바 토글",          Category="View"     },
            new() { Keys="Ctrl+H",       Description="방문 기록",               Category="Navigate" },
            new() { Keys="Ctrl+J",       Description="다운로드",                Category="Navigate" },
            new() { Keys="F12",          Description="개발자 도구",             Category="View"     },
            new() { Keys="Ctrl+Plus",    Description="확대",                    Category="View"     },
            new() { Keys="Ctrl+Minus",   Description="축소",                    Category="View"     },
            new() { Keys="Ctrl+0",       Description="기본 배율",               Category="View"     },
        ]
    };

    static AppPreset Windows() => new()
    {
        Name = "Windows 단축키",
        Shortcuts =
        [
            new() { Keys="Win+D",         Description="바탕화면 표시/숨기기",    Category="View"     },
            new() { Keys="Win+E",         Description="파일 탐색기",            Category="File"     },
            new() { Keys="Win+L",         Description="화면 잠금",              Category="Other"    },
            new() { Keys="Win+Tab",       Description="작업 보기",              Category="View"     },
            new() { Keys="Win+Left",      Description="왼쪽 절반 스냅",         Category="View"     },
            new() { Keys="Win+Right",     Description="오른쪽 절반 스냅",       Category="View"     },
            new() { Keys="Win+Up",        Description="최대화",                 Category="View"     },
            new() { Keys="Win+Down",      Description="최소화/원래 크기",       Category="View"     },
            new() { Keys="Alt+Tab",       Description="창 전환",                Category="Navigate" },
            new() { Keys="Alt+F4",        Description="창 닫기",                Category="File"     },
            new() { Keys="Win+Shift+S",   Description="캡처 도구",              Category="Other"    },
            new() { Keys="Ctrl+Shift+Esc", Description="작업 관리자",           Category="Other"    },
            new() { Keys="Win+V",         Description="클립보드 기록",          Category="Edit"     },
            new() { Keys="Win+I",         Description="설정",                   Category="Other"    },
            new() { Keys="Win+A",         Description="알림 센터",              Category="Other"    },
        ]
    };

    static AppPreset Vim() => new()
    {
        Name = "Vim / Neovim",
        Shortcuts =
        [
            new() { Keys="Esc",           Description="Normal 모드",            Category="Navigate" },
            new() { Keys="I",             Description="Insert 모드 (커서 앞)",   Category="Edit"     },
            new() { Keys="A",             Description="Insert 모드 (커서 뒤)",   Category="Edit"     },
            new() { Keys="O",             Description="아래 줄 추가 후 Insert", Category="Edit"     },
            new() { Keys="V",             Description="Visual 모드",            Category="Edit"     },
            new() { Keys="G",             Description="파일 끝으로",            Category="Navigate" },
            new() { Keys="H",             Description="왼쪽",                   Category="Navigate" },
            new() { Keys="J",             Description="아래",                   Category="Navigate" },
            new() { Keys="K",             Description="위",                     Category="Navigate" },
            new() { Keys="L",             Description="오른쪽",                 Category="Navigate" },
            new() { Keys="W",             Description="다음 단어",              Category="Navigate" },
            new() { Keys="B",             Description="이전 단어",              Category="Navigate" },
            new() { Keys="D",             Description="삭제 (Visual 선택)",     Category="Edit"     },
            new() { Keys="Y",             Description="복사 (Yank)",            Category="Edit"     },
            new() { Keys="P",             Description="붙여넣기",               Category="Edit"     },
            new() { Keys="U",             Description="실행 취소",              Category="Edit"     },
            new() { Keys="Ctrl+R",        Description="다시 실행",              Category="Edit"     },
            new() { Keys="Ctrl+F",        Description="페이지 아래로",          Category="Navigate" },
            new() { Keys="Ctrl+B",        Description="페이지 위로",            Category="Navigate" },
        ]
    };
}
