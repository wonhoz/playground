using System.Text.RegularExpressions;
using BatchRename.Models;

namespace BatchRename.Services;

/// <summary>
/// 실제 파일명 변경 실행 + 실행 취소 스택 관리.
/// </summary>
public class RenameService
{
    // 실행 취소용: (원본 경로, 변경 후 경로) 묶음 목록
    private readonly Stack<List<(string From, string To)>> _undoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// 미리보기를 기반으로 파일명을 실제 변경하고 결과를 반환.
    /// </summary>
    public (int Success, int Fail, string? Error) Apply(IReadOnlyList<RenameEntry> entries)
    {
        int ok = 0, fail = 0;
        var batch = new List<(string From, string To)>();

        foreach (var e in entries)
        {
            if (e.HasError) { fail++; continue; }

            var newPath = Path.Combine(Path.GetDirectoryName(e.OriginalPath)!, e.PreviewName);
            if (e.OriginalPath.Equals(newPath, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                File.Move(e.OriginalPath, newPath);
                batch.Add((e.OriginalPath, newPath));
                ok++;
            }
            catch (Exception ex)
            {
                e.ErrorMessage = ex.Message;
                fail++;
            }
        }

        if (batch.Count > 0) _undoStack.Push(batch);
        return (ok, fail, null);
    }

    /// <summary>마지막 적용 배치를 원래대로 되돌림.</summary>
    public (int Success, int Fail) Undo()
    {
        if (!_undoStack.TryPop(out var batch)) return (0, 0);

        int ok = 0, fail = 0;
        // 역순으로 되돌려 이름 충돌 방지
        foreach (var (from, to) in Enumerable.Reverse(batch))
        {
            try
            {
                if (File.Exists(to)) { File.Move(to, from); ok++; }
                else fail++;
            }
            catch { fail++; }
        }
        return (ok, fail);
    }

    /// <summary>되돌리기 스택 초기화 (새 파일 목록 로드 시 호출)</summary>
    public void ClearUndo() => _undoStack.Clear();

    // ─────────────────────────────────────────────
    // 미리보기 생성
    // ─────────────────────────────────────────────

    /// <summary>패턴 모드 미리보기 갱신</summary>
    public static void UpdatePreviewPattern(
        IList<RenameEntry> entries, string pattern)
    {
        // 중복 이름 감지용
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            e.ErrorMessage = "";

            try
            {
                e.PreviewName = PatternEngine.Apply(pattern, e, i);

                if (string.IsNullOrWhiteSpace(e.PreviewName))
                    e.ErrorMessage = "빈 이름";
                else if (!seen.Add(e.PreviewName))
                    e.ErrorMessage = "중복 이름";
                else if (ContainsInvalidChars(e.PreviewName))
                    e.ErrorMessage = "사용 불가 문자 포함";
            }
            catch (Exception ex)
            {
                e.ErrorMessage = ex.Message;
                e.PreviewName  = e.OriginalName;
            }
        }
    }

    /// <summary>정규식 모드 미리보기 갱신</summary>
    public static void UpdatePreviewRegex(
        IList<RenameEntry> entries, string find, string replace)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 정규식 유효성 사전 체크
        if (!string.IsNullOrEmpty(find))
        {
            try { _ = new Regex(find); }
            catch (RegexParseException ex)
            {
                foreach (var e in entries)
                {
                    e.ErrorMessage = $"정규식 오류: {ex.Message}";
                    e.PreviewName  = e.OriginalName;
                }
                return;
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            e.ErrorMessage = "";

            try
            {
                e.PreviewName = PatternEngine.ApplyRegex(find, replace, e, i);

                if (string.IsNullOrWhiteSpace(e.PreviewName))
                    e.ErrorMessage = "빈 이름";
                else if (!seen.Add(e.PreviewName))
                    e.ErrorMessage = "중복 이름";
                else if (ContainsInvalidChars(e.PreviewName))
                    e.ErrorMessage = "사용 불가 문자 포함";
            }
            catch (Exception ex)
            {
                e.ErrorMessage = ex.Message;
                e.PreviewName  = e.OriginalName;
            }
        }
    }

    private static bool ContainsInvalidChars(string name) =>
        name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
}
