using System.Text.RegularExpressions;
using BatchRename.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace BatchRename.Services;

/// <summary>
/// 패턴 문자열을 해석해 새 파일명을 생성.
///
/// 지원 변수:
///   {name}           — 원본 파일명(확장자 제외)
///   {ext}            — 확장자(점 제외, 소문자)
///   {date}           — 파일 수정일 yyyyMMdd
///   {date:포맷}      — 파일 수정일 커스텀 포맷 (예: {date:yyyy-MM-dd})
///   {num}            — 1-기반 순번
///   {num:N}          — N자리 제로패딩 순번 (예: {num:3} → 001)
///   {exif}           — EXIF DateTimeOriginal yyyyMMdd (없으면 파일 수정일)
///   {exif:포맷}      — EXIF 날짜 커스텀 포맷
/// </summary>
public static class PatternEngine
{
    private static readonly Regex VarRegex = new(@"\{(\w+)(?::([^}]*))?\}", RegexOptions.Compiled);

    /// <summary>패턴 모드: 변수 치환</summary>
    public static string Apply(string pattern, RenameEntry entry, int index)
    {
        var fi       = new FileInfo(entry.OriginalPath);
        var nameOnly = Path.GetFileNameWithoutExtension(entry.OriginalName);
        var ext      = Path.GetExtension(entry.OriginalName).TrimStart('.').ToLower();

        return VarRegex.Replace(pattern, m =>
        {
            var key  = m.Groups[1].Value.ToLower();
            var fmt  = m.Groups[2].Success ? m.Groups[2].Value : null;

            return key switch
            {
                "name" => nameOnly,
                "ext"  => ext,
                "num"  => fmt is { } f && int.TryParse(f, out int pad)
                              ? (index + 1).ToString().PadLeft(pad, '0')
                              : (index + 1).ToString(),
                "date" => fi.LastWriteTime.ToString(fmt ?? "yyyyMMdd"),
                "exif" => GetExifDate(entry.OriginalPath, fi.LastWriteTime)
                              .ToString(fmt ?? "yyyyMMdd"),
                _      => m.Value,   // 알 수 없는 변수 → 원문 유지
            };
        });
    }

    /// <summary>정규식 모드: Find → Replace 치환</summary>
    public static string ApplyRegex(string find, string replace, RenameEntry entry, int index)
    {
        if (string.IsNullOrEmpty(find)) return entry.OriginalName;

        var regex = new Regex(find, RegexOptions.IgnoreCase);
        // $1 $2 그룹 참조 + {num} 변수 지원
        var result = regex.Replace(entry.OriginalName, m =>
        {
            // $0..$9 그룹 치환
            var r = replace;
            for (int g = 0; g < m.Groups.Count; g++)
                r = r.Replace($"${g}", m.Groups[g].Value);
            return r;
        });

        // {num:N} 후처리
        result = VarRegex.Replace(result, m =>
        {
            if (m.Groups[1].Value.ToLower() != "num") return m.Value;
            return m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out int pad)
                ? (index + 1).ToString().PadLeft(pad, '0')
                : (index + 1).ToString();
        });

        return result;
    }

    private static DateTime GetExifDate(string path, DateTime fallback)
    {
        try
        {
            var dirs = ImageMetadataReader.ReadMetadata(path);
            foreach (var dir in dirs.OfType<ExifSubIfdDirectory>())
            {
                if (dir.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                    return dt;
            }
        }
        catch { }
        return fallback;
    }
}
