namespace GlyphMap.Models;

/// <summary>유니코드 문자 단일 항목</summary>
public sealed class GlyphEntry
{
    public int    CodePoint  { get; init; }
    public string Name       { get; init; } = "";
    public string Category   { get; init; } = "";
    public string Block      { get; init; } = "";

    /// <summary>U+HHHH 표기</summary>
    public string CodePointHex => $"U+{CodePoint:X4}";

    /// <summary>실제 문자 (서로게이트 쌍 포함)</summary>
    public string Char => char.ConvertFromUtf32(CodePoint);

    /// <summary>HTML 엔티티 (&#xHHHH;)</summary>
    public string HtmlEntity => $"&#x{CodePoint:X};";

    /// <summary>C# / Java 유니코드 이스케이프 (\uHHHH 또는 \UHHHHHHHH)</summary>
    public string CsEscape => CodePoint <= 0xFFFF
        ? $"\\u{CodePoint:X4}"
        : $"\\U{CodePoint:X8}";

    /// <summary>CSS content 값 (\\HHHHHH)</summary>
    public string CssContent => $"\\{CodePoint:X}";

    /// <summary>URL 인코딩 (%uHHHH)</summary>
    public string UrlEncoded => $"%u{CodePoint:X4}";

    /// <summary>범주 설명 (표시용)</summary>
    public string CategoryLabel => Category switch
    {
        "Lu" => "대문자",    "Ll" => "소문자",   "Lt" => "타이틀케이스",
        "Lm" => "수정 문자", "Lo" => "기타 문자",
        "Mn" => "비공백 조합", "Mc" => "공백 조합", "Me" => "외부 조합",
        "Nd" => "십진수자", "Nl" => "문자형 숫자", "No" => "기타 숫자",
        "Pc" => "연결 구두점", "Pd" => "대시",      "Ps" => "열기 괄호",
        "Pe" => "닫기 괄호",  "Pi" => "열기 따옴표","Pf" => "닫기 따옴표",
        "Po" => "기타 구두점",
        "Sm" => "수학 기호",  "Sc" => "통화 기호", "Sk" => "수정 기호",
        "So" => "기타 기호",
        "Zs" => "공백",      "Zl" => "줄 구분자", "Zp" => "단락 구분자",
        "Cc" => "제어 문자",  "Cf" => "서식 문자", "Co" => "사용자 정의",
        "Cs" => "서로게이트",
        _ => Category
    };

    /// <summary>렌더링 가능 여부 (제어문자/서로게이트 제외)</summary>
    public bool IsRenderable => Category is not ("Cc" or "Cs" or "Cf")
                                && CodePoint is not (0xFFFD);

    /// <summary>검색용 소문자 이름 캐시</summary>
    public string SearchKey { get; init; } = "";
}
